using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VRCLearn.App.Markup;
using VRCLearn.App.Services;
using VRCLearn.App.ViewModels;

namespace VRCLearn.App.Views;

public partial class ProjectDetailPage : UserControl
{
    private readonly DispatcherTimer _unityTimer;
    private int _backupsAnimation;
    private bool _updatingBatchLayout;

    public ProjectDetailPage()
    {
        InitializeComponent();
        DataContextChanged += (_, args) =>
        {
            var viewModel = args.NewValue as ProjectDetailViewModel;
            PackageRows.Collection = viewModel?.PackageView;
        };

        _unityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _unityTimer.Tick += (_, _) => (DataContext as ProjectDetailViewModel)?.RefreshUnityState();

        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true && DataContext is ProjectDetailViewModel viewModel)
            {
                _ = viewModel.LoadAsync();
                _unityTimer.Start();
            }
            else
            {
                _unityTimer.Stop();
            }
        };

        Unloaded += (_, _) => _unityTimer.Stop();
    }

    private void OnBatchToolbarLoaded(object sender, RoutedEventArgs e) => UpdateBatchLayout();

    private void OnBatchToolbarSizeChanged(object sender, SizeChangedEventArgs e) => UpdateBatchLayout();

    private void UpdateBatchLayout()
    {
        if (_updatingBatchLayout || BatchToolbar?.IsLoaded != true)
        {
            return;
        }

        _updatingBatchLayout = true;
        try
        {
            // Keep the whole strip on one line. Less frequent actions remain available in the menu.
            FrameworkElement[] priority =
            [
                ReinstallSelectedButton, RemoveSelectedButton, ClearSelectionButton,
                UpgradeSelectedButton, InstallSelectedButton, UpgradeAllButton,
            ];
            var size = new Size(double.PositiveInfinity, double.PositiveInfinity);
            BatchTitle.Measure(size);
            BatchMoreButton.Measure(size);
            var available = Math.Max(0, BatchToolbar.ActualWidth - BatchTitle.DesiredSize.Width);
            var total = BatchMoreButton.DesiredSize.Width;
            foreach (var button in priority)
            {
                button.Visibility = Visibility.Visible;
                button.InvalidateMeasure();
                button.Measure(size);
                total += button.DesiredSize.Width;
            }

            foreach (var button in priority)
            {
                if (total <= available + 0.5)
                {
                    break;
                }

                total -= button.DesiredSize.Width;
                button.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            _updatingBatchLayout = false;
        }
    }

    private void OnBatchMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnBatchMenuOpened(object sender, RoutedEventArgs e)
    {
        PrereleaseUpgradeItem.Visibility = DataContext is ProjectDetailViewModel { ShowPrereleaseUpgrades: true }
            ? Visibility.Visible
            : Visibility.Collapsed;

        (FrameworkElement Button, MenuItem Item)[] overflow =
        [
            (InstallSelectedButton, InstallSelectedOverflowItem),
            (UpgradeSelectedButton, UpgradeSelectedOverflowItem),
            (UpgradeAllButton, UpgradeAllOverflowItem),
            (ReinstallSelectedButton, ReinstallSelectedOverflowItem),
            (RemoveSelectedButton, RemoveSelectedOverflowItem),
            (ClearSelectionButton, ClearSelectionOverflowItem),
        ];
        foreach (var (button, item) in overflow)
        {
            item.Visibility = button.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        BatchOverflowSeparator.Visibility = overflow.Any(x => x.Item.Visibility == Visibility.Visible)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnInstallSelectedClick(object sender, RoutedEventArgs e) =>
        await ReviewBatchAsync(viewModel => viewModel.PrepareInstallSelectedAsync());

    private async void OnUpgradeSelectedClick(object sender, RoutedEventArgs e) =>
        await ReviewBatchAsync(viewModel => viewModel.PrepareSelectedUpgradesAsync());

    private async void OnUpgradeAllClick(object sender, RoutedEventArgs e) =>
        await ReviewBatchAsync(viewModel => viewModel.PrepareStableUpgradesAsync());

    private async void OnUpgradePrereleaseClick(object sender, RoutedEventArgs e) =>
        await ReviewBatchAsync(viewModel => viewModel.PreparePrereleaseUpgradesAsync());

    private async void OnReinstallAllClick(object sender, RoutedEventArgs e) =>
        await ReviewBatchAsync(viewModel => viewModel.PrepareReinstallAllAsync());

    private async Task ReviewBatchAsync(Func<ProjectDetailViewModel, Task<bool>> prepare)
    {
        if (DataContext is ProjectDetailViewModel viewModel
            && await prepare(viewModel)
            && IsVisible
            && ReferenceEquals(DataContext, viewModel))
        {
            JumpToSection(PendingChangesSection, PendingChangesTitle, ProjectActionsSection);
        }
    }

    private void JumpToSection(FrameworkElement section, FrameworkElement focusTarget, FrameworkElement? item = null)
    {
        var index = ProjectItems.Items.IndexOf(item ?? section);
        if (index < 0
            || VisualTree.FindDescendant<VirtualizingStackPanel>(ProjectItems) is not { ScrollOwner: { } viewer } panel)
        {
            return;
        }

        var start = viewer.VerticalOffset;
        panel.BringIndexIntoViewPublic(index);
        ProjectItems.UpdateLayout();
        if (SectionOffset() is not { } destination)
        {
            return;
        }

        focusTarget.Focus();
        if (AnimationService.Current?.Enabled != false)
        {
            // Start near a distant section so the transition stays short even in large projects.
            var distance = viewer.ViewportHeight / 2;
            var approach = Math.Clamp(start, destination - distance, destination + distance);
            viewer.ScrollToVerticalOffset(approach);
            ProjectItems.UpdateLayout();
        }

        SmoothScroll.ScrollTo(viewer, SectionOffset);

        double? SectionOffset() => section.IsDescendantOf(panel)
            ? viewer.VerticalOffset + section.TranslatePoint(new Point(), panel).Y
            : null;
    }

    private void OnBackupsChecked(object sender, RoutedEventArgs e) => ShowBackups(true);

    private void OnBackupsUnchecked(object sender, RoutedEventArgs e) => ShowBackups(false);

    private void ShowBackups(bool expand)
    {
        var generation = ++_backupsAnimation;

        BackupsContent.BeginAnimation(MaxHeightProperty, null);

        if (!IsVisible || AnimationService.Current?.Enabled == false)
        {
            BackupsContent.MaxHeight = double.PositiveInfinity;
            BackupsContent.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;

            return;
        }

        if (expand)
        {
            BackupsContent.Visibility = Visibility.Visible;
            BackupsContent.MaxHeight = double.PositiveInfinity;
            BackupsContent.Measure(new Size(
                BackupsContent.ActualWidth > 0 ? BackupsContent.ActualWidth : ActualWidth,
                double.PositiveInfinity));

            var animation = new DoubleAnimation(0, BackupsContent.DesiredSize.Height, AnimationService.Switch)
            {
                EasingFunction = AnimationService.Ease,
            };

            animation.Completed += (_, _) =>
            {
                if (generation != _backupsAnimation)
                {
                    return;
                }

                BackupsContent.BeginAnimation(MaxHeightProperty, null);
                BackupsContent.MaxHeight = double.PositiveInfinity;
            };

            BackupsContent.MaxHeight = 0;
            BackupsContent.BeginAnimation(MaxHeightProperty, animation);

            return;
        }

        var collapse = new DoubleAnimation(BackupsContent.ActualHeight, 0, AnimationService.Switch)
        {
            EasingFunction = AnimationService.Ease,
        };

        collapse.Completed += (_, _) =>
        {
            if (generation != _backupsAnimation)
            {
                return;
            }

            BackupsContent.BeginAnimation(MaxHeightProperty, null);
            BackupsContent.Visibility = Visibility.Collapsed;
            BackupsContent.MaxHeight = double.PositiveInfinity;
        };

        BackupsContent.BeginAnimation(MaxHeightProperty, collapse);
    }

    private void OnUpgradeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PackageRowViewModel { IsInstalled: true, NewerVersion: not null } package }
            && DataContext is ProjectDetailViewModel viewModel)
        {
            _ = viewModel.SwitchToVersionAsync(package.Name, package.NewerVersion!);
        }
    }

    private void OnBrowseCopyDestinationClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectDetailViewModel viewModel)
        {
            return;
        }

        var selected = DialogService.Instance.PickFolder(
            LocalizationService.Instance["ProjectDetail.Copy.Title"],
            Path.GetDirectoryName(viewModel.CopyDestination ?? "") ?? "");

        if (selected is not null)
        {
            viewModel.CopyDestination = Path.Combine(
                selected,
                Path.GetFileName(viewModel.CopyDestination ?? "") is { Length: > 0 } name
                    ? name
                    : viewModel.ProjectName);
        }
    }
}
