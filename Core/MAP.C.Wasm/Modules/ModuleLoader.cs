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
    private readonly Dictionary<string, Task<Assembly>> _inFlightAssemblyLoads = new();
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
            _logger.LogInformation("Module cache hit. Assembly={Assembly} Component={Component}", menuItem.Assembly, menuItem.Component);
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
            _logger.LogInformation("Web module loaded. Assembly={Assembly} Component={Component} DurationMs={DurationMs}",
                menuItem.Assembly, menuItem.Component, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return type;
        }
        finally
        {
            var count = Interlocked.Decrement(ref _activeLoadCount);
            if (count < 0)
            {
                // Never allow count to go below zero
                Interlocked.CompareExchange(ref _activeLoadCount, 0, count);
                count = 0;
            }
            if (count == 0)
                OnLoadingChanged?.Invoke(false);
        }
    }

    private static string CreateCacheKey(string assemblyName, string componentName)
        => $"{assemblyName}|{componentName}";

    /// <summary>
    /// Returns the loaded assembly for the given name, loading it if necessary.
    /// Concurrent callers for the same uncached assembly share one in-flight load task.
    /// Failures are not cached — the next caller may retry.
    /// </summary>
    private Task<Assembly> GetOrLoadAssemblyAsync(string assemblyName)
    {
        // Fast path: already loaded
        if (_loadedAssemblies.TryGetValue(assemblyName, out var cached))
            return Task.FromResult(cached);

        Task<Assembly>? inFlight;
        lock (_syncLock)
        {
            // Recheck after acquiring lock (another caller may have committed the assembly)
            if (_loadedAssemblies.TryGetValue(assemblyName, out cached))
                return Task.FromResult(cached);

            // Reuse an existing in-flight load
            if (_inFlightAssemblyLoads.TryGetValue(assemblyName, out var existing))
                return existing;

            // Create and register a new load task
            inFlight = LoadAssemblyInternalAsync(assemblyName);
            _inFlightAssemblyLoads[assemblyName] = inFlight;
        }

        return AwaitAndCommitAsync(assemblyName, inFlight);
    }

    private async Task<Assembly> AwaitAndCommitAsync(string assemblyName, Task<Assembly> loadTask)
    {
        try
        {
            var assembly = await loadTask;
            // Commit to durable cache on success
            lock (_syncLock)
            {
                _loadedAssemblies[assemblyName] = assembly;
                _inFlightAssemblyLoads.Remove(assemblyName);
            }
            return assembly;
        }
        catch
        {
            // Do not cache failures — remove in-flight entry so next caller may retry
            lock (_syncLock)
            {
                _inFlightAssemblyLoads.Remove(assemblyName);
            }
            throw;
        }
    }

    private async Task<Assembly> LoadAssemblyInternalAsync(string assemblyName)
    {
        _logger.LogInformation("Lazy loading web module. Assembly={Assembly}", assemblyName);
        var assemblies = (await _assemblyLoader
            .LoadAssembliesAsync(new[] { assemblyName }))
            .ToList();

        if (assemblies.Count == 0)
        {
            throw new InvalidOperationException(
                $"LoadAssembliesAsync returned empty list for assembly '{assemblyName}'.");
        }

        var loadedAssembly = assemblies[0];

        // Commit to cache only after localization succeeds, so a failed
        // localization keeps the assembly retryable on next load.
        await LoadModuleLocalizationAsync(loadedAssembly);
        return loadedAssembly;
    }

    private async Task LoadModuleLocalizationAsync(Assembly assembly)
    {
        var moduleName = assembly.GetName().Name!;
        await _resourceLoader.LoadModuleResourcesAsync(_langService, assembly, moduleName);
    }
}
