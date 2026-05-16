using Tamp.AdjacentContainer.Azurite;
using Tamp.AdjacentContainer.Postgres;
using Tamp.AdjacentContainer.ServiceBus;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Static entry point for the fixture-side acquisition API. Each static factory
/// returns a resource-specific builder configured with the canonical env-var key,
/// which the caller can override via fluent chaining before terminating with
/// <c>AcquireAsync()</c>.
/// </summary>
public static class TampAdjacentContainer
{
    /// <summary>
    /// Build a Postgres acquisition. Default env var <c>TAMP_PG_CONNECTION</c>;
    /// default local-fallback image <c>postgres:16-alpine</c>.
    /// </summary>
    public static PostgresAdjacentBuilder ForPostgres() => new();

    /// <summary>
    /// Build an Azurite (Azure Storage emulator) acquisition. Default env var
    /// <c>TAMP_AZURITE_CONNECTION</c>; default local-fallback image
    /// <c>mcr.microsoft.com/azure-storage/azurite:latest</c>.
    /// </summary>
    public static AzuriteAdjacentBuilder ForAzurite() => new();

    /// <summary>
    /// Build a Service Bus emulator acquisition. Default env var
    /// <c>TAMP_SBUS_CONNECTION</c>; default local-fallback image
    /// <c>mcr.microsoft.com/azure-messaging/servicebus-emulator:latest</c>.
    /// </summary>
    public static ServiceBusEmulatorAdjacentBuilder ForServiceBusEmulator() => new();
}
