using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniCds.Infrastructure.Persistence;

namespace MiniCds.Wpf;

/// <summary>
/// Interaction logic for App.xaml.
/// Base class is fully qualified: inside namespace MiniCds.Wpf the simple name
/// Application resolves to the MiniCds.Application namespace before any using alias applies.
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config => config
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false))
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("CdsDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:CdsDb is not configured.");
                services.AddMiniCdsPersistence(connectionString);
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // SQLite resolves the relative "data/cds.db" from appsettings against the process
        // working directory, so the folder must be created there too. The data/ folder is git-ignored.
        Directory.CreateDirectory("data");

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CdsDbContext>();
            await db.Database.MigrateAsync();
        }

        // One DI scope per window: DbContext lives as long as the window does.
        var windowScope = _host.Services.CreateScope();
        var mainWindow = windowScope.ServiceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => windowScope.Dispose();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}

