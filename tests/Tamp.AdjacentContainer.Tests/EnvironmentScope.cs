using System;

namespace Tamp.AdjacentContainer.Tests;

/// <summary>
/// Test helper: sets an env var on construction, restores the prior value on dispose.
/// Tests use unique GUID-derived keys so they never collide with each other or with
/// real env vars set on the dev/CI machine.
/// </summary>
internal sealed class EnvironmentScope : IDisposable
{
    private readonly string _key;
    private readonly string? _priorValue;

    public EnvironmentScope(string key, string value)
    {
        _key = key;
        _priorValue = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_key, _priorValue);
}
