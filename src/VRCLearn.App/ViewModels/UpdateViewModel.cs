using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VRCLearn.App.Services;
using VRCLearn.Infrastructure.Http;
using VRCLearn.Infrastructure.Shell;
using VRCLearn.Infrastructure.Storage;
using VRCLearn.Infrastructure.Updates;

namespace VRCLearn.App.ViewModels;

public enum UpdatePhase
{
    Idle,

    UpToDate,

    Available,

    Failed,

    Downloading,

    DownloadFailed,

    Pending,

    InstallFailed,
}

public sealed partial class UpdateViewModel : ObservableObject
{
    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(5);

    private readonly UpdateService _service;
    private readonly LauncherUpdater _updater;
    private readonly GuiConfigStore _configs;
    private readonly MutableProxy _proxy;
    private readonly LocalizationService _localization;
    private readonly ProgressService _progress;
    private readonly ILogger<UpdateViewModel> _logger;
    private readonly bool _canInstall;
    private readonly TransientAction _expiry;

    private bool _useDownloadPage;
    private string? _downloadUrl;
    private bool _checking;
    private CancellationTokenSource? _downloadCancellation;
    private bool _automaticDownload;

    public UpdateViewModel(
        UpdateService service,
        LauncherUpdater updater,
        GuiConfigStore configs,
        MutableProxy proxy,
        LocalizationService localization,
        ProgressService progress,
        ILogger<UpdateViewModel> logger)
    {
        _service = service;
        _updater = updater;
        _configs = configs;
        _proxy = proxy;
        _localization = localization;
        _progress = progress;
        _logger = logger;
        UpdateChannels =
        [
            new("stable", () => _localization["Settings.Update.Channel.Stable"]),
            new("beta", "Beta"),
            new("alpha", "Alpha"),
            new(UpdateService.OfficialDownloadChannel, () => _localization["Settings.Update.Channel.Official"]),
        ];
        _canInstall = updater.CanInstall;
        _useDownloadPage = ShouldUseDownloadPage();

        _expiry = new TransientAction(NoticeDuration, () =>
        {
            if (Phase is UpdatePhase.UpToDate or UpdatePhase.Failed)
            {
                Phase = UpdatePhase.Idle;
            }
        });

        _localization.PropertyChanged += (_, _) =>
        {
            LocalizedOptions.Refresh(UpdateChannels);
            RefreshUpdateRoute();
        };
        _proxy.AvailabilityChanged += (_, _) => RefreshUpdateRoute();
        _progress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProgressService.IsRunning))
            {
                OnPropertyChanged(nameof(CanSelectUpdateChannel));
                ActCommand.NotifyCanExecuteChanged();
                InstallNowCommand.NotifyCanExecuteChanged();
            }
        };
        _configs.Changed += (_, _) =>
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.InvokeAsync(RefreshUpdateSettings);
            }
            else
            {
                RefreshUpdateSettings();
            }
        };
    }

    public string CurrentVersionText => _service.CurrentVersionText;

    public bool HasUpdateChannels => _service.IsForkBuild;

    public bool CanSelectUpdateChannel => !_checking && !IsDownloading && !_progress.IsRunning;

    public IReadOnlyList<Option> UpdateChannels { get; }

    public string SelectedUpdateChannel
    {
        get => _service.UpdateChannel;
        set
        {
            if (value == SelectedUpdateChannel || !CanSelectUpdateChannel)
            {
                return;
            }

            if (!_service.TrySetUpdateChannel(value))
            {
                OnPropertyChanged(nameof(SelectedUpdateChannel));
                return;
            }

            if (Phase == UpdatePhase.Pending)
            {
                if (_updater.Pending is not null)
                {
                    return;
                }

                TryCancelPending();
            }

            Phase = UpdatePhase.Idle;
            Version = null;
            _downloadUrl = null;
            _ = CheckAsync(force: true);
        }
    }

    public bool CanAutoUpdate => _canInstall && !_useDownloadPage;

    public string AutoUpdateHint => _localization[_service.UsesOfficialDownloadPage
        ? "Settings.Update.AutoUpdateOfficialHint"
        : _useDownloadPage ? "Settings.Update.AutoUpdateProxyHint" : "Settings.Update.AutoUpdateHint"];

    public bool AutoUpdateEnabled
    {
        get => _canInstall && !_service.UsesOfficialDownloadPage && _configs.Current.LauncherAutoUpdate;
        set
        {
            if (!CanAutoUpdate || _progress.IsRunning || value == AutoUpdateEnabled)
            {
                return;
            }

            if (!_configs.TrySetLauncherAutoUpdate(value))
            {
                OnPropertyChanged(nameof(AutoUpdateEnabled));
                return;
            }

            if (value)
            {
                if (Phase != UpdatePhase.Pending && !IsDownloading)
                {
                    _ = CheckAsync(force: true);
                }

                return;
            }

            if (_automaticDownload)
            {
                _downloadCancellation?.Cancel();
            }

            if (Phase == UpdatePhase.Pending && _updater.Pending is { Automatic: true })
            {
                TryCancelPending();
                Phase = UpdatePhase.Available;
            }
        }
    }

    [ObservableProperty]
    private UpdatePhase _phase;

    [ObservableProperty]
    private string? _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _percent;

    public bool HasNotice => Phase is UpdatePhase.Available or UpdatePhase.Pending
        or UpdatePhase.DownloadFailed or UpdatePhase.InstallFailed;

    public bool IsDownloading => Phase == UpdatePhase.Downloading;

    public bool ShowInstallNow => _canInstall && Phase == UpdatePhase.Pending && !_service.UsesOfficialDownloadPage;

    public string ButtonText => Phase switch
    {
        _ when _service.UsesOfficialDownloadPage => _localization["Settings.Update.Stage"],
        UpdatePhase.Pending => _localization["Settings.Update.CancelPending"],
        UpdatePhase.DownloadFailed => _localization["Settings.Update.ManualDownload"],
        _ when _useDownloadPage => _localization["Settings.Update.Stage"],
        UpdatePhase.UpToDate => _localization["Settings.Update.UpToDate"],
        UpdatePhase.Available when _canInstall => _localization["Settings.Update.Stage"],
        UpdatePhase.Available => _localization.Format("Settings.Update.Available", Version ?? ""),
        UpdatePhase.Failed => _localization["Settings.Update.Failed"],
        _ => _localization["Settings.CheckUpdate"],
    };

    public string StatusText => Phase switch
    {
        _ when _service.UsesOfficialDownloadPage => "",
        UpdatePhase.Downloading => _localization.Format("Settings.Update.Downloading", Percent),
        UpdatePhase.Pending => _localization.Format("Settings.Update.Pending", Version ?? ""),
        UpdatePhase.DownloadFailed => _localization["Settings.Update.DownloadFailed"],
        UpdatePhase.InstallFailed when _useDownloadPage => _localization["Settings.Update.InstallFailedDownloadPage"],
        _ when _useDownloadPage => _localization["Settings.Update.DownloadPageHint"],
        UpdatePhase.Available when _canInstall => _localization.Format("Settings.Update.StageHint", Version ?? ""),
        UpdatePhase.InstallFailed => _localization["Settings.Update.InstallFailed"],
        _ => "",
    };

    public bool HasStatus => StatusText.Length != 0;

    partial void OnPhaseChanged(UpdatePhase value)
    {
        NotifyTexts();
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(CanSelectUpdateChannel));
        ActCommand.NotifyCanExecuteChanged();
    }

    partial void OnVersionChanged(string? value) => NotifyTexts();

    private void NotifyTexts()
    {
        OnPropertyChanged(nameof(HasNotice));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(ShowInstallNow));
        InstallNowCommand.NotifyCanExecuteChanged();
    }

    private bool ShouldUseDownloadPage()
    {
        var language = _localization.CurrentLanguage;
        return _service.UsesOfficialDownloadPage || (!_proxy.IsConfigured
            && (language.Equals("zh", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)));
    }

    private void RefreshUpdateSettings()
    {
        OnPropertyChanged(nameof(AutoUpdateEnabled));
        OnPropertyChanged(nameof(SelectedUpdateChannel));
        RefreshUpdateRoute();
    }

    private void RefreshUpdateRoute()
    {
        var previous = _useDownloadPage;
        _useDownloadPage = ShouldUseDownloadPage();
        NotifyTexts();
        OnPropertyChanged(nameof(AutoUpdateHint));

        if (previous == _useDownloadPage)
        {
            return;
        }

        OnPropertyChanged(nameof(CanAutoUpdate));
        ActCommand.NotifyCanExecuteChanged();

        if (_useDownloadPage)
        {
            if (_automaticDownload)
            {
                _downloadCancellation?.Cancel();
            }
        }
        else if (AutoUpdateEnabled && Phase != UpdatePhase.Pending && !IsDownloading)
        {
            _ = CheckAsync(force: true);
        }
    }

    public Task InitializeAsync()
    {
        if (_updater.Pending is { } pending && (!pending.Automatic || AutoUpdateEnabled))
        {
            Version = pending.Version;
            Phase = UpdatePhase.Pending;
            return Task.CompletedTask;
        }

        if (_updater.InstallFailed)
        {
            Phase = UpdatePhase.InstallFailed;
            return Task.CompletedTask;
        }

        return CheckAsync(force: false);
    }

    private bool CanInstallNow() => ShowInstallNow && !_progress.IsRunning;

    [RelayCommand(CanExecute = nameof(CanInstallNow))]
    private async Task InstallNow()
    {
        if (!CanInstallNow())
        {
            return;
        }

        var started = await _progress.RunAsync(
            _localization["Settings.Update.PrepareInstall"],
            _ => Task.Run(_updater.TryStartInstallNow));

        if (started)
        {
            Application.Current.Shutdown();
        }
        else
        {
            Phase = UpdatePhase.InstallFailed;
        }
    }

    private bool CanAct() => (_useDownloadPage || !_checking) && !IsDownloading && !_progress.IsRunning;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task Act()
    {
        if (!CanAct())
        {
            return;
        }

        if (_service.UsesOfficialDownloadPage
            || (_useDownloadPage && Phase is not (UpdatePhase.Pending or UpdatePhase.DownloadFailed)))
        {
            OpenDownload(_service.BrowserDownloadUrl);
            return;
        }

        switch (Phase)
        {
            case UpdatePhase.Pending:
                if (TryCancelPending())
                {
                    Phase = UpdatePhase.Available;
                }

                break;

            case UpdatePhase.DownloadFailed:
                OpenDownload(_service.GetReleaseDownloadPage(Version));
                break;

            case UpdatePhase.Available when _canInstall && Version is { } version:
                await DownloadAsync(version, automatic: false);
                break;

            case UpdatePhase.Available:
                OpenDownload(_downloadUrl ?? _service.ManualDownloadUrl);
                break;

            default:
                await CheckAsync(force: true);
                break;
        }
    }

    private void OpenDownload(string url)
    {
        try
        {
            ShellLauncher.OpenUrl(url);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "打开下载页失败");
        }
    }

    [RelayCommand]
    private void CancelDownload() => _downloadCancellation?.Cancel();

    private bool TryCancelPending()
    {
        try
        {
            _updater.CancelPending();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "取消待安装的更新失败");
            return false;
        }
    }

    private async Task DownloadAsync(string version, bool automatic)
    {
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        _automaticDownload = automatic;
        Percent = 0;
        Phase = UpdatePhase.Downloading;

        try
        {
            await _updater.StageAsync(version, new Progress<int>(percent => Percent = percent), cancellation.Token, automatic);

            if (automatic && !AutoUpdateEnabled)
            {
                TryCancelPending();
                Phase = UpdatePhase.Available;
            }
            else
            {
                Phase = cancellation.IsCancellationRequested && TryCancelPending()
                    ? UpdatePhase.Available
                    : UpdatePhase.Pending;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Phase = UpdatePhase.Available;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or HttpRequestException or InvalidDataException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            _logger.LogWarning(exception, "下载启动器更新失败");
            Phase = UpdatePhase.DownloadFailed;
        }
        finally
        {
            _automaticDownload = false;
            _downloadCancellation = null;
        }
    }

    private async Task CheckAsync(bool force)
    {
        if (_checking || _useDownloadPage)
        {
            return;
        }

        _checking = true;
        OnPropertyChanged(nameof(CanSelectUpdateChannel));
        ActCommand.NotifyCanExecuteChanged();
        _expiry.Cancel();

        try
        {
            var status = await _service.CheckAsync(force);

            if (_useDownloadPage)
            {
                return;
            }

            switch (status.Kind)
            {
                case UpdateKind.UpToDate:
                    Phase = UpdatePhase.UpToDate;
                    _expiry.Run();
                    break;

                case UpdateKind.Failed:
                    Phase = UpdatePhase.Failed;
                    _expiry.Run();
                    break;

                case UpdateKind.UpdateAvailable:
                    _downloadUrl = status.DownloadUrl;
                    Version = status.Version;
                    Phase = UpdatePhase.Available;

                    if (CanAutoUpdate && AutoUpdateEnabled && status.Version is { } version)
                    {
                        await DownloadAsync(version, automatic: true);
                    }

                    break;
            }
        }
        finally
        {
            _checking = false;
            OnPropertyChanged(nameof(CanSelectUpdateChannel));
            ActCommand.NotifyCanExecuteChanged();
        }
    }
}
