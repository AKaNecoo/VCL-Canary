using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using VRCLearn.Core.Archives;
using VRCLearn.Core.Configuration;
using VRCLearn.Core.IO;

namespace VRCLearn.Core.Projects;

public sealed record LocalArchivePackage(string Name, string Source, string Destination, long Bytes);

/// <summary>归档内的本地包和清单覆盖项。原项目和依赖目录仅用于读取。</summary>
public sealed class PortableProjectArchive
{
    private const string LockPath = "Packages/packages-lock.json";
    private const string LocalPrefix = "file:";
    private readonly IFileIo _io;
    private readonly string _backupDirectory;
    private readonly ProjectContent _projectContent;
    private readonly List<string> _directories;
    private readonly Dictionary<string, FileSnapshot> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _replacements = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LocalArchivePackage> _packages = [];
    private readonly Dictionary<string, DateTime> _packageDirectories = new(StringComparer.OrdinalIgnoreCase);

    private PortableProjectArchive(IFileIo io, string backupDirectory)
    {
        _io = io;
        _backupDirectory = backupDirectory;
        _projectContent = ProjectFiles.Collect(io, ProjectFileFilter.ForBackup());
        _directories = [.. _projectContent.Directories];
        foreach (var file in _projectContent.Files)
        {
            _files.Add(file, Snapshot(Path.Combine(io.RootDirectory, file)));
        }
    }

    public IReadOnlyList<LocalArchivePackage> Packages => _packages;

    internal ProjectContent Content => new(_directories, [.. _files.Keys]);

    internal long EstimatedBytes => _files.Sum(pair =>
        _replacements.TryGetValue(pair.Key, out var bytes) ? bytes.LongLength : pair.Value.Length);

    public static PortableProjectArchive Prepare(UnityProject project, string backupDirectory)
    {
        var root = PathUtil.Normalize(project.ProjectDirectory);
        if (PathUtil.SamePath(backupDirectory, root) || PathUtil.IsUnder(backupDirectory, root))
        {
            throw new IOException($"The backup directory is inside the project: {backupDirectory}");
        }

        EnsureUnlinked(root);
        var plan = new PortableProjectArchive(project.Io, PathUtil.Normalize(backupDirectory));
        plan.CheckDestination(backupDirectory);
        if (!plan._files.ContainsKey(UnityProject.ProjectVersionPath))
        {
            throw new InvalidDataException($"Missing {UnityProject.ProjectVersionPath}.");
        }

        plan.AddLocalPackages(root, backupDirectory);
        return plan;
    }

    private void AddLocalPackages(string root, string backupDirectory)
    {
        var manifest = ReadJson(UpmManifest.RelativePath);
        var dependencies = Dependencies(manifest, UpmManifest.RelativePath);
        var lockFile = ReadJson(LockPath);
        var locked = Dependencies(lockFile, LockPath);
        var packages = Path.Combine(root, UnityProject.PackagesDirectory);
        var roots = _io.ListEntries("").Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bundle = "VCLLocalPackages";
        for (var suffix = 2; roots.Contains(bundle); suffix++)
        {
            bundle = $"VCLLocalPackages-{suffix}";
        }

        var directLocal = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;
        var lockChanged = false;
        foreach (var (name, value) in dependencies.ToArray())
        {
            if (LocalReference(value) is not { } reference)
            {
                continue;
            }

            directLocal.Add(name);
            var source = ResolveLocalPath(reference, packages);
            var target = AddPackage(name, source, root, backupDirectory, bundle);
            var replacement = LocalPrefix + Path.GetRelativePath(
                packages, Path.Combine(root, target)).Replace('\\', '/');
            if (replacement != reference)
            {
                dependencies[name] = replacement;
                changed = true;
            }

            if (locked[name] is JsonObject entry && !IsEmbedded(entry) && LocalReference(entry["version"]) is { } old
                && old != replacement)
            {
                entry["version"] = replacement;
                lockChanged = true;
            }
        }

        foreach (var (name, value) in locked)
        {
            if (value is not JsonObject entry || LocalReference(entry["version"]) is not { } reference)
            {
                continue;
            }

            if (IsEmbedded(entry))
            {
                var relativeReference = VerifyEmbeddedPackage(name, reference, packages);
                if (reference != relativeReference)
                {
                    entry["version"] = relativeReference;
                    lockChanged = true;
                }
            }
            else if (!directLocal.Contains(name))
            {
                throw new InvalidDataException($"{LockPath}: {name} has a local reference absent from {UpmManifest.RelativePath}.");
            }
        }

        if (changed)
        {
            _replacements.Add(UpmManifest.RelativePath, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.Indented));
        }

        if (lockChanged)
        {
            _replacements.Add(LockPath, JsonSerializer.SerializeToUtf8Bytes(lockFile, JsonDefaults.Indented));
        }
    }

    private string VerifyEmbeddedPackage(string name, string reference, string packages)
    {
        var source = ResolveLocalPath(reference, packages);
        var folder = Path.GetFileName(source);
        var destination = $"{UnityProject.PackagesDirectory}/{folder}";
        if (Path.GetDirectoryName(source) is not { } parent || !PathUtil.SamePath(parent, packages)
            || !_files.ContainsKey($"{destination}/package.json"))
        {
            throw new InvalidDataException($"{LockPath}: embedded package {name} is not included at {source}.");
        }

        EnsureUnlinked(source);
        ReadPackageManifest(name, source, directory: true);
        AddPackageDirectory(source, destination, included: true);
        return LocalPrefix + folder;
    }

    private static bool IsEmbedded(JsonObject entry) =>
        entry["source"] is JsonValue source && source.TryGetValue<string>(out var value) && value == "embedded";

    private string AddPackage(string name, string source, string root, string backupDirectory, string bundle)
    {
        if (!PathUtil.IsSafePathSegment(name))
        {
            throw new InvalidDataException($"Invalid local package name: {name}");
        }

        var attributes = EnsureUnlinked(source);
        var directory = (attributes & FileAttributes.Directory) != 0;
        if (directory && (PathUtil.SamePath(root, source) || PathUtil.IsUnder(root, source)
            || PathUtil.SamePath(backupDirectory, source) || PathUtil.IsUnder(backupDirectory, source)))
        {
            throw new InvalidDataException($"The local package contains the project or backup folder: {source}");
        }

        ReadPackageManifest(name, source, directory);
        var relative = Path.GetRelativePath(root, source).Replace('\\', '/');
        var included = PathUtil.IsUnder(source, root) && (directory
            ? _files.ContainsKey($"{relative}/package.json")
            : _files.ContainsKey(relative));
        var destination = included ? relative : $"{bundle}/{name}";
        var size = 0L;
        if (directory)
        {
            size = AddPackageDirectory(source, destination, included);
        }
        else if (!included)
        {
            destination += ".tgz";
            var file = Snapshot(source);
            _files.Add(destination, file);
            size = file.Length;
        }

        if (!included)
        {
            if (!_directories.Contains(bundle))
            {
                _directories.Add(bundle);
            }

            _packages.Add(new LocalArchivePackage(name, source, destination, size));
        }

        return destination;
    }

    private long AddPackageDirectory(string source, string destination, bool included)
    {
        var size = 0L;
        _packageDirectories[source] = Directory.GetLastWriteTimeUtc(source);
        if (!included)
        {
            _directories.Add(destination);
        }

        foreach (var item in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            if (item.Name.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RejectLink(item.FullName, item.Attributes);
            var target = $"{destination}/{item.Name}";
            if (item is DirectoryInfo)
            {
                size += AddPackageDirectory(item.FullName, target, included);
            }
            else if (!included)
            {
                var file = Snapshot(item.FullName);
                _files.Add(target, file);
                size += file.Length;
            }
            else if (!_files.ContainsKey(target))
            {
                throw new InvalidDataException($"The project backup excludes a required local package file: {item.FullName}");
            }
        }

        return size;
    }

    private static void ReadPackageManifest(string name, string source, bool directory)
    {
        if (directory)
        {
            var path = Path.Combine(source, "package.json");
            EnsureUnlinked(path);
            using var stream = File.OpenRead(path);
            CheckPackageManifest(stream, name, source);
            return;
        }

        if (!source.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"A local package must be a folder or a .tgz archive: {source}");
        }

        using var file = File.OpenRead(source);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var foundManifest = false;
        while (tar.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is TarEntryType.SymbolicLink or TarEntryType.HardLink)
            {
                throw new InvalidDataException($"The local package archive contains a link: {source} ({entry.Name})");
            }

            if (ZipEntryPath.Normalize(entry.Name) is "package/package.json" or "package.json"
                && entry.DataStream is { } manifest)
            {
                CheckPackageManifest(manifest, name, source);
                foundManifest = true;
            }
        }

        if (!foundManifest)
        {
            throw new InvalidDataException($"No package.json found in the local package: {source}");
        }
    }

    private static void CheckPackageManifest(Stream stream, string name, string source)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var manifest = JsonNode.Parse(reader.ReadToEnd()) as JsonObject;
        if (manifest?["name"] is not JsonValue value || !value.TryGetValue<string>(out var actual) || actual != name)
        {
            throw new InvalidDataException($"The package.json name does not match {name}: {source}");
        }

        if (Dependencies(manifest, source).Any(pair => LocalReference(pair.Value) is not null))
        {
            throw new InvalidDataException($"Nested file: dependencies are not supported: {source}");
        }
    }

    private static string ResolveLocalPath(string reference, string packages)
    {
        var path = reference[LocalPrefix.Length..];
        if (reference.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(reference, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }

        if (string.IsNullOrWhiteSpace(path) || (Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path)))
        {
            throw new InvalidDataException($"Invalid local package path: {reference}");
        }

        return PathUtil.Normalize(Path.GetFullPath(path, packages));
    }

    private static string? LocalReference(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
            && text.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase) ? text : null;

    private static JsonObject Dependencies(JsonObject? document, string source) =>
        document?["dependencies"] switch
        {
            null => new JsonObject(),
            JsonObject dependencies => dependencies,
            _ => throw new InvalidDataException($"Invalid dependencies in {source}."),
        };

    private JsonObject? ReadJson(string path)
    {
        if (!_files.TryGetValue(path, out var file))
        {
            if (File.Exists(Path.Combine(_io.RootDirectory, path)))
            {
                throw new InvalidDataException($"The project backup excludes {path}.");
            }

            return null;
        }

        CheckSnapshot(file);
        return JsonNode.Parse(File.ReadAllText(file.Path)) as JsonObject
            ?? throw new InvalidDataException($"Invalid JSON object: {path}");
    }

    internal Stream OpenFile(string entry)
    {
        var file = _files[entry];
        CheckSnapshot(file);
        return _replacements.TryGetValue(entry, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : File.OpenRead(file.Path);
    }

    internal void CheckDestination(string directory)
    {
        if (!PathUtil.SamePath(directory, _backupDirectory))
        {
            throw new IOException("The backup directory changed during archiving.");
        }

        var existing = _backupDirectory;
        while (!Directory.Exists(existing) && !File.Exists(existing)
            && Path.GetDirectoryName(existing) is { } parent)
        {
            existing = parent;
        }

        EnsureUnlinked(existing);
    }

    internal async Task VerifyAsync(Stream zip, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        var plan = SafeZipExtractor.Plan(archive, rootPrefix: null);
        var expected = _directories.ToDictionary(path => path, _ => true, StringComparer.OrdinalIgnoreCase);
        foreach (var path in _files.Keys)
        {
            expected.Add(path, false);
        }

        foreach (var item in plan.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!expected.Remove(item.RelativePath, out var directory) || directory != item.IsDirectory)
            {
                throw new InvalidDataException($"Unexpected archive entry: {item.RelativePath}");
            }

            if (!directory)
            {
                await using var actual = item.Entry.Open();
                await using var source = OpenFile(item.RelativePath);
                var expectedHash = await SHA256.HashDataAsync(source, cancellationToken).ConfigureAwait(false);
                var actualHash = await SHA256.HashDataAsync(actual, cancellationToken).ConfigureAwait(false);
                if (!actualHash.AsSpan().SequenceEqual(expectedHash))
                {
                    throw new InvalidDataException($"Archive content differs from the source: {item.RelativePath}");
                }
            }
        }

        if (expected.Count != 0)
        {
            throw new InvalidDataException($"Archive is missing {expected.Keys.First()}.");
        }

        var current = ProjectFiles.Collect(_io, ProjectFileFilter.ForBackup());
        if (!_projectContent.Files.ToHashSet(StringComparer.Ordinal).SetEquals(current.Files)
            || !_projectContent.Directories.ToHashSet(StringComparer.Ordinal).SetEquals(current.Directories))
        {
            throw new IOException("The project contents changed during archiving.");
        }

        foreach (var (path, lastWriteTime) in _packageDirectories)
        {
            EnsureUnlinked(path);
            if (Directory.GetLastWriteTimeUtc(path) != lastWriteTime)
            {
                throw new IOException($"The local package contents changed during archiving: {path}");
            }
        }

        foreach (var file in _files.Values)
        {
            CheckSnapshot(file);
        }
    }

    private static FileSnapshot Snapshot(string path)
    {
        var info = new FileInfo(path);
        return new FileSnapshot(path, info.Length, info.LastWriteTimeUtc);
    }

    private static void CheckSnapshot(FileSnapshot file)
    {
        EnsureUnlinked(file.Path);
        if (Snapshot(file.Path) != file)
        {
            throw new IOException($"The file changed during archiving: {file.Path}");
        }
    }

    private static FileAttributes EnsureUnlinked(string path)
    {
        var attributes = File.GetAttributes(path);
        RejectLink(path, attributes);
        for (var parent = Path.GetDirectoryName(path); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            RejectLink(parent, File.GetAttributes(parent));
        }

        return attributes;
    }

    private static void RejectLink(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The path contains a symbolic link or junction: {path}");
        }
    }

    private sealed record FileSnapshot(string Path, long Length, DateTime LastWriteTime);
}
