using System.IO;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VRCLearn.Core.IO;
using VRCLearn.Core.Plugins;
using VRCLearn.Core.Plugins.Hub;
using VRCLearn.Infrastructure.Plugins;
using VRCLearn.Plugin.Sdk;

namespace VRCLearn.App.ViewModels;

public sealed partial class PluginsViewModel
{
    private PluginHubIndex? _catalogIndex;
    private IReadOnlyList<PluginCatalogItem> _catalogItems = [];
    private PluginCatalogItem? _catalogDownload;

    [ObservableProperty]
    private int _selectedPluginTab;

    [ObservableProperty]
    private string _catalogSearch = "";

    [ObservableProperty]
    private bool _isLoadingCatalog;

    [ObservableProperty]
    private bool _catalogFailed;

    public IReadOnlyList<PluginCatalogItem> VisibleCatalog
    {
        get
        {
            var query = CatalogSearch.Trim();
            return query.Length == 0 ? _catalogItems : _catalogItems.Where(item =>
                item.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }

    public string CatalogNotice => CatalogFailed ? _localization["Plugins.Catalog.Offline"]
        : IsLoadingCatalog && _catalogIndex is null ? _localization["Plugins.Catalog.Loading"]
        : _catalogIndex is null ? ""
        : _catalogItems.Count == 0 ? _localization["Plugins.Catalog.Empty"]
        : VisibleCatalog.Count == 0 ? _localization["Plugins.SearchEmpty"]
        : "";

    public bool HasCatalogNotice => CatalogNotice.Length > 0;

    public string CatalogRefreshText =>
        _localization[CatalogFailed ? "Plugins.Catalog.Retry" : "Plugins.Catalog.Refresh"];

    partial void OnSelectedPluginTabChanged(int value)
    {
        if (value == 1 && _catalogIndex is null && !CatalogFailed && RefreshCatalogCommand.CanExecute(null))
        {
            _ = RefreshCatalogCommand.ExecuteAsync(null);
        }
    }

    partial void OnCatalogSearchChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleCatalog));
        NotifyCatalogNotice();
    }

    partial void OnIsLoadingCatalogChanged(bool value)
    {
        ActOnCatalogItemCommand.NotifyCanExecuteChanged();
        NotifyCatalogNotice();
    }

    partial void OnCatalogFailedChanged(bool value) => NotifyCatalogNotice();

    private void NotifyCatalogNotice()
    {
        OnPropertyChanged(nameof(CatalogNotice));
        OnPropertyChanged(nameof(HasCatalogNotice));
        OnPropertyChanged(nameof(CatalogRefreshText));
    }

    [RelayCommand(CanExecute = nameof(CanInstallFromFile))]
    private async Task RefreshCatalog()
    {
        IsLoadingCatalog = true;
        CatalogFailed = false;
        try
        {
            _catalogIndex = await _hub.BrowseAsync(CancellationToken.None);
            RebuildCatalog();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException
            or JsonException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(exception, "读取插件市场失败");
            CatalogFailed = true;
        }
        finally
        {
            IsLoadingCatalog = false;
        }
    }

    private void RebuildCatalog()
    {
        if (_catalogDownload is not null)
        {
            return;
        }

        if (_catalogIndex is { } index)
        {
            var installed = Plugins.ToDictionary(item => item.Id, StringComparer.Ordinal);
            _catalogItems = index.Plugins
                .Select(entry => MakeCatalogItem(entry, installed.GetValueOrDefault(entry.Id)))
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            OnPropertyChanged(nameof(VisibleCatalog));
        }

        NotifyCatalogNotice();
    }

    private PluginCatalogItem MakeCatalogItem(PluginHubEntry entry, PluginItemViewModel? installed)
    {
        var name = entry.Name.Pick(_localization.CurrentLanguage).Trim();
        var description = entry.Description.Pick(_localization.CurrentLanguage).Trim();
        var latest = entry.SelectLatestRelease();
        var reason = "";
        PluginHubRelease? release = null;
        Uri? package = null;

        if (installed is null)
        {
            if (entry.MinHostVersion is { } minimum && PluginHostInfo.Version < minimum)
            {
                reason = _localization.Format("Plugins.Catalog.NeedsLauncher", minimum.ToString());
            }
            else if (entry.ApiVersion != PluginHostInfo.ApiVersion)
            {
                reason = _localization["Plugins.Catalog.Incompatible"];
            }
            else
            {
                foreach (var candidate in entry.Versions.OrderByDescending(version => version.Version))
                {
                    if (candidate.Sha256 is not null && candidate.Size <= PluginPackageInstaller.MaxPackageBytes
                        && PluginHubEndpoint.TryResolvePackage(PluginMarketClient.Market, candidate.Url, out package))
                    {
                        release = candidate;
                        break;
                    }
                }

                if (release is null)
                {
                    reason = _localization["Plugins.Catalog.NoPackage"];
                }
            }
        }

        var action = installed is not null ? "Plugins.Catalog.ViewInstalled"
            : release is not null ? "Plugins.Catalog.Download"
            : "Plugins.Catalog.Unavailable";
        return new PluginCatalogItem(
            entry.Id, name.Length == 0 ? installed?.Name ?? entry.Id : name,
            description.Length == 0 ? installed?.Description ?? "" : description,
            installed?.VersionText ?? (release ?? latest)?.Version.ToString() ?? "",
            reason, _localization[action], _localization["Plugins.Catalog.Downloading"],
            installed is not null, release, package);
    }

    private bool CanActOnCatalogItem(PluginCatalogItem? item) =>
        !IsBusy && !IsLoadingCatalog && item is { CanAct: true };

    [RelayCommand(CanExecute = nameof(CanActOnCatalogItem))]
    private async Task ActOnCatalogItem(PluginCatalogItem? item)
    {
        if (item is null || !CanActOnCatalogItem(item))
        {
            return;
        }

        if (item.IsInstalled)
        {
            SearchText = item.Id;
            SelectedPluginTab = 0;
            return;
        }

        if (item.Package is not { } package || item.Release is not { } release)
        {
            return;
        }

        await _installGate.WaitAsync();
        if (Plugins.Any(plugin => plugin.Id == item.Id))
        {
            _installGate.Release();
            RebuildCatalog();
            return;
        }

        _catalogDownload = item;
        IsBusy = true;
        item.IsDownloading = true;
        var directory = Path.Combine(_paths.PluginUpdateTempDirectory, Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "plugin.vclplugin");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var progress = new Progress<DownloadProgress>(value =>
            {
                if (item.IsDownloading && value.Total is > 0)
                {
                    item.ProgressPercent = Math.Clamp(value.Received * 100d / value.Total.Value, 0, 100);
                }
            });
            await _download.DownloadAsync(new DownloadRequest(package, path, release.Sha256), progress,
                timeout.Token, release.Size > 0 ? release.Size : PluginPackageInstaller.MaxPackageBytes);
            item.ProgressPercent = 100;

            var (manifest, actualSha256) = await _manager.InspectPackageAsync(path, timeout.Token);
            if (!string.Equals(manifest.Id, item.Id, StringComparison.Ordinal) || manifest.Version != release.Version
                || !string.Equals(actualSha256, release.Sha256, StringComparison.OrdinalIgnoreCase)
                || (release.Size > 0 && new FileInfo(path).Length != release.Size))
            {
                throw new InvalidDataException(_localization["Plugins.UpdatePackageMismatch"]);
            }

            var compatibility = PluginCompatibility.Check(manifest, PluginHostInfo.Version, PluginHostInfo.ApiVersion);
            if (compatibility.Count > 0)
            {
                throw new InvalidDataException(string.Join("; ", compatibility));
            }

            await ConfirmAndInstallAsync(path, manifest, actualSha256);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("下载插件超时：{Id}", item.Id);
            Status = _localization["Plugins.Catalog.DownloadTimeout"];
        }
        catch (Exception exception) when (IsPluginFileError(exception)
            || exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "从市场安装插件失败：{Id}", item.Id);
            Status = _localization.Format("Plugins.InstallFailed", exception.Message);
        }
        finally
        {
            DirectoryCleanup.TryDelete(directory);
            item.IsDownloading = false;
            _catalogDownload = null;
            IsBusy = false;
            _installGate.Release();
            Rebuild();
        }
    }
}

public sealed partial class PluginCatalogItem(
    string id, string name, string description, string version, string reason,
    string action, string downloading, bool installed, PluginHubRelease? release, Uri? package) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool HasDescription => Description.Length > 0;
    public string Version { get; } = version;
    public string Meta => string.Equals(Name, Id, StringComparison.Ordinal) ? Version : $"{Id} · {Version}";
    public string Reason { get; } = reason;
    public bool HasReason => Reason.Length > 0;
    public bool IsInstalled { get; } = installed;
    public bool CanAct => IsInstalled || Package is not null;
    public PluginHubRelease? Release { get; } = release;
    public Uri? Package { get; } = package;
    public string ButtonText => IsDownloading ? downloading : action;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progressPercent;
}
