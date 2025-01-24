using Avalonia.Controls;
using Avalonia.Threading;
using k8s;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Text.Json;

namespace AvaloniaTerminal;

public partial class MainWindow : Window
{
    private WebSocket? _webSocket;
    private StreamDemuxer? _streamDemuxer;
    private Stream? _stream;
    private Stream? _refreshStream;

    public MainWindow()
    {
        InitializeComponent();

        var name = "ubuntu-sleep-deployment-566b5954cf-6ffcl";
        var @namespace = "default";
        var containerName = "ubuntu-sleep";

        tc.UserInput = Input;
        tc.SizeChanged += TerminalControl_SizeChanged;

        var command = new string[]
        {
            "sh",
            "-c",
            "clear; (bash || ash || sh || echo 'No Shell Found!')",
        };

        var command2 = new string[]
        {
            "cmatrix"
            //"mc"
        };

        var client = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());

        _webSocket = client.WebSocketNamespacedPodExecAsync(name, @namespace, command, containerName).GetAwaiter().GetResult();

        _streamDemuxer = new StreamDemuxer(_webSocket);
        _streamDemuxer.Start();

        _stream = _streamDemuxer.GetStream(ChannelIndex.StdOut, ChannelIndex.StdIn);
        _refreshStream = _streamDemuxer.GetStream(null, ChannelIndex.Resize);

        _ = Task.Run(async () =>
        {
            while (_stream.CanRead)
            {
                try
                {
                    const int bufferSize = 4096; // 4KB buffer size
                    byte[] buffer = new byte[bufferSize];
                    if (await _stream.ReadAsync(buffer.AsMemory(0, bufferSize)) > 0)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            tc.Feed(buffer, bufferSize);
                        });
                    }
                }
                catch (IOException ex) when (ex.Message.Equals("The request was aborted.")) { break; }
                catch (ObjectDisposedException) { break; }
            }
        });
    }

    /// <summary>
    /// Terminal Control Size Changed
    /// </summary>
    /// <param name="cols">Cols</param>
    /// <param name="rows">Rows</param>
    /// <param name="width">Width</param>
    /// <param name="height">Height</param>
    private void TerminalControl_SizeChanged(int cols, int rows, double width, double height)
    {
        var size = new TerminalSize
        {
            Width = (ushort)cols,
            Height = (ushort)rows,
        };

        if (_refreshStream?.CanWrite == true)
        {
            try
            {
                _refreshStream?.Write(JsonSerializer.SerializeToUtf8Bytes(size));
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error Sending Resize to console");
            }
        }
    }

    private void Input(byte[] input)
    {
        _stream?.Write(input);
    }
}

public struct TerminalSize
{
    public ushort Width { get; set; }
    public ushort Height { get; set; }
}