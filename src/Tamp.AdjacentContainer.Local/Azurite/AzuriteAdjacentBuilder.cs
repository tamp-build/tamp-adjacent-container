using System.Threading;
using System.Threading.Tasks;
using Testcontainers.Azurite;

namespace Tamp.AdjacentContainer.Azurite;

/// <summary>
/// Fluent builder for an adjacent-or-local Azurite (Azure Storage emulator) connection.
/// Default env var is <c>TAMP_AZURITE_CONNECTION</c>; when absent, the standard
/// <c>mcr.microsoft.com/azure-storage/azurite</c> image is spawned via Testcontainers.
/// </summary>
public sealed class AzuriteAdjacentBuilder : AdjacentContainerBuilder<AzuriteAdjacentBuilder>
{
    private string _image = "mcr.microsoft.com/azure-storage/azurite:latest";
    private bool _inMemoryPersistence = true;

    public const string DefaultEnvironmentVariableKey = "TAMP_AZURITE_CONNECTION";

    internal AzuriteAdjacentBuilder() : base("azurite", DefaultEnvironmentVariableKey) { }

    /// <summary>
    /// Docker image used by the local-fallback spawn.
    /// Default <c>mcr.microsoft.com/azure-storage/azurite:latest</c>.
    /// </summary>
    public AzuriteAdjacentBuilder WithLocalFallback(string image = "mcr.microsoft.com/azure-storage/azurite:latest")
    {
        _image = image;
        return this;
    }

    /// <summary>
    /// When true (default), the locally-spawned Azurite stores data in-memory and discards on stop.
    /// Set false to disk-back the data within the container — useful when reproducing a flaky test
    /// that depends on data persisting across container restarts.
    /// </summary>
    public AzuriteAdjacentBuilder WithInMemoryPersistence(bool inMemory = true)
    {
        _inMemoryPersistence = inMemory;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<TampConnection> SpawnLocalAsync(CancellationToken cancellationToken)
    {
        var builder = new AzuriteBuilder(_image);
        if (_inMemoryPersistence) builder = builder.WithInMemoryPersistence();

        var container = builder.Build();
        await container.StartAsync(cancellationToken).ConfigureAwait(false);

        return new TampConnection(
            container.GetConnectionString(),
            AdjacentMode.LocalSpawned,
            dispose: async () => await container.DisposeAsync().ConfigureAwait(false));
    }
}
