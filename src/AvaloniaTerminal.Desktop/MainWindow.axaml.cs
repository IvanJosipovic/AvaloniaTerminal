using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;

namespace AvaloniaTerminal;

public partial class MainWindow : Window
{
    private Process _process;
    private StreamWriter _streamWriter;

    public MainWindow()
    {
        InitializeComponent();
        StartProcess();
    }

    private void StartProcess()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh.exe",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            },
        };

        _process.Start();

        tc.UserInput = Input;

        _streamWriter = _process.StandardInput;

        _ = Task.Run(async () =>
        {
            while (_process.StandardOutput.BaseStream.CanRead)
            {
                const int bufferSize = 4096; // 4KB buffer size
                byte[] buffer = new byte[bufferSize];
                if (await _process.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(0, bufferSize)) > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tc.Feed(buffer, bufferSize);
                    });
                }
            }
        });
    }

    private void Input(byte[] input)
    {
        if (_streamWriter.BaseStream.CanWrite)
        {
            try
            {
                _streamWriter.Write(Encoding.UTF8.GetString(input));
            }
            catch (IOException)
            {
            }
        }
    }
}
