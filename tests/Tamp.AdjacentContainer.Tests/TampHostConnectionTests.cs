using System;
using System.Threading.Tasks;
using Xunit;

namespace Tamp.AdjacentContainer.Tests;

/// <summary>
/// TampHostConnection — the host-mode-only acquisition path that lives in the core
/// Tamp.AdjacentContainer package (no Testcontainers dependency). Adopters who only
/// expect adjacent sidecars (typical for Build.cs orchestration) reach for this
/// instead of the dual-mode TampAdjacentContainer.ForXxx() facade.
/// </summary>
public sealed class TampHostConnectionTests
{
    private static string UniqueKey() => $"TAMP_HOST_TEST_{Guid.NewGuid():N}";

    [Fact]
    public void FromEnvironment_Returns_Adjacent_Connection_When_Env_Var_Is_Set()
    {
        var key = UniqueKey();
        const string expected = "Host=db.sidecar;Database=tamp;Username=u;Password=p";
        using var _ = new EnvironmentScope(key, expected);

        var conn = TampHostConnection.FromEnvironment(key, "postgres");

        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
        Assert.Equal(expected, conn.ConnectionString);
    }

    [Fact]
    public void FromEnvironment_Throws_Unavailable_When_Env_Var_Unset()
    {
        var key = UniqueKey();
        // No EnvironmentScope — key is guaranteed-unset.

        var ex = Assert.Throws<TampAdjacentContainerUnavailableException>(
            () => TampHostConnection.FromEnvironment(key, "postgres"));

        Assert.Equal("postgres", ex.Resource);
        Assert.Equal(key, ex.EnvVarKey);
        // Adopter remediation should mention the .Local package as the alternative.
        Assert.Contains("Tamp.AdjacentContainer.Local", ex.Message);
    }

    [Fact]
    public void FromEnvironment_Throws_Unavailable_When_Env_Var_Empty()
    {
        var key = UniqueKey();
        // Environment.SetEnvironmentVariable(..., "") clears the var on Windows
        // and stores an empty string on Unix — both paths flow through our
        // IsNullOrEmpty check and surface as unavailable.
        using var _ = new EnvironmentScope(key, "");

        Assert.Throws<TampAdjacentContainerUnavailableException>(
            () => TampHostConnection.FromEnvironment(key, "postgres"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromEnvironment_Rejects_Empty_EnvVarKey(string? key)
    {
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException for null
        // and ArgumentException for empty — ThrowsAny<ArgumentException> covers both.
        Assert.ThrowsAny<ArgumentException>(() => TampHostConnection.FromEnvironment(key!, "postgres"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromEnvironment_Rejects_Empty_ResourceName(string? resource)
    {
        Assert.ThrowsAny<ArgumentException>(() => TampHostConnection.FromEnvironment("ANYTHING", resource!));
    }

    [Fact]
    public void FromEnvironment_Defaults_Resource_Name_To_resource()
    {
        var key = UniqueKey();
        var ex = Assert.Throws<TampAdjacentContainerUnavailableException>(
            () => TampHostConnection.FromEnvironment(key));

        Assert.Equal("resource", ex.Resource);
    }

    [Fact]
    public async Task FromEnvironment_Connection_Disposal_Is_NoOp()
    {
        var key = UniqueKey();
        using var _ = new EnvironmentScope(key, "Host=foo");

        var conn = TampHostConnection.FromEnvironment(key, "postgres");
        // Adjacent-mode connections never own a container — dispose should be
        // safe even though the env var supplied no callback.
        await conn.DisposeAsync();
        await conn.DisposeAsync();
    }
}
