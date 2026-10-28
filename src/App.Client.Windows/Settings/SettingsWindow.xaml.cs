using App.Core.Api;
using App.Updates.Feed;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace App.Client.Windows.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly IUpdateChannelSettings _updateSettings;

    public SettingsWindow(IUpdateChannelSettings updateSettings)
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

        CurrentRadioButton.IsChecked = updateSettings.GetChannel() == UpdateChannel.Current;
        StableRadioButton.IsChecked = !(CurrentRadioButton.IsChecked == true);
    }

    private void okButtonClick(object sender, RoutedEventArgs e)
    {
        _updateSettings.SetChannel(CurrentRadioButton.IsChecked == true ? UpdateChannel.Current : UpdateChannel.Stable);
        Close();
    }

    private void cancelButtonClick(object sender, RoutedEventArgs e) => Close();
}