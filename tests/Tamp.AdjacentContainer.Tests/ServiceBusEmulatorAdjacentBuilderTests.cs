using System;
using System.Threading.Tasks;
using Tamp.AdjacentContainer.ServiceBus;
using Xunit;

namespace Tamp.AdjacentContainer.Tests;

public sealed class ServiceBusEmulatorAdjacentBuilderTests
{
    private static string UniqueKey() => $"TAMP_SBUS_TEST_{Guid.NewGuid():N}";

    [Fact]
    public async Task Adjacent_Mode_When_Env_Var_Is_Set()
    {
        var key = UniqueKey();
        const string expected = "Endpoint=sb://sb.sidecar/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS;";
        using var _ = new EnvironmentScope(key, expected);

        var conn = await TampAdjacentContainer.ForServiceBusEmulator()
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
            TampAdjacentContainer.ForServiceBusEmulator()
                .WithEnvironmentOverride(key)
                .DisableLocalFallback()
                .AcquireAsync());

        Assert.Equal("servicebus", ex.Resource);
        Assert.Equal(key, ex.EnvVarKey);
    }

    [Fact]
    public async Task Default_Env_Var_Key_Used_When_Unspecified()
    {
        using var _ = new EnvironmentScope(
            ServiceBusEmulatorAdjacentBuilder.DefaultEnvironmentVariableKey,
            "Endpoint=sb://default/;");

        var conn = await TampAdjacentContainer.ForServiceBusEmulator().AcquireAsync();
        Assert.Equal(AdjacentMode.Adjacent, conn.Mode);
    }
}
