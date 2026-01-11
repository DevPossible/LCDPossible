using System.Reflection;
using System.Runtime.Loader;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation.
/// Each plugin loads in its own context to prevent dependency conflicts.
/// Uses collectible mode to allow unloading.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _sharedAssemblies;

    /// <summary>
    /// Creates a new plugin load context.
    /// </summary>
    /// <param name="pluginPath">Path to the main plugin assembly.</param>
    public PluginLoadContext(string pluginPath) : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);

        // Assemblies that should always come from the host (shared between host and plugins)
        _sharedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // SDK and Core
            "LCDPossible.Sdk",
            "LCDPossible.Core",

            // ImageSharp (shared rendering library)
            "SixLabors.ImageSharp",
            "SixLabors.ImageSharp.Drawing",
            "SixLabors.Fonts",

            // Microsoft.Extensions (logging, DI)
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Primitives",

            // System assemblies that might cause conflicts
            "System.Text.Json",
            "System.Memory",
            "System.Buffers",
            "System.Runtime.CompilerServices.Unsafe"
        };
    }

    /// <summary>
    /// Loads an assembly by name. Shared assemblies are loaded from the host context.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Shared assemblies always come from the host (default context)
        if (_sharedAssemblies.Contains(assemblyName.Name!))
        {
            return null; // Fallback to default context
        }

        // Try to resolve from plugin directory
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Let the default context handle it
        return null;
    }

    /// <summary>
    /// Loads a native library from the plugin directory.
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
