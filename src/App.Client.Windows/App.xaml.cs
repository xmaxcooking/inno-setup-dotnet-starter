using App.Client.Windows.Settings;
using App.Core.Api;
using App.Updates.Feed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Reflection;

namespace App.Client.Windows;

public partial class App : Application
{
    // Must match Setup.iss's AppMutex exactly - held for the app's whole lifetime so a
    // re-run of the installer (manual repair, or the auto-update below) can detect this
    // instance is still running and ask the user to close it before overwriting its files,
    // instead of silently failing to replace a locked exe/dll.
    private const string AppMutexName = "InnoSetupDotnetStarterAppMutex";

    private readonly Mutex _appMutex;

    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        _appMutex = new Mutex(initiallyOwned: true, name: AppMutexName);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        configureServices(services);
        var provider = services.BuildServiceProvider();

        _mainWindow = provider.GetRequiredService<MainWindow>();
        _mainWindow.Closed += (_, _) =>
        {
            provider.Dispose();
            _appMutex.ReleaseMutex();
            _appMutex.Dispose();
        };
        _mainWindow.Activate();

        await checkForUpdatesAsync(provider);
    }

    private async Task checkForUpdatesAsync(ServiceProvider provider)
    {
        var updateService = provider.GetRequiredService<IUpdateService>();
        var result = await updateService.CheckForUpdateAsync();

        if (result.NewerMajorVersion is { } newerMajorVersion)
        {
            _mainWindow!.ShowNewerMajorVersionNotice(newerMajorVersion);
        }

        if (result.Update is not { } update)
        {
            return;
        }

        var accepted = await showUpdateDialogAsync(update);
        if (!accepted)
        {
            return;
        }

        try
        {
            await downloadWithProgressAsync(updateService, update);

            // A hard, immediate process exit (not the async Application.Exit()) so the mutex
            // above and any locked files are released before the installer we just launched
            // gets to the point of overwriting them.
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            provider.GetRequiredService<ILogger<App>>().LogWarning(ex, "Failed to download or launch the confirmed update.");
        }
    }

    private async Task downloadWithProgressAsync(IUpdateService updateService, UpdateInfo update)
    {
        var statusText = new TextBlock { Text = "Starting download…" };
        var progressBar = new ProgressBar { IsIndeterminate = true, Width = 280 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(statusText);
        panel.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = "Downloading update",
            Content = panel,
            XamlRoot = _mainWindow!.Content.XamlRoot
        };

        var progress = new Progress<double>(fraction =>
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = fraction * 100;
            statusText.Text = $"Downloading… {fraction:P0}";
        });

        _ = dialog.ShowAsync();
        try
        {
            await updateService.DownloadAndLaunchUpdateAsync(update, progress);
        }
        finally
        {
            dialog.Hide();
        }
    }

    private async Task<bool> showUpdateDialogAsync(UpdateInfo update)
    {
        var currentVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        var dialog = new ContentDialog
        {
            Title = "Update available",
            Content = $"Version {update.Version} is available (you have {currentVersion}). Install it now?",
            PrimaryButtonText = "Update now",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _mainWindow!.Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static void configureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddDebug());

        services.AddCoreApi();

        services.AddTransient<SettingsWindow>();
        services.AddSingleton<Func<SettingsWindow>>(sp => sp.GetRequiredService<SettingsWindow>);
        services.AddTransient<MainWindow>();
    }
}