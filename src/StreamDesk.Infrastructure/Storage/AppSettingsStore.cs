using System.Text.Json;
using System.Text.Json.Serialization;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Infrastructure.Storage;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON inside the SQLite settings
/// table. API keys for providers are additionally protected at rest by
/// <see cref="SecretProtector"/> when supplied.
/// </summary>
public interface IAppSettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string SettingsKey = "app.settings.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SettingsRepository _settings;

    public AppSettingsStore(SettingsRepository settings)
    {
        _settings = settings;
    }

    public AppSettings Load()
    {
        var json = _settings.Get(SettingsKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt settings fall back to defaults instead of crashing startup.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        _settings.Set(SettingsKey, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
