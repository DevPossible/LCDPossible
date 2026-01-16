using Avalonia.Controls;
using LCDPossible.VirtualLcd.Network;
using LCDPossible.VirtualLcd.ViewModels;

namespace LCDPossible.VirtualLcd.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        // Set initial window size based on display dimensions
        Width = viewModel.DisplayWidth;
        Height = viewModel.DisplayHeight + 30; // Add space for status bar

        // Wire up frame events
        viewModel.FrameReady += OnFrameReady;

        // Start receiving
        viewModel.Start();
    }

    private void OnFrameReady(object? sender, FrameReceivedEventArgs e)
    {
        LcdDisplay.UpdateFrame(e.ImageData, e.Format);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.FrameReady -= OnFrameReady;
            _ = _viewModel.StopAsync();
            _viewModel.Dispose();
        }

        base.OnClosing(e);
    }
}
