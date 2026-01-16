using System.Net;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LCDPossible.VirtualLcd.Protocols;
using LCDPossible.VirtualLcd.ViewModels;
using LCDPossible.VirtualLcd.Views;

namespace LCDPossible.VirtualLcd;

public class App : Application
{
    /// <summary>
    /// Startup options passed from CLI.
    /// </summary>
    public static StartupOptions Options { get; set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create protocol
            var protocol = ProtocolRegistry.Default.CreateProtocol(Options.Protocol);

            // Create view model
            var viewModel = new MainViewModel(
                protocol,
                Options.BindAddress,
                Options.Port,
                Options.ShowStats,
                Options.InstanceName);

            // Create and show main window
            var mainWindow = new MainWindow();

            // Apply window options
            if (Options.AlwaysOnTop)
            {
                mainWindow.Topmost = true;
            }

            if (Options.Borderless)
            {
                mainWindow.SystemDecorations = Avalonia.Controls.SystemDecorations.None;
            }

            if (Options.Scale != 1.0)
            {
                mainWindow.Width = protocol.Width * Options.Scale;
                mainWindow.Height = (protocol.Height + 30) * Options.Scale;
            }

            mainWindow.Initialize(viewModel);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Startup options from command line.
/// </summary>
public class StartupOptions
{
    public string Protocol { get; set; } = ProtocolRegistry.DefaultProtocolId;
    public int Port { get; set; } = 5302;
    public IPAddress BindAddress { get; set; } = IPAddress.Any;
    public bool ShowStats { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool Borderless { get; set; }
    public double Scale { get; set; } = 1.0;
    public string? InstanceName { get; set; }
}
