using System.Diagnostics;
using TerrainBuilder.App.Services;
using TerrainBuilder.Infrastructure.Stl;

if (args.Length != 1) throw new ArgumentException("Supply one STL path.");
var cache = new HelixMeshCache(new StlParser());
var stopwatch = Stopwatch.StartNew();
var mesh = await cache.GetAsync(Path.GetFullPath(args[0]));
stopwatch.Stop();
Console.WriteLine($"Triangles={mesh.Indices?.Count / 3:N0}; Vertices={mesh.Positions?.Count:N0}; Elapsed={stopwatch.Elapsed}");
