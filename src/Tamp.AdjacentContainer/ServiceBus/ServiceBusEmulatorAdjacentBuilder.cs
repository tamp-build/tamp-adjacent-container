using System.Threading;
using System.Threading.Tasks;
using Testcontainers.ServiceBus;

namespace Tamp.AdjacentContainer.ServiceBus;

/// <summary>
/// Fluent builder for an adjacent-or-local Service Bus emulator connection. Default env var is
/// <c>TAMP_SBUS_CONNECTION</c>; when absent, Microsoft's <c>mcr.microsoft.com/azure-messaging/servicebus-emulator</c>
/// image is spawned via Testcontainers.
/// </summary>
public sealed class ServiceBusEmulatorAdjacentBuilder : AdjacentContainerBuilder<ServiceBusEmulatorAdjacentBuilder>
{
    private string _image = "mcr.microsoft.com/azure-messaging/servicebus-emulator:latest";
    private bool _acceptEula = true;

    public const string DefaultEnvironmentVariableKey = "TAMP_SBUS_CONNECTION";

    internal ServiceBusEmulatorAdjacentBuilder() : base("servicebus", DefaultEnvironmentVariableKey) { }

    /// <summary>
    /// Docker image used by the local-fallback spawn.
    /// Default <c>mcr.microsoft.com/azure-messaging/servicebus-emulator:latest</c>.
    /// </summary>
    public ServiceBusEmulatorAdjacentBuilder WithLocalFallback(string image = "mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
    {
        _image = image;
        return this;
    }

    /// <summary>
    /// Microsoft's emulator image requires explicit EULA acceptance. Default true —
    /// using the builder is interpreted as acceptance. Override to false to opt
    /// out (the spawn will then fail at runtime per Microsoft's contract).
    /// </summary>
    public ServiceBusEmulatorAdjacentBuilder WithEulaAcceptance(bool accept = true)
    {
        _acceptEula = accept;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<TampConnection> SpawnLocalAsync(CancellationToken cancellationToken)
    {
        var builder = new ServiceBusBuilder(_image);
        if (_acceptEula) builder = builder.WithAcceptLicenseAgreement(true);

        var container = builder.Build();
        await container.StartAsync(cancellationToken).ConfigureAwait(false);

        return new TampConnection(
            container.GetConnectionString(),
            AdjacentMode.LocalSpawned,
            dispose: async () => await container.DisposeAsync().ConfigureAwait(false));
    }
}
