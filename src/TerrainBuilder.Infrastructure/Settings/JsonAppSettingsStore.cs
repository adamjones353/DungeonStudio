using System.Text.Json;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public JsonAppSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerrainBuilder",
            "settings.json");
    }

    public async Task<TerrainBuilderSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath)) return new TerrainBuilderSettings();

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<TerrainBuilderSettings>(stream, _jsonOptions, cancellationToken)
                   ?? new TerrainBuilderSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new TerrainBuilderSettings();
        }
    }

    public async Task SaveAsync(TerrainBuilderSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath)
                        ?? throw new InvalidOperationException("Invalid application settings path.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
