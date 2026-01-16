# Plugin System, Sensors & CLI Refactor Plan

## Executive Summary

This document outlines a major refactor of LCDPossible's architecture to:
1. **Unified Services**: Single `ILcdServices` facade providing centralized access to all plugin entities
2. **Sensors**: Unified sensor abstraction layer for all data acquisition
3. **Effects**: Visual post-processing effects as a plugin-extensible system
4. **Transitions**: Extended transition system that is plugin-extensible
5. **Plugin System**: Enhanced to support Panels, Sensors, Transitions, and Effects with consistent patterns
6. **CLI Overhaul**: Restructure for better organization, discoverability, and extensibility

## Design Principles

### Principle 1: Unified Service Facade

Instead of injecting 4+ different registries into panels and other consumers, we provide a single `ILcdServices` facade:

```csharp
// Panels only need ONE dependency
public class CpuUsagePanel : WidgetPanel
{
    public CpuUsagePanel(ILcdServices services) : base(services) { }
}

// Easy access to everything
var temp = await Services.Sensors.ReadAsync<float>("cpu.temperature", ct);
var effect = Services.Effects.Create("vignette");
var transition = Services.Transitions.Create("crossfade");
var panel = Services.Panels.Create("cpu-info");
```

### Principle 2: Consistent Naming Patterns

All plugin entity types follow identical patterns:

| Concept | Sensors | Panels | Transitions | Effects |
|---------|---------|--------|-------------|---------|
| Entity Interface | `ISensor<T>` | `IDisplayPanel` | `ITransitionEffect` | `IEffect` |
| Type Info Class | `SensorTypeInfo` | `PanelTypeInfo` | `TransitionTypeInfo` | `EffectTypeInfo` |
| Registry Interface | `ISensorRegistry` | `IPanelRegistry` | `ITransitionRegistry` | `IEffectRegistry` |
| Provider Interface | `ISensorProvider` | (via plugin) | `ITransitionProvider` | `IEffectProvider` |
| Creation Context | `SensorCreationContext` | `PanelCreationContext` | `TransitionCreationContext` | `EffectCreationContext` |

### Principle 3: Consistent Registry API

All registries share the same method patterns:

```csharp
// All registries implement these patterns
interface IXxxRegistry
{
    // Discovery
    XxxTypeInfo? GetTypeInfo(string typeId);
    IEnumerable<XxxTypeInfo> GetTypes(string? category = null);
    IEnumerable<string> GetCategories();
    bool HasType(string typeId);

    // Creation (for factory-based registries)
    IXxx? Create(string typeId, XxxCreationContext? context = null);

    // Registration (internal use)
    void Register(string typeId, XxxTypeInfo info, ...);
    void Unregister(string typeId);
}
```

---

## Part 1: Sensor Architecture

### 1.1 Current State Analysis

**Current data flow:**
```
Panel → ISystemInfoProvider → HardwareMonitorProvider → Platform Monitor (Win/Linux/macOS)
```

**Problems with current approach:**
- Panels directly depend on ISystemInfoProvider
- Each plugin may create its own provider (wasteful)
- No caching strategy - each call hits hardware
- Platform abstraction is buried inside plugin code
- No way for plugins to add custom sensors
- No sensor discovery or listing capability

### 1.2 Proposed Sensor Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           ISensorRegistry                                │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐   │
│  │ CoreSensor  │  │ ProxmoxSens │  │ NetworkSens │  │ Plugin Sens  │   │
│  │  Provider   │  │  Provider   │  │  Provider   │  │  Providers   │   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬───────┘   │
│         └────────────────┴────────────────┴─────────────────┘           │
│                            ISensorProvider                               │
└────────────────────────────────────────────────────────────────┬────────┘
                                                                  │
                                    GetSensor("cpu.temperature")  │
                                                                  ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  ISensor<float> cpuTemp = registry.GetSensor<float>("cpu.temperature")  │
│  float? value = await cpuTemp.GetValueAsync(ct);                        │
│  float? cached = cpuTemp.CachedValue;                                   │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Core Interfaces

```csharp
// src/LCDPossible.Core/Sensors/ISensor.cs
namespace LCDPossible.Core.Sensors;

/// <summary>
/// Base interface for all sensors providing real-time or cached data.
/// </summary>
public interface ISensor : IDisposable
{
    /// <summary>
    /// Unique sensor identifier using dot-notation hierarchy.
    /// Examples: "cpu.usage", "cpu.temperature", "gpu.nvidia.0.usage"
    /// </summary>
    string SensorId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Category for grouping (CPU, GPU, Memory, Network, Storage, System, Custom).
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Optional unit of measurement (%, °C, GB, MHz, etc.).
    /// </summary>
    string? Unit { get; }

    /// <summary>
    /// Type of value this sensor returns.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// How this sensor updates its data.
    /// </summary>
    SensorUpdateMode UpdateMode { get; }

    /// <summary>
    /// Recommended polling interval for this sensor.
    /// </summary>
    TimeSpan UpdateInterval { get; }

    /// <summary>
    /// Whether this sensor is currently available and can provide data.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// When the cached value was last updated.
    /// </summary>
    DateTime LastUpdated { get; }

    /// <summary>
    /// Initialize the sensor. Called once during registration.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current value (may use cache based on UpdateMode and interval).
    /// </summary>
    Task<object?> GetValueAsync(CancellationToken ct = default);

    /// <summary>
    /// Force a fresh read bypassing cache.
    /// </summary>
    Task<object?> RefreshAsync(CancellationToken ct = default);
}

/// <summary>
/// Strongly-typed sensor interface.
/// </summary>
public interface ISensor<T> : ISensor
{
    /// <summary>
    /// Last cached value (may be default if never read).
    /// </summary>
    T? CachedValue { get; }

    /// <summary>
    /// Get the current value as the correct type.
    /// </summary>
    new Task<T?> GetValueAsync(CancellationToken ct = default);

    /// <summary>
    /// Force a fresh read bypassing cache.
    /// </summary>
    new Task<T?> RefreshAsync(CancellationToken ct = default);
}

/// <summary>
/// How a sensor updates its data.
/// </summary>
public enum SensorUpdateMode
{
    /// <summary>
    /// Sensor is polled at regular intervals.
    /// </summary>
    Polling,

    /// <summary>
    /// Sensor value changes rarely (e.g., hardware info).
    /// </summary>
    Static,

    /// <summary>
    /// Sensor pushes updates via events.
    /// </summary>
    EventDriven,

    /// <summary>
    /// Sensor value must be manually refreshed.
    /// </summary>
    Manual
}
```

```csharp
// src/LCDPossible.Core/Sensors/ISensorProvider.cs
namespace LCDPossible.Core.Sensors;

/// <summary>
/// Provides a collection of related sensors.
/// </summary>
public interface ISensorProvider : IDisposable
{
    /// <summary>
    /// Unique provider identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider is available on the current platform.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Initialize the provider and discover available sensors.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all sensors provided by this provider.
    /// </summary>
    IEnumerable<ISensor> GetSensors();
}
```

```csharp
// src/LCDPossible.Core/Sensors/ISensorRegistry.cs
namespace LCDPossible.Core.Sensors;

/// <summary>
/// Central registry for all sensors in the system.
/// </summary>
public interface ISensorRegistry : IDisposable
{
    /// <summary>
    /// Register a sensor provider.
    /// </summary>
    Task RegisterProviderAsync(ISensorProvider provider, CancellationToken ct = default);

    /// <summary>
    /// Unregister a provider by ID.
    /// </summary>
    void UnregisterProvider(string providerId);

    /// <summary>
    /// Get a sensor by ID.
    /// </summary>
    ISensor? GetSensor(string sensorId);

    /// <summary>
    /// Get a typed sensor by ID.
    /// </summary>
    ISensor<T>? GetSensor<T>(string sensorId);

    /// <summary>
    /// Get all sensors, optionally filtered by category.
    /// </summary>
    IEnumerable<ISensor> GetSensors(string? category = null);

    /// <summary>
    /// Get all available categories.
    /// </summary>
    IEnumerable<string> GetCategories();

    /// <summary>
    /// Convenience method to get a sensor value.
    /// </summary>
    Task<T?> GetValueAsync<T>(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Get cached value without async.
    /// </summary>
    T? GetCachedValue<T>(string sensorId);

    /// <summary>
    /// Subscribe to sensor value changes (for EventDriven sensors).
    /// </summary>
    IDisposable Subscribe(string sensorId, Action<object?> callback);
}
```

### 1.4 Sensor Hierarchy

Standard sensor IDs (all lowercase, dot-separated):

```
system.hostname           string    Static      System hostname
system.os.name            string    Static      Operating system name
system.os.version         string    Static      OS version
system.uptime             TimeSpan  Polling     System uptime

cpu.name                  string    Static      CPU model name
cpu.cores.physical        int       Static      Physical core count
cpu.cores.logical         int       Static      Logical core count
cpu.usage                 float     Polling     Total CPU usage %
cpu.usage.core.{n}        float     Polling     Per-core usage %
cpu.temperature           float     Polling     Package temperature °C
cpu.temperature.core.{n}  float     Polling     Per-core temperature °C
cpu.frequency             float     Polling     Current frequency MHz
cpu.power                 float     Polling     Package power W

gpu.{index}.name          string    Static      GPU model name
gpu.{index}.usage         float     Polling     GPU usage %
gpu.{index}.temperature   float     Polling     GPU temperature °C
gpu.{index}.memory.total  float     Static      Total VRAM MB
gpu.{index}.memory.used   float     Polling     Used VRAM MB
gpu.{index}.memory.usage  float     Polling     VRAM usage %
gpu.{index}.power         float     Polling     Power draw W
gpu.{index}.clock.core    float     Polling     Core clock MHz
gpu.{index}.clock.memory  float     Polling     Memory clock MHz

memory.total              float     Static      Total RAM GB
memory.used               float     Polling     Used RAM GB
memory.available          float     Polling     Available RAM GB
memory.usage              float     Polling     RAM usage %

network.{interface}.name  string    Static      Interface name
network.{interface}.ip    string    Polling     IP address
network.{interface}.mac   string    Static      MAC address
network.{interface}.speed float     Polling     Link speed Mbps
network.{interface}.rx    float     Polling     Download speed MB/s
network.{interface}.tx    float     Polling     Upload speed MB/s

storage.{index}.name      string    Static      Drive name
storage.{index}.total     float     Static      Total capacity GB
storage.{index}.used      float     Polling     Used space GB
storage.{index}.usage     float     Polling     Usage %
storage.{index}.temperature float   Polling     Temperature °C
storage.{index}.read      float     Polling     Read speed MB/s
storage.{index}.write     float     Polling     Write speed MB/s
```

### 1.5 Caching Strategy

```csharp
// src/LCDPossible.Core/Sensors/BaseSensor.cs
namespace LCDPossible.Core.Sensors;

/// <summary>
/// Base implementation with built-in caching.
/// </summary>
public abstract class BaseSensor<T> : ISensor<T>
{
    private T? _cachedValue;
    private DateTime _lastUpdated = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public abstract string SensorId { get; }
    public abstract string DisplayName { get; }
    public abstract string Category { get; }
    public virtual string? Unit => null;
    public Type ValueType => typeof(T);
    public virtual SensorUpdateMode UpdateMode => SensorUpdateMode.Polling;
    public virtual TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);
    public virtual bool IsAvailable => true;
    public DateTime LastUpdated => _lastUpdated;
    public T? CachedValue => _cachedValue;

    public virtual Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<T?> GetValueAsync(CancellationToken ct = default)
    {
        // For static sensors, only read once
        if (UpdateMode == SensorUpdateMode.Static && _lastUpdated != DateTime.MinValue)
            return _cachedValue;

        // Check if cache is still valid
        if (DateTime.UtcNow - _lastUpdated < UpdateInterval)
            return _cachedValue;

        return await RefreshAsync(ct);
    }

    public async Task<T?> RefreshAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cachedValue = await ReadValueAsync(ct);
            _lastUpdated = DateTime.UtcNow;
            return _cachedValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    async Task<object?> ISensor.GetValueAsync(CancellationToken ct) => await GetValueAsync(ct);
    async Task<object?> ISensor.RefreshAsync(CancellationToken ct) => await RefreshAsync(ct);

    /// <summary>
    /// Override to implement actual sensor reading.
    /// </summary>
    protected abstract Task<T?> ReadValueAsync(CancellationToken ct);

    public virtual void Dispose() { }
}
```

### 1.6 Migration from ISystemInfoProvider

**Phase 1**: Create adapter to bridge ISystemInfoProvider → ISensorRegistry
```csharp
// SystemInfoProviderAdapter - wraps old provider as sensor provider
public class SystemInfoProviderAdapter : ISensorProvider
{
    private readonly ISystemInfoProvider _provider;
    // Creates sensors that delegate to _provider.GetMetricsAsync()
}
```

**Phase 2**: Create native sensor providers in CorePlugin
- `HardwareSensorProvider` (wraps platform monitors directly)
- One sensor per metric instead of monolithic SystemMetrics

**Phase 3**: Update panels to use ISensorRegistry
- Panels receive ISensorRegistry instead of ISystemInfoProvider
- Read individual sensors as needed
- Sensors handle their own caching

---

## Part 2: Unified Services Architecture

### 2.1 The ILcdServices Facade

The `ILcdServices` interface is the **single entry point** for all plugin functionality. Panels, CLI commands, and other consumers inject only this one interface.

```csharp
// src/LCDPossible.Core/Services/ILcdServices.cs
namespace LCDPossible.Core.Services;

/// <summary>
/// Unified facade providing access to all LCD plugin services.
/// This is the single dependency panels and other consumers need.
/// </summary>
public interface ILcdServices : IDisposable
{
    // === Registry Access ===

    /// <summary>
    /// Access to sensor registry for reading hardware/system data.
    /// </summary>
    ISensorRegistry Sensors { get; }

    /// <summary>
    /// Access to panel registry for creating display panels.
    /// </summary>
    IPanelRegistry Panels { get; }

    /// <summary>
    /// Access to transition registry for creating slide transitions.
    /// </summary>
    ITransitionRegistry Transitions { get; }

    /// <summary>
    /// Access to effect registry for creating visual effects.
    /// </summary>
    IEffectRegistry Effects { get; }

    /// <summary>
    /// Access to theme registry for color schemes.
    /// </summary>
    IThemeRegistry Themes { get; }

    // === Convenience Methods (delegates to registries) ===

    /// <summary>
    /// Read a sensor value (delegates to Sensors.ReadAsync).
    /// </summary>
    Task<T?> ReadSensorAsync<T>(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Get cached sensor value (delegates to Sensors.ReadCached).
    /// </summary>
    T? ReadSensorCached<T>(string sensorId);

    /// <summary>
    /// Create a panel (delegates to Panels.Create).
    /// </summary>
    IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null);

    /// <summary>
    /// Create a transition (delegates to Transitions.Create).
    /// </summary>
    ITransitionEffect? CreateTransition(string typeId, TransitionCreationContext? context = null);

    /// <summary>
    /// Create an effect (delegates to Effects.Create).
    /// </summary>
    IEffect? CreateEffect(string typeId, EffectCreationContext? context = null);

    /// <summary>
    /// Create an effect pipeline for chaining effects.
    /// </summary>
    IEffectPipeline CreateEffectPipeline();
}
```

### 2.2 Usage Examples

**Panels use ILcdServices:**
```csharp
public class CpuUsagePanel : WidgetPanel
{
    public CpuUsagePanel(ILcdServices services) : base(services) { }

    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        // Read sensors through the unified service
        var usage = await Services.Sensors.ReadAsync<float>("cpu.usage", ct);
        var temp = await Services.Sensors.ReadAsync<float>("cpu.temperature", ct);
        var name = Services.Sensors.ReadCached<string>("cpu.name");

        return new {
            name,
            usage = usage ?? 0,
            temperature = temp
        };
    }
}
```

**CLI commands use ILcdServices:**
```csharp
public class SensorListCommand : ICliCommand
{
    public async Task<int> ExecuteAsync(CliContext context, CancellationToken ct)
    {
        var services = context.Services;

        foreach (var category in services.Sensors.GetCategories())
        {
            Console.WriteLine($"{category}:");
            foreach (var sensor in services.Sensors.GetTypes(category))
            {
                var value = await services.ReadSensorAsync<object>(sensor.TypeId, ct);
                Console.WriteLine($"  {sensor.TypeId}: {value}");
            }
        }
        return 0;
    }
}
```

**Effect pipeline:**
```csharp
var pipeline = services.CreateEffectPipeline()
    .Add("vignette", intensity: 0.5f)
    .Add("scanlines", intensity: 0.3f)
    .Add("noise", intensity: 0.1f);

pipeline.Apply(renderedFrame);
```

### 2.3 Implementation

```csharp
// src/LCDPossible.Core/Services/LcdServices.cs
namespace LCDPossible.Core.Services;

/// <summary>
/// Default implementation of ILcdServices.
/// </summary>
public sealed class LcdServices : ILcdServices
{
    public ISensorRegistry Sensors { get; }
    public IPanelRegistry Panels { get; }
    public ITransitionRegistry Transitions { get; }
    public IEffectRegistry Effects { get; }
    public IThemeRegistry Themes { get; }

    public LcdServices(
        ISensorRegistry sensors,
        IPanelRegistry panels,
        ITransitionRegistry transitions,
        IEffectRegistry effects,
        IThemeRegistry themes)
    {
        Sensors = sensors;
        Panels = panels;
        Transitions = transitions;
        Effects = effects;
        Themes = themes;
    }

    // Convenience method implementations
    public Task<T?> ReadSensorAsync<T>(string sensorId, CancellationToken ct = default)
        => Sensors.ReadAsync<T>(sensorId, ct);

    public T? ReadSensorCached<T>(string sensorId)
        => Sensors.ReadCached<T>(sensorId);

    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null)
        => Panels.Create(typeId, context);

    public ITransitionEffect? CreateTransition(string typeId, TransitionCreationContext? context = null)
        => Transitions.Create(typeId, context);

    public IEffect? CreateEffect(string typeId, EffectCreationContext? context = null)
        => Effects.Create(typeId, context);

    public IEffectPipeline CreateEffectPipeline()
        => new EffectPipeline(Effects);

    public void Dispose()
    {
        Sensors.Dispose();
        Panels.Dispose();
        Transitions.Dispose();
        Effects.Dispose();
    }
}
```

### 2.4 Dependency Injection Setup

```csharp
// In service registration
services.AddSingleton<ISensorRegistry, SensorRegistry>();
services.AddSingleton<IPanelRegistry, PanelRegistry>();
services.AddSingleton<ITransitionRegistry, TransitionRegistry>();
services.AddSingleton<IEffectRegistry, EffectRegistry>();
services.AddSingleton<IThemeRegistry, ThemeRegistry>();
services.AddSingleton<ILcdServices, LcdServices>();

// Panels just need ILcdServices
services.AddTransient<CpuUsagePanel>();
```

---

## Part 3: Consistent Registry Pattern

### 3.1 Base Type Info Class

All `*TypeInfo` classes derive from a common base:

```csharp
// src/LCDPossible.Core/Plugins/TypeInfoBase.cs
namespace LCDPossible.Core.Plugins;

/// <summary>
/// Base class for all plugin entity type information.
/// </summary>
public abstract class TypeInfoBase
{
    /// <summary>
    /// Unique type identifier (e.g., "cpu-info", "crossfade", "vignette").
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for grouping in UI (e.g., "System", "Blur", "Slide").
    /// </summary>
    public string? Category { get; init; }
}
```

### 3.2 Entity-Specific Type Info Classes

```csharp
// All type info classes follow the same pattern
public sealed class SensorTypeInfo : TypeInfoBase
{
    public Type ValueType { get; init; } = typeof(object);
    public string? Unit { get; init; }
    public SensorUpdateMode UpdateMode { get; init; } = SensorUpdateMode.Polling;
    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed class PanelTypeInfo : TypeInfoBase
{
    public string? PrefixPattern { get; init; }
    public string[]? Dependencies { get; init; }
    public bool IsLive { get; init; }
    public bool IsAnimated { get; init; }
}

public sealed class TransitionTypeInfo : TypeInfoBase
{
    public TimeSpan DefaultDuration { get; init; } = TimeSpan.FromMilliseconds(500);
    public bool SupportsReverse { get; init; } = true;
}

public sealed class EffectTypeInfo : TypeInfoBase
{
    public bool SupportsIntensity { get; init; } = true;
    public float DefaultIntensity { get; init; } = 1.0f;
}
```

### 3.3 Base Registry Interface

```csharp
// src/LCDPossible.Core/Plugins/ITypeRegistry.cs
namespace LCDPossible.Core.Plugins;

/// <summary>
/// Base interface for all entity registries.
/// </summary>
public interface ITypeRegistry<TTypeInfo> : IDisposable
    where TTypeInfo : TypeInfoBase
{
    /// <summary>
    /// Get type info by ID.
    /// </summary>
    TTypeInfo? GetTypeInfo(string typeId);

    /// <summary>
    /// Get all types, optionally filtered by category.
    /// </summary>
    IEnumerable<TTypeInfo> GetTypes(string? category = null);

    /// <summary>
    /// Get all available categories.
    /// </summary>
    IEnumerable<string> GetCategories();

    /// <summary>
    /// Check if a type is registered.
    /// </summary>
    bool HasType(string typeId);
}
```

### 3.4 Concrete Registry Interfaces

```csharp
// Sensors - instance-based (get shared instances)
public interface ISensorRegistry : ITypeRegistry<SensorTypeInfo>
{
    // Provider registration
    Task RegisterProviderAsync(ISensorProvider provider, CancellationToken ct = default);
    void UnregisterProvider(string providerId);

    // Get sensor instance (sensors are shared)
    ISensor? Get(string sensorId);
    ISensor<T>? Get<T>(string sensorId);
    IEnumerable<ISensor> GetAll(string? category = null);

    // Read values
    Task<T?> ReadAsync<T>(string sensorId, CancellationToken ct = default);
    T? ReadCached<T>(string sensorId);

    // Subscribe to changes
    IDisposable Subscribe(string sensorId, Action<object?> callback);
}

// Panels - factory-based (create new instances)
public interface IPanelRegistry : ITypeRegistry<PanelTypeInfo>
{
    void Register(string typeId, PanelTypeInfo info, Func<PanelCreationContext, IDisplayPanel?> factory);
    void Unregister(string typeId);
    IDisplayPanel? Create(string typeId, PanelCreationContext? context = null);
}

// Transitions - factory-based (create new instances)
public interface ITransitionRegistry : ITypeRegistry<TransitionTypeInfo>
{
    void Register(string typeId, TransitionTypeInfo info, Func<TransitionCreationContext, ITransitionEffect?> factory);
    void Unregister(string typeId);
    ITransitionEffect? Create(string typeId, TransitionCreationContext? context = null);
}

// Effects - factory-based (create new instances)
public interface IEffectRegistry : ITypeRegistry<EffectTypeInfo>
{
    void Register(string typeId, EffectTypeInfo info, Func<EffectCreationContext, IEffect?> factory);
    void Unregister(string typeId);
    IEffect? Create(string typeId, EffectCreationContext? context = null);
}
```

### 3.5 Creation Context Classes

All creation contexts follow the same pattern:

```csharp
// Base context with common properties
public abstract class CreationContextBase
{
    public IReadOnlyDictionary<string, string>? Settings { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }

    public string? GetSetting(string key, string? defaultValue = null) =>
        Settings?.TryGetValue(key, out var value) == true ? value : defaultValue;

    public int GetSettingInt(string key, int defaultValue = 0) =>
        int.TryParse(GetSetting(key), out var result) ? result : defaultValue;

    public float GetSettingFloat(string key, float defaultValue = 0f) =>
        float.TryParse(GetSetting(key), out var result) ? result : defaultValue;

    public bool GetSettingBool(string key, bool defaultValue = false)
    {
        var value = GetSetting(key);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }
}

public sealed class SensorCreationContext : CreationContextBase
{
    public required string SensorId { get; init; }
}

public sealed class PanelCreationContext : CreationContextBase
{
    public required string PanelTypeId { get; init; }
    public string? Argument { get; init; }
    public ILcdServices? Services { get; init; }
    public ResolvedColorScheme? ColorScheme { get; init; }
}

public sealed class TransitionCreationContext : CreationContextBase
{
    public required string TransitionTypeId { get; init; }
    public TimeSpan Duration { get; init; } = TimeSpan.FromMilliseconds(500);
    public EasingFunction Easing { get; init; } = EasingFunction.EaseInOut;
}

public sealed class EffectCreationContext : CreationContextBase
{
    public required string EffectTypeId { get; init; }
    public float Intensity { get; init; } = 1.0f;
}
```

---

## Part 4: Effects System

### 4.1 Overview

Effects are visual post-processing operations applied to rendered panel output.

### 4.2 Core Interfaces

```csharp
// src/LCDPossible.Core/Effects/IEffect.cs
namespace LCDPossible.Core.Effects;

/// <summary>
/// Visual effect that can be applied to panel output.
/// </summary>
public interface IEffect : IDisposable
{
    /// <summary>
    /// Unique effect identifier.
    /// </summary>
    string EffectId { get; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Effect category (Blur, Color, Distortion, Overlay, etc.).
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Description of what this effect does.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Apply the effect to an image.
    /// </summary>
    /// <param name="source">Source image (will be modified in place).</param>
    /// <param name="intensity">Effect intensity from 0.0 (none) to 1.0 (full).</param>
    void Apply(Image<Rgba32> source, float intensity = 1.0f);
}

/// <summary>
/// Information about an effect type (see Part 3 for TypeInfoBase definition).
/// </summary>
public sealed class EffectTypeInfo : TypeInfoBase
{
    public bool SupportsIntensity { get; init; } = true;
    public float DefaultIntensity { get; init; } = 1.0f;
}
```

### 4.3 Built-in Effects

```
Effects/
├── Blur/
│   ├── GaussianBlurEffect    # Gaussian blur
│   └── BoxBlurEffect         # Box blur (faster)
├── Color/
│   ├── GrayscaleEffect       # Convert to grayscale
│   ├── SepiaEffect           # Sepia tone
│   ├── SaturationEffect      # Adjust saturation
│   ├── ContrastEffect        # Adjust contrast
│   └── BrightnessEffect      # Adjust brightness
├── Overlay/
│   ├── VignetteEffect        # Darkened edges
│   ├── ScanlinesEffect       # CRT scanlines
│   ├── NoiseEffect           # Film grain
│   └── GlowEffect            # Bloom effect
└── Distortion/
    ├── PixelateEffect        # Pixelation
    └── CrtCurvatureEffect    # CRT monitor curvature
```

### 4.4 Effect Pipeline

Effects can be chained in sequence:

```csharp
public interface IEffectPipeline
{
    IEffectPipeline Add(string effectId, float intensity = 1.0f);
    IEffectPipeline Add(IEffect effect, float intensity = 1.0f);
    void Apply(Image<Rgba32> image);
}
```

---

## Part 5: Extended Transition System

### 5.1 Current State

The transition system already exists with:
- `ITransitionEffect` interface
- `TransitionType` enum
- `TransitionEngine` class
- Built-in effects (fade, slide, wipe, zoom, push)

### 5.2 Extensions Needed

1. Make transitions plugin-extensible
2. Add `ITransitionProvider` interface
3. Register custom transitions via plugins
4. Add transition parameters (duration, easing)

```csharp
// src/LCDPossible.Core/Transitions/ITransitionProvider.cs
namespace LCDPossible.Core.Transitions;

/// <summary>
/// Provides custom transition effects.
/// </summary>
public interface ITransitionProvider
{
    /// <summary>
    /// Provider identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Get transition types provided.
    /// </summary>
    IReadOnlyDictionary<string, TransitionTypeInfo> TransitionTypes { get; }

    /// <summary>
    /// Create a transition instance.
    /// </summary>
    ITransitionEffect? CreateTransition(string transitionId, TransitionParameters? parameters = null);
}

/// <summary>
/// Information about a transition type (see Part 3 for TypeInfoBase definition).
/// </summary>
public sealed class TransitionTypeInfo : TypeInfoBase
{
    public TimeSpan DefaultDuration { get; init; } = TimeSpan.FromMilliseconds(500);
    public bool SupportsReverse { get; init; } = true;
}

/// <summary>
/// Parameters for creating a transition (see Part 3 for TransitionCreationContext).
/// Note: TransitionParameters is an alias for TransitionCreationContext for backward compatibility.
/// </summary>
public sealed class TransitionParameters
{
    public TimeSpan Duration { get; init; } = TimeSpan.FromMilliseconds(500);
    public EasingFunction Easing { get; init; } = EasingFunction.EaseInOut;
}
```

---

## Part 6: Enhanced Plugin System

### 6.1 New Plugin Interface

```csharp
// src/LCDPossible.Core/Plugins/ILcdPlugin.cs
namespace LCDPossible.Core.Plugins;

/// <summary>
/// Full-featured plugin interface supporting all extensibility points.
/// </summary>
public interface ILcdPlugin : IDisposable
{
    // === Identity ===
    string PluginId { get; }
    string DisplayName { get; }
    Version Version { get; }
    string Author { get; }
    Version MinimumSdkVersion { get; }

    // === Capabilities ===

    /// <summary>
    /// Panel types provided by this plugin.
    /// </summary>
    IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; }

    /// <summary>
    /// Sensor types provided by this plugin.
    /// </summary>
    IReadOnlyDictionary<string, SensorTypeInfo> SensorTypes { get; }

    /// <summary>
    /// Transition types provided by this plugin.
    /// </summary>
    IReadOnlyDictionary<string, TransitionTypeInfo> TransitionTypes { get; }

    /// <summary>
    /// Effect types provided by this plugin.
    /// </summary>
    IReadOnlyDictionary<string, EffectTypeInfo> EffectTypes { get; }

    // === Lifecycle ===
    Task InitializeAsync(IPluginContext context, CancellationToken ct = default);

    // === Factory Methods ===
    IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context);
    ISensorProvider? CreateSensorProvider();
    ITransitionEffect? CreateTransition(string transitionTypeId, TransitionParameters? parameters = null);
    IEffect? CreateEffect(string effectTypeId);
}
```

### 6.2 Backward Compatibility

`IPanelPlugin` remains unchanged. The `PluginManager` detects which interface is implemented:

```csharp
// In PluginManager
if (plugin is ILcdPlugin fullPlugin)
{
    // Register all capabilities
    RegisterPanelTypes(fullPlugin.PanelTypes);
    RegisterSensorProvider(fullPlugin.CreateSensorProvider());
    RegisterTransitionTypes(fullPlugin.TransitionTypes);
    RegisterEffectTypes(fullPlugin.EffectTypes);
}
else if (plugin is IPanelPlugin panelPlugin)
{
    // Legacy plugin - panels only
    RegisterPanelTypes(panelPlugin.PanelTypes);
}
```

### 6.3 Updated Plugin Context

```csharp
public interface IPluginContext
{
    ILoggerFactory LoggerFactory { get; }
    string PluginDataDirectory { get; }
    Version SdkVersion { get; }
    IServiceProvider Services { get; }

    // New: Access to sensor registry
    ISensorRegistry SensorRegistry { get; }

    // New: Access to effect registry
    IEffectRegistry EffectRegistry { get; }

    // New: Access to transition registry
    ITransitionRegistry TransitionRegistry { get; }

    ILogger<T> CreateLogger<T>();
    ILogger CreateLogger(string categoryName);
}
```

### 6.4 Updated Panel Creation Context

See Part 3.5 for the `PanelCreationContext` definition. Key updates:

1. **Derives from `CreationContextBase`** - Gets standard settings helpers
2. **Uses `ILcdServices`** - Single service facade instead of multiple providers
3. **Deprecates `ISystemInfoProvider`** - Use sensors via `ILcdServices.Sensors`

```csharp
// Updated PanelCreationContext (from Part 3.5)
public sealed class PanelCreationContext : CreationContextBase
{
    public required string PanelTypeId { get; init; }
    public string? Argument { get; init; }

    // New: Single service facade for all plugin functionality
    public ILcdServices? Services { get; init; }

    public ResolvedColorScheme? ColorScheme { get; init; }

    // Deprecated - use Services.Sensors instead
    [Obsolete("Use Services.Sensors instead")]
    public ISystemInfoProvider? SystemProvider { get; init; }

    // Deprecated - use dedicated Proxmox sensors
    [Obsolete("Use Proxmox sensors instead")]
    public IProxmoxProvider? ProxmoxProvider { get; init; }
}
```

---

## Part 7: CLI Overhaul

### 7.1 Current State Problems

- Monolithic Program.cs (~1400 lines)
- Inconsistent command naming
- Help text embedded in code
- Limited extensibility
- No command grouping

### 7.2 New CLI Structure

```
src/LCDPossible/
├── Program.cs                    # Entry point only (~50 lines)
├── Cli/
│   ├── ICliCommand.cs            # Command interface
│   ├── CliRouter.cs              # Command dispatch
│   ├── CliParser.cs              # Argument parsing
│   ├── CliHelp.cs                # Help generation
│   ├── CliContext.cs             # Shared context
│   │
│   ├── Commands/
│   │   ├── RootCommands.cs       # help, version
│   │   ├── ServiceCommands.cs    # serve, run, service install/remove/start/stop
│   │   ├── DeviceCommands.cs     # device list, device test-pattern
│   │   ├── DisplayCommands.cs    # show, display set-image
│   │   ├── ProfileCommands.cs    # profile list/new/edit/delete/export/import
│   │   ├── ConfigCommands.cs     # config show/set/get
│   │   ├── PanelCommands.cs      # panel list, panel help <type>
│   │   ├── SensorCommands.cs     # sensor list, sensor read <id>
│   │   ├── EffectCommands.cs     # effect list
│   │   ├── TransitionCommands.cs # transition list
│   │   ├── TestCommands.cs       # test render panels to files
│   │   └── RuntimeCommands.cs    # next, previous, goto, status, stop
│   │
│   └── Output/
│       ├── ConsoleOutput.cs      # Console formatting
│       ├── TableFormatter.cs     # Table output
│       └── JsonOutput.cs         # JSON output mode
```

### 7.3 Command Interface

```csharp
// src/LCDPossible/Cli/ICliCommand.cs
namespace LCDPossible.Cli;

public interface ICliCommand
{
    /// <summary>
    /// Command name (e.g., "list", "show").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Command aliases (e.g., "ls" for "list").
    /// </summary>
    string[] Aliases => [];

    /// <summary>
    /// Parent command (null for root commands).
    /// </summary>
    string? Parent => null;

    /// <summary>
    /// Short description for help.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this command requires IPC when service is running.
    /// </summary>
    bool UseIpcWhenServiceRunning => false;

    /// <summary>
    /// Execute the command.
    /// </summary>
    Task<int> ExecuteAsync(CliContext context, CancellationToken ct);
}
```

### 7.4 New Command Hierarchy

```
lcdpossible
├── help                          # Show help
├── version                       # Show version
│
├── serve                         # Start service (foreground)
│   └── --service                 # Run as Windows Service
│
├── device                        # Device management
│   ├── list                      # List connected devices
│   └── test-pattern              # Display test pattern
│
├── panel                         # Panel information
│   ├── list                      # List all panels (by category)
│   └── help <type>               # Show panel help
│
├── sensor                        # Sensor information
│   ├── list                      # List all sensors (by category)
│   ├── read <id>                 # Read sensor value
│   └── watch <id>                # Watch sensor (real-time)
│
├── effect                        # Effect information
│   └── list                      # List all effects
│
├── transition                    # Transition information
│   └── list                      # List all transitions
│
├── show                          # Quick display (shortcut)
│   └── <panels...>               # Panel spec (inline profile)
│
├── test                          # Render to file (no device)
│   ├── <panels...>               # Panel spec
│   ├── --resolution, -r          # Target resolution
│   ├── --output, -o              # Output directory
│   ├── --wait, -w                # Wait seconds
│   ├── --theme, -T               # Theme(s) to use
│   └── --transitions, -t         # Enable transitions
│
├── profile                       # Profile management
│   ├── list                      # List profiles
│   ├── show [name]               # Show profile details
│   ├── new <name>                # Create profile
│   ├── delete <name>             # Delete profile
│   ├── set-default <name>        # Set default profile
│   ├── append-panel <type>       # Add panel to profile
│   ├── remove-panel <index>      # Remove panel from profile
│   ├── export <name> -o <file>   # Export profile
│   └── import <file>             # Import profile
│
├── config                        # Configuration
│   ├── show                      # Show current config
│   ├── set <key> <value>         # Set config value
│   ├── get <key>                 # Get config value
│   ├── set-theme <name>          # Set default theme
│   ├── list-themes               # List themes
│   └── set-proxmox               # Configure Proxmox
│
├── service                       # Service management (Windows)
│   ├── install                   # Install service
│   ├── remove                    # Remove service
│   ├── start                     # Start service
│   ├── stop                      # Stop service
│   ├── restart                   # Restart service
│   └── status                    # Service status
│
└── [runtime] (when service running)
    ├── status                    # Get service status
    ├── next                      # Next slide
    ├── previous                  # Previous slide
    ├── goto <index>              # Go to slide
    ├── set-brightness <0-100>    # Set brightness
    └── stop                      # Stop service
```

### 7.5 Help System

Auto-generated from command definitions:

```
LCDPOSSIBLE v1.0.0
Cross-platform LCD controller for HID-based displays

USAGE:
    lcdpossible <command> [options]

COMMANDS:
    serve               Start the LCD service
    device              Device management (list, test-pattern)
    panel               Panel information (list, help)
    sensor              Sensor information (list, read, watch)
    show                Quick panel display
    test                Render panels to files
    profile             Profile management
    config              Configuration
    service             Windows service management

Use 'lcdpossible <command> --help' for detailed help on a command.
Use 'lcdpossible panel list' for available panel types.
Use 'lcdpossible sensor list' for available sensors.
```

---

## Part 8: Implementation Plan

### Phase 1: Core Infrastructure (Foundation)

**Duration**: Estimated significant effort

1. **Sensor Core**
   - [ ] Create `src/LCDPossible.Core/Sensors/` directory
   - [ ] Implement `ISensor`, `ISensor<T>` interfaces
   - [ ] Implement `ISensorProvider` interface
   - [ ] Implement `ISensorRegistry` interface
   - [ ] Implement `BaseSensor<T>` base class
   - [ ] Implement `SensorRegistry` class
   - [ ] Add sensor interfaces to `LCDPossible.Core`

2. **Effect Core**
   - [ ] Create `src/LCDPossible.Core/Effects/` directory
   - [ ] Implement `IEffect` interface
   - [ ] Implement `IEffectProvider` interface
   - [ ] Implement `IEffectRegistry` interface
   - [ ] Implement `EffectPipeline` class

3. **Transition Extensions**
   - [ ] Add `ITransitionProvider` interface
   - [ ] Add `TransitionTypeInfo` class
   - [ ] Add `TransitionParameters` class
   - [ ] Implement `ITransitionRegistry`

4. **Plugin System Updates**
   - [ ] Create `ILcdPlugin` interface
   - [ ] Update `IPluginContext` with new registries
   - [ ] Update `PanelCreationContext` with `ISensorRegistry`
   - [ ] Update `PluginManager` to detect interface type
   - [ ] Add sensor/effect/transition registration

### Phase 2: Core Sensors (Data Layer)

**Duration**: Estimated significant effort

1. **System Sensors**
   - [ ] Create `LCDPossible.Plugins.Core/Sensors/` directory
   - [ ] Implement `SystemSensorProvider`
     - [ ] `system.hostname`
     - [ ] `system.os.name`, `system.os.version`
     - [ ] `system.uptime`

2. **CPU Sensors**
   - [ ] Implement `CpuSensorProvider`
     - [ ] `cpu.name`, `cpu.cores.*`
     - [ ] `cpu.usage`, `cpu.usage.core.{n}`
     - [ ] `cpu.temperature`, `cpu.temperature.core.{n}`
     - [ ] `cpu.frequency`, `cpu.power`

3. **GPU Sensors**
   - [ ] Implement `GpuSensorProvider`
     - [ ] `gpu.{n}.name`, `gpu.{n}.usage`
     - [ ] `gpu.{n}.temperature`, `gpu.{n}.memory.*`
     - [ ] `gpu.{n}.power`, `gpu.{n}.clock.*`

4. **Memory Sensors**
   - [ ] Implement `MemorySensorProvider`
     - [ ] `memory.total`, `memory.used`
     - [ ] `memory.available`, `memory.usage`

5. **Network Sensors**
   - [ ] Implement `NetworkSensorProvider`
     - [ ] `network.{iface}.name`, `network.{iface}.ip`
     - [ ] `network.{iface}.speed`, `network.{iface}.rx/tx`

6. **Storage Sensors**
   - [ ] Implement `StorageSensorProvider`
     - [ ] `storage.{n}.name`, `storage.{n}.total/used/usage`
     - [ ] `storage.{n}.temperature`, `storage.{n}.read/write`

7. **Migration Adapter**
   - [ ] Create `SystemInfoProviderAdapter`
   - [ ] Wrap ISystemInfoProvider as ISensorProvider

### Phase 3: Built-in Effects

**Duration**: Estimated moderate effort

1. **Effect Implementations**
   - [ ] Create `LCDPossible.Plugins.Effects/` project
   - [ ] Implement `GaussianBlurEffect`
   - [ ] Implement `VignetteEffect`
   - [ ] Implement `ScanlinesEffect`
   - [ ] Implement `NoiseEffect`
   - [ ] Implement `GlowEffect`
   - [ ] Implement `GrayscaleEffect`
   - [ ] Implement `SepiaEffect`
   - [ ] Implement `SaturationEffect`
   - [ ] Implement `ContrastEffect`
   - [ ] Implement `BrightnessEffect`
   - [ ] Implement `PixelateEffect`

2. **Effect Provider**
   - [ ] Implement `CoreEffectProvider`
   - [ ] Register in `CorePlugin` or new `EffectsPlugin`

### Phase 4: CLI Overhaul

**Duration**: Estimated significant effort

1. **CLI Framework**
   - [ ] Create `ICliCommand` interface
   - [ ] Create `CliContext` class
   - [ ] Create `CliRouter` class
   - [ ] Create `CliParser` class
   - [ ] Create `CliHelp` class

2. **Command Implementation**
   - [ ] Migrate `RootCommands` (help, version)
   - [ ] Migrate `ServiceCommands` (serve, service)
   - [ ] Migrate `DeviceCommands` (device list, test-pattern)
   - [ ] Migrate `DisplayCommands` (show)
   - [ ] Migrate `TestCommands` (test)
   - [ ] Migrate `ProfileCommands`
   - [ ] Migrate `ConfigCommands`
   - [ ] Migrate `RuntimeCommands`
   - [ ] Implement `PanelCommands` (panel list, panel help)
   - [ ] Implement `SensorCommands` (sensor list, sensor read, sensor watch)
   - [ ] Implement `EffectCommands` (effect list)
   - [ ] Implement `TransitionCommands` (transition list)

3. **Output Formatting**
   - [ ] Create `ConsoleOutput` helper
   - [ ] Create `TableFormatter` for tabular data
   - [ ] Create `JsonOutput` for `--json` flag

4. **Program.cs Refactor**
   - [ ] Replace monolithic switch with `CliRouter`
   - [ ] Reduce Program.cs to entry point only

### Phase 5: Panel Migration

**Duration**: Estimated significant effort

1. **Update Base Classes**
   - [ ] Add `ISensorRegistry` to `BasePanel`
   - [ ] Update `WidgetPanel` to use sensors
   - [ ] Update `CanvasPanel` to use sensors
   - [ ] Deprecate direct `ISystemInfoProvider` usage

2. **Migrate Core Panels**
   - [ ] Migrate `CpuInfoPanel`
   - [ ] Migrate `CpuUsageGraphicPanel`
   - [ ] Migrate `CpuStatusPanel`
   - [ ] Migrate `GpuInfoPanel`
   - [ ] Migrate `GpuUsageGraphicPanel`
   - [ ] Migrate `RamInfoPanel`
   - [ ] Migrate `RamUsageGraphicPanel`
   - [ ] Migrate `NetworkWidgetPanel`
   - [ ] Migrate `BasicInfoPanel`
   - [ ] Migrate thermal panels

3. **Update CorePlugin**
   - [ ] Implement `ILcdPlugin` instead of `IPanelPlugin`
   - [ ] Register sensor provider
   - [ ] Remove internal `HardwareMonitorProvider`

### Phase 6: Testing & Documentation

**Duration**: Estimated moderate effort

1. **Unit Tests**
   - [ ] Test sensor caching behavior
   - [ ] Test sensor registry
   - [ ] Test effect pipeline
   - [ ] Test CLI parser
   - [ ] Test command routing

2. **Integration Tests**
   - [ ] Test full sensor → panel flow
   - [ ] Test plugin loading with new interfaces
   - [ ] Test CLI end-to-end

3. **Documentation**
   - [ ] Update CLAUDE.md with new architecture
   - [ ] Document sensor ID conventions
   - [ ] Document effect usage
   - [ ] Document CLI changes
   - [ ] Create migration guide for existing panels

---

## Part 9: Migration Guide

### For Existing Panels

**Before (using ISystemInfoProvider):**
```csharp
public class CpuUsagePanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public CpuUsagePanel(ISystemInfoProvider provider)
    {
        _provider = provider;
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        var metrics = await _provider.GetMetricsAsync(ct);
        return new {
            usage = metrics?.Cpu?.UsagePercent ?? 0,
            temp = metrics?.Cpu?.TemperatureCelsius
        };
    }
}
```

**After (using ILcdServices):**
```csharp
public class CpuUsagePanel : WidgetPanel
{
    // Inject single unified service facade
    public CpuUsagePanel(ILcdServices services) : base(services) { }

    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        // Read sensors through unified service
        return new {
            usage = await Services.ReadSensorAsync<float>("cpu.usage", ct) ?? 0,
            temp = await Services.ReadSensorAsync<float>("cpu.temperature", ct)
        };
    }
}
```

**Key Changes:**
1. Single `ILcdServices` dependency instead of specific providers
2. Use `Services.ReadSensorAsync<T>()` for sensor values
3. Use `Services.Sensors.GetCachedValue<T>()` for cached access without async

### For Existing Plugins

**Before:**
```csharp
public class MyPlugin : IPanelPlugin
{
    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = ...;
    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext context) => ...;
}
```

**After (backward compatible - Option 1: Keep IPanelPlugin):**
```csharp
// IPanelPlugin still works unchanged
public class MyPlugin : IPanelPlugin
{
    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = ...;
    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext context) => ...;
}
```

**After (full features - Option 2: Upgrade to ILcdPlugin):**
```csharp
public class MyPlugin : ILcdPlugin
{
    // Panel types (required)
    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = ...;

    // Optional: Sensor types (return empty if not providing sensors)
    public IReadOnlyDictionary<string, SensorTypeInfo> SensorTypes { get; } = new();

    // Optional: Transition types
    public IReadOnlyDictionary<string, TransitionTypeInfo> TransitionTypes { get; } = new();

    // Optional: Effect types
    public IReadOnlyDictionary<string, EffectTypeInfo> EffectTypes { get; } = new();

    // Factory methods - return null for capabilities you don't provide
    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext context) => ...;
    public ISensorProvider? CreateSensorProvider() => null;
    public ITransitionEffect? CreateTransition(string typeId, TransitionCreationContext context) => null;
    public IEffect? CreateEffect(string typeId, EffectCreationContext context) => null;
}
```

**Key Benefits of ILcdPlugin:**
1. Provide custom sensors that other panels can use
2. Add custom visual effects
3. Add custom transitions
4. Full access to ILcdServices for inter-plugin communication

---

## Appendix A: File Structure After Refactor

```
src/
├── LCDPossible.Core/
│   ├── Sensors/
│   │   ├── ISensor.cs
│   │   ├── ISensorProvider.cs
│   │   ├── ISensorRegistry.cs
│   │   ├── BaseSensor.cs
│   │   ├── SensorRegistry.cs
│   │   └── SensorUpdateMode.cs
│   ├── Effects/
│   │   ├── IEffect.cs
│   │   ├── IEffectProvider.cs
│   │   ├── IEffectRegistry.cs
│   │   ├── EffectPipeline.cs
│   │   └── EffectTypeInfo.cs
│   ├── Transitions/
│   │   ├── ITransitionEffect.cs
│   │   ├── ITransitionProvider.cs      # NEW
│   │   ├── ITransitionRegistry.cs      # NEW
│   │   ├── TransitionType.cs
│   │   ├── TransitionTypeInfo.cs       # NEW
│   │   ├── TransitionParameters.cs     # NEW
│   │   └── TransitionEngine.cs
│   ├── Plugins/
│   │   ├── IPanelPlugin.cs             # UNCHANGED (backward compat)
│   │   ├── ILcdPlugin.cs               # NEW
│   │   ├── IPluginContext.cs
│   │   └── PluginManager.cs
│   └── ...
│
├── LCDPossible/
│   ├── Program.cs                      # Minimal entry point
│   ├── Cli/
│   │   ├── ICliCommand.cs
│   │   ├── CliRouter.cs
│   │   ├── CliParser.cs
│   │   ├── CliContext.cs
│   │   ├── CliHelp.cs
│   │   ├── Commands/
│   │   │   ├── RootCommands.cs
│   │   │   ├── ServiceCommands.cs
│   │   │   ├── DeviceCommands.cs
│   │   │   ├── DisplayCommands.cs
│   │   │   ├── PanelCommands.cs
│   │   │   ├── SensorCommands.cs
│   │   │   ├── EffectCommands.cs
│   │   │   ├── TransitionCommands.cs
│   │   │   ├── TestCommands.cs
│   │   │   ├── ProfileCommands.cs
│   │   │   ├── ConfigCommands.cs
│   │   │   └── RuntimeCommands.cs
│   │   └── Output/
│   │       ├── ConsoleOutput.cs
│   │       ├── TableFormatter.cs
│   │       └── JsonOutput.cs
│   └── ...
│
├── Plugins/
│   ├── LCDPossible.Plugins.Core/
│   │   ├── CorePlugin.cs               # Implements ILcdPlugin
│   │   ├── Sensors/
│   │   │   ├── SystemSensorProvider.cs
│   │   │   ├── CpuSensorProvider.cs
│   │   │   ├── GpuSensorProvider.cs
│   │   │   ├── MemorySensorProvider.cs
│   │   │   ├── NetworkSensorProvider.cs
│   │   │   └── StorageSensorProvider.cs
│   │   ├── Panels/
│   │   │   └── ... (existing)
│   │   └── Monitoring/
│   │       └── ... (platform monitors - kept for sensors)
│   │
│   ├── LCDPossible.Plugins.Effects/    # NEW
│   │   ├── EffectsPlugin.cs
│   │   └── Effects/
│   │       ├── GaussianBlurEffect.cs
│   │       ├── VignetteEffect.cs
│   │       └── ...
│   │
│   └── ... (other plugins)
```

---

## Appendix B: Sensor ID Reference

### Naming Convention

```
{category}.{subcategory}.{metric}
{category}.{instance}.{metric}
```

- All lowercase
- Dot-separated hierarchy
- Instance identifiers (like GPU index) use numeric values

### Complete Sensor List

| Sensor ID | Type | Unit | Mode | Interval | Description |
|-----------|------|------|------|----------|-------------|
| `system.hostname` | string | - | Static | - | Computer hostname |
| `system.os.name` | string | - | Static | - | OS name (Windows 11, Ubuntu, etc.) |
| `system.os.version` | string | - | Static | - | OS version |
| `system.uptime` | TimeSpan | - | Polling | 1s | System uptime |
| `cpu.name` | string | - | Static | - | CPU model name |
| `cpu.cores.physical` | int | - | Static | - | Physical core count |
| `cpu.cores.logical` | int | - | Static | - | Logical core count |
| `cpu.usage` | float | % | Polling | 1s | Total CPU usage |
| `cpu.usage.core.{n}` | float | % | Polling | 1s | Per-core usage |
| `cpu.temperature` | float | °C | Polling | 1s | Package temperature |
| `cpu.frequency` | float | MHz | Polling | 1s | Current frequency |
| `cpu.power` | float | W | Polling | 1s | Package power |
| `gpu.count` | int | - | Static | - | Number of GPUs |
| `gpu.{n}.name` | string | - | Static | - | GPU model name |
| `gpu.{n}.usage` | float | % | Polling | 1s | GPU usage |
| `gpu.{n}.temperature` | float | °C | Polling | 1s | GPU temperature |
| `gpu.{n}.memory.total` | float | MB | Static | - | Total VRAM |
| `gpu.{n}.memory.used` | float | MB | Polling | 1s | Used VRAM |
| `gpu.{n}.memory.usage` | float | % | Polling | 1s | VRAM usage |
| `gpu.{n}.power` | float | W | Polling | 1s | Power draw |
| `gpu.{n}.clock.core` | float | MHz | Polling | 1s | Core clock |
| `gpu.{n}.clock.memory` | float | MHz | Polling | 1s | Memory clock |
| `memory.total` | float | GB | Static | - | Total RAM |
| `memory.used` | float | GB | Polling | 1s | Used RAM |
| `memory.available` | float | GB | Polling | 1s | Available RAM |
| `memory.usage` | float | % | Polling | 1s | RAM usage |
| `network.{iface}.name` | string | - | Static | - | Interface name |
| `network.{iface}.ip` | string | - | Polling | 10s | IP address |
| `network.{iface}.mac` | string | - | Static | - | MAC address |
| `network.{iface}.speed` | float | Mbps | Polling | 5s | Link speed |
| `network.{iface}.rx` | float | MB/s | Polling | 1s | Download speed |
| `network.{iface}.tx` | float | MB/s | Polling | 1s | Upload speed |
| `storage.{n}.name` | string | - | Static | - | Drive name |
| `storage.{n}.total` | float | GB | Static | - | Total capacity |
| `storage.{n}.used` | float | GB | Polling | 10s | Used space |
| `storage.{n}.usage` | float | % | Polling | 10s | Usage percent |
| `storage.{n}.temperature` | float | °C | Polling | 5s | Temperature |

---

## Summary

This refactor introduces a more extensible and maintainable architecture:

1. **Unified Services (`ILcdServices`)** - Single entry point for all plugin functionality, eliminating the need to inject multiple registries
2. **Consistent Patterns** - All plugin entities (sensors, panels, effects, transitions) follow identical TypeInfo, Registry, and CreationContext patterns
3. **Sensors** - Unified, cached, platform-aware data layer replacing direct ISystemInfoProvider usage
4. **Effects** - Visual post-processing with plugin extensibility and pipeline support
5. **Transitions** - Plugin-extensible while keeping existing functionality
6. **Enhanced Plugin System** - ILcdPlugin interface supports all extension types with backward compatibility for IPanelPlugin
7. **CLI Overhaul** - Modular command structure that's discoverable and easy to extend

**Key Design Decisions:**
- Panels inject only `ILcdServices`, not individual registries
- Sensors are instance-based (shared); Panels/Effects/Transitions are factory-based (create new)
- All TypeInfo classes derive from `TypeInfoBase`
- All creation contexts derive from `CreationContextBase`

The implementation is phased to allow incremental progress while maintaining a working system throughout.
