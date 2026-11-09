using System.Windows;
using System.Windows.Automation;
using Wpf.Ui.Controls;

namespace VRCLearn.App.Views;

public partial class ConfirmDialog : DialogWindowBase
{
    public ConfirmDialog(
        string title,
        string message,
        string confirmText,
        string? extraText = null,
        bool confirmDanger = false,
        bool extraDanger = false,
        string? detailTitle = null,
        string? detailBody = null)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (detailTitle is not null)
        {
            DetailTitleText.Text = detailTitle;
            DetailBodyText.Text = detailBody ?? "";
            DetailBlock.Visibility = Visibility.Visible;
        }

        if (confirmDanger)
        {
            ConfirmButton.Appearance = ControlAppearance.Danger;
        }

        if (extraText is null)
        {
            ExtraButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ExtraButton.Content = extraText;

            if (extraDanger)
            {
                ExtraButton.Appearance = ControlAppearance.Danger;
            }
        }
    }

    public bool ExtraClicked { get; private set; }

    internal bool IncludesLocalPackages => IncludeLocalPackagesCheckBox.IsChecked == true;

    internal void OfferLocalPackages(string label, string previewText)
    {
        IncludeLocalPackagesLabel.Text = label;
        AutomationProperties.SetName(IncludeLocalPackagesCheckBox, label);
        var archiveText = ConfirmButton.Content;
        var archiveAppearance = ConfirmButton.Appearance;
        IncludeLocalPackagesCheckBox.Checked += (_, _) =>
        {
            ConfirmButton.Content = previewText;
            ConfirmButton.Appearance = ControlAppearance.Primary;
        };
        IncludeLocalPackagesCheckBox.Unchecked += (_, _) =>
        {
            ConfirmButton.Content = archiveText;
            ConfirmButton.Appearance = archiveAppearance;
        };
        IncludeLocalPackagesCheckBox.Visibility = Visibility.Visible;
    }

    private void OnExtraClick(object sender, RoutedEventArgs e)
    {
        ExtraClicked = true;
        DialogResult = false;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
