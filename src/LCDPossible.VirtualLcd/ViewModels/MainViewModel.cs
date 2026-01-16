using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using LCDPossible.VirtualLcd.Network;
using LCDPossible.VirtualLcd.Protocols;

namespace LCDPossible.VirtualLcd.ViewModels;

/// <summary>
/// Main view model for the Virtual LCD application.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILcdProtocol _protocol;
    private readonly UdpHidReceiver _receiver;
    private readonly DiscoveryResponder _discoveryResponder;
    private bool _disposed;

    [ObservableProperty]
    private string _windowTitle = "Virtual LCD";

    [ObservableProperty]
    private string _statusText = "Initializing...";

    [ObservableProperty]
    private bool _showStats;

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private long _packetsReceived;

    [ObservableProperty]
    private long _framesReceived;

    [ObservableProperty]
    private double _bytesPerSecond;

    [ObservableProperty]
    private string _protocolName = "";

    [ObservableProperty]
    private int _displayWidth;

    [ObservableProperty]
    private int _displayHeight;

    [ObservableProperty]
    private string _listenEndpoint = "";

    /// <summary>
    /// Event raised when a new frame is ready for display.
    /// </summary>
    public event EventHandler<FrameReceivedEventArgs>? FrameReady;

    public MainViewModel(ILcdProtocol protocol, IPAddress bindAddress, int port, bool showStats = false, string? instanceName = null)
    {
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _receiver = new UdpHidReceiver(protocol, bindAddress, port);
        _discoveryResponder = new DiscoveryResponder(protocol, port, instanceName);

        // Set up properties
        ProtocolName = protocol.DisplayName;
        DisplayWidth = protocol.Width;
        DisplayHeight = protocol.Height;
        ListenEndpoint = $"{bindAddress}:{port}";
        WindowTitle = $"Virtual LCD - {protocol.DisplayName} ({protocol.Width}x{protocol.Height}) - Port {port}";
        ShowStats = showStats;

        // Wire up events
        _receiver.FrameReceived += OnFrameReceived;
        _receiver.Error += OnReceiverError;
        _receiver.StatsUpdated += OnStatsUpdated;
    }

    /// <summary>
    /// Start receiving frames and responding to discovery requests.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StatusText = $"Listening on {ListenEndpoint}...";
        _discoveryResponder.Start();
        _receiver.Start();
    }

    /// <summary>
    /// Stop receiving frames and responding to discovery requests.
    /// </summary>
    public async Task StopAsync()
    {
        StatusText = "Stopping...";
        await _discoveryResponder.StopAsync();
        await _receiver.StopAsync();
        StatusText = "Stopped";
    }

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        // Forward to view for display
        FrameReady?.Invoke(this, e);
    }

    private void OnReceiverError(object? sender, ReceiverErrorEventArgs e)
    {
        StatusText = $"Error: {e.Message}";
    }

    private void OnStatsUpdated(object? sender, ReceiverStatsEventArgs e)
    {
        var stats = e.Stats;
        Fps = stats.Fps;
        PacketsReceived = stats.PacketsReceived;
        FramesReceived = stats.FramesReceived;
        BytesPerSecond = stats.BytesPerSecond;

        if (stats.FramesReceived > 0)
        {
            StatusText = $"Receiving frames at {stats.Fps:F1} FPS";
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _receiver.FrameReceived -= OnFrameReceived;
        _receiver.Error -= OnReceiverError;
        _receiver.StatsUpdated -= OnStatsUpdated;
        _receiver.Dispose();
        _discoveryResponder.Dispose();
        _disposed = true;
    }
}
