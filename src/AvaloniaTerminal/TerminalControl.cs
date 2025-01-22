using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using System.Globalization;
using System.Text;
using XtermSharp;
using Color = Avalonia.Media.Color;
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
        UpdateDisplay();

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
                    Feed(0x01);  // Ctrl+A
                    break;
                case Key.B:
                    Feed(0x02);  // Ctrl+B
                    break;
                case Key.C:
                    Feed(0x03);  // Ctrl+C
                    break;
                case Key.D:
                    Feed(0x04);  // Ctrl+D
                    break;
                case Key.E:
                    Feed(0x05);  // Ctrl+E
                    break;
                case Key.F:
                    Feed(0x06);  // Ctrl+F
                    break;
                case Key.G:
                    Feed(0x07);  // Ctrl+G
                    break;
                case Key.H:
                    Feed(0x08);  // Ctrl+H
                    break;
                case Key.I:
                    Feed(0x09);  // Ctrl+I (Tab)
                    break;
                case Key.J:
                    Feed(0x0A);  // Ctrl+J (Line Feed)
                    break;
                case Key.K:
                    Feed(0x0B);  // Ctrl+K
                    break;
                case Key.L:
                    Feed(0x0C);  // Ctrl+L
                    break;
                case Key.M:
                    Feed(0x0D);  // Ctrl+M (Carriage Return)
                    break;
                case Key.N:
                    Feed(0x0E);  // Ctrl+N
                    break;
                case Key.O:
                    Feed(0x0F);  // Ctrl+O
                    break;
                case Key.P:
                    Feed(0x10);  // Ctrl+P
                    break;
                case Key.Q:
                    Feed(0x11);  // Ctrl+Q
                    break;
                case Key.R:
                    Feed(0x12);  // Ctrl+R
                    break;
                case Key.S:
                    Feed(0x13);  // Ctrl+S
                    break;
                case Key.T:
                    Feed(0x14);  // Ctrl+T
                    break;
                case Key.U:
                    Feed(0x15);  // Ctrl+U
                    break;
                case Key.V:
                    //_ = dc1.Paste();
                    Feed(0x16);  // Ctrl+V
                    break;
                case Key.W:
                    Feed(0x17);  // Ctrl+W
                    break;
                case Key.X:
                    Feed(0x18);  // Ctrl+X
                    break;
                case Key.Y:
                    Feed(0x19);  // Ctrl+Y
                    break;
                case Key.Z:
                    Feed(0x1A);  // Ctrl+Z
                    break;
                case Key.D1: // Ctrl+1
                    Feed(0x31);  // ASCII '1'
                    break;
                case Key.D2: // Ctrl+2
                    Feed(0x32);  // ASCII '2'
                    break;
                case Key.D3: // Ctrl+3
                    Feed(0x33);  // ASCII '3'
                    break;
                case Key.D4: // Ctrl+4
                    Feed(0x34);  // ASCII '4'
                    break;
                case Key.D5: // Ctrl+5
                    Feed(0x35);  // ASCII '5'
                    break;
                case Key.D6: // Ctrl+6
                    Feed(0x36);  // ASCII '6'
                    break;
                case Key.D7: // Ctrl+7
                    Feed(0x37);  // ASCII '7'
                    break;
                case Key.D8: // Ctrl+8
                    Feed(0x38);  // ASCII '8'
                    break;
                case Key.D9: // Ctrl+9
                    Feed(0x39);  // ASCII '9'
                    break;
                case Key.D0: // Ctrl+0
                    Feed(0x30);  // ASCII '0'
                    break;
                case Key.OemOpenBrackets: // Ctrl+[
                    Feed(0x1B);
                    break;
                case Key.OemBackslash: // Ctrl+\
                    Feed(0x1C);
                    break;
                case Key.OemCloseBrackets: // Ctrl+]
                    Feed(0x1D);
                    break;
                case Key.Space: // Ctrl+Space
                    Feed(0x00);
                    break;
                case Key.OemMinus: // Ctrl+_
                    Feed(0x1F);
                    break;
                default:
                    if (!string.IsNullOrEmpty(e.KeySymbol))
                    {
                        Feed(e.KeySymbol);
                    }
                    break;
            }
        }
        if (e.KeyModifiers is KeyModifiers.Alt)
        {
            Feed(0x1B);
            if (!string.IsNullOrEmpty(e.KeySymbol))
            {
                Feed(e.KeySymbol);
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Feed(0x1b);
                    break;
                case Key.Space:
                    Feed(0x20);
                    break;
                case Key.Delete:
                    Feed(EscapeSequences.CmdDelKey);
                    break;
                case Key.Back:
                    Feed(0x7f);
                    break;
                case Key.Up:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveUpApp : EscapeSequences.MoveUpNormal);
                    break;
                case Key.Down:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveDownApp : EscapeSequences.MoveDownNormal);
                    break;
                case Key.Left:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveLeftApp : EscapeSequences.MoveLeftNormal);
                    break;
                case Key.Right:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveRightApp : EscapeSequences.MoveRightNormal);
                    break;
                case Key.PageUp:
                    if (Terminal.ApplicationCursor)
                    {
                        Feed(EscapeSequences.CmdPageUp);
                    }
                    else
                    {
                        // TODO: view should scroll one page up.
                    }
                    break;
                case Key.PageDown:
                    if (Terminal.ApplicationCursor)
                    {
                        Feed(EscapeSequences.CmdPageDown);
                    }
                    else
                    {
                        // TODO: view should scroll one page down
                    }
                    break;
                case Key.Home:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveHomeApp : EscapeSequences.MoveHomeNormal);
                    break;
                case Key.End:
                    Feed(Terminal.ApplicationCursor ? EscapeSequences.MoveEndApp : EscapeSequences.MoveEndNormal);
                    break;
                case Key.Insert:
                    break;
                case Key.F1:
                    Feed(EscapeSequences.CmdF[0]);
                    break;
                case Key.F2:
                    Feed(EscapeSequences.CmdF[1]);
                    break;
                case Key.F3:
                    Feed(EscapeSequences.CmdF[2]);
                    break;
                case Key.F4:
                    Feed(EscapeSequences.CmdF[3]);
                    break;
                case Key.F5:
                    Feed(EscapeSequences.CmdF[4]);
                    break;
                case Key.F6:
                    Feed(EscapeSequences.CmdF[5]);
                    break;
                case Key.F7:
                    Feed(EscapeSequences.CmdF[6]);
                    break;
                case Key.F8:
                    Feed(EscapeSequences.CmdF[7]);
                    break;
                case Key.F9:
                    Feed(EscapeSequences.CmdF[8]);
                    break;
                case Key.F10:
                    Feed(EscapeSequences.CmdF[9]);
                    break;
                case Key.OemBackTab:
                    Feed(EscapeSequences.CmdBackTab);
                    break;
                case Key.Tab:
                    Feed(EscapeSequences.CmdTab);
                    break;
                case Key.Enter:
                    Feed(EscapeSequences.CmdRet, 1);
                    break;
                default:
                    if (!string.IsNullOrEmpty(e.KeySymbol))
                    {
                        Feed(e.KeySymbol);
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
            DispatcherTimer.RunOnce(UpdateDisplay, TimeSpan.FromMilliseconds(33.34)); // Delay of 33.34 ms
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
        var size = CalculateVisibleRowsAndColumns();
        Terminal.Resize(size.cols, size.rows);

        SizeChanged?.Invoke(size.cols, size.rows, Bounds.Width, Bounds.Height);
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

                var text = SetStyling(new TextObject(), cd);

                text.Text = cd.Code == 0 ? "" : ((char)cd.Rune).ToString();
                ConsoleText[(cell, line)] = text;
            }
        }
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

                    text.Text = cd.Code == 0 ? "" : ((char)cd.Rune).ToString();
                }
                else
                {
                    var text2 = SetStyling(new TextObject(), cd);

                    text2.Text = cd.Code == 0 ? "" : ((char)cd.Rune).ToString();
                    ConsoleText[(cell, line)] = text2;
                }
            }
        }

        //UpdateCursorPosition();
        //UpdateScroller();

        pendingDisplay = false;
        InvalidateVisual();
    }

    // Simple tester API.
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

    public void Feed(byte text)
    {
        SearchService.Invalidate();
        Terminal.Feed([text], -1);
        QueuePendingDisplay();
    }

    private static Brush ConvertXtermColor(int xtermColor)
    {
        return xtermColor switch
        {
            0 => new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            1 => new SolidColorBrush(Color.FromRgb(128, 0, 0)),
            2 => new SolidColorBrush(Color.FromRgb(0, 128, 0)),
            3 => new SolidColorBrush(Color.FromRgb(128, 128, 0)),
            4 => new SolidColorBrush(Color.FromRgb(0, 0, 128)),
            5 => new SolidColorBrush(Color.FromRgb(128, 0, 128)),
            6 => new SolidColorBrush(Color.FromRgb(0, 128, 128)),
            7 => new SolidColorBrush(Color.FromRgb(192, 192, 192)),
            8 => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            9 => new SolidColorBrush(Color.FromRgb(255, 0, 0)),
            10 => new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            11 => new SolidColorBrush(Color.FromRgb(255, 255, 0)),
            12 => new SolidColorBrush(Color.FromRgb(0, 0, 255)),
            13 => new SolidColorBrush(Color.FromRgb(255, 0, 255)),
            14 => new SolidColorBrush(Color.FromRgb(0, 255, 255)),
            15 => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            16 => new SolidColorBrush(Color.FromRgb(0, 0, 0)),
            17 => new SolidColorBrush(Color.FromRgb(0, 0, 95)),
            18 => new SolidColorBrush(Color.FromRgb(0, 0, 135)),
            19 => new SolidColorBrush(Color.FromRgb(0, 0, 175)),
            20 => new SolidColorBrush(Color.FromRgb(0, 0, 215)),
            21 => new SolidColorBrush(Color.FromRgb(0, 0, 255)),
            22 => new SolidColorBrush(Color.FromRgb(0, 95, 0)),
            23 => new SolidColorBrush(Color.FromRgb(0, 95, 95)),
            24 => new SolidColorBrush(Color.FromRgb(0, 95, 135)),
            25 => new SolidColorBrush(Color.FromRgb(0, 95, 175)),
            26 => new SolidColorBrush(Color.FromRgb(0, 95, 215)),
            27 => new SolidColorBrush(Color.FromRgb(0, 95, 255)),
            28 => new SolidColorBrush(Color.FromRgb(0, 135, 0)),
            29 => new SolidColorBrush(Color.FromRgb(0, 135, 95)),
            30 => new SolidColorBrush(Color.FromRgb(0, 135, 135)),
            31 => new SolidColorBrush(Color.FromRgb(0, 135, 175)),
            32 => new SolidColorBrush(Color.FromRgb(0, 135, 215)),
            33 => new SolidColorBrush(Color.FromRgb(0, 135, 255)),
            34 => new SolidColorBrush(Color.FromRgb(0, 175, 0)),
            35 => new SolidColorBrush(Color.FromRgb(0, 175, 95)),
            36 => new SolidColorBrush(Color.FromRgb(0, 175, 135)),
            37 => new SolidColorBrush(Color.FromRgb(0, 175, 175)),
            38 => new SolidColorBrush(Color.FromRgb(0, 175, 215)),
            39 => new SolidColorBrush(Color.FromRgb(0, 175, 255)),
            40 => new SolidColorBrush(Color.FromRgb(0, 215, 0)),
            41 => new SolidColorBrush(Color.FromRgb(0, 215, 95)),
            42 => new SolidColorBrush(Color.FromRgb(0, 215, 135)),
            43 => new SolidColorBrush(Color.FromRgb(0, 215, 175)),
            44 => new SolidColorBrush(Color.FromRgb(0, 215, 215)),
            45 => new SolidColorBrush(Color.FromRgb(0, 215, 255)),
            46 => new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            47 => new SolidColorBrush(Color.FromRgb(0, 255, 95)),
            48 => new SolidColorBrush(Color.FromRgb(0, 255, 135)),
            49 => new SolidColorBrush(Color.FromRgb(0, 255, 175)),
            50 => new SolidColorBrush(Color.FromRgb(0, 255, 215)),
            51 => new SolidColorBrush(Color.FromRgb(0, 255, 255)),
            52 => new SolidColorBrush(Color.FromRgb(95, 0, 0)),
            53 => new SolidColorBrush(Color.FromRgb(95, 0, 95)),
            54 => new SolidColorBrush(Color.FromRgb(95, 0, 135)),
            55 => new SolidColorBrush(Color.FromRgb(95, 0, 175)),
            56 => new SolidColorBrush(Color.FromRgb(95, 0, 215)),
            57 => new SolidColorBrush(Color.FromRgb(95, 0, 255)),
            58 => new SolidColorBrush(Color.FromRgb(95, 95, 0)),
            59 => new SolidColorBrush(Color.FromRgb(95, 95, 95)),
            60 => new SolidColorBrush(Color.FromRgb(95, 95, 135)),
            61 => new SolidColorBrush(Color.FromRgb(95, 95, 175)),
            62 => new SolidColorBrush(Color.FromRgb(95, 95, 215)),
            63 => new SolidColorBrush(Color.FromRgb(95, 95, 255)),
            64 => new SolidColorBrush(Color.FromRgb(95, 135, 0)),
            65 => new SolidColorBrush(Color.FromRgb(95, 135, 95)),
            66 => new SolidColorBrush(Color.FromRgb(95, 135, 135)),
            67 => new SolidColorBrush(Color.FromRgb(95, 135, 175)),
            68 => new SolidColorBrush(Color.FromRgb(95, 135, 215)),
            69 => new SolidColorBrush(Color.FromRgb(95, 135, 255)),
            70 => new SolidColorBrush(Color.FromRgb(95, 175, 0)),
            71 => new SolidColorBrush(Color.FromRgb(95, 175, 95)),
            72 => new SolidColorBrush(Color.FromRgb(95, 175, 135)),
            73 => new SolidColorBrush(Color.FromRgb(95, 175, 175)),
            74 => new SolidColorBrush(Color.FromRgb(95, 175, 215)),
            75 => new SolidColorBrush(Color.FromRgb(95, 175, 255)),
            76 => new SolidColorBrush(Color.FromRgb(95, 215, 0)),
            77 => new SolidColorBrush(Color.FromRgb(95, 215, 95)),
            78 => new SolidColorBrush(Color.FromRgb(95, 215, 135)),
            79 => new SolidColorBrush(Color.FromRgb(95, 215, 175)),
            80 => new SolidColorBrush(Color.FromRgb(95, 215, 215)),
            81 => new SolidColorBrush(Color.FromRgb(95, 215, 255)),
            82 => new SolidColorBrush(Color.FromRgb(95, 255, 0)),
            83 => new SolidColorBrush(Color.FromRgb(95, 255, 95)),
            84 => new SolidColorBrush(Color.FromRgb(95, 255, 135)),
            85 => new SolidColorBrush(Color.FromRgb(95, 255, 175)),
            86 => new SolidColorBrush(Color.FromRgb(95, 255, 215)),
            87 => new SolidColorBrush(Color.FromRgb(95, 255, 255)),
            88 => new SolidColorBrush(Color.FromRgb(135, 0, 0)),
            89 => new SolidColorBrush(Color.FromRgb(135, 0, 95)),
            90 => new SolidColorBrush(Color.FromRgb(135, 0, 135)),
            91 => new SolidColorBrush(Color.FromRgb(135, 0, 175)),
            92 => new SolidColorBrush(Color.FromRgb(135, 0, 215)),
            93 => new SolidColorBrush(Color.FromRgb(135, 0, 255)),
            94 => new SolidColorBrush(Color.FromRgb(135, 95, 0)),
            95 => new SolidColorBrush(Color.FromRgb(135, 95, 95)),
            96 => new SolidColorBrush(Color.FromRgb(135, 95, 135)),
            97 => new SolidColorBrush(Color.FromRgb(135, 95, 175)),
            98 => new SolidColorBrush(Color.FromRgb(135, 95, 215)),
            99 => new SolidColorBrush(Color.FromRgb(135, 95, 255)),
            100 => new SolidColorBrush(Color.FromRgb(135, 135, 0)),
            101 => new SolidColorBrush(Color.FromRgb(135, 135, 95)),
            102 => new SolidColorBrush(Color.FromRgb(135, 135, 135)),
            103 => new SolidColorBrush(Color.FromRgb(135, 135, 175)),
            104 => new SolidColorBrush(Color.FromRgb(135, 135, 215)),
            105 => new SolidColorBrush(Color.FromRgb(135, 135, 255)),
            106 => new SolidColorBrush(Color.FromRgb(135, 175, 0)),
            107 => new SolidColorBrush(Color.FromRgb(135, 175, 95)),
            108 => new SolidColorBrush(Color.FromRgb(135, 175, 135)),
            109 => new SolidColorBrush(Color.FromRgb(135, 175, 175)),
            110 => new SolidColorBrush(Color.FromRgb(135, 175, 215)),
            111 => new SolidColorBrush(Color.FromRgb(135, 175, 255)),
            112 => new SolidColorBrush(Color.FromRgb(135, 215, 0)),
            113 => new SolidColorBrush(Color.FromRgb(135, 215, 95)),
            114 => new SolidColorBrush(Color.FromRgb(135, 215, 135)),
            115 => new SolidColorBrush(Color.FromRgb(135, 215, 175)),
            116 => new SolidColorBrush(Color.FromRgb(135, 215, 215)),
            117 => new SolidColorBrush(Color.FromRgb(135, 215, 255)),
            118 => new SolidColorBrush(Color.FromRgb(135, 255, 0)),
            119 => new SolidColorBrush(Color.FromRgb(135, 255, 95)),
            120 => new SolidColorBrush(Color.FromRgb(135, 255, 135)),
            121 => new SolidColorBrush(Color.FromRgb(135, 255, 175)),
            122 => new SolidColorBrush(Color.FromRgb(135, 255, 215)),
            123 => new SolidColorBrush(Color.FromRgb(135, 255, 255)),
            124 => new SolidColorBrush(Color.FromRgb(175, 0, 0)),
            125 => new SolidColorBrush(Color.FromRgb(175, 0, 95)),
            126 => new SolidColorBrush(Color.FromRgb(175, 0, 135)),
            127 => new SolidColorBrush(Color.FromRgb(175, 0, 175)),
            128 => new SolidColorBrush(Color.FromRgb(175, 0, 215)),
            129 => new SolidColorBrush(Color.FromRgb(175, 0, 255)),
            130 => new SolidColorBrush(Color.FromRgb(175, 95, 0)),
            131 => new SolidColorBrush(Color.FromRgb(175, 95, 95)),
            132 => new SolidColorBrush(Color.FromRgb(175, 95, 135)),
            133 => new SolidColorBrush(Color.FromRgb(175, 95, 175)),
            134 => new SolidColorBrush(Color.FromRgb(175, 95, 215)),
            135 => new SolidColorBrush(Color.FromRgb(175, 95, 255)),
            136 => new SolidColorBrush(Color.FromRgb(175, 135, 0)),
            137 => new SolidColorBrush(Color.FromRgb(175, 135, 95)),
            138 => new SolidColorBrush(Color.FromRgb(175, 135, 135)),
            139 => new SolidColorBrush(Color.FromRgb(175, 135, 175)),
            140 => new SolidColorBrush(Color.FromRgb(175, 135, 215)),
            141 => new SolidColorBrush(Color.FromRgb(175, 135, 255)),
            142 => new SolidColorBrush(Color.FromRgb(175, 175, 0)),
            143 => new SolidColorBrush(Color.FromRgb(175, 175, 95)),
            144 => new SolidColorBrush(Color.FromRgb(175, 175, 135)),
            145 => new SolidColorBrush(Color.FromRgb(175, 175, 175)),
            146 => new SolidColorBrush(Color.FromRgb(175, 175, 215)),
            147 => new SolidColorBrush(Color.FromRgb(175, 175, 255)),
            148 => new SolidColorBrush(Color.FromRgb(175, 215, 0)),
            149 => new SolidColorBrush(Color.FromRgb(175, 215, 95)),
            150 => new SolidColorBrush(Color.FromRgb(175, 215, 135)),
            151 => new SolidColorBrush(Color.FromRgb(175, 215, 175)),
            152 => new SolidColorBrush(Color.FromRgb(175, 215, 215)),
            153 => new SolidColorBrush(Color.FromRgb(175, 215, 255)),
            154 => new SolidColorBrush(Color.FromRgb(175, 255, 0)),
            155 => new SolidColorBrush(Color.FromRgb(175, 255, 95)),
            156 => new SolidColorBrush(Color.FromRgb(175, 255, 135)),
            157 => new SolidColorBrush(Color.FromRgb(175, 255, 175)),
            158 => new SolidColorBrush(Color.FromRgb(175, 255, 215)),
            159 => new SolidColorBrush(Color.FromRgb(175, 255, 255)),
            160 => new SolidColorBrush(Color.FromRgb(215, 0, 0)),
            161 => new SolidColorBrush(Color.FromRgb(215, 0, 95)),
            162 => new SolidColorBrush(Color.FromRgb(215, 0, 135)),
            163 => new SolidColorBrush(Color.FromRgb(215, 0, 175)),
            164 => new SolidColorBrush(Color.FromRgb(215, 0, 215)),
            165 => new SolidColorBrush(Color.FromRgb(215, 0, 255)),
            166 => new SolidColorBrush(Color.FromRgb(215, 95, 0)),
            167 => new SolidColorBrush(Color.FromRgb(215, 95, 95)),
            168 => new SolidColorBrush(Color.FromRgb(215, 95, 135)),
            169 => new SolidColorBrush(Color.FromRgb(215, 95, 175)),
            170 => new SolidColorBrush(Color.FromRgb(215, 95, 215)),
            171 => new SolidColorBrush(Color.FromRgb(215, 95, 255)),
            172 => new SolidColorBrush(Color.FromRgb(215, 135, 0)),
            173 => new SolidColorBrush(Color.FromRgb(215, 135, 95)),
            174 => new SolidColorBrush(Color.FromRgb(215, 135, 135)),
            175 => new SolidColorBrush(Color.FromRgb(215, 135, 175)),
            176 => new SolidColorBrush(Color.FromRgb(215, 135, 215)),
            177 => new SolidColorBrush(Color.FromRgb(215, 135, 255)),
            178 => new SolidColorBrush(Color.FromRgb(215, 175, 0)),
            179 => new SolidColorBrush(Color.FromRgb(215, 175, 95)),
            180 => new SolidColorBrush(Color.FromRgb(215, 175, 135)),
            181 => new SolidColorBrush(Color.FromRgb(215, 175, 175)),
            182 => new SolidColorBrush(Color.FromRgb(215, 175, 215)),
            183 => new SolidColorBrush(Color.FromRgb(215, 175, 255)),
            184 => new SolidColorBrush(Color.FromRgb(215, 215, 0)),
            185 => new SolidColorBrush(Color.FromRgb(215, 215, 95)),
            186 => new SolidColorBrush(Color.FromRgb(215, 215, 135)),
            187 => new SolidColorBrush(Color.FromRgb(215, 215, 175)),
            188 => new SolidColorBrush(Color.FromRgb(215, 215, 215)),
            189 => new SolidColorBrush(Color.FromRgb(215, 215, 255)),
            190 => new SolidColorBrush(Color.FromRgb(215, 255, 0)),
            191 => new SolidColorBrush(Color.FromRgb(215, 255, 95)),
            192 => new SolidColorBrush(Color.FromRgb(215, 255, 135)),
            193 => new SolidColorBrush(Color.FromRgb(215, 255, 175)),
            194 => new SolidColorBrush(Color.FromRgb(215, 255, 215)),
            195 => new SolidColorBrush(Color.FromRgb(215, 255, 255)),
            196 => new SolidColorBrush(Color.FromRgb(255, 0, 0)),
            197 => new SolidColorBrush(Color.FromRgb(255, 0, 95)),
            198 => new SolidColorBrush(Color.FromRgb(255, 0, 135)),
            199 => new SolidColorBrush(Color.FromRgb(255, 0, 175)),
            200 => new SolidColorBrush(Color.FromRgb(255, 0, 215)),
            201 => new SolidColorBrush(Color.FromRgb(255, 0, 255)),
            202 => new SolidColorBrush(Color.FromRgb(255, 95, 0)),
            203 => new SolidColorBrush(Color.FromRgb(255, 95, 95)),
            204 => new SolidColorBrush(Color.FromRgb(255, 95, 135)),
            205 => new SolidColorBrush(Color.FromRgb(255, 95, 175)),
            206 => new SolidColorBrush(Color.FromRgb(255, 95, 215)),
            207 => new SolidColorBrush(Color.FromRgb(255, 95, 255)),
            208 => new SolidColorBrush(Color.FromRgb(255, 135, 0)),
            209 => new SolidColorBrush(Color.FromRgb(255, 135, 95)),
            210 => new SolidColorBrush(Color.FromRgb(255, 135, 135)),
            211 => new SolidColorBrush(Color.FromRgb(255, 135, 175)),
            212 => new SolidColorBrush(Color.FromRgb(255, 135, 215)),
            213 => new SolidColorBrush(Color.FromRgb(255, 135, 255)),
            214 => new SolidColorBrush(Color.FromRgb(255, 175, 0)),
            215 => new SolidColorBrush(Color.FromRgb(255, 175, 95)),
            216 => new SolidColorBrush(Color.FromRgb(255, 175, 135)),
            217 => new SolidColorBrush(Color.FromRgb(255, 175, 175)),
            218 => new SolidColorBrush(Color.FromRgb(255, 175, 215)),
            219 => new SolidColorBrush(Color.FromRgb(255, 175, 255)),
            220 => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            221 => new SolidColorBrush(Color.FromRgb(255, 215, 95)),
            222 => new SolidColorBrush(Color.FromRgb(255, 215, 135)),
            223 => new SolidColorBrush(Color.FromRgb(255, 215, 175)),
            224 => new SolidColorBrush(Color.FromRgb(255, 215, 215)),
            225 => new SolidColorBrush(Color.FromRgb(255, 215, 255)),
            226 => new SolidColorBrush(Color.FromRgb(255, 255, 0)),
            227 => new SolidColorBrush(Color.FromRgb(255, 255, 95)),
            228 => new SolidColorBrush(Color.FromRgb(255, 255, 135)),
            229 => new SolidColorBrush(Color.FromRgb(255, 255, 175)),
            230 => new SolidColorBrush(Color.FromRgb(255, 255, 215)),
            231 => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            232 => new SolidColorBrush(Color.FromRgb(8, 8, 8)),
            233 => new SolidColorBrush(Color.FromRgb(18, 18, 18)),
            234 => new SolidColorBrush(Color.FromRgb(28, 28, 28)),
            235 => new SolidColorBrush(Color.FromRgb(38, 38, 38)),
            236 => new SolidColorBrush(Color.FromRgb(48, 48, 48)),
            237 => new SolidColorBrush(Color.FromRgb(58, 58, 58)),
            238 => new SolidColorBrush(Color.FromRgb(68, 68, 68)),
            239 => new SolidColorBrush(Color.FromRgb(78, 78, 78)),
            240 => new SolidColorBrush(Color.FromRgb(88, 88, 88)),
            241 => new SolidColorBrush(Color.FromRgb(98, 98, 98)),
            242 => new SolidColorBrush(Color.FromRgb(108, 108, 108)),
            243 => new SolidColorBrush(Color.FromRgb(118, 118, 118)),
            244 => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            245 => new SolidColorBrush(Color.FromRgb(138, 138, 138)),
            246 => new SolidColorBrush(Color.FromRgb(148, 148, 148)),
            247 => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            248 => new SolidColorBrush(Color.FromRgb(168, 168, 168)),
            249 => new SolidColorBrush(Color.FromRgb(178, 178, 178)),
            250 => new SolidColorBrush(Color.FromRgb(188, 188, 188)),
            251 => new SolidColorBrush(Color.FromRgb(198, 198, 198)),
            252 => new SolidColorBrush(Color.FromRgb(208, 208, 208)),
            253 => new SolidColorBrush(Color.FromRgb(218, 218, 218)),
            254 => new SolidColorBrush(Color.FromRgb(228, 228, 228)),
            255 => new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            _ => throw new ArgumentOutOfRangeException(nameof(xtermColor), "Color code must be between 0 and 255."),
        };
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
        context.FillRectangle(Brushes.Black, rect);

        foreach (var item in ConsoleText)
        {
            var rect2 = new Rect(_consoleTextSize.Width * item.Key.x, _consoleTextSize.Height * item.Key.y, _consoleTextSize.Width +1, _consoleTextSize.Height+1);
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