using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using System.Globalization;
using System.Text;
using XtermSharp;
using Point = Avalonia.Point;

namespace AvaloniaTerminal;

public partial class TerminalControl : Control, ITerminalDelegate
{
    public TerminalControl()
    {
        // get the dimensions of terminal (cols and rows)
        CalculateTextSize();
        var dimensions = CalculateVisibleRowsAndColumns();
        var options = new TerminalOptions() { Cols = dimensions.cols, Rows = dimensions.rows };

        Focusable = true;

        // the terminal itself and services
        Terminal = new Terminal(this, options);
        SelectionService = new SelectionService(Terminal);
        SearchService = new SearchService(Terminal);

        // trigger an update of the buffers
        FullBufferUpdate();
        //UpdateDisplay();

        KeyUp += TerminalControl_KeyUp;
        this.Focus(NavigationMethod.Pointer);
    }


    public static readonly StyledProperty<Terminal> TerminalProperty = AvaloniaProperty.Register<TerminalControl, Terminal>(nameof(Terminal));

    public Terminal Terminal
    {
        get => GetValue(TerminalProperty);
        set => SetValue(TerminalProperty, value);
    }

    public static readonly StyledProperty<SelectionService> SelectionServiceProperty = AvaloniaProperty.Register<TerminalControl, SelectionService>(nameof(SelectionService));

    public SelectionService SelectionService
    {
        get => GetValue(SelectionServiceProperty);
        set => SetValue(SelectionServiceProperty, value);
    }

    public static readonly StyledProperty<SearchService> SearchServiceProperty = AvaloniaProperty.Register<TerminalControl, SearchService>(nameof(SelectionService));

    public SearchService SearchService
    {
        get => GetValue(SearchServiceProperty);
        set => SetValue(SearchServiceProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<TerminalControl, string>(nameof(Title));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<SortedDictionary<(int x, int y), TextObject>> ConsoleTextProperty = AvaloniaProperty.Register<TerminalControl, SortedDictionary<(int x, int y), TextObject>>(nameof(ConsoleText), []);

    public SortedDictionary<(int x, int y), TextObject> ConsoleText
    {
        get => GetValue(ConsoleTextProperty);
        set => SetValue(ConsoleTextProperty, value);
    }

    public static readonly StyledProperty<string> FontNameProperty = AvaloniaProperty.Register<TerminalControl, string>(nameof(FontName), "Cascadia Mono");

    public string FontName
    {
        get => GetValue(FontNameProperty);
        set => SetValue(FontNameProperty, value);
    }

    public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<TerminalControl, double>(nameof(FontSize), 12);

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

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

    private Size _consoleTextSize;

    private Typeface _typeface;

    // The code below is intended to not repaint too often, which can produce flicker, for example
    // when the user refreshes the display, and this repaints the screen, as dispatch delivers data
    // in blocks of 1024 bytes, which is not enough to cover the whole screen, so this delays
    // the update for a 1/600th of a second.
    bool pendingDisplay;

    private void TerminalControl_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.A:
                    Send([0x01]);  // Ctrl+A
                    break;
                case Key.B:
                    Feed([0x02]);  // Ctrl+B
                    break;
                case Key.C:
                    Send([0x03]);  // Ctrl+C
                    break;
                case Key.D:
                    Send([0x04]);  // Ctrl+D
                    break;
                case Key.E:
                    Send([0x05]);  // Ctrl+E
                    break;
                case Key.F:
                    Send([0x06]);  // Ctrl+F
                    break;
                case Key.G:
                    Send([0x07]);  // Ctrl+G
                    break;
                case Key.H:
                    Send([0x08]);  // Ctrl+H
                    break;
                case Key.I:
                    Send([0x09]);  // Ctrl+I (Tab)
                    break;
                case Key.J:
                    Send([0x0A]);  // Ctrl+J (Line Feed)
                    break;
                case Key.K:
                    Send([0x0B]);  // Ctrl+K
                    break;
                case Key.L:
                    Send([0x0C]);  // Ctrl+L
                    break;
                case Key.M:
                    Send([0x0D]);  // Ctrl+M (Carriage Return)
                    break;
                case Key.N:
                    Send([0x0E]);  // Ctrl+N
                    break;
                case Key.O:
                    Send([0x0F]);  // Ctrl+O
                    break;
                case Key.P:
                    Send([0x10]);  // Ctrl+P
                    break;
                case Key.Q:
                    Send([0x11]);  // Ctrl+Q
                    break;
                case Key.R:
                    Send([0x12]);  // Ctrl+R
                    break;
                case Key.S:
                    Send([0x13]);  // Ctrl+S
                    break;
                case Key.T:
                    Send([0x14]);  // Ctrl+T
                    break;
                case Key.U:
                    Send([0x15]);  // Ctrl+U
                    break;
                case Key.V:
                    //_ = dc1.Paste(]);
                    Send([0x16]);  // Ctrl+V
                    break;
                case Key.W:
                    Send([0x17]);  // Ctrl+W
                    break;
                case Key.X:
                    Send([0x18]);  // Ctrl+X
                    break;
                case Key.Y:
                    Send([0x19]);  // Ctrl+Y
                    break;
                case Key.Z:
                    Send([0x1A]);  // Ctrl+Z
                    break;
                case Key.D1: // Ctrl+1
                    Send([0x31]);  // ASCII '1'
                    break;
                case Key.D2: // Ctrl+2
                    Send([0x32]);  // ASCII '2'
                    break;
                case Key.D3: // Ctrl+3
                    Send([0x33]);  // ASCII '3'
                    break;
                case Key.D4: // Ctrl+4
                    Send([0x34]);  // ASCII '4'
                    break;
                case Key.D5: // Ctrl+5
                    Send([0x35]);  // ASCII '5'
                    break;
                case Key.D6: // Ctrl+6
                    Send([0x36]);  // ASCII '6'
                    break;
                case Key.D7: // Ctrl+7
                    Send([0x37]);  // ASCII '7'
                    break;
                case Key.D8: // Ctrl+8
                    Send([0x38]);  // ASCII '8'
                    break;
                case Key.D9: // Ctrl+9
                    Send([0x39]);  // ASCII '9'
                    break;
                case Key.D0: // Ctrl+0
                    Send([0x30]);  // ASCII '0'
                    break;
                case Key.OemOpenBrackets: // Ctrl+[
                    Send([0x1B]);
                    break;
                case Key.OemBackslash: // Ctrl+\
                    Send([0x1C]);
                    break;
                case Key.OemCloseBrackets: // Ctrl+]
                    Send([0x1D]);
                    break;
                case Key.Space: // Ctrl+Space
                    Send([0x00]);
                    break;
                case Key.OemMinus: // Ctrl+_
                    Send([0x1F]);
                    break;
                default:
                    if (!string.IsNullOrEmpty(e.KeySymbol))
                    {
                        Send(e.KeySymbol);
                    }
                    break;
            }
        }
        if (e.KeyModifiers is KeyModifiers.Alt)
        {
            Send([0x1B]);
            if (!string.IsNullOrEmpty(e.KeySymbol))
            {
                Send(e.KeySymbol);
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Send([0x1b]);
                    break;
                case Key.Space:
                    Send([0x20]);
                    break;
                case Key.Delete:
                    Send(EscapeSequences.CmdDelKey);
                    break;
                case Key.Back:
                    Send([0x7f]);
                    break;
                case Key.Up:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveUpApp : EscapeSequences.MoveUpNormal);
                    break;
                case Key.Down:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveDownApp : EscapeSequences.MoveDownNormal);
                    break;
                case Key.Left:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveLeftApp : EscapeSequences.MoveLeftNormal);
                    break;
                case Key.Right:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveRightApp : EscapeSequences.MoveRightNormal);
                    break;
                case Key.PageUp:
                    if (Terminal.ApplicationCursor)
                    {
                        Send(EscapeSequences.CmdPageUp);
                    }
                    else
                    {
                        // TODO: view should scroll one page up.
                    }
                    break;
                case Key.PageDown:
                    if (Terminal.ApplicationCursor)
                    {
                        Send(EscapeSequences.CmdPageDown);
                    }
                    else
                    {
                        // TODO: view should scroll one page down
                    }
                    break;
                case Key.Home:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveHomeApp : EscapeSequences.MoveHomeNormal);
                    break;
                case Key.End:
                    Send(Terminal.ApplicationCursor ? EscapeSequences.MoveEndApp : EscapeSequences.MoveEndNormal);
                    break;
                case Key.Insert:
                    break;
                case Key.F1:
                    Send(EscapeSequences.CmdF[0]);
                    break;
                case Key.F2:
                    Send(EscapeSequences.CmdF[1]);
                    break;
                case Key.F3:
                    Send(EscapeSequences.CmdF[2]);
                    break;
                case Key.F4:
                    Send(EscapeSequences.CmdF[3]);
                    break;
                case Key.F5:
                    Send(EscapeSequences.CmdF[4]);
                    break;
                case Key.F6:
                    Send(EscapeSequences.CmdF[5]);
                    break;
                case Key.F7:
                    Send(EscapeSequences.CmdF[6]);
                    break;
                case Key.F8:
                    Send(EscapeSequences.CmdF[7]);
                    break;
                case Key.F9:
                    Send(EscapeSequences.CmdF[8]);
                    break;
                case Key.F10:
                    Send(EscapeSequences.CmdF[9]);
                    break;
                case Key.OemBackTab:
                    Send(EscapeSequences.CmdBackTab);
                    break;
                case Key.Tab:
                    Send(EscapeSequences.CmdTab);
                    break;
                //case Key.Enter:
                //    Send([EscapeSequences.CmdRet]);
                //    break;
                default:
                    if (!string.IsNullOrEmpty(e.KeySymbol))
                    {
                        Send(e.KeySymbol);
                    }
                    break;
            }
        }

        e.Handled = true;
    }

    void QueuePendingDisplay()
    {
        // throttle
        if (!pendingDisplay)
        {
            pendingDisplay = true;

            DispatcherTimer.RunOnce(FullBufferUpdate, TimeSpan.FromMilliseconds(33.34)); // Delay of 33.34 ms
            //DispatcherTimer.RunOnce(UpdateDisplay, TimeSpan.FromMilliseconds(33.34)); // Delay of 33.34 ms
        }
    }

    private void CalculateTextSize()
    {
        var myFont = FontFamily.Parse(FontName) ?? throw new ArgumentException($"The resource {FontName} is not a FontFamily.");

        _typeface = new Typeface(myFont);
        var shaped = TextShaper.Current.ShapeText("a", new TextShaperOptions(_typeface.GlyphTypeface, FontSize));
        var run = new ShapedTextRun(shaped, new GenericTextRunProperties(_typeface, FontSize));

        _consoleTextSize = run.Size;
    }

    private (int cols, int rows) CalculateVisibleRowsAndColumns()
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return (80, 25);
        }

        var cols = (int)(Bounds.Width / _consoleTextSize.Width);
        var rows = (int)(Bounds.Height / _consoleTextSize.Height);

        return (cols, rows);
    }

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

    public void Resize()
    {
        var (cols, rows) = CalculateVisibleRowsAndColumns();
        Terminal.Resize(cols, rows);
        RemoveItemsDictionary();

        SizeChanged?.Invoke(cols, rows, Bounds.Width, Bounds.Height);
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

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        Resize();
    }

    void FullBufferUpdate()
    {
        for (var line = Terminal.Buffer.YBase; line < Terminal.Buffer.YBase + Terminal.Rows; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var cd = Terminal.Buffer.Lines[line][cell];

                if (ConsoleText.TryGetValue((cell, line), out TextObject text))
                {
                    text = SetStyling(text, cd);

                    text.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
                }
                else
                {
                    var text2 = SetStyling(new TextObject(), cd);

                    text2.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
                    ConsoleText[(cell, line)] = text2;
                }
            }
        }

        pendingDisplay = false;

        InvalidateVisual();
    }

    void UpdateDisplay()
    {
        Terminal.GetUpdateRange(out var lineStart, out var lineEnd);
        Terminal.ClearUpdateRange();

        var tb = Terminal.Buffer;
        for (int line = lineStart + tb.YDisp; line <= lineEnd + tb.YDisp; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var cd = Terminal.Buffer.Lines[line][cell];

                if (ConsoleText.TryGetValue((cell, line), out TextObject text))
                {
                    text = SetStyling(text, cd);

                    text.Text = cd.Code == 0 ? " " : ((char)cd.Rune).ToString();
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
        InvalidateVisual();
    }

    public void Feed(string text)
    {
        SearchService.Invalidate();
        Terminal.Feed(Encoding.UTF8.GetBytes(text));
        QueuePendingDisplay();
    }

    public void Feed(byte[] text, int length = -1)
    {
        SearchService.Invalidate();
        Terminal.Feed(text, length);
        QueuePendingDisplay();
    }

    public static Brush ConvertXtermColor(int xtermColor)
    {
        return Application.Current.FindResource("AvaloniaTerminalColor" + xtermColor) as SolidColorBrush;
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
            control.Foreground = ConvertXtermColor(fg);
        }
        else if (fg == 256) // DefaultColor
        {
            control.Foreground = ConvertXtermColor(15);
        }
        else if (fg == 257) // InvertedDefaultColor
        {
            control.Foreground = ConvertXtermColor(0);
        }

        if (bg <= 255)
        {
            control.Background = ConvertXtermColor(bg);
        }
        else if (bg == 256) // DefaultColor
        {
            control.Background = ConvertXtermColor(0);
        }
        else if (bg == 257) // InvertedDefaultColor
        {
            control.Background = ConvertXtermColor(15);
        }

        return control;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0,0, Bounds.Width, Bounds.Height);
        context.FillRectangle(ConvertXtermColor(0), rect);

        foreach (var item in ConsoleText)
        {
            var rect2 = new Rect(_consoleTextSize.Width * item.Key.x, _consoleTextSize.Height * item.Key.y, _consoleTextSize.Width + 1, _consoleTextSize.Height + 1);
            context.FillRectangle(item.Value.Background, rect2);

            var formattedText = new FormattedText(item.Value.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, FontSize, item.Value.Foreground);
            formattedText.SetTextDecorations(item.Value.TextDecorations);
            formattedText.SetFontWeight(item.Value.FontWeight);
            formattedText.SetFontStyle(item.Value.FontStyle);

            context.DrawText(formattedText, new Point(_consoleTextSize.Width * item.Key.x, _consoleTextSize.Height * item.Key.y));
        }
    }
}

public class TextObject
{
    public IBrush Foreground { get; set; }

    public IBrush Background { get; set; }

    public string Text { get; set; }

    public FontWeight FontWeight { get; set; }

    public FontStyle FontStyle { get; set; }

    public TextDecorationCollection? TextDecorations { get; set; }
}