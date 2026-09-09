// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Composition\CompositionTests.cs
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniCds.Application.Audit;
using MiniCds.Application.Auth;
using MiniCds.Domain.Abstractions;
using MiniCds.Infrastructure.Persistence;

namespace MiniCds.Tests.Composition;

/// <summary>
/// Verifies the production DI graph resolves and honours service lifetimes.
/// ValidateScopes catches captive dependencies (scoped service captured by a singleton),
/// which is the classic desktop-app bug since WPF objects live for the app lifetime.
/// </summary>
public class CompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMiniCdsPersistence("Data Source=:memory:");
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void Provider_ResolvesAllDomainServices()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IHashChain>().Should().BeOfType<HashChain>();
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditTrail>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISignatureService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AuditService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IUserStore>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AuthService>().Should().NotBeNull();
    }

    [Fact]
    public void HashChain_IsSingleton_SameInstanceAcrossScopes()
    {
        using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IHashChain>();
        var b = scope2.ServiceProvider.GetRequiredService<IHashChain>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void AuditTrail_IsScoped_DifferentInstancePerScope()
    {
        using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IAuditTrail>();
        var b = scope2.ServiceProvider.GetRequiredService<IAuditTrail>();

        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void AuditTrail_ResolvesWithinScope_WithDbContext()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var trail = scope.ServiceProvider.GetRequiredService<IAuditTrail>();

        trail.Should().BeOfType<AuditTrail>();
    }
}