using System.Reflection;
using System.Diagnostics;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Localization;

namespace MAP.C.Wasm.Modules;

public class ModuleLoader : IModuleLoader
{
    private readonly LazyAssemblyLoader _assemblyLoader;
    private readonly ILanguageService _langService;
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger<ModuleLoader> _logger;
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly Dictionary<string, Type> _cachedTypes = new();
    private readonly Dictionary<string, Lazy<Task<Assembly>>> _inFlightAssemblyLoads = new();
    private readonly object _syncLock = new();
    private int _activeLoadCount;

    public event Action<bool>? OnLoadingChanged;

    public ModuleLoader(LazyAssemblyLoader assemblyLoader, ILanguageService langService, IResourceLoader resourceLoader, ILogger<ModuleLoader> logger)
    {
        _assemblyLoader = assemblyLoader;
        _langService = langService;
        _resourceLoader = resourceLoader;
        _logger = logger;
    }

    public async Task<Type> LoadComponentAsync(MenuItem menuItem)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(menuItem.Assembly))
            throw new ArgumentException("MenuItem.Assembly is null or empty.", nameof(menuItem));
        if (string.IsNullOrWhiteSpace(menuItem.Component))
            throw new ArgumentException("MenuItem.Component is null or empty.", nameof(menuItem));

        var started = Stopwatch.GetTimestamp();
        var cacheKey = CreateCacheKey(menuItem.Assembly, menuItem.Component);

        if (_cachedTypes.TryGetValue(cacheKey, out var cachedType))
        {
            _logger.LogDebug("Module cache hit. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);
            return cachedType;
        }

        try
        {
            if (Interlocked.Increment(ref _activeLoadCount) == 1)
                OnLoadingChanged?.Invoke(true);

            var assembly = await GetOrLoadAssemblyAsync(menuItem.Assembly);
            var type = assembly.GetType(menuItem.Component);

            if (type is null)
            {
                throw new InvalidOperationException(
                    $"Component '{menuItem.Component}' not found in assembly '{menuItem.Assembly}'.");
            }

            _cachedTypes[cacheKey] = type;
            _logger.LogInformation("Module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}",
                menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return type;
        }
        finally
        {
            if (Interlocked.Decrement(ref _activeLoadCount) == 0)
                OnLoadingChanged?.Invoke(false);
        }
    }

    private static string CreateCacheKey(string assemblyName, string componentName)
        => $"{assemblyName}|{componentName}";

    /// <summary>
    /// Returns the loaded assembly for the given name, loading it if necessary.
    /// Concurrent callers for the same uncached assembly share one in-flight
    /// load-and-commit task.  Failures are not cached — the next caller may retry.
    /// </summary>
    private Task<Assembly> GetOrLoadAssemblyAsync(string assemblyName)
    {
        // Fast path: already loaded
        if (_loadedAssemblies.TryGetValue(assemblyName, out var cached))
            return Task.FromResult(cached);

        Lazy<Task<Assembly>> operation;

        lock (_syncLock)
        {
            // Recheck after acquiring lock (another caller may have committed the assembly)
            if (_loadedAssemblies.TryGetValue(assemblyName, out cached))
                return Task.FromResult(cached);

            // Reuse an existing in-flight load-and-commit task
            if (!_inFlightAssemblyLoads.TryGetValue(assemblyName, out operation!))
            {
                operation = new Lazy<Task<Assembly>>(
                    () => LoadAndCommitAsync(assemblyName),
                    LazyThreadSafetyMode.ExecutionAndPublication);

                _inFlightAssemblyLoads[assemblyName] = operation;
            }
        }

        return operation.Value;
    }

    /// <summary>
    /// Loads the assembly, initializes localization, and commits to the durable
    /// cache.  On failure the in-flight entry is removed so the next request
    /// may retry.
    /// </summary>
    private async Task<Assembly> LoadAndCommitAsync(string assemblyName)
    {
        try
        {
            var assembly = await LoadAssemblyInternalAsync(assemblyName);
            lock (_syncLock)
            {
                _loadedAssemblies[assemblyName] = assembly;
                _inFlightAssemblyLoads.Remove(assemblyName);
            }
            return assembly;
        }
        catch
        {
            lock (_syncLock)
            {
                _inFlightAssemblyLoads.Remove(assemblyName);
            }
            throw;
        }
    }

    private async Task<Assembly> LoadAssemblyInternalAsync(string assemblyName)
    {
        _logger.LogDebug("Lazy loading web module. Assembly={Assembly}", assemblyName);
        var assemblies = (await _assemblyLoader
            .LoadAssembliesAsync(new[] { assemblyName }))
            .ToList();

        if (assemblies.Count == 0)
        {
            throw new InvalidOperationException(
                $"LoadAssembliesAsync returned empty list for assembly '{assemblyName}'.");
        }

        var loadedAssembly = assemblies[0];

        // Localization must succeed before we consider the assembly ready.
        await LoadModuleLocalizationAsync(loadedAssembly);
        return loadedAssembly;
    }

    private async Task LoadModuleLocalizationAsync(Assembly assembly)
    {
        var moduleName = assembly.GetName().Name!;
        await _resourceLoader.LoadModuleResourcesAsync(_langService, assembly, moduleName);
    }
}
