using System.Collections.Concurrent;
using System.Text.Json;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Library;

public sealed class JsonLibraryIndexService : ILibraryIndexService
{
    private readonly IStlParser _stlParser;
    private readonly string _indexPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public JsonLibraryIndexService(IStlParser stlParser, string? indexPath = null)
    {
        _stlParser = stlParser;
        _indexPath = indexPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerrainBuilder",
            "library-index.json");
    }

    public async Task<IReadOnlyList<ModelLibraryItem>> ScanAsync(
        string rootFolder,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(rootFolder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var cached = await LoadCacheAsync(root, cancellationToken);
        var cachedByPath = cached.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        var files = EnumerateStlFilesSafely(root, cancellationToken).ToArray();
        var output = new ConcurrentBag<ModelLibraryItem>();
        var completed = 0;

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = cancellationToken },
            async (filePath, token) =>
            {
                var info = new FileInfo(filePath);
                ModelLibraryItem item;
                if (cachedByPath.TryGetValue(filePath, out var existing) &&
                    existing.FileSizeBytes == info.Length &&
                    existing.LastModifiedUtc == info.LastWriteTimeUtc)
                {
                    item = existing;
                }
                else
                {
                    item = await IndexFileAsync(info, token);
                }

                output.Add(item);
                progress?.Report(Interlocked.Increment(ref completed) * 100 / Math.Max(files.Length, 1));
            });

        var ordered = output.OrderBy(item => item.FullPath, StringComparer.CurrentCultureIgnoreCase).ToArray();
        await SaveCacheAsync(root, ordered, cancellationToken);
        return ordered;
    }

    private async Task<ModelLibraryItem> IndexFileAsync(FileInfo info, CancellationToken cancellationToken)
    {
        try
        {
            return new ModelLibraryItem
            {
                FileName = info.Name,
                FullPath = info.FullName,
                FolderPath = info.DirectoryName ?? string.Empty,
                FileSizeBytes = info.Length,
                LastModifiedUtc = info.LastWriteTimeUtc,
                Dimensions = await _stlParser.ReadDimensionsAsync(info.FullName, cancellationToken)
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new ModelLibraryItem
            {
                FileName = info.Name,
                FullPath = info.FullName,
                FolderPath = info.DirectoryName ?? string.Empty,
                FileSizeBytes = info.Exists ? info.Length : 0,
                LastModifiedUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                Dimensions = ModelDimensions.Empty,
                IsValid = false,
                ErrorMessage = exception.Message
            };
        }
    }

    private async Task<IReadOnlyList<ModelLibraryItem>> LoadCacheAsync(string root, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_indexPath)) return [];
            await using var stream = File.OpenRead(_indexPath);
            var document = await JsonSerializer.DeserializeAsync<LibraryIndexDocument>(stream, _jsonOptions, cancellationToken);
            return document is not null && string.Equals(document.RootFolder, root, StringComparison.OrdinalIgnoreCase)
                ? document.Items
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveCacheAsync(
        string root,
        IReadOnlyList<ModelLibraryItem> items,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_indexPath) ?? throw new InvalidOperationException("Invalid index path.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _indexPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, new LibraryIndexDocument(root, items), _jsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, _indexPath, overwrite: true);
    }

    private static IEnumerable<string> EnumerateStlFilesSafely(string root, CancellationToken cancellationToken)
    {
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = folders.Pop();
            string[] childFolders;
            string[] files;
            try
            {
                childFolders = Directory.GetDirectories(folder);
                files = Directory.GetFiles(folder, "*.stl");
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var child in childFolders) folders.Push(child);
            foreach (var file in files) yield return Path.GetFullPath(file);
        }
    }

    private sealed record LibraryIndexDocument(string RootFolder, IReadOnlyList<ModelLibraryItem> Items);
}
