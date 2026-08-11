using System.Collections.Concurrent;
using System.Text.Json;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Library;

public sealed class JsonLibraryIndexService : ILibraryIndexService
{
    private const int MaximumIndexingParallelism = 6;
    private readonly IStlParser _stlParser;
    private readonly string _indexPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General);

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
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(rootFolder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        var cached = await LoadCacheAsync(root, cancellationToken);
        var cachedByPath = cached.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        var files = EnumerateStlFilesSafely(root, cancellationToken).ToArray();
        var output = new ConcurrentBag<ModelLibraryItem>();
        var progressState = new ScanProgressState(files.Length, progress);

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, MaximumIndexingParallelism),
                CancellationToken = cancellationToken
            },
            async (filePath, token) =>
            {
                var info = new FileInfo(filePath);
                ModelLibraryItem item;
                var wasIndexed = false;
                if (cachedByPath.TryGetValue(filePath, out var existing) &&
                    existing.FileSizeBytes == info.Length &&
                    existing.LastModifiedUtc == info.LastWriteTimeUtc)
                {
                    item = existing;
                }
                else
                {
                    wasIndexed = true;
                    progressState.ReportFileStarted(filePath);
                    item = await IndexFileAsync(info, token);
                }

                output.Add(item);
                progressState.ReportFileCompleted(item, wasIndexed);
            });
        progressState.ReportScanCompleted();

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
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var file in Directory.EnumerateFiles(root, "*.stl", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Path.GetFullPath(file);
        }
    }

    private sealed class ScanProgressState(int totalFiles, IProgress<LibraryScanProgress>? progress)
    {
        private readonly object _gate = new();
        private readonly List<ModelLibraryItem> _pendingItems = [];
        private readonly int _batchSize = Math.Clamp((totalFiles + 199) / 200, 1, 250);
        private int _completedFiles;
        private int _lastPercentage = -1;

        public void ReportFileStarted(string filePath)
        {
            if (progress is null) return;
            lock (_gate)
            {
                progress.Report(CreateProgress(filePath));
            }
        }

        public void ReportFileCompleted(ModelLibraryItem item, bool wasIndexed)
        {
            if (progress is null) return;
            lock (_gate)
            {
                _completedFiles++;
                _pendingItems.Add(item);
                var percentage = GetPercentage();
                if (!wasIndexed && _pendingItems.Count < _batchSize && _completedFiles < totalFiles) return;
                _lastPercentage = percentage;
                ReportPendingItems(item.FullPath);
            }
        }

        public void ReportScanCompleted()
        {
            if (progress is null) return;
            lock (_gate)
            {
                _completedFiles = totalFiles;
                if (_pendingItems.Count > 0)
                {
                    ReportPendingItems(null);
                }
                else if (_lastPercentage != 100)
                {
                    _lastPercentage = 100;
                    progress.Report(CreateProgress(null));
                }
            }
        }

        private void ReportPendingItems(string? filePath)
        {
            var items = _pendingItems.ToArray();
            _pendingItems.Clear();
            progress!.Report(CreateProgress(filePath, items));
        }

        private LibraryScanProgress CreateProgress(
            string? filePath,
            IReadOnlyList<ModelLibraryItem>? completedItems = null) =>
            new(GetPercentage(), _completedFiles, totalFiles, filePath, completedItems);

        private int GetPercentage() =>
            totalFiles == 0 ? 100 : _completedFiles * 100 / totalFiles;
    }

    private sealed record LibraryIndexDocument(string RootFolder, IReadOnlyList<ModelLibraryItem> Items);
}
