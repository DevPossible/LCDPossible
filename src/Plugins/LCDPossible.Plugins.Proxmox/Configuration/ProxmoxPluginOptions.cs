namespace LCDPossible.Plugins.Proxmox.Configuration;

/// <summary>
/// Proxmox VE connection options.
/// </summary>
public sealed class ProxmoxPluginOptions
{
    /// <summary>
    /// Whether Proxmox integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Proxmox API URL (e.g., "https://proxmox.local:8006").
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// API token ID (format: "user@realm!tokenid").
    /// </summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// API token secret.
    /// </summary>
    public string TokenSecret { get; set; } = string.Empty;

    /// <summary>
    /// Whether to skip SSL certificate verification (for self-signed certs).
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = false;

    /// <summary>
    /// Polling interval in seconds for fetching metrics.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Whether to show individual VM status.
    /// </summary>
    public bool ShowVms { get; set; } = true;

    /// <summary>
    /// Whether to show individual container status.
    /// </summary>
    public bool ShowContainers { get; set; } = true;

    /// <summary>
    /// Whether to show cluster alerts.
    /// </summary>
    public bool ShowAlerts { get; set; } = true;

    /// <summary>
    /// Maximum number of items to display per category.
    /// </summary>
    public int MaxDisplayItems { get; set; } = 10;
}
