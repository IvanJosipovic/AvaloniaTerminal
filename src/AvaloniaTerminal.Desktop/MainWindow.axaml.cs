using Avalonia.Controls;
using Avalonia.Threading;
using CliWrap;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AvaloniaTerminal;

public partial class MainWindow : Window
{
    private readonly Stream _outputStream = new MemoryStream();

    private readonly Stream _inputStream = new MemoryStream();

    public MainWindow()
    {
        InitializeComponent();

        tc.UserInput = Input;

        var result = Cli.Wrap("cmd")
        .WithStandardOutputPipe(PipeTarget.ToStream(_outputStream))
        .WithStandardInputPipe(PipeSource.FromStream(_inputStream))
        .ExecuteAsync();

        result.GetAwaiter();

        _ = Task.Run(async () => {

            byte[] buffer = new byte[2048];
            int bytesRead = 0;

            _outputStream.Position = bytesRead;
            while ((bytesRead = _outputStream.Read(buffer, bytesRead, buffer.Length)) > 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tc.Feed(buffer, buffer.Length);
                });
            }
        });
    }

    private void Input(byte[] input)
    {
        _inputStream.Write(input, 0, input.Length);
    }
}

