using VRCLearn.Core.Configuration;

namespace VRCLearn.Core.Projects;

public sealed class ProjectBackupService(IBackupStore store)
{
    public PortableProjectArchive PreparePortableArchive(UnityProject project) =>
        PortableProjectArchive.Prepare(project, store.Directory);

    public async Task<BackupRecord> CreateAsync(
        UnityProject project,
        string projectName,
        BackupFormat format,
        DateTime timestamp,
        IProgress<FileProgress>? progress,
        CancellationToken cancellationToken,
        bool archive = false,
        string? note = null,
        bool auto = false,
        bool includeLibrary = false,
        ICollection<string>? skippedFiles = null)
    {
        var content = await Task.Run(
            () => ProjectFiles.Collect(project, ProjectFileFilter.ForBackup(includeLibrary)),
            cancellationToken).ConfigureAwait(false);

        var estimated = await Task.Run(
            () => EstimateBytes(project, content),
            cancellationToken).ConfigureAwait(false);

        DiskSpace.Ensure(store.Directory, estimated);

        var request = new BackupCreateRequest(projectName, timestamp, archive, note, auto);

        return await store.CreateAsync(
            request,
            (destination, token) => Task.Run(
                () => ProjectBackup.CreateAsync(
                    project.Io,
                    content,
                    destination,
                    format,
                    progress,
                    token,
                    skipLockedLibraryFiles: includeLibrary,
                    skippedFiles: skippedFiles),
                token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<BackupRecord> CreatePortableArchiveAsync(
        UnityProject project,
        PortableProjectArchive plan,
        string projectName,
        BackupFormat format,
        DateTime timestamp,
        IProgress<FileProgress>? progress,
        CancellationToken cancellationToken)
    {
        plan.CheckDestination(store.Directory);
        DiskSpace.Ensure(store.Directory, plan.EstimatedBytes);

        return store.CreateAsync(
            new BackupCreateRequest(projectName, timestamp, IsArchive: true, Note: null, IsAuto: false),
            (destination, token) => ProjectBackup.CreateAsync(
                project.Io, plan.Content, destination, format, progress, token, plan.OpenFile),
            cancellationToken,
            plan.VerifyAsync);
    }

    private static long EstimateBytes(UnityProject project, ProjectContent content)
    {
        var total = 0L;

        foreach (var file in content.Files)
        {
            try
            {
                total += new FileInfo(Path.Combine(project.Io.RootDirectory, file)).Length;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        return total;
    }

    public static BackupFormat ParseFormat(string? text) => text switch
    {
        "zip-store" => BackupFormat.ZipStore,
        "zip-best" => BackupFormat.ZipBest,
        _ => BackupFormat.Zip,
    };
}
