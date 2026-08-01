using System.Text.Json;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Projects;

public sealed class JsonProjectStore : IProjectStore
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        string filePath,
        TerrainProject project,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectPath(filePath);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, project, _options, cancellationToken);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    public async Task<TerrainProject> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectPath(filePath);
        await using var stream = File.OpenRead(filePath);
        var project = await JsonSerializer.DeserializeAsync<TerrainProject>(stream, _options, cancellationToken);
        return project ?? throw new InvalidDataException("The project file is empty or invalid.");
    }

    private static void ValidateProjectPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException("An absolute project path is required.", nameof(filePath));
        }

        if (!string.Equals(Path.GetExtension(filePath), ".terrainproject", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("DungeonStudio projects use the .terrainproject extension.");
        }
    }
}

