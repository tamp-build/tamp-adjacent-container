using System;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Thrown when neither the adjacent env-var nor the local-fallback path can produce
/// a working connection. The message is intentionally long and actionable — adopters
/// see it once at fixture init and need the remediation in the log without grepping.
/// </summary>
public sealed class TampAdjacentContainerUnavailableException : InvalidOperationException
{
    public TampAdjacentContainerUnavailableException(
        string resource,
        string envVarKey,
        string detail)
        : base(BuildMessage(resource, envVarKey, detail))
    {
        Resource = resource;
        EnvVarKey = envVarKey;
    }

    /// <summary>Resource name being acquired (e.g. <c>postgres</c>, <c>azurite</c>).</summary>
    public string Resource { get; }

    /// <summary>The env var the builder was probing for an adjacent connection string.</summary>
    public string EnvVarKey { get; }

    private static string BuildMessage(string resource, string envVarKey, string detail) =>
        $"Tamp.AdjacentContainer could not acquire a {resource} connection. " +
        $"Set {envVarKey}=<connection-string> to use an adjacent sidecar, or make a Docker daemon " +
        $"reachable so the local-fallback Testcontainers spawn can succeed. {detail}";
}
