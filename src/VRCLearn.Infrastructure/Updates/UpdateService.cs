using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VRCLearn.Infrastructure.Http;
using VRCLearn.Infrastructure.Storage;

namespace VRCLearn.Infrastructure.Updates;

public enum UpdateKind
{
    UpToDate,

    UpdateAvailable,

    Failed,

    ManualDownload,
}

public sealed record UpdateStatus(UpdateKind Kind, string? Version = null, string? DownloadUrl = null);

internal sealed record LauncherReleaseAsset(Uri Url, string Sha256, long Size);

public sealed class UpdateService(
    HttpClient http, UpdateStateStore states, ILogger<UpdateService> logger, GuiConfigStore configs)
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private const string DefaultReleaseApiUrl =
        "https://api.github.com/repos/VRLearn-Official/VRCLearn_Launcher/releases/latest";

    public const string OfficialDownloadChannel = "official";

    public const string DefaultDownloadUrl = "https://start.gamemode1.com/pages/vrclearnlauncher/";

    private const string OfficialReleasePage = "https://github.com/VRLearn-Official/VRCLearn_Launcher/releases";

    private static readonly IReadOnlyDictionary<string, string> ApiHeaders =
        new Dictionary<string, string>
        {
            ["Accept"] = "application/vnd.github+json",
            ["X-GitHub-Api-Version"] = "2026-03-10",
        };

    private readonly ForkReleaseFeed? _fork = ForkReleaseFeed.ForCurrentBuild();

    public bool IsForkBuild => _fork is not null;

    public string CurrentVersionText => _fork?.DisplayText ?? _fork?.CurrentText ?? CurrentVersion.ToString();

    public string UpdateChannel => configs.Current.LauncherUpdateChannel is { } channel
        && IsSupportedChannel(channel) ? channel : _fork?.Channel ?? "stable";

    public bool UsesOfficialDownloadPage => IsForkBuild && UpdateChannel == OfficialDownloadChannel;

    public bool TrySetUpdateChannel(string channel) =>
        IsForkBuild && IsSupportedChannel(channel)
        && configs.TrySetLauncherUpdateChannel(channel, disableAutoUpdate: channel == OfficialDownloadChannel);

    private static bool IsSupportedChannel(string channel) =>
        channel == OfficialDownloadChannel || ForkReleaseFeed.ChannelRank(channel) >= 0;

    internal Version CurrentVersion => _fork?.CurrentVersion
        ?? Version.Parse(typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    internal Version? ParseVersion(string text) =>
        _fork is null ? ParseOfficialVersion(text) : ForkReleaseFeed.ParseVersion(text, UpdateChannel);

    internal bool HasCustomSource =>
        Environment.GetEnvironmentVariable("VRCL_VERSION_URL") is { Length: > 0 }
        || Environment.GetEnvironmentVariable("VRCL_DOWNLOAD_URL") is { Length: > 0 };

    public string ManualDownloadUrl => DownloadUrl();

    public string BrowserDownloadUrl => IsForkBuild && !UsesOfficialDownloadPage
        ? ForkReleaseFeed.DownloadPage : DefaultDownloadUrl;

    public string GetReleaseDownloadPage(string? version)
    {
        var releases = IsForkBuild ? ForkReleaseFeed.DownloadPage : OfficialReleasePage;
        if (version is null || ParseVersion(version) is null)
        {
            return releases;
        }

        var tag = IsForkBuild ? ForkReleaseFeed.TagFor(version) : $"v{version}";
        return $"{releases}/tag/{Uri.EscapeDataString(tag)}";
    }

    internal async Task<LauncherReleaseAsset> GetInstallerAsync(string version, CancellationToken cancellationToken)
    {
        if (HasCustomSource || ParseVersion(version) is not { } target
            || !ForkReleaseFeed.IsNewer(target, CurrentVersion)
            || (_fork is null && target.ToString() != version))
        {
            throw new InvalidDataException("The requested version is not eligible for automatic installation.");
        }

        if (_fork is not null && _fork.IsCrossChannel(version))
        {
            // Save the inferred subscription before a package from another channel can replace this build.
            var preference = UpdateChannel;
            if (configs.Current.LauncherUpdateChannel != preference && !TrySetUpdateChannel(preference))
            {
                throw new IOException("Could not save the update channel.");
            }

            if (!await _fork.IncludesCurrentBuildAsync(http, version, ApiHeaders, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidDataException("The release could not be verified as an upgrade from this build.");
            }
        }

        var metadata = await RemoteTextFile.GetAsync(
            http, _fork is null ? DefaultReleaseApiUrl : ForkReleaseFeed.ReleaseApi(version),
            cancellationToken, ApiHeaders, maxBytes: 512 * 1024).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (metadata.Outcome != RemoteTextOutcome.Fetched)
        {
            throw new IOException($"The release could not be read: {metadata.Detail}");
        }

        var release = JsonSerializer.Deserialize<GitHubInstallerRelease>(metadata.Text.TrimStart('\uFEFF'));
        var tag = _fork is null ? $"v{version}" : ForkReleaseFeed.TagFor(version);

        if (release is not { Draft: false, Assets: { } assets } || release.TagName != tag
            || release.Prerelease != (_fork is not null && ForkReleaseFeed.IsPreview(version)))
        {
            throw new InvalidDataException("The release does not match this update channel. Check for updates again.");
        }

        var name = ForkReleaseFeed.InstallerName(version);
        var installer = FindAsset(assets, version, tag, name);
        var checksum = FindAsset(assets, version, tag, name + ".sha256");

        if (installer.Size is <= 0 or > UpdateInstallation.MaxInstallerSize)
        {
            throw new InvalidDataException("The installer size is invalid.");
        }

        var remote = await RemoteTextFile.GetAsync(
            http, checksum.Url!, cancellationToken, maxBytes: 512).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (remote.Outcome != RemoteTextOutcome.Fetched)
        {
            throw new IOException($"The installer checksum could not be downloaded: {remote.Detail}");
        }

        var parts = remote.Text.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (parts is not [var hash, var file]
            || file != name || !UpdateInstallation.IsHash(hash)
            || (installer.Digest is { Length: > 0 } digest
                && !digest.Equals($"sha256:{hash}", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The checksum does not match the installer.");
        }

        return new LauncherReleaseAsset(new Uri(installer.Url!), hash, installer.Size);
    }

    private GitHubAsset FindAsset(IEnumerable<GitHubAsset> assets, string version, string tag, string name)
    {
        var matches = assets.Where(asset => asset.Name == name).ToArray();

        if (matches is not [var asset]
            || (_fork is null
                ? asset.Url != $"{OfficialReleasePage}/download/{tag}/{name}"
                : !ForkReleaseFeed.IsAssetUrl(asset.Url, version, name)))
        {
            throw new InvalidDataException($"The release does not contain {name}.");
        }

        return asset;
    }

    public async Task<UpdateStatus> CheckAsync(bool force = false)
    {
        if (UsesOfficialDownloadPage)
        {
            return new UpdateStatus(UpdateKind.ManualDownload, DownloadUrl: DefaultDownloadUrl);
        }

        var fork = Environment.GetEnvironmentVariable("VRCL_VERSION_URL") is { Length: > 0 } ? null : _fork;
        var channel = UpdateChannel;
        var source = fork is null ? VersionUrl() : $"fork:{fork.CurrentText}:{fork.Commit}:{channel}";
        var state = states.Load();
        if (state.Source != source)
        {
            state = new UpdateState { Source = source };
        }

        if (!force && state.Tag.Length > 0 && DateTimeOffset.UtcNow - state.CheckedAt < CheckInterval)
        {
            return Evaluate(state.Tag, fork, channel);
        }

        var remote = fork is null
            ? await RemoteTextFile.GetForUpdateCheckAsync(http, VersionUrl(), CancellationToken.None, HeadersFor(state.ETag))
            : await fork.FetchAsync(http, channel,
                HeadersFor(channel == "stable" && fork.Channel == "stable" ? state.ETag : ""), CancellationToken.None);

        if (remote.Outcome == RemoteTextOutcome.NotModified && state.Tag.Length > 0)
        {
            state.CheckedAt = DateTimeOffset.UtcNow;
            state.ETag = remote.ETag ?? state.ETag;
            states.Save(state);

            return Evaluate(state.Tag, fork, channel);
        }

        if (remote.Outcome != RemoteTextOutcome.Fetched)
        {
            return Fail(channel, $"获取最新版本号失败：{remote.Detail}");
        }

        if ((fork is null ? ReadTag(remote.Text) : remote.Text) is not { } text)
        {
            return Fail(channel, "版本接口返回的内容无法解析");
        }

        var status = Evaluate(text, fork, channel);
        if (status.Kind == UpdateKind.Failed)
        {
            return status;
        }

        state.Tag = text;
        state.ETag = remote.ETag ?? state.ETag;
        state.CheckedAt = DateTimeOffset.UtcNow;
        states.Save(state);

        return status;
    }

    private UpdateStatus Fail(string channel, string detail)
    {
        logger.LogError("检查启动器更新失败（{Version}，{Channel}）：{Detail}", CurrentVersion.ToString(3), channel, detail);

        return new UpdateStatus(UpdateKind.Failed);
    }

    private UpdateStatus Evaluate(string tag, ForkReleaseFeed? fork, string channel)
    {
        var version = fork is null ? ParseOfficialVersion(tag)
            : tag == fork.CurrentText ? fork.CurrentVersion : ForkReleaseFeed.ParseVersion(tag, channel);
        if (version is not { } latest)
        {
            return Fail(channel, "版本接口返回的版本号无效");
        }

        var current = CurrentVersion;

        if (ForkReleaseFeed.IsNewer(latest, current))
        {
            logger.LogInformation("发现新版本：{Latest}，当前 {Current}", latest.ToString(3), current.ToString(3));

            return new UpdateStatus(UpdateKind.UpdateAvailable, tag, DownloadUrl());
        }

        logger.LogInformation("已是最新版本：{Current}", current.ToString(3));

        return new UpdateStatus(UpdateKind.UpToDate);
    }

    private static IReadOnlyDictionary<string, string> HeadersFor(string etag)
    {
        if (etag.Length == 0)
        {
            return ApiHeaders;
        }

        var headers = new Dictionary<string, string>(ApiHeaders) { ["If-None-Match"] = etag };

        return headers;
    }

    private static string? ReadTag(string json)
    {
        try
        {
            var release = JsonSerializer.Deserialize<GitHubRelease>(json.TrimStart('\uFEFF'));

            return NormalizeTag(release?.TagName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeTag(string? tag) =>
        (tag ?? "").Trim().TrimStart('\uFEFF').Trim().TrimStart('v', 'V').Trim();

    private static Version? ParseOfficialVersion(string text) =>
        text.Split('.') is { Length: 3 } parts
        && parts.All(x => int.TryParse(x, out _))
        && Version.TryParse(text, out var version)
            ? version
            : null;

    private static string VersionUrl() =>
        Environment.GetEnvironmentVariable("VRCL_VERSION_URL") is { Length: > 0 } custom
            ? custom
            : DefaultReleaseApiUrl;

    private string DownloadUrl() =>
        Environment.GetEnvironmentVariable("VRCL_DOWNLOAD_URL") is { Length: > 0 } custom
            ? custom
            : BrowserDownloadUrl;

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
    }

    private sealed class GitHubInstallerRelease : GitHubRelease
    {
        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? Url { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }
}
