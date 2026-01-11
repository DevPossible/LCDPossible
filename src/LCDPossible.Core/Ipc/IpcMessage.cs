using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCDPossible.Core.Ipc;

/// <summary>
/// IPC request message sent from CLI to service.
/// </summary>
public sealed record IpcRequest
{
    /// <summary>
    /// Unique request identifier for correlating responses.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Command name (e.g., "show", "status", "set-image").
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Optional command arguments.
    /// </summary>
    [JsonPropertyName("args")]
    public Dictionary<string, JsonElement>? Args { get; init; }

    /// <summary>
    /// Creates a new request with a generated ID.
    /// </summary>
    public static IpcRequest Create(string command, Dictionary<string, JsonElement>? args = null)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Command = command,
            Args = args
        };

    /// <summary>
    /// Creates a new request with string arguments.
    /// </summary>
    public static IpcRequest Create(string command, Dictionary<string, string>? args)
    {
        Dictionary<string, JsonElement>? jsonArgs = null;
        if (args is { Count: > 0 })
        {
            jsonArgs = args.ToDictionary(
                kvp => kvp.Key,
                kvp => JsonDocument.Parse($"\"{kvp.Value}\"").RootElement.Clone());
        }
        return Create(command, jsonArgs);
    }

    /// <summary>
    /// Gets a string argument value.
    /// </summary>
    public string? GetString(string key)
    {
        if (Args is null || !Args.TryGetValue(key, out var element))
            return null;

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    /// <summary>
    /// Gets an integer argument value.
    /// </summary>
    public int? GetInt(string key)
    {
        if (Args is null || !Args.TryGetValue(key, out var element))
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.String when int.TryParse(element.GetString(), out var val) => val,
            _ => null
        };
    }
}

/// <summary>
/// IPC response message sent from service to CLI.
/// </summary>
public sealed record IpcResponse
{
    /// <summary>
    /// Request ID this response corresponds to.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Whether the command succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// Response data (command-specific).
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; init; }

    /// <summary>
    /// Error details if success is false.
    /// </summary>
    [JsonPropertyName("error")]
    public IpcError? Error { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static IpcResponse Ok(string requestId, object? data = null)
        => new() { Id = requestId, Success = true, Data = data };

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static IpcResponse Fail(string requestId, string code, string message)
        => new()
        {
            Id = requestId,
            Success = false,
            Error = new IpcError { Code = code, Message = message }
        };

    /// <summary>
    /// Creates an error response from an exception.
    /// </summary>
    public static IpcResponse Fail(string requestId, Exception ex)
        => Fail(requestId, IpcErrorCodes.InternalError, ex.Message);
}

/// <summary>
/// Error details in an IPC response.
/// </summary>
public sealed record IpcError
{
    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Standard IPC error codes.
/// </summary>
public static class IpcErrorCodes
{
    /// <summary>No LCD devices found.</summary>
    public const string DeviceNotFound = "DEVICE_NOT_FOUND";

    /// <summary>Unknown or unsupported command.</summary>
    public const string InvalidCommand = "INVALID_COMMAND";

    /// <summary>Invalid or missing arguments.</summary>
    public const string InvalidArgs = "INVALID_ARGS";

    /// <summary>Specified file does not exist.</summary>
    public const string FileNotFound = "FILE_NOT_FOUND";

    /// <summary>Service is busy and cannot process request.</summary>
    public const string ServiceBusy = "SERVICE_BUSY";

    /// <summary>Service is not running.</summary>
    public const string ServiceNotRunning = "SERVICE_NOT_RUNNING";

    /// <summary>An internal error occurred.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Operation timed out.</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>Invalid panel specification.</summary>
    public const string InvalidPanel = "INVALID_PANEL";

    /// <summary>Profile not found or invalid.</summary>
    public const string ProfileError = "PROFILE_ERROR";
}

/// <summary>
/// Service status information returned by the status command.
/// </summary>
public sealed record ServiceStatus
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("profileName")]
    public string? ProfileName { get; init; }

    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; init; } = true;

    [JsonPropertyName("connectedDevices")]
    public int ConnectedDevices { get; init; }

    [JsonPropertyName("devices")]
    public List<DeviceStatus> Devices { get; init; } = [];

    [JsonPropertyName("currentSlideshow")]
    public SlideshowStatus? CurrentSlideshow { get; init; }
}

/// <summary>
/// Device status information.
/// </summary>
public sealed record DeviceStatus
{
    [JsonPropertyName("uniqueId")]
    public required string UniqueId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("currentMode")]
    public string? CurrentMode { get; init; }

    [JsonPropertyName("brightness")]
    public int? Brightness { get; init; }
}

/// <summary>
/// Slideshow status information.
/// </summary>
public sealed record SlideshowStatus
{
    [JsonPropertyName("currentIndex")]
    public int CurrentIndex { get; init; }

    [JsonPropertyName("totalSlides")]
    public int TotalSlides { get; init; }

    [JsonPropertyName("currentPanel")]
    public string? CurrentPanel { get; init; }

    [JsonPropertyName("secondsRemaining")]
    public int SecondsRemaining { get; init; }
}

/// <summary>
/// JSON serialization options for IPC messages.
/// </summary>
public static class IpcJsonOptions
{
    /// <summary>
    /// Shared JSON serializer options for IPC messages.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
