using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniCds.Infrastructure.Instruments;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Domain.Abstractions;
using Serilog;

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
            .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("CdsDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:CdsDb is not configured.");
                services.AddMiniCdsPersistence(connectionString);

                // Instrument source: Simulator or Mqtt based on configuration
                var instrumentMode = context.Configuration["Instrument:Mode"] ?? "Simulator";
                if (instrumentMode.Equals("Mqtt", StringComparison.OrdinalIgnoreCase))
                {
                    var mqttHost = context.Configuration["Instrument:Mqtt:BrokerHost"] ?? "localhost";
                    var mqttPort = int.Parse(context.Configuration["Instrument:Mqtt:BrokerPort"] ?? "1883");
                    var deviceId = context.Configuration["Instrument:Mqtt:DeviceId"] ?? "device-001";
                    services.AddScoped<IInstrumentSource>(_ =>
                        new MqttInstrumentSource(mqttHost, mqttPort, deviceId));
                }
                else
                {
                    services.AddScoped<IInstrumentSource>(_ => new SimulatorInstrumentSource(
                        sampleRateHz: 10,
                        durationSeconds: 60,
                        peaks: new[]
                        {
                            new SimulatorPeakDefinition(100, 5.0, 0.3),
                            new SimulatorPeakDefinition(80, 12.0, 0.4),
                            new SimulatorPeakDefinition(120, 20.0, 0.5),
                            new SimulatorPeakDefinition(60, 30.0, 0.6)
                        },
                        noiseStdDev: 2.0,
                        baselineSlope: 0.1));
                }

                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
                services.AddTransient<Views.ReportDialog>();
                services.AddTransient<Views.AuditWindow>();
            })
            .Build();

        // Last-resort handler: UI-thread crashes become logged and visible.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI exception");
        MessageBox.Show(e.Exception.Message, "Unexpected error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // async void swallows exceptions: surface them, otherwise the app exits silently.
        try
        {
            await StartupCoreAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show(ex.ToString(), "Startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
        base.OnStartup(e);
    }

    private async Task StartupCoreAsync()
    {
        await _host.StartAsync();
        Log.Information("MiniCds host started");

        // SQLite resolves the relative "data/cds.db" from appsettings against the process
        // working directory, so the folder must be created there too. The data/ folder is git-ignored.
        Directory.CreateDirectory("data");

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CdsDbContext>();
            await db.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            var demoPassword = scope.ServiceProvider
                .GetRequiredService<IConfiguration>()
                ["Demo:Password"];

            var seed = await seeder.SeedAsync(demoPassword);
            Log.Information("Seeding complete: created {CreatedCount} user(s), system id={SystemUserId}",
                seed.Created.Count, seed.SystemUserId);

            var generated = seed.Created.Where(u => u.Password is not null).ToList();
            if (generated.Count > 0)
            {
                // Credentials must never reach the log file: show them once in a dialog.
                var credentials = string.Join(Environment.NewLine,
                    generated.Select(u => $"{u.Username}: {u.Password}"));
                MessageBox.Show(
                    $"Created demo users with generated passwords:{Environment.NewLine}{Environment.NewLine}" +
                    $"{credentials}{Environment.NewLine}{Environment.NewLine}" +
                    "Set Demo__Password to choose your own.",
                    "Database seeded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Login flow: show LoginWindow as modal dialog.
        using (var loginScope = _host.Services.CreateScope())
        {
            var loginWindow = loginScope.ServiceProvider.GetRequiredService<LoginWindow>();
            var dialogResult = loginWindow.ShowDialog();

            if (dialogResult != true)
            {
                Log.Information("Login cancelled or failed. Application shutting down.");
                Shutdown();
                return;
            }

            Log.Information("User {Username} logged in successfully.", loginWindow.AuthResult?.Username);

            var actorUserId = loginWindow.AuthResult?.UserId ?? 0;

            // One DI scope per window: DbContext lives as long as the window does.
            var windowScope = _host.Services.CreateScope();
            var mainWindow = ActivatorUtilities.CreateInstance<MainWindow>(windowScope.ServiceProvider, actorUserId);
            MainWindow = mainWindow;
            mainWindow.Closed += (_, _) =>
            {
                windowScope.Dispose();
                Shutdown();
            };
            mainWindow.Show();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("MiniCds host stopping");
        await _host.StopAsync();
        _host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

