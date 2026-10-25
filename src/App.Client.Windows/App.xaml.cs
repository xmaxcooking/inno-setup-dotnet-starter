using App.Client.Windows.Settings;
using App.Infrastructure;
using App.Updates;
using App.Updates.Feed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        var updateCheck = provider.GetRequiredService<UpdateCheck>();
        var update = await updateCheck.CheckAsync();
        if (update is null)
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
            await updateCheck.DownloadAndLaunchAsync(update);

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

    private async Task<bool> showUpdateDialogAsync(UpdateInfo update)
    {
        var dialog = new ContentDialog
        {
            Title = "Update available",
            Content = $"Version {update.Version} is available. Install it now?",
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
        services.AddHttpClient();

        services.AddInfrastructure();
        services.AddUpdates();

        services.AddTransient<SettingsWindow>();
        services.AddSingleton<Func<SettingsWindow>>(sp => sp.GetRequiredService<SettingsWindow>);
        services.AddTransient<MainWindow>();
    }
}