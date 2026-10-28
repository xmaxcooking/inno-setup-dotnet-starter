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
}