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
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();
        configureServices(services);
        var provider = services.BuildServiceProvider();

        _mainWindow = provider.GetRequiredService<MainWindow>();
        _mainWindow.Closed += (_, _) => provider.Dispose();
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
            Exit();
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