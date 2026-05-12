using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Base class shared by every resource-specific builder. Encapsulates the dual-mode
/// contract: probe an env var first, fall through to a Testcontainers-spawned local
/// instance, throw a typed exception if neither is reachable. Resource-specific state
/// (database name, image tag, port mappings) lives on the derived builder.
/// </summary>
/// <typeparam name="TSelf">CRTP self-type so fluent chaining returns the derived class.</typeparam>
public abstract class AdjacentContainerBuilder<TSelf> where TSelf : AdjacentContainerBuilder<TSelf>
{
    private bool _localFallbackEnabled = true;

    /// <summary>
    /// Default env var key the base contract reads. Resource-specific
    /// (e.g. <c>TAMP_PG_CONNECTION</c> for Postgres). Set by derived
    /// constructors; <see cref="WithEnvironmentOverride"/> overrides it.
    /// </summary>
    protected string EnvironmentVariableKey { get; private set; }

    /// <summary>Human-friendly resource name for diagnostics (e.g. <c>postgres</c>).</summary>
    protected string ResourceName { get; }

    /// <summary>True when the local Testcontainers spawn fallback is allowed.</summary>
    protected bool LocalFallbackEnabled => _localFallbackEnabled;

    /// <summary>CRTP self-cast for fluent chaining on the derived builder.</summary>
    protected TSelf This => (TSelf)this;

    protected AdjacentContainerBuilder(string resourceName, string defaultEnvVarKey)
    {
        ResourceName = resourceName;
        EnvironmentVariableKey = defaultEnvVarKey;
    }

    /// <summary>
    /// Override the env var the builder reads. Default is resource-specific —
    /// use this when an existing pipeline already exports a different name.
    /// </summary>
    public TSelf WithEnvironmentOverride(string envVarKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(envVarKey);
        EnvironmentVariableKey = envVarKey;
        return This;
    }

    /// <summary>
    /// Disable the local Testcontainers spawn. When called, acquisition either
    /// reads the env var or throws — useful on CI agents where falling through
    /// to docker spawn is a misconfiguration, not a workflow.
    /// </summary>
    public TSelf DisableLocalFallback()
    {
        _localFallbackEnabled = false;
        return This;
    }

    /// <summary>
    /// Acquire a <see cref="TampConnection"/>. Reads <see cref="EnvironmentVariableKey"/>
    /// first; if absent and <see cref="LocalFallbackEnabled"/> is true, spawns the
    /// resource locally via Testcontainers; otherwise throws
    /// <see cref="TampAdjacentContainerUnavailableException"/>.
    /// </summary>
    public async Task<TampConnection> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariableKey);
        if (!string.IsNullOrEmpty(fromEnv))
            return new TampConnection(fromEnv, AdjacentMode.Adjacent, dispose: null);

        if (!_localFallbackEnabled)
            throw new TampAdjacentContainerUnavailableException(
                ResourceName, EnvironmentVariableKey,
                "Local fallback is disabled. This usually indicates a missing pipeline-step that should have exported the env var.");

        try
        {
            return await SpawnLocalAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new TampAdjacentContainerUnavailableException(
                ResourceName, EnvironmentVariableKey,
                $"Local-fallback spawn failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resource-specific Testcontainers spawn. Derived classes implement this and
    /// must return a <see cref="TampConnection"/> with <see cref="AdjacentMode.LocalSpawned"/>
    /// and a non-null disposal hook that stops + removes the container.
    /// </summary>
    protected abstract Task<TampConnection> SpawnLocalAsync(CancellationToken cancellationToken);
}
