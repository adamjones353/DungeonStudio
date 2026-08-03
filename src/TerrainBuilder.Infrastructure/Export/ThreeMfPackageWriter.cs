using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Xml;
using TerrainBuilder.Core.Models;
using TerrainBuilder.Core.Services;

namespace TerrainBuilder.Infrastructure.Export;

internal sealed class ThreeMfPackageWriter
{
    internal const string PackageFileName = "Print Package.project.3mf";
    internal const string PlateFolderName = "3MF Plates";
    private const string CoreNamespace = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string StartPartRelationship = "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel";
    private const string ModelContentType = "application/vnd.ms-package.3dmanufacturing-3dmodel+xml";
    private const string RelationshipContentType = "application/vnd.openxmlformats-package.relationships+xml";

    private readonly IStlParser _parser;
    private readonly PrintPlatePlanner _planner = new();

    public ThreeMfPackageWriter(IStlParser parser)
    {
        _parser = parser;
    }

    public async Task<ThreeMfPackageResult> WriteAsync(
        string exportFolder,
        string projectName,
        IReadOnlyList<PrintListItem> items,
        CancellationToken cancellationToken)
    {
        var plan = _planner.Plan(items);
        var omittedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (plan.Plates.Count == 0)
        {
            return new ThreeMfPackageResult(null, [], [], plan);
        }

        var projectPath = Path.Combine(exportFolder, PackageFileName);
        var allPlacements = plan.Plates.SelectMany(plate => plate.Placements).ToArray();
        var projectCreated = await WritePackageAsync(
            projectPath,
            projectName,
            allPlacements,
            plan.Plates,
            includeMultiPlateMetadata: true,
            omittedFiles,
            cancellationToken).ConfigureAwait(false);

        var plateFolder = Path.Combine(exportFolder, PlateFolderName);
        Directory.CreateDirectory(plateFolder);
        var platePaths = new List<string>(plan.Plates.Count);
        foreach (var plate in plan.Plates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var platePath = Path.Combine(plateFolder, $"Plate {plate.Number:00}.build.3mf");
            var plateCreated = await WritePackageAsync(
                platePath,
                $"{projectName} - Plate {plate.Number}",
                plate.Placements,
                [plate],
                includeMultiPlateMetadata: false,
                omittedFiles,
                cancellationToken).ConfigureAwait(false);
            if (plateCreated) platePaths.Add(platePath);
        }

        if (platePaths.Count == 0 && Directory.Exists(plateFolder)) Directory.Delete(plateFolder);

        return new ThreeMfPackageResult(
            projectCreated ? projectPath : null,
            platePaths,
            omittedFiles.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            plan);
    }

    private async Task<bool> WritePackageAsync(
        string packagePath,
        string projectName,
        IReadOnlyList<PrintPlatePlacement> placements,
        IReadOnlyList<PrintPlate> plates,
        bool includeMultiPlateMetadata,
        ISet<string> omittedFiles,
        CancellationToken cancellationToken)
    {
        var temporaryPath = packagePath + $".{Guid.NewGuid():N}.tmp";
        var objects = new Dictionary<string, ThreeMfObject>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using (var fileStream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteContentTypes(archive);
                WriteRelationships(archive);

                var modelEntry = archive.CreateEntry("3D/3dmodel.model", CompressionLevel.Optimal);
                await using (var modelStream = modelEntry.Open())
                using (var writer = XmlWriter.Create(modelStream, CreateXmlSettings()))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("model", CoreNamespace);
                    writer.WriteAttributeString("unit", "millimeter");
                    writer.WriteAttributeString("xml", "lang", "http://www.w3.org/XML/1998/namespace", "en-GB");
                    WriteMetadata(writer, "Title", projectName);
                    WriteMetadata(writer, "Application", "DungeonStudio");
                    WriteMetadata(writer, "Description", includeMultiPlateMetadata
                        ? "Creality Hi multi-plate print project generated by DungeonStudio."
                        : "Creality Hi print plate generated by DungeonStudio.");
                    if (includeMultiPlateMetadata) WriteMetadata(writer, "BambuStudio:3mfVersion", "1");
                    writer.WriteStartElement("resources", CoreNamespace);

                    var objectId = 1;
                    foreach (var item in placements
                                 .Select(placement => placement.Item)
                                 .DistinctBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var source = await _parser.LoadMeshAsync(item.FullPath, cancellationToken).ConfigureAwait(false);
                            var geometry = PrepareGeometry(source, cancellationToken);
                            if (geometry is null)
                            {
                                omittedFiles.Add(item.FullPath);
                                continue;
                            }

                            WriteObject(writer, objectId, item.ModelName, geometry, cancellationToken);
                            objects.Add(item.FullPath, new ThreeMfObject(objectId, item, geometry.Bounds));
                            objectId++;
                        }
                        catch (Exception exception) when (
                            exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
                        {
                            omittedFiles.Add(item.FullPath);
                        }
                    }

                    writer.WriteEndElement();
                    writer.WriteStartElement("build", CoreNamespace);
                    WriteBuildItems(writer, placements, objects);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                }

                if (includeMultiPlateMetadata && objects.Count > 0)
                {
                    WriteModelSettings(archive, plates, objects);
                }
            }

            if (objects.Count == 0) return false;
            File.Move(temporaryPath, packagePath, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static PreparedGeometry? PrepareGeometry(StlMeshData source, CancellationToken cancellationToken)
    {
        var positions = new List<Vector3>();
        var triangles = new List<int>(source.Indices.Length);
        var vertexLookup = new Dictionary<Vector3, int>();

        int GetVertexIndex(Vector3 position)
        {
            if (vertexLookup.TryGetValue(position, out var existing)) return existing;
            var index = positions.Count;
            positions.Add(position);
            vertexLookup.Add(position, index);
            return index;
        }

        for (var index = 0; index + 2 < source.Indices.Length; index += 3)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var a = GetVertexIndex(source.Positions[source.Indices[index]]);
            var b = GetVertexIndex(source.Positions[source.Indices[index + 1]]);
            var c = GetVertexIndex(source.Positions[source.Indices[index + 2]]);
            if (a == b || b == c || c == a) continue;
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        if (positions.Count == 0 || triangles.Count == 0) return null;

        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        foreach (var position in positions)
        {
            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        return new PreparedGeometry(
            positions,
            triangles,
            new MeshBounds(minimum.X, minimum.Y, minimum.Z, maximum.X, maximum.Y, maximum.Z));
    }

    private static void WriteObject(
        XmlWriter writer,
        int objectId,
        string modelName,
        PreparedGeometry geometry,
        CancellationToken cancellationToken)
    {
        writer.WriteStartElement("object", CoreNamespace);
        writer.WriteAttributeString("id", objectId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("type", "model");
        writer.WriteAttributeString("name", modelName);
        writer.WriteStartElement("mesh", CoreNamespace);
        writer.WriteStartElement("vertices", CoreNamespace);
        for (var index = 0; index < geometry.Positions.Count; index++)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            var position = geometry.Positions[index];
            writer.WriteStartElement("vertex", CoreNamespace);
            writer.WriteAttributeString("x", XmlConvert.ToString(position.X - geometry.Bounds.MinimumX));
            writer.WriteAttributeString("y", XmlConvert.ToString(position.Y - geometry.Bounds.MinimumY));
            writer.WriteAttributeString("z", XmlConvert.ToString(position.Z - geometry.Bounds.MinimumZ));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("triangles", CoreNamespace);
        for (var index = 0; index < geometry.Triangles.Count; index += 3)
        {
            if ((index & 0x3fff) == 0) cancellationToken.ThrowIfCancellationRequested();
            writer.WriteStartElement("triangle", CoreNamespace);
            writer.WriteAttributeString("v1", geometry.Triangles[index].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("v2", geometry.Triangles[index + 1].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("v3", geometry.Triangles[index + 2].ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteBuildItems(
        XmlWriter writer,
        IReadOnlyList<PrintPlatePlacement> placements,
        IReadOnlyDictionary<string, ThreeMfObject> objects)
    {
        foreach (var placement in placements)
        {
            if (!objects.TryGetValue(placement.Item.FullPath, out var model)) continue;
            writer.WriteStartElement("item", CoreNamespace);
            writer.WriteAttributeString("objectid", model.ObjectId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("transform", CreateTransform(placement, model.Bounds));
            writer.WriteEndElement();
        }
    }

    private static string CreateTransform(PrintPlatePlacement placement, MeshBounds bounds) =>
        placement.IsRotated90Degrees
            ? FormattableString.Invariant(
                $"0 1 0 -1 0 0 0 0 1 {placement.X + bounds.Depth:R} {placement.Y:R} 0")
            : FormattableString.Invariant(
                $"1 0 0 0 1 0 0 0 1 {placement.X:R} {placement.Y:R} 0");

    private static void WriteModelSettings(
        ZipArchive archive,
        IReadOnlyList<PrintPlate> plates,
        IReadOnlyDictionary<string, ThreeMfObject> objects)
    {
        var entry = archive.CreateEntry("Metadata/model_settings.config", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("config");

        foreach (var model in objects.Values.OrderBy(model => model.ObjectId))
        {
            writer.WriteStartElement("object");
            writer.WriteAttributeString("id", model.ObjectId.ToString(CultureInfo.InvariantCulture));
            WriteConfigMetadata(writer, "name", model.Item.ModelName);
            writer.WriteStartElement("part");
            writer.WriteAttributeString("id", model.ObjectId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("subtype", "normal_part");
            WriteConfigMetadata(writer, "name", model.Item.ModelName);
            WriteConfigMetadata(writer, "matrix", "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1");
            WriteConfigMetadata(writer, "source_file", model.Item.FullPath);
            writer.WriteStartElement("mesh_stat");
            writer.WriteAttributeString("edges_fixed", "0");
            writer.WriteAttributeString("degenerate_facets", "0");
            writer.WriteAttributeString("facets_removed", "0");
            writer.WriteAttributeString("facets_reversed", "0");
            writer.WriteAttributeString("backwards_edges", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        var identifyId = 1;
        foreach (var plate in plates)
        {
            writer.WriteStartElement("plate");
            WriteConfigMetadata(writer, "plater_id", plate.Number.ToString(CultureInfo.InvariantCulture));
            WriteConfigMetadata(writer, "plater_name", $"Plate {plate.Number}");
            WriteConfigMetadata(writer, "locked", "false");
            foreach (var placement in plate.Placements)
            {
                if (!objects.TryGetValue(placement.Item.FullPath, out var model)) continue;
                writer.WriteStartElement("model_instance");
                WriteConfigMetadata(writer, "object_id", model.ObjectId.ToString(CultureInfo.InvariantCulture));
                WriteConfigMetadata(writer, "instance_id", placement.InstanceIndex.ToString(CultureInfo.InvariantCulture));
                WriteConfigMetadata(writer, "identify_id", identifyId.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                identifyId++;
            }
            writer.WriteEndElement();
        }

        writer.WriteStartElement("assemble");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteConfigMetadata(XmlWriter writer, string key, string value)
    {
        writer.WriteStartElement("metadata");
        writer.WriteAttributeString("key", key);
        writer.WriteAttributeString("value", value);
        writer.WriteEndElement();
    }

    private static void WriteContentTypes(ZipArchive archive)
    {
        var entry = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteDefaultContentType(writer, "rels", RelationshipContentType);
        WriteDefaultContentType(writer, "model", ModelContentType);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRelationships(ZipArchive archive)
    {
        var entry = archive.CreateEntry("_rels/.rels", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", RelationshipsNamespace);
        writer.WriteStartElement("Relationship", RelationshipsNamespace);
        writer.WriteAttributeString("Target", "/3D/3dmodel.model");
        writer.WriteAttributeString("Id", "rel0");
        writer.WriteAttributeString("Type", StartPartRelationship);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteDefaultContentType(XmlWriter writer, string extension, string contentType)
    {
        writer.WriteStartElement("Default", ContentTypesNamespace);
        writer.WriteAttributeString("Extension", extension);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteMetadata(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement("metadata", CoreNamespace);
        writer.WriteAttributeString("name", name);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static XmlWriterSettings CreateXmlSettings() => new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
        CloseOutput = false
    };

    private sealed record PreparedGeometry(
        IReadOnlyList<Vector3> Positions,
        IReadOnlyList<int> Triangles,
        MeshBounds Bounds);

    private sealed record ThreeMfObject(int ObjectId, PrintListItem Item, MeshBounds Bounds);

    private readonly record struct MeshBounds(
        double MinimumX,
        double MinimumY,
        double MinimumZ,
        double MaximumX,
        double MaximumY,
        double MaximumZ)
    {
        public double Depth => MaximumY - MinimumY;
    }
}

internal sealed record ThreeMfPackageResult(
    string? FilePath,
    IReadOnlyList<string> PlateFilePaths,
    IReadOnlyList<string> OmittedFiles,
    PrintPlatePlan Plan);
