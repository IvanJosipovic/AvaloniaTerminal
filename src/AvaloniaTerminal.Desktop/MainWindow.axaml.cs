using Avalonia.Controls;

namespace AvaloniaTerminal;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();


        tc.Feed("This is a test 123");
    }
}

