using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using System.Text;
using XtermSharp;

namespace AvaloniaTerminal;

public partial class TerminalControl : UserControl, ITerminalDelegate
{
    private Grid _grid;

    static TerminalControl()
    {
        AffectsRender<TerminalControl>(ConsoleTextProperty);
    }

    public TerminalControl()
    {
        // get the dimensions of terminal (cols and rows)
        var dimensions = CalculateVisibleRowsAndColumns();
        var options = new TerminalOptions() { Cols = dimensions.cols, Rows = dimensions.rows };

        _grid = new Grid();
        Focusable = true;

        this.Content = _grid;

        for (int i = 0; i < dimensions.rows; i++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        }

        for (int i = 0; i < dimensions.cols; i++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        }

        // the terminal itself and services
        Terminal = new Terminal(this, options);
        SelectionService = new SelectionService(Terminal);
        SearchService = new SearchService(Terminal);

        // trigger an update of the buffers
        FullBufferUpdate();
        UpdateDisplay();

        KeyUp += TerminalControl_KeyUp;
        //this.Focus(NavigationMethod.Pointer);
    }

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

    public static readonly StyledProperty<string> ConsoleTextProperty = AvaloniaProperty.Register<TerminalControl, string>(nameof(ConsoleText));

    public string ConsoleText
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
    public event Action<int, int, float, float> SizeChanged;

    /// <summary>
    /// Invoked to raise input on the control, which should probably be sent to the actual child process or remote connection
    /// </summary>
    public Action<byte[]> UserInput;

    private Size? _textSize;

    // The code below is intended to not repaint too often, which can produce flicker, for example
    // when the user refreshes the display, and this repains the screen, as dispatch delivers data
    // in blocks of 1024 bytes, which is not enough to cover the whole screen, so this delays
    // the update for a 1/600th of a secon.
    bool pendingDisplay;

    void QueuePendingDisplay()
    {
        // throttle
        if (!pendingDisplay)
        {
            pendingDisplay = true;
            DispatcherTimer.RunOnce(UpdateDisplay, TimeSpan.FromMilliseconds(33.34)); // Delay of 33.34 ms
        }
    }

    private Size CalculateTextSize()
    {
        if (_textSize != null)
        {
            return _textSize.Value;
        }

        var myFont = FontFamily.Parse(FontName) ?? throw new ArgumentException($"The resource {FontName} is not a FontFamily.");

        var typeface = new Typeface(myFont);
        var shaped = TextShaper.Current.ShapeText("a", new TextShaperOptions(typeface.GlyphTypeface, FontSize));
        var run = new ShapedTextRun(shaped, new GenericTextRunProperties(typeface, FontSize));
        _textSize = run.Size;

        return run.Size;
    }

    private (int cols, int rows) CalculateVisibleRowsAndColumns()
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return (80, 25);
        }

        var fontSize = CalculateTextSize();

        var cols = (int)(Bounds.Width / fontSize.Width);
        var rows = (int)(Bounds.Height / fontSize.Height);

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
        //SendResize();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        Resize();
    }

    void FullBufferUpdate()
    {
        _grid.Children.Clear();

        for (var line = Terminal.Buffer.YBase; line < Terminal.Buffer.YBase + Terminal.Rows; line++)
        {
            for (var cell = 0; cell < Terminal.Cols; cell++)
            {
                var cd = Terminal.Buffer.Lines[line][cell];
                var text = new TextBlock();

                text[Grid.ColumnProperty] = cell;
                text[Grid.RowProperty] = line;

                text.FontFamily = FontFamily.Parse(FontName);
                text.FontSize = FontSize;

                text.Text = cd.Code == 0 ? "" : ((char)cd.Rune).ToString();
                //text.Text = "X";
                _grid.Children.Add(text);
            }
        }
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

                var existing = _grid.Children.FirstOrDefault(c => c.GetValue(Grid.RowProperty) == line && c.GetValue(Grid.ColumnProperty) == cell);

                TextBlock text = null;

                if (existing is TextBlock txt)
                {
                    text = txt;
                }
                else
                {
                    text = new TextBlock();
                    text[Grid.ColumnProperty] = cell;
                    text[Grid.RowProperty] = line;
                }

                text.FontFamily = FontFamily.Parse(FontName);
                text.FontSize = FontSize;

                text.Text = cd.Code == 0 ? "" : ((char)cd.Rune).ToString();

                if (existing == null)
                {
                    _grid.Children.Add(text);
                }
            }
        }

        //UpdateCursorPosition();
        //UpdateScroller();

        pendingDisplay = false;
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
}
