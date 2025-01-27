using Avalonia.Collections;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text;
using XtermSharp;

namespace AvaloniaTerminal;

public partial class TerminalControlModel : ObservableObject, ITerminalDelegate
{
    public TerminalControlModel()
    {
        // get the dimensions of terminal (cols and rows)
        Terminal = new Terminal(this);
        SelectionService = new SelectionService(Terminal);
        SearchService = new SearchService(Terminal);

        // trigger an update of the buffers
        FullBufferUpdate();
        UpdateDisplay();
    }

    [ObservableProperty]
    public partial Terminal Terminal { get; set; }

    [ObservableProperty]
    public partial SelectionService SelectionService { get; set; }

    [ObservableProperty]
    public partial SearchService SearchService { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial AvaloniaDictionary<(int x, int y), TextObject> ConsoleText { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="T:AvaloniaTerminal.TerminalControl"/> treats the "Alt/Option" key on the mac keyboard as a meta key,
    /// which has the effect of sending ESC+letter when Meta-letter is pressed.   Otherwise, it passes the keystroke that MacOS provides from the OS keyboard.
    /// </summary>
    /// <value><c>true</c> if option acts as a meta key; otherwise, <c>false</c>.</value>
    public bool OptionAsMetaKey { get; set; } = true;

    /// <summary>
    /// Gets a value indicating the relative position of the terminal scroller
    /// </summary>
    public double ScrollPosition
    {
        get
        {
            if (Terminal.Buffers.IsAlternateBuffer)
                return 0;

            // strictly speaking these ought not to be outside these bounds
            if (Terminal.Buffer.YDisp <= 0)
                return 0;

            var maxScrollback = Terminal.Buffer.Lines.Length - Terminal.Rows;
            if (Terminal.Buffer.YDisp >= maxScrollback)
                return 1;

            return (double)Terminal.Buffer.YDisp / (double)maxScrollback;
        }
    }

    /// <summary>
    /// Gets a value indicating the scroll thumbsize
    /// </summary>
    public float ScrollThumbsize
    {
        get
        {
            if (Terminal.Buffers.IsAlternateBuffer)
                return 0;

            // the thumb size is the proportion of the visible content of the
            // entire content but don't make it too small
            return Math.Max((float)Terminal.Rows / (float)Terminal.Buffer.Lines.Length, 0.01f);
        }
    }

    /// <summary>
    /// Gets a value indicating whether or not the user can scroll the terminal contents
    /// </summary>
    public bool CanScroll
    {
        get
        {
            var shouldBeEnabled = !Terminal.Buffers.IsAlternateBuffer;
            shouldBeEnabled = shouldBeEnabled && Terminal.Buffer.HasScrollback;
            shouldBeEnabled = shouldBeEnabled && Terminal.Buffer.Lines.Length > Terminal.Rows;
            return shouldBeEnabled;
        }
    }

    /// <summary>
    ///  This event is raised when the terminal size (cols and rows, width, height) has change, due to a NSView frame changed.
    /// </summary>
    public event Action<int, int, double, double> SizeChanged;

    /// <summary>
    /// Invoked to raise input on the control, which should probably be sent to the actual child process or remote connection
    /// </summary>
    public Action<byte[]> UserInput;

    // The code below is intended to not repaint too often, which can produce flicker, for example
    // when the user refreshes the display, and this repaints the screen, as dispatch delivers data
    // in blocks of 1024 bytes, which is not enough to cover the whole screen, so this delays
    // the update for a 1/600th of a second.
    bool pendingDisplay;

    public void ShowCursor(Terminal source)
    {
    }

    public void SetTerminalTitle(Terminal source, string title)
    {
        Title = title;
    }

    public void SetTerminalIconTitle(Terminal source, string title)
    {
    }

    void ITerminalDelegate.SizeChanged(Terminal source)
    {
    }

    public void Send(string text)
    {
        Send(Encoding.UTF8.GetBytes(text));
    }

    public void Send(byte[] data)
    {
        //EnsureCaretIsVisible();
        UserInput?.Invoke(data);
    }

    public string? WindowCommand(Terminal source, WindowManipulationCommand command, params int[] args)
    {
        return null;
    }

    public bool IsProcessTrusted()
    {
        return true;
    }

    public void Resize(double width, double height, double textWidth, double textHeight)
    {
        if (width == 0 || height == 0)
        {
            width = 640;
            height = 480;
        }

        var cols = (int)(width / textWidth);
        var rows = (int)(height / textHeight);


        Terminal?.Resize(cols, rows);
        RemoveItemsDictionary();

        SizeChanged?.Invoke(cols, rows, width, height);
    }

    /// <summary>
    /// Removes Items which are outside bounds Cols/Rows
    /// </summary>
    private void RemoveItemsDictionary()
    {
        var itemsToRemove = ConsoleText.Keys
            .Where(key => key.x >= Terminal.Cols || key.y >= Terminal.Rows)
            .ToList();

        foreach (var item in itemsToRemove)
        {
            ConsoleText.Remove(item);
        }
    }

    public void FullBufferUpdate()
    {
        for (var line = Terminal.Buffer.YBase; line < Terminal.Buffer.YBase + Terminal.Rows; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var cd = Terminal.Buffer.Lines[line][cell];

                var text = SetStyling(new TextObject(), cd);

                text.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
                ConsoleText[(cell, line)] = text;
            }
        }

        pendingDisplay = false;
    }

    public void UpdateDisplay()
    {
        Terminal.GetUpdateRange(out var lineStart, out var lineEnd);
        Terminal.ClearUpdateRange();

        var tb = Terminal.Buffer;
        for (int line = lineStart + tb.YDisp; line <= lineEnd + tb.YDisp; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var cd = Terminal.Buffer.Lines[line][cell];

                if (ConsoleText.TryGetValue((cell, line), out TextObject? text) && text != null)
                {
                    text = SetStyling(text, cd);

                    text.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
                    ConsoleText[(cell, line)] = text;
                }
                else
                {
                    var text2 = SetStyling(new TextObject(), cd);

                    text2.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
                    ConsoleText[(cell, line)] = text2;
                }
            }
        }

        //UpdateCursorPosition();
        //UpdateScroller();

        pendingDisplay = false;
    }

    public void Feed(string text)
    {
        Feed(Encoding.UTF8.GetBytes(text));
    }

    public void Feed(byte[] text, int length = -1)
    {
        SearchService?.Invalidate();
        Terminal?.Feed(text, length);
        UpdateDisplay();
    }

    private static TextObject SetStyling(TextObject control, CharData cd)
    {
        var attribute = cd.Attribute;

        // ((int)flags << 18) | (fg << 9) | bg;
        int bg = attribute & 0x1ff;
        int fg = (attribute >> 9) & 0x1ff;
        var flags = (FLAGS)(attribute >> 18);

        if (flags.HasFlag(FLAGS.INVERSE))
        {
            var tmp = bg;
            bg = fg;
            fg = tmp;

            if (fg == Renderer.DefaultColor)
                fg = Renderer.InvertedDefaultColor;
            if (bg == Renderer.DefaultColor)
                bg = Renderer.InvertedDefaultColor;
        }

        if (flags.HasFlag(FLAGS.BOLD))
        {
            control.FontWeight = FontWeight.Bold;
        }
        else
        {
            control.FontWeight = FontWeight.Normal;
        }

        if (flags.HasFlag(FLAGS.ITALIC))
        {
            control.FontStyle = FontStyle.Italic;
        }
        else
        {
            control.FontStyle = FontStyle.Normal;
        }

        if (flags.HasFlag(FLAGS.UNDERLINE))
        {
            control.TextDecorations = TextDecorations.Underline;
        }
        else
        {
            var dec = control.TextDecorations?.FirstOrDefault(x => x.Location == TextDecorationLocation.Underline);
            if (dec != null)
            {
                control.TextDecorations.Remove(dec);
            }
        }

        if (flags.HasFlag(FLAGS.CrossedOut))
        {
            control.TextDecorations = TextDecorations.Strikethrough;
        }
        else
        {
            var dec = control.TextDecorations?.FirstOrDefault(x => x.Location == TextDecorationLocation.Strikethrough);
            if (dec != null)
            {
                control.TextDecorations.Remove(dec);
            }
        }

        if (fg <= 255)
        {
            control.Foreground = TerminalControl.ConvertXtermColor(fg);
        }
        else if (fg == 256) // DefaultColor
        {
            control.Foreground = TerminalControl.ConvertXtermColor(15);
        }
        else if (fg == 257) // InvertedDefaultColor
        {
            control.Foreground = TerminalControl.ConvertXtermColor(0);
        }

        if (bg <= 255)
        {
            control.Background = TerminalControl.ConvertXtermColor(bg);
        }
        else if (bg == 256) // DefaultColor
        {
            control.Background = TerminalControl.ConvertXtermColor(0);
        }
        else if (bg == 257) // InvertedDefaultColor
        {
            control.Background = TerminalControl.ConvertXtermColor(15);
        }

        return control;
    }
}

public partial class TextObject : ObservableObject
{
    [ObservableProperty]
    public partial IBrush Foreground { get; set; }
    [ObservableProperty]

    public partial IBrush Background { get; set; }
    [ObservableProperty]

    public partial string Text { get; set; }
    [ObservableProperty]

    public partial FontWeight FontWeight { get; set; }
    [ObservableProperty]

    public partial FontStyle FontStyle { get; set; }
    [ObservableProperty]

    public partial TextDecorationCollection? TextDecorations { get; set; }
}