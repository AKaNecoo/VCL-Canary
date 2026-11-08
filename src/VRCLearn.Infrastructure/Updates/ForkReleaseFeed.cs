using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VRCLearn.Infrastructure.Http;

namespace VRCLearn.Infrastructure.Updates;

internal sealed partial class ForkReleaseFeed
{
    private const string Repository = "AKaNecoo/VRCLearn_Launcher";
    private const string TagPrefix = "fork-v";
    private const string Api = "https://api.github.com/repos/" + Repository;
    internal const string DownloadPage = "https://github.com/" + Repository + "/releases";
    private const int PageSize = 100;
    private const int MaxResponseSize = 4 * 1024 * 1024;

    private ForkReleaseFeed(string text, string channel, Version version, string? commit)
    {
        CurrentText = text;
        Channel = channel;
        CurrentVersion = version;
        Commit = commit;
    }

    internal string CurrentText { get; }
    internal string? DisplayText { get; private init; }
    internal string Channel { get; }
    internal Version CurrentVersion { get; }
    internal string? Commit { get; }

    internal static ForkReleaseFeed? ForCurrentBuild()
    {
        var assembly = typeof(ForkReleaseFeed).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        if (!metadata.Any(attribute => attribute.Key == "VclForkBuild" && attribute.Value == "true")
            || assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is not { } version
            || ReadBuildVersion(version) is not { } build)
        {
            return null;
        }

        var commit = metadata.FirstOrDefault(attribute => attribute.Key == "VclForkCommit")?.Value;
        return new ForkReleaseFeed(version, build.Channel, build.Version, IsCommit(commit) ? commit : null)
        {
            DisplayText = metadata.FirstOrDefault(attribute => attribute.Key == "VclCanaryDisplayVersion")?.Value,
        };
    }

    internal static int ChannelRank(string? channel) => channel switch
    {
        "stable" => 0,
        "beta" => 1,
        "alpha" => 2,
        _ => -1,
    };

    internal static Version? ParseVersion(string text, string preference) =>
        ReadBuildVersion(text) is { } build && ChannelRank(build.Channel) <= ChannelRank(preference)
            ? build.Version
            : null;

    internal static bool IsPreview(string version) =>
        ReadBuildVersion(version) is { Channel: "alpha" or "beta" };

    internal bool IsCrossChannel(string version) =>
        ReadBuildVersion(version) is { } build && build.Channel != Channel;

    internal static string TagFor(string version) => TagPrefix + version;

    internal static string InstallerName(string version) =>
        $"VRCLearnLauncher-{version.Replace('+', '-')}-win-x64-Setup.exe";

    internal static string ReleaseApi(string version) => $"{Api}/releases/tags/{Uri.EscapeDataString(TagFor(version))}";

    internal static bool IsAssetUrl(string? url, string version, string name) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host == "github.com"
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && Uri.UnescapeDataString(uri.AbsolutePath) == $"/{Repository}/releases/download/{TagFor(version)}/{name}";

    internal async Task<RemoteText> FetchAsync(HttpClient http, string preference,
        IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (preference == "stable")
        {
            var response = await RemoteTextFile.GetAsync(
                http, $"{Api}/releases/latest", cancellationToken, headers, MaxResponseSize).ConfigureAwait(false);
            if (response.Outcome != RemoteTextOutcome.Fetched)
            {
                return response;
            }

            var version = Match(Deserialize<Release>(response.Text), preference);
            if (version is null)
            {
                return RemoteText.Failure("The fork's latest release does not match the stable channel.");
            }

            return ParseVersion(version, preference) > CurrentVersion
                && await IncludesCurrentBuildAsync(http, version, headers, cancellationToken).ConfigureAwait(false)
                ? response with { Text = version }
                : response with { Text = CurrentText };
        }

        for (var page = 1; ; page++)
        {
            var response = await RemoteTextFile.GetAsync(
                http, $"{Api}/releases?per_page={PageSize}&page={page}", cancellationToken, headers, MaxResponseSize)
                .ConfigureAwait(false);
            if (response.Outcome != RemoteTextOutcome.Fetched)
            {
                return response;
            }

            var releases = Deserialize<Release[]>(response.Text);
            if (releases is null)
            {
                return RemoteText.Failure("The fork's release list could not be read.");
            }

            var candidates = releases.Select(release => Match(release, preference)).OfType<string>()
                .Select(text => (Text: text, Version: ParseVersion(text, preference)))
                .Where(candidate => candidate.Version > CurrentVersion)
                .OrderByDescending(candidate => candidate.Version);

            foreach (var candidate in candidates)
            {
                if (await IncludesCurrentBuildAsync(http, candidate.Text, headers, cancellationToken).ConfigureAwait(false))
                {
                    return response with { Text = candidate.Text, ETag = null };
                }
            }

            if (releases.Length < PageSize)
            {
                return response with { Text = CurrentText, ETag = null };
            }
        }
    }

    internal async Task<bool> IncludesCurrentBuildAsync(HttpClient http, string version,
        IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (!IsCrossChannel(version))
        {
            return true;
        }

        if (Commit is null)
        {
            return false;
        }

        var tag = Uri.EscapeDataString(TagFor(version));
        var reference = await RemoteTextFile.GetAsync(
            http, $"{Api}/git/ref/tags/{tag}", cancellationToken, headers, maxBytes: 8192).ConfigureAwait(false);
        if (reference.Outcome != RemoteTextOutcome.Fetched
            || Deserialize<TagReference>(reference.Text)?.Object is not { Type: "commit", Sha: { } candidate }
            || !IsCommit(candidate))
        {
            return false;
        }

        if (candidate.Equals(Commit, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var comparison = await RemoteTextFile.GetAsync(
            http, $"{Api}/compare/{Commit}...{candidate}?per_page=1", cancellationToken, headers, MaxResponseSize)
            .ConfigureAwait(false);
        return comparison.Outcome == RemoteTextOutcome.Fetched
            && Deserialize<Comparison>(comparison.Text)?.Status is "ahead" or "identical";
    }

    private static bool IsCommit(string? text) => text is { Length: 40 } && text.All(Uri.IsHexDigit);

    private static string? Match(Release? release, string preference) =>
        release is { Draft: false, Prerelease: var prerelease, Tag: { } tag }
        && tag.StartsWith(TagPrefix, StringComparison.Ordinal)
        && ParseVersion(tag[TagPrefix.Length..], preference) is not null
        && prerelease == IsPreview(tag[TagPrefix.Length..])
            ? tag[TagPrefix.Length..]
            : null;

    private static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json.TrimStart('\uFEFF'));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string Channel, Version Version)? ReadBuildVersion(string version)
    {
        var match = BuildVersion().Match(version);
        if (!match.Success || !Version.TryParse($"{match.Groups[1].Value}.{match.Groups[3].Value}", out var number))
        {
            return null;
        }

        return (match.Groups[2].Value switch
        {
            "-alpha." => "alpha",
            "-beta." => "beta",
            _ => "stable",
        }, number);
    }

    [GeneratedRegex(@"^(\d+\.\d+\.\d+)(\+build\.|-beta\.|-alpha\.)([1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex BuildVersion();

    private sealed class Release
    {
        [JsonPropertyName("tag_name")]
        public string? Tag { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }

    private sealed class TagReference
    {
        [JsonPropertyName("object")]
        public TagTarget? Object { get; init; }
    }

    private sealed class TagTarget
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("sha")]
        public string? Sha { get; init; }
    }

    private sealed class Comparison
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }
}
