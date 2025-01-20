using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using System;
using System.Globalization;

namespace AvaloniaTerminal;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

public class TerminalControl : Control
{
    static TerminalControl()
    {
        AffectsRender<TerminalControl>(AngleProperty);
    }

    public TerminalControl()
    {
    }

    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<TerminalControl, double>(nameof(Angle));

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public override void Render(DrawingContext drawingContext)
    {
        var brush = new SolidColorBrush();
        brush.Color = Colors.Black;

        var typeface = new Typeface("Cascadia Mono");

        var formattedText = new FormattedText("HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, brush);

        for (int i = 0; i < 14; i++)
        {
            var point1 = GetPoint(i, 0);
            drawingContext.DrawText(formattedText, point1);
        }
    }

    private Point GetPoint(int row, int column)
    {
        var size = CalculateTextSize("a", "Cascadia Mono", 12);

        return new Point(column * size.Width, row * (size.Height - 5));
    }

    public static Size CalculateTextSize(string text, string fontName, double myFontSize)
    {
        var myFont = FontFamily.Parse(fontName) ?? throw new ArgumentException($"The resource {fontName} is not a FontFamily.");

        var typeface = new Typeface(myFont);
        var shaped = TextShaper.Current.ShapeText(text, new TextShaperOptions(typeface.GlyphTypeface, myFontSize));
        var run = new ShapedTextRun(shaped, new GenericTextRunProperties(typeface, myFontSize));
        return run.Size;
    }
}

