namespace Tamp.AdjacentContainer;

/// <summary>
/// How a <see cref="TampConnection"/> was acquired. Surfaces on the connection
/// so test diagnostics, log lines, and skip-decision logic can branch on it
/// without re-running the acquisition heuristic.
/// </summary>
public enum AdjacentMode
{
    /// <summary>
    /// The resource is hosted alongside the test process (sidecar on the same CI
    /// agent, host service, etc.). The connection string was read from an env var
    /// supplied by the build orchestration. Tests must not assume ownership of
    /// the resource — schema setup / teardown must be idempotent.
    /// </summary>
    Adjacent,

    /// <summary>
    /// The resource was spawned locally via Testcontainers for the lifetime of the
    /// test session. <see cref="TampConnection.DisposeAsync"/> will stop and remove
    /// the container. Use this mode for dev-workstation iteration.
    /// </summary>
    LocalSpawned,
}
