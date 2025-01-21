using Avalonia;
using Avalonia.Controls;
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
        FullBufferUpdate();
        return;
        Terminal.GetUpdateRange(out var rowStart, out var rowEnd);
        Terminal.ClearUpdateRange();

        var cols = Terminal.Cols;
        var tb = Terminal.Buffer;
        for (int row = rowStart; row <= rowEnd; row++)
        {
            //buffer[row + tb.YDisp] = BuildAttributedString(Terminal.Buffer.Lines[row + tb.YDisp], cols);
        }

        //UpdateCursorPosition();
        //UpdateScroller();

        if (rowStart == int.MaxValue || rowEnd < 0)
        {
            //SetNeedsDisplayInRect(Bounds);
        }
        else
        {
            //var rowY = Bounds.Height - contentPadding - cellDimensions.GetRowPos(rowEnd);
            //var region = new CGRect(0, rowY, Bounds.Width, cellDimensions.Height * (rowEnd - rowStart + 1));

            //SetNeedsDisplayInRect(region);
        }

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
}
