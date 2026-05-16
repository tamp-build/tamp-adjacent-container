using System;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Host-mode-only acquisition helper. Reads a single env var and returns a
/// <see cref="TampConnection"/> in <see cref="AdjacentMode.Adjacent"/> mode, or throws
/// <see cref="TampAdjacentContainerUnavailableException"/> if the variable is unset.
/// <para>
/// Use this from build-side projects (Build.cs, orchestration scripts, integration
/// hooks) that only ever expect an adjacent sidecar — never a locally-spawned
/// container. The point is to avoid pulling <c>Tamp.AdjacentContainer.Local</c>
/// (and its <c>Testcontainers</c> transitive), whose <c>DotNet.Testcontainers</c>
/// namespace shadows <c>Tamp.NetCli.V10.DotNet</c> in any project that does
/// <c>using Tamp.NetCli.V10;</c>.
/// </para>
/// <para>
/// For full dual-mode acquisition (env-var probe → Testcontainers fallback),
/// reference <c>Tamp.AdjacentContainer.Local</c> and call
/// <c>TampAdjacentContainer.ForPostgres()</c> / <c>ForAzurite()</c> / <c>ForServiceBusEmulator()</c>.
/// </para>
/// </summary>
public static class TampHostConnection
{
    /// <summary>
    /// Read <paramref name="environmentVariableKey"/> and return its value wrapped
    /// in an adjacent-mode <see cref="TampConnection"/>. <paramref name="resourceName"/>
    /// appears only in the diagnostic exception message — pick something an adopter
    /// would recognize (e.g. <c>"postgres"</c>, <c>"servicebus"</c>).
    /// </summary>
    /// <exception cref="TampAdjacentContainerUnavailableException">
    /// Thrown if the env var is unset or empty. Message points the adopter at both
    /// the env var name and the <c>Tamp.AdjacentContainer.Local</c> package as the
    /// alternative path.
    /// </exception>
    public static TampConnection FromEnvironment(string environmentVariableKey, string resourceName = "resource")
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentVariableKey);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);

        var value = Environment.GetEnvironmentVariable(environmentVariableKey);
        if (string.IsNullOrEmpty(value))
            throw new TampAdjacentContainerUnavailableException(
                resourceName, environmentVariableKey,
                "Host-only acquisition has no local-fallback path. Either export the env var, or reference Tamp.AdjacentContainer.Local and use TampAdjacentContainer.ForXxx() to enable the Testcontainers fallback.");

        return new TampConnection(value, AdjacentMode.Adjacent, dispose: null);
    }
}
