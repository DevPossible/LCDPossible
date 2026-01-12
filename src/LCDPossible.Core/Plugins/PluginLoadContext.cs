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
    private readonly string _hostDirectory;

    /// <summary>
    /// Creates a new plugin load context.
    /// </summary>
    /// <param name="pluginPath">Path to the main plugin assembly.</param>
    public PluginLoadContext(string pluginPath) : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);

        // Get the host application's directory for resolving shared assemblies
        _hostDirectory = AppContext.BaseDirectory;

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
        // Shared assemblies should come from the host application
        if (_sharedAssemblies.Contains(assemblyName.Name!))
        {
            // First check if already loaded in the default context
            var loaded = Default.Assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (loaded != null)
            {
                return loaded;
            }

            // Try to load from the host application directory (for non-single-file deploys)
            var hostAssemblyPath = Path.Combine(_hostDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(hostAssemblyPath))
            {
                // Load into the default context so all plugins share the same instance
                return Default.LoadFromAssemblyPath(hostAssemblyPath);
            }

            // For single-file deployment: try to load from the default context
            // The assembly may be embedded in the single-file bundle
            try
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }
            catch (FileNotFoundException)
            {
                // Assembly not found in bundle either, fall through
            }

            // Fall back to default context resolution
            return null;
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
