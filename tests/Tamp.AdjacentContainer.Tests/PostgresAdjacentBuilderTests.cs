using System;
using System.Threading.Tasks;
using Tamp.AdjacentContainer.Postgres;
using Xunit;

namespace Tamp.AdjacentContainer.Tests;

/// <summary>
/// PostgresAdjacentBuilder — unit tests for the adjacent and disabled-fallback paths.
/// Local-spawn path (Docker required) is not exercised here; covered by integration
/// tests that gate on Tamp.Testcontainers.V4's probe.
/// </summary>
public sealed class PostgresAdjacentBuilderTests
{
    private static string UniqueKey() => $"TAMP_PG_TEST_{Guid.NewGuid():N}";

    [Fact]
    public async Task Adjacent_Mode_When_Env_Var_Is_Set()
    {
        var key = UniqueKey();
        const string expected = "Host=pg.sidecar;Database=strata_test;Username=tamp;Password=secret";
        using var _ = new EnvironmentScope(key, expected);

        var conn = await TampAdjacentContainer.ForPostgres()
            .WithEnvironmentOverride(key)
            .AcquireAsync();

        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
        Assert.Equal(expected, conn.ConnectionString);

        // Disposing an Adjacent connection must NOT do anything destructive.
        await conn.DisposeAsync();
    }

    [Fact]
    public async Task Disabled_Fallback_With_Missing_Env_Var_Throws_With_Diagnostic()
    {
        var key = UniqueKey();
        // No EnvironmentScope — env var deliberately unset.

        var ex = await Assert.ThrowsAsync<TampAdjacentContainerUnavailableException>(() =>
            TampAdjacentContainer.ForPostgres()
                .WithEnvironmentOverride(key)
                .DisableLocalFallback()
                .AcquireAsync());

        Assert.Equal("postgres", ex.Resource);
        Assert.Equal(key, ex.EnvVarKey);
        Assert.Contains(key, ex.Message);
        Assert.Contains("postgres", ex.Message);
    }

    [Fact]
    public async Task Default_Env_Var_Key_Is_Used_When_No_Override_Given()
    {
        // Confirms the documented constant. Use the canonical key so adopters can
        // grep for it. Use EnvironmentScope to avoid leaking into other tests.
        using var _ = new EnvironmentScope(
            PostgresAdjacentBuilder.DefaultEnvironmentVariableKey,
            "Host=default;Database=tamp_test");

        var conn = await TampAdjacentContainer.ForPostgres().AcquireAsync();

        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
        Assert.Equal("Host=default;Database=tamp_test", conn.ConnectionString);
    }

    [Fact]
    public void Fluent_Chain_Returns_Same_Builder_Type()
    {
        // CRTP smoke — chaining returns PostgresAdjacentBuilder, not the base type,
        // so resource-specific methods (WithDatabase, WithUsername) remain reachable.
        var b1 = TampAdjacentContainer.ForPostgres();
        var b2 = b1.WithEnvironmentOverride("X").DisableLocalFallback().WithDatabase("y");

        Assert.IsType<PostgresAdjacentBuilder>(b2);
        Assert.Same(b1, b2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void WithEnvironmentOverride_Rejects_Empty_Or_Null(string? key)
    {
        // ThrowsAny catches both ArgumentException (empty) and the derived
        // ArgumentNullException (null) that ThrowIfNullOrEmpty emits.
        var builder = TampAdjacentContainer.ForPostgres();
        Assert.ThrowsAny<ArgumentException>(() => builder.WithEnvironmentOverride(key!));
    }
}
