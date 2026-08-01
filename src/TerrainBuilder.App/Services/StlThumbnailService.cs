using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.App.Services;

public sealed class StlThumbnailService : IThumbnailService
{
    private const int PixelWidth = 320;
    private const int PixelHeight = 240;
    private const string CacheVersion = "v3";
    private readonly IStlParser _parser;
    private readonly string _cacheFolder;
    private readonly SemaphoreSlim _thumbnailGate = new(1, 1);

    public StlThumbnailService(IStlParser parser)
    {
        _parser = parser;
        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TerrainBuilder",
            "Thumbnails");
    }

    public async Task<ImageSource?> GetAsync(ModelLibraryItem model, CancellationToken cancellationToken = default)
    {
        if (!model.IsValid || !File.Exists(model.FullPath)) return null;

        await _thumbnailGate.WaitAsync(cancellationToken);
        try
        {
            var cachePath = GetCachePath(model);
            if (File.Exists(cachePath))
            {
                return await RunOnBackgroundStaAsync(() => LoadImage(cachePath), cancellationToken);
            }

            var mesh = await Task.Run(
                () => _parser.LoadMeshAsync(model.FullPath, cancellationToken),
                cancellationToken);
            Directory.CreateDirectory(_cacheFolder);
            var temporaryPath = cachePath + ".tmp";

            return await RunOnBackgroundStaAsync(() =>
            {
                var bitmap = Render(mesh);
                using (var stream = File.Create(temporaryPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                File.Move(temporaryPath, cachePath, overwrite: true);
                return LoadImage(cachePath);
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            _thumbnailGate.Release();
        }
    }

    private string GetCachePath(ModelLibraryItem model)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model.FullPath.ToUpperInvariant())));
        return Path.Combine(_cacheFolder, $"{CacheVersion}-{key}-{model.LastModifiedUtc.Ticks}.png");
    }

    private static Task<T> RunOnBackgroundStaAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "DungeonStudio thumbnail renderer"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static BitmapSource Render(StlMeshData mesh)
    {
        var count = mesh.Positions.Length;
        var projectedX = new double[count];
        var projectedY = new double[count];
        var projectedDepth = new double[count];
        var minimumX = double.PositiveInfinity;
        var minimumY = double.PositiveInfinity;
        var maximumX = double.NegativeInfinity;
        var maximumY = double.NegativeInfinity;

        for (var index = 0; index < count; index++)
        {
            var vertex = mesh.Positions[index];
            var horizontal = (vertex.X - vertex.Y) * 0.70710678;
            var vertical = vertex.Z * 0.81649658 - (vertex.X + vertex.Y) * 0.40824829;
            projectedX[index] = horizontal;
            projectedY[index] = vertical;
            projectedDepth[index] = (vertex.X + vertex.Y + vertex.Z) * 0.57735027;
            minimumX = Math.Min(minimumX, horizontal);
            minimumY = Math.Min(minimumY, vertical);
            maximumX = Math.Max(maximumX, horizontal);
            maximumY = Math.Max(maximumY, vertical);
        }

        var modelWidth = Math.Max(maximumX - minimumX, 1);
        var modelHeight = Math.Max(maximumY - minimumY, 1);
        var scale = Math.Min((PixelWidth - 24) / modelWidth, (PixelHeight - 24) / modelHeight);
        var horizontalMargin = (PixelWidth - modelWidth * scale) / 2;
        var verticalMargin = (PixelHeight - modelHeight * scale) / 2;
        for (var index = 0; index < count; index++)
        {
            projectedX[index] = (projectedX[index] - minimumX) * scale + horizontalMargin;
            projectedY[index] = (maximumY - projectedY[index]) * scale + verticalMargin;
        }

        var pixelCount = PixelWidth * PixelHeight;
        var pixels = new byte[pixelCount * 4];
        var depthBuffer = new double[pixelCount];
        var occupied = new bool[pixelCount];
        Array.Fill(depthBuffer, double.NegativeInfinity);
        for (var index = 0; index < pixelCount; index++)
        {
            var offset = index * 4;
            pixels[offset] = 33;
            pixels[offset + 1] = 39;
            pixels[offset + 2] = 31;
            pixels[offset + 3] = 255;
        }

        var lightDirection = Vector3.Normalize(new Vector3(-0.35f, -0.5f, 0.8f));
        for (var triangle = 0; triangle + 2 < mesh.Indices.Length; triangle += 3)
        {
            var ia = mesh.Indices[triangle];
            var ib = mesh.Indices[triangle + 1];
            var ic = mesh.Indices[triangle + 2];
            var a = mesh.Positions[ia];
            var b = mesh.Positions[ib];
            var c = mesh.Positions[ic];
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 0.000001f) continue;
            normal = Vector3.Normalize(normal);
            var brightness = 0.42 + 0.58 * Math.Abs(Vector3.Dot(normal, lightDirection));
            var red = (byte)Math.Clamp(105 * brightness, 0, 255);
            var green = (byte)Math.Clamp(139 * brightness, 0, 255);
            var blue = (byte)Math.Clamp(116 * brightness, 0, 255);

            RasterizeTriangle(
                projectedX[ia], projectedY[ia], projectedDepth[ia],
                projectedX[ib], projectedY[ib], projectedDepth[ib],
                projectedX[ic], projectedY[ic], projectedDepth[ic],
                red, green, blue,
                pixels, depthBuffer, occupied);
        }

        AddSilhouette(pixels, occupied);
        var bitmap = BitmapSource.Create(
            PixelWidth,
            PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            PixelWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void RasterizeTriangle(
        double ax, double ay, double az,
        double bx, double by, double bz,
        double cx, double cy, double cz,
        byte red, byte green, byte blue,
        byte[] pixels,
        double[] depthBuffer,
        bool[] occupied)
    {
        var denominator = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
        if (Math.Abs(denominator) < 0.000001) return;

        var minimumPixelX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
        var maximumPixelX = Math.Min(PixelWidth - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
        var minimumPixelY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
        var maximumPixelY = Math.Min(PixelHeight - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));

        for (var y = minimumPixelY; y <= maximumPixelY; y++)
        {
            var sampleY = y + 0.5;
            for (var x = minimumPixelX; x <= maximumPixelX; x++)
            {
                var sampleX = x + 0.5;
                var weightA = ((by - cy) * (sampleX - cx) + (cx - bx) * (sampleY - cy)) / denominator;
                var weightB = ((cy - ay) * (sampleX - cx) + (ax - cx) * (sampleY - cy)) / denominator;
                var weightC = 1 - weightA - weightB;
                if (weightA < -0.0001 || weightB < -0.0001 || weightC < -0.0001) continue;

                var depth = weightA * az + weightB * bz + weightC * cz;
                var pixelIndex = y * PixelWidth + x;
                if (depth <= depthBuffer[pixelIndex]) continue;

                depthBuffer[pixelIndex] = depth;
                occupied[pixelIndex] = true;
                var offset = pixelIndex * 4;
                pixels[offset] = blue;
                pixels[offset + 1] = green;
                pixels[offset + 2] = red;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static void AddSilhouette(byte[] pixels, bool[] occupied)
    {
        var outlined = (byte[])pixels.Clone();
        for (var y = 1; y < PixelHeight - 1; y++)
        {
            for (var x = 1; x < PixelWidth - 1; x++)
            {
                var index = y * PixelWidth + x;
                if (!occupied[index]) continue;
                if (occupied[index - 1] &&
                    occupied[index + 1] &&
                    occupied[index - PixelWidth] &&
                    occupied[index + PixelWidth])
                {
                    continue;
                }

                var offset = index * 4;
                outlined[offset] = 89;
                outlined[offset + 1] = 170;
                outlined[offset + 2] = 219;
            }
        }

        Buffer.BlockCopy(outlined, 0, pixels, 0, pixels.Length);
    }

    private static BitmapImage LoadImage(string filePath)
    {
        var image = new BitmapImage();
        using var stream = File.OpenRead(filePath);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

