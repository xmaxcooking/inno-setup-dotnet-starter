using App.Client.Windows.Settings;
using App.Infrastructure;
using App.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

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

        if (await provider.GetRequiredService<UpdateCheck>().RunIfDueAsync())
        {
            provider.Dispose();
            Exit();
            return;
        }

        _mainWindow = provider.GetRequiredService<MainWindow>();
        _mainWindow.Closed += (_, _) => provider.Dispose();
        _mainWindow.Activate();
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