using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Tamp.AdjacentContainer;

/// <summary>
/// Resolves a short, stable per-worker discriminator so N agents in N worktrees can spin up the
/// same emulator concurrently and still tell their containers apart / clean them up independently
/// (tamp-build/tamp#18, ADR 0019 Pillar 4). Testcontainers already gives every spawn a random
/// name + ephemeral host ports, so this is for attribution, not collision-avoidance.
///
/// Resolution: explicit <c>TAMP_WORKER_ID</c> (sanitized) → short hash of the worktree root path.
/// </summary>
internal static class WorkerScope
{
    /// <summary>
    /// A safe discriminator token (e.g. <c>w-a1b2c3d4e5</c>). <paramref name="getEnv"/> and
    /// <paramref name="worktree"/> are injectable so tests get deterministic output.
    /// </summary>
    public static string Discriminator(Func<string, string?>? getEnv = null, string? worktree = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;

        var explicitId = getEnv("TAMP_WORKER_ID");
        if (!string.IsNullOrWhiteSpace(explicitId))
            return Sanitize(explicitId!);

        worktree ??= SafeWorktree();
        return "w-" + ShortHash(worktree);
    }

    private static string Sanitize(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw.Trim())
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        var token = sb.ToString().Trim('-');
        while (token.Contains("--")) token = token.Replace("--", "-");
        if (token.Length > 40) token = token[..40].Trim('-');
        return token.Length == 0 ? "w-unknown" : token;
    }

    private static string ShortHash(string value)
    {
        var normalized = (value ?? string.Empty).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..10];
    }

    private static string SafeWorktree()
    {
        try { return TampBuild.RootDirectory.Value; }
        catch { }
        try { return Directory.GetCurrentDirectory(); }
        catch { return "unknown"; }
    }
}
