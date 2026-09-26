using System.Collections.Generic;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Standard Docker labels stamped on every locally-spawned adjacent container (#18). They make
/// concurrent containers from different worktrees/workers attributable and enable per-worker
/// cleanup (e.g. <c>docker ps --filter label=tamp.worker=&lt;id&gt;</c>).
/// </summary>
internal static class ContainerLabels
{
    /// <summary>Per-worker discriminator (worktree/worker) that spawned the container.</summary>
    public const string Worker = "tamp.worker";

    /// <summary>Resource kind (e.g. <c>postgres</c>, <c>azurite</c>, <c>servicebus</c>).</summary>
    public const string Resource = "tamp.resource";

    /// <summary>The label set for a spawn: worker discriminator + resource name.</summary>
    public static IReadOnlyDictionary<string, string> ForWorker(string resourceName, string discriminator)
        => new Dictionary<string, string>
        {
            [Worker] = discriminator,
            [Resource] = resourceName,
        };
}
