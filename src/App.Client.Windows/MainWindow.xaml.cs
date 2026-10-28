using App.Client.Windows.Settings;
using App.Core.Api;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace App.Client.Windows;

public sealed partial class MainWindow : Window
{
    private readonly Func<SettingsWindow> _settingsWindowFactory;

    public MainWindow(Func<SettingsWindow> settingsWindowFactory, ICustomerDirectory customerDirectory)
    {
        _settingsWindowFactory = settingsWindowFactory;
        InitializeComponent();
        Title = "Inno Setup .NET Starter — Sample Data";

        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(960, 640));
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 480;
        }

        var customers = customerDirectory.GetAll();
        CustomersList.ItemsSource = customers;
        CustomerCountText.Text = $"{customers.Count} customers";
    }

    private void settingsMenuItemClick(object sender, RoutedEventArgs e)
    {
        var mainWindowHandle = WindowNative.GetWindowHandle(this);
        var settingsWindow = _settingsWindowFactory();
        var settingsWindowHandle = WindowNative.GetWindowHandle(settingsWindow);

        NativeMethods.SetOwner(settingsWindowHandle, mainWindowHandle);
        NativeMethods.EnableWindow(mainWindowHandle, false);
        settingsWindow.Closed += (_, _) => NativeMethods.EnableWindow(mainWindowHandle, true);
        settingsWindow.Activate();
    }

    // Purely informational - Stable's own major ceiling always climbs to include whatever major
    // a client is already on once a newer one ships, so nobody needs to act on this to eventually
    // receive it; it's just letting an impatient user know it exists sooner than that.
    public void ShowNewerMajorVersionNotice(string newerMajorVersion)
    {
        NewerMajorNotice.Message = $"Version {newerMajorVersion} is out. You'll receive it automatically in a future update.";
        NewerMajorNotice.IsOpen = true;
    }
}