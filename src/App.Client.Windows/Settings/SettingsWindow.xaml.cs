using App.Updates.Feed;
using App.Updates.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace App.Client.Windows.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly IUpdateSettingsStore _updateSettings;

    public SettingsWindow(IUpdateSettingsStore updateSettings)
    {
        _updateSettings = updateSettings;
        InitializeComponent();
        Title = "Settings";

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(420, 420));
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 360;
            presenter.PreferredMinimumHeight = 360;
        }

        BetaRadioButton.IsChecked = updateSettings.GetChannel() == UpdateChannel.Beta;
        StableRadioButton.IsChecked = !(BetaRadioButton.IsChecked == true);
    }

    private void okButtonClick(object sender, RoutedEventArgs e)
    {
        _updateSettings.SetChannel(BetaRadioButton.IsChecked == true ? UpdateChannel.Beta : UpdateChannel.Stable);
        Close();
    }

    private void cancelButtonClick(object sender, RoutedEventArgs e) => Close();
}