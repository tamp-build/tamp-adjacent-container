using System;
using System.Threading.Tasks;
using Tamp.AdjacentContainer.Azurite;
using Xunit;

namespace Tamp.AdjacentContainer.Tests;

public sealed class AzuriteAdjacentBuilderTests
{
    private static string UniqueKey() => $"TAMP_AZURITE_TEST_{Guid.NewGuid():N}";

    [Fact]
    public async Task Adjacent_Mode_When_Env_Var_Is_Set()
    {
        var key = UniqueKey();
        const string expected = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vd;BlobEndpoint=http://az.sidecar:10000/devstoreaccount1;";
        using var _ = new EnvironmentScope(key, expected);

        var conn = await TampAdjacentContainer.ForAzurite()
            .WithEnvironmentOverride(key)
            .AcquireAsync();

        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
        Assert.Equal(expected, conn.ConnectionString);
    }

    [Fact]
    public async Task Disabled_Fallback_With_Missing_Env_Var_Throws()
    {
        var key = UniqueKey();

        var ex = await Assert.ThrowsAsync<TampAdjacentContainerUnavailableException>(() =>
            TampAdjacentContainer.ForAzurite()
                .WithEnvironmentOverride(key)
                .DisableLocalFallback()
                .AcquireAsync());

        Assert.Equal("azurite", ex.Resource);
        Assert.Equal(key, ex.EnvVarKey);
    }

    [Fact]
    public async Task Default_Env_Var_Key_Used_When_Unspecified()
    {
        using var _ = new EnvironmentScope(
            AzuriteAdjacentBuilder.DefaultEnvironmentVariableKey,
            "BlobEndpoint=http://localhost:10000/;");

        var conn = await TampAdjacentContainer.ForAzurite().AcquireAsync();
        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
    }
}
