using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VRCLearn.App.Services;
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
}

public sealed partial class UpdateViewModel : ObservableObject
{
    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(5);

    private readonly UpdateService _service;
    private readonly GuiConfigStore _configs;
    private readonly LocalizationService _localization;
    private readonly ILogger<UpdateViewModel> _logger;

    private readonly TransientAction _expiry;

    private string? _downloadUrl;
    private bool _running;

    public UpdateViewModel(
        UpdateService service,
        GuiConfigStore configs,
        LocalizationService localization,
        ILogger<UpdateViewModel> logger)
    {
        _service = service;
        _configs = configs;
        _localization = localization;
        _logger = logger;

        _expiry = new TransientAction(NoticeDuration, () =>
        {
            if (Phase is UpdatePhase.UpToDate or UpdatePhase.Failed)
            {
                Phase = UpdatePhase.Idle;
            }
        });

        _localization.PropertyChanged += (_, _) => NotifyTexts();
        _configs.Changed += (_, _) => NotifyTexts();
    }

    public string CurrentVersionText => _service.CurrentVersionText;

    [ObservableProperty]
    private UpdatePhase _phase;

    [ObservableProperty]
    private string? _version;

    public bool HasNotice => !UseForkDownloadPage && Phase is UpdatePhase.Available;

    private bool UseForkDownloadPage =>
        _service.IsForkBuild
        && (_localization.CurrentLanguage.Equals("zh", StringComparison.OrdinalIgnoreCase)
            || _localization.CurrentLanguage.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
        && (!_configs.Current.ProxyEnabled
            || !Uri.TryCreate(_configs.Current.ProxyUrl.Trim(), UriKind.Absolute, out _));

    public string ButtonText => UseForkDownloadPage ? _localization["Settings.Update.ForkDownload"] : Phase switch
    {
        UpdatePhase.UpToDate => _localization["Settings.Update.UpToDate"],
        UpdatePhase.Available => _localization.Format("Settings.Update.Available", Version ?? ""),
        UpdatePhase.Failed => _localization["Settings.Update.Failed"],
        _ => _localization["Settings.CheckUpdate"],
    };

    partial void OnPhaseChanged(UpdatePhase value)
    {
        NotifyTexts();
        ActCommand.NotifyCanExecuteChanged();
    }

    partial void OnVersionChanged(string? value) => NotifyTexts();

    private void NotifyTexts()
    {
        OnPropertyChanged(nameof(HasNotice));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(CurrentVersionText));
    }

    public Task InitializeAsync() => UseForkDownloadPage ? Task.CompletedTask : CheckAsync(force: false);

    private bool CanAct() => !_running;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Act()
    {
        if (UseForkDownloadPage)
        {
            return OpenDownload(_service.ManualDownloadUrl);
        }

        return Phase == UpdatePhase.Available ? OpenDownload(_downloadUrl) : CheckAsync(force: true);
    }

    private Task OpenDownload(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        try
        {
            ShellLauncher.OpenUrl(url);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "打开下载页失败");
        }

        return Task.CompletedTask;
    }

    private async Task CheckAsync(bool force)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        Expire();

        var status = await _service.CheckAsync(force);

        _running = false;
        ActCommand.NotifyCanExecuteChanged();

        switch (status.Kind)
        {
            case UpdateKind.UpToDate:
                Phase = UpdatePhase.UpToDate;
                ScheduleExpiry();
                return;

            case UpdateKind.Failed:
                Phase = UpdatePhase.Failed;
                ScheduleExpiry();
                return;
        }

        _downloadUrl = status.DownloadUrl;
        Version = status.Version;
        Phase = UpdatePhase.Available;
    }

    private void ScheduleExpiry() => _expiry.Run();

    private void Expire() => _expiry.Cancel();
}
