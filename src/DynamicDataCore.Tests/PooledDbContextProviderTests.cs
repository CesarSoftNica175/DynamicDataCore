using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicDataCore.Tests;

public class PooledDbContextProviderTests
{
    private static PooledDbContextProvider BuildProvider(IReadOnlyDictionary<string, Type> map, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        var sp = services.BuildServiceProvider();
        return new PooledDbContextProvider(sp, map);
    }

    // ── Happy paths ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateContext_ReturnsCorrectType_WhenKeyAndTypeMatch()
    {
        var map = new Dictionary<string, Type> { ["A"] = typeof(ProviderDbA) };
        var provider = BuildProvider(map, svc =>
            svc.AddDbContextFactory<ProviderDbA>(opt => opt.UseInMemoryDatabase("A")));

        var ctx = provider.CreateContext<ProviderDbA>("A");
        Assert.NotNull(ctx);
        Assert.IsType<ProviderDbA>(ctx);
        ctx.Dispose();
    }

    [Fact]
    public void CreateContext_WithTwoKeys_ResolvesEachIndependently()
    {
        var map = new Dictionary<string, Type>
        {
            ["A"] = typeof(ProviderDbA),
            ["B"] = typeof(ProviderDbB)
        };
        var provider = BuildProvider(map, svc =>
        {
            svc.AddDbContextFactory<ProviderDbA>(opt => opt.UseInMemoryDatabase("A"));
            svc.AddDbContextFactory<ProviderDbB>(opt => opt.UseInMemoryDatabase("B"));
        });

        using var ctxA = provider.CreateContext<ProviderDbA>("A");
        using var ctxB = provider.CreateContext<ProviderDbB>("B");

        Assert.IsType<ProviderDbA>(ctxA);
        Assert.IsType<ProviderDbB>(ctxB);
    }

    // ── Error conditions ─────────────────────────────────────────────────────

    [Fact]
    public void CreateContext_Throws_WhenKeyNotRegistered()
    {
        var provider = BuildProvider(new Dictionary<string, Type>());

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.CreateContext<ProviderDbA>("nonexistent"));

        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void CreateContext_Throws_WhenTypeMismatch()
    {
        var map = new Dictionary<string, Type> { ["A"] = typeof(ProviderDbA) };
        var provider = BuildProvider(map, svc =>
            svc.AddDbContextFactory<ProviderDbA>(opt => opt.UseInMemoryDatabase("A")));

        // Key "A" is registered for ProviderDbA, but caller asks for ProviderDbB
        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.CreateContext<ProviderDbB>("A"));

        Assert.Contains("ProviderDbA", ex.Message);
        Assert.Contains("ProviderDbB", ex.Message);
    }

    // ── GetRegisteredKeys ────────────────────────────────────────────────────

    [Fact]
    public void GetRegisteredKeys_ReturnsAllMappedKeys()
    {
        var map = new Dictionary<string, Type>
        {
            ["Primary"] = typeof(ProviderDbA),
            ["Reporting"] = typeof(ProviderDbB)
        };
        var provider = BuildProvider(map);

        var keys = provider.GetRegisteredKeys();

        Assert.Contains("Primary", keys);
        Assert.Contains("Reporting", keys);
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void GetRegisteredKeys_ReturnsEmpty_WhenNoKeysRegistered()
    {
        var provider = BuildProvider(new Dictionary<string, Type>());
        Assert.Empty(provider.GetRegisteredKeys());
    }

    // ── Constructor validation ───────────────────────────────────────────────

    [Fact]
    public void Constructor_Throws_WhenServiceProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PooledDbContextProvider(null!, new Dictionary<string, Type>()));
    }

    [Fact]
    public void Constructor_Throws_WhenKeyMapIsNull()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new PooledDbContextProvider(sp, null!));
    }
}
