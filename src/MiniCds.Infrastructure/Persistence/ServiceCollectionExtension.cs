// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Persistence\ServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniCds.Application.Acquisition;
using MiniCds.Application.Audit;
using MiniCds.Application.Auth;
using MiniCds.Application.SignalProcessing;
using MiniCds.Domain.Abstractions;
using MiniCds.Infrastructure.Persistence.Repositories;
using MiniCds.Infrastructure.Security;

namespace MiniCds.Infrastructure.Persistence;

/// <summary>
/// Registers persistence-backed services. All are Scoped: the desktop host creates one
/// scope per window, so the DbContext lifetime matches the window lifetime.
/// AppendOnlyInterceptor is intentionally NOT registered here — it lives in CdsDbContext.OnConfiguring.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMiniCdsPersistence(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be blank.", nameof(connectionString));

        services.AddDbContext<CdsDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<IHashChain, HashChain>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<ISignatureService, SignatureService>();
        services.AddScoped<AuditService>();
        services.AddScoped<IUserStore, EfUserStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DbSeeder>();
        services.AddScoped<ISampleRepository, SampleRepository>();
        services.AddScoped<IMethodRepository, MethodRepository>();
        services.AddScoped<IRawSignalRepository, RawSignalRepository>();
        services.AddScoped<IPeakRepository, PeakRepository>();
        services.AddScoped<ISignalProcessor, SignalProcessor>();
        services.AddScoped<AcquisitionService>();
        return services;
    }
}
