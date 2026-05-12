using System;
using System.Threading.Tasks;

namespace Tamp.AdjacentContainer;

/// <summary>
/// A live handle to a container resource — either an adjacent sidecar whose lifetime
/// the test doesn't own, or a locally spawned Testcontainers instance whose lifetime
/// it does. The <see cref="ConnectionString"/> shape is identical regardless of mode,
/// so test code remains mode-agnostic.
/// </summary>
public sealed class TampConnection : IAsyncDisposable
{
    private readonly Func<ValueTask>? _dispose;
    private bool _disposed;

    internal TampConnection(string connectionString, AdjacentMode mode, Func<ValueTask>? dispose)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ConnectionString = connectionString;
        Mode = mode;
        _dispose = dispose;
    }

    /// <summary>
    /// The fully qualified connection string. Shape depends on the resource
    /// (Npgsql for Postgres, Azure storage for Azurite, etc.) but is identical
    /// across <see cref="AdjacentMode"/>s for the same resource type.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>How the connection was acquired. See <see cref="AdjacentMode"/>.</summary>
    public AdjacentMode Mode { get; }

    /// <summary>
    /// Disposes the locally-spawned container if <see cref="Mode"/> is
    /// <see cref="AdjacentMode.LocalSpawned"/>; no-op for
    /// <see cref="AdjacentMode.Adjacent"/> (we never tear down resources we don't own).
    /// Safe to call multiple times.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_dispose is not null) await _dispose().ConfigureAwait(false);
    }
}
