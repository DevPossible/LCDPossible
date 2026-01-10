using HidSharp;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Usb;

/// <summary>
/// HidSharp-based implementation of IHidDevice.
/// </summary>
internal sealed class HidSharpDevice : IHidDevice
{
    private readonly HidDevice _device;
    private readonly ILogger<HidSharpDevice>? _logger;
    private HidStream? _stream;
    private bool _disposed;

    public HidSharpDevice(HidDevice device, ILogger<HidSharpDevice>? logger = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger;
    }

    public string DevicePath => _device.DevicePath;
    public ushort VendorId => (ushort)_device.VendorID;
    public ushort ProductId => (ushort)_device.ProductID;
    public string? Manufacturer => TryGetProperty(() => _device.GetManufacturer());
    public string? ProductName => TryGetProperty(() => _device.GetProductName());
    public bool IsOpen => _stream != null;
    public int MaxOutputReportLength => _device.GetMaxOutputReportLength();
    public int MaxInputReportLength => _device.GetMaxInputReportLength();

    public void Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stream != null)
        {
            throw new InvalidOperationException("Device is already open.");
        }

        try
        {
            _stream = _device.Open();
            _stream.ReadTimeout = 1000;
            _stream.WriteTimeout = 1000;
            _logger?.LogDebug("Opened HID device: {DevicePath}", DevicePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open HID device: {DevicePath}", DevicePath);
            throw new IOException($"Failed to open device: {DevicePath}", ex);
        }
    }

    public void Close()
    {
        if (_stream == null)
        {
            return;
        }

        try
        {
            _stream.Close();
            _stream.Dispose();
            _logger?.LogDebug("Closed HID device: {DevicePath}", DevicePath);
        }
        finally
        {
            _stream = null;
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stream == null)
        {
            throw new InvalidOperationException("Device is not open.");
        }

        try
        {
            _stream.Write(data);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write to HID device: {DevicePath}", DevicePath);
            throw new IOException($"Failed to write to device: {DevicePath}", ex);
        }
    }

    public int Read(Span<byte> buffer, int timeout = 1000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stream == null)
        {
            throw new InvalidOperationException("Device is not open.");
        }

        try
        {
            _stream.ReadTimeout = timeout;
            return _stream.Read(buffer);
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read from HID device: {DevicePath}", DevicePath);
            throw new IOException($"Failed to read from device: {DevicePath}", ex);
        }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stream == null)
        {
            throw new InvalidOperationException("Device is not open.");
        }

        try
        {
            await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to write to HID device: {DevicePath}", DevicePath);
            throw new IOException($"Failed to write to device: {DevicePath}", ex);
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, int timeout = 1000, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_stream == null)
        {
            throw new InvalidOperationException("Device is not open.");
        }

        try
        {
            _stream.ReadTimeout = timeout;
            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to read from HID device: {DevicePath}", DevicePath);
            throw new IOException($"Failed to read from device: {DevicePath}", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _disposed = true;
    }

    private static string? TryGetProperty(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
