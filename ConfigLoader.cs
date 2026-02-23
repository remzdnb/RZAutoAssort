// RemzDNB - 2026

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace RZAutoAssort;

[Injectable]
public class ConfigLoader(ILogger<ConfigLoader> logger)
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly string _configDir = Path.Combine(AppContext.BaseDirectory, "user", "mods", "RZAutoAssort", "config");

    private readonly Dictionary<Type, object> _cachedConfigs = new();

    public T Load<T>(string filename)
        where T : new()
    {
        if (_cachedConfigs.TryGetValue(typeof(T), out var cached))
            return (T)cached;

        var path = Path.Combine(_configDir, filename);
        if (!File.Exists(path))
        {
            logger.LogWarning("[RZAutoAssort] {File} not found — using default config.", filename);
            var def = new T();
            _cachedConfigs[typeof(T)] = def;
            return def;
        }

        var result = JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options) ?? new T();
        _cachedConfigs[typeof(T)] = result;
        return result;
    }
}
