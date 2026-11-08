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
    private const string Api = "https://api.github.com/repos/" + Repository + "/releases";
    internal const string DownloadPage = "https://github.com/" + Repository + "/releases";
    private const int PageSize = 100;
    private const int MaxResponseSize = 4 * 1024 * 1024;

    private ForkReleaseFeed(string version, string channel)
    {
        CurrentText = version;
        Channel = channel;
    }

    internal string CurrentText { get; }
    internal string Channel { get; }
    internal bool IsPreview => Channel != "+build.";

    internal static ForkReleaseFeed? ForCurrentBuild()
    {
        var assembly = typeof(ForkReleaseFeed).Assembly;
        if (!assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == "VclForkBuild" && attribute.Value == "true")
            || assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is not { } version
            || ReadBuildVersion(version) is not { } build)
        {
            return null;
        }

        return new ForkReleaseFeed(version, build.Channel);
    }

    internal Version? ParseVersion(string text) =>
        ReadBuildVersion(text) is { } build && build.Channel == Channel ? build.Version : null;

    internal static string TagFor(string version) => TagPrefix + version;
    internal static string InstallerName(string version) =>
        $"VRCLearnLauncher-{version.Replace('+', '-')}-win-x64-Setup.exe";

    internal static string ReleaseApi(string version) => $"{Api}/tags/{Uri.EscapeDataString(TagFor(version))}";

    internal static bool IsAssetUrl(string? url, string version, string name) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host == "github.com"
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && Uri.UnescapeDataString(uri.AbsolutePath) == $"/{Repository}/releases/download/{TagFor(version)}/{name}";

    internal async Task<RemoteText> FetchAsync(
        HttpClient http, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (!IsPreview)
        {
            var response = await RemoteTextFile.GetAsync(http, $"{Api}/latest", cancellationToken, headers, MaxResponseSize);
            if (response.Outcome != RemoteTextOutcome.Fetched)
            {
                return response;
            }

            return ReadVersion(response.Text, preview: false) is { } version
                ? response with { Text = version }
                : RemoteText.Failure("The fork's latest release does not match the stable channel.");
        }

        for (var page = 1; ; page++)
        {
            var response = await RemoteTextFile.GetAsync(http, $"{Api}?per_page={PageSize}&page={page}",
                cancellationToken, headers, MaxResponseSize);
            if (response.Outcome != RemoteTextOutcome.Fetched)
            {
                return response;
            }

            var releases = Deserialize<Release[]>(response.Text);
            if (releases is null)
            {
                return RemoteText.Failure("The fork's release list could not be read.");
            }

            var latest = releases.Select(release => Match(release, preview: true))
                .OfType<string>().OrderByDescending(ParseVersion).FirstOrDefault();
            if (latest is not null)
            {
                return response with { Text = latest, ETag = null };
            }

            if (releases.Length < PageSize)
            {
                return RemoteText.Failure("No published release matches this fork channel.");
            }
        }
    }

    private string? ReadVersion(string json, bool preview) => Match(Deserialize<Release>(json), preview);

    private string? Match(Release? release, bool preview) =>
        release is { Draft: false, Prerelease: var prerelease, Tag: { } tag }
        && prerelease == preview
        && tag.StartsWith(TagPrefix, StringComparison.Ordinal)
        && ParseVersion(tag[TagPrefix.Length..]) is not null
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
        return match.Success && Version.TryParse($"{match.Groups[1].Value}.{match.Groups[3].Value}", out var number)
            ? (match.Groups[2].Value, number)
            : null;
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
}
