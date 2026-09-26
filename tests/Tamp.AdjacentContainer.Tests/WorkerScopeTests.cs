using Xunit;

namespace Tamp.AdjacentContainer.Tests;

/// <summary>
/// #18 per-worker attribution: the worker discriminator and the container-label set stamped on
/// locally-spawned adjacent containers so parallel worktrees are attributable and cleanable.
/// (The actual container spawn is Docker-gated and covered by integration tests, as elsewhere.)
/// </summary>
public sealed class WorkerScopeTests
{
    [Fact]
    public void Discriminator_Honors_TampWorkerId_And_Sanitizes()
    {
        Assert.Equal("agent-pool-3",
            WorkerScope.Discriminator(getEnv: k => k == "TAMP_WORKER_ID" ? "agent:pool/3" : null));
    }

    [Fact]
    public void Discriminator_Is_Stable_Per_Worktree_And_Distinct_Across_Worktrees()
    {
        var a = WorkerScope.Discriminator(getEnv: _ => null, worktree: "/repos/wt-a");
        var a2 = WorkerScope.Discriminator(getEnv: _ => null, worktree: "/repos/wt-a");
        var b = WorkerScope.Discriminator(getEnv: _ => null, worktree: "/repos/wt-b");

        Assert.Equal(a, a2);
        Assert.NotEqual(a, b);
        Assert.StartsWith("w-", a);
    }

    [Fact]
    public void ForWorker_Labels_Carry_Worker_And_Resource()
    {
        var labels = ContainerLabels.ForWorker("postgres", "agent-3");

        Assert.Equal("agent-3", labels[ContainerLabels.Worker]);
        Assert.Equal("postgres", labels[ContainerLabels.Resource]);
    }

    [Fact]
    public void Different_Workers_Get_Distinct_Worker_Labels()
    {
        var a = ContainerLabels.ForWorker("postgres", WorkerScope.Discriminator(getEnv: _ => null, worktree: "/repos/wt-a"));
        var b = ContainerLabels.ForWorker("postgres", WorkerScope.Discriminator(getEnv: _ => null, worktree: "/repos/wt-b"));

        Assert.NotEqual(a[ContainerLabels.Worker], b[ContainerLabels.Worker]);
    }
}
