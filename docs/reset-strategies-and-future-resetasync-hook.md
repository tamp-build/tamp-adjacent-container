# Schema reset strategies + the future `TampConnection.ResetAsync()` hook

**Status: design exploration only.** No v0.x commitment to ship.

This note records what adopters do today to reset schema state between test runs in `Adjacent` mode, what a framework-level `ResetAsync()` hook would look like if we shipped one, and the rule we're applying for whether to ship it.

## Why this matters

`Tamp.AdjacentContainer` deliberately does NOT reset state between acquisitions. In `LocalSpawned` mode that's irrelevant — each acquisition gets a fresh container. In `Adjacent` mode the sidecar Postgres / Azurite / Service Bus is long-lived and carries whatever schema and data the previous test run left.

Adopters need *some* reset mechanism for tests to be deterministic. The framework punts on the question because the right answer depends on test-suite shape, schema complexity, and how migration code is organized. Shipping a one-size-fits-all reset would be wrong; shipping nothing forces every adopter to invent their own.

## The three patterns adopters use today

### 1. `DROP SCHEMA ... CASCADE` per test

```csharp
public class AuditFixture : IAsyncLifetime
{
    public TampConnection? Pg { get; private set; }
    public async Task InitializeAsync()
    {
        Pg = await AdjacentPostgres.AcquireAsync(...);
        await using var conn = new NpgsqlConnection(Pg.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DROP SCHEMA public CASCADE; CREATE SCHEMA public;";
        await cmd.ExecuteNonQueryAsync();
        // Re-run migrations against the empty schema
        await RunMigrationsAsync(Pg);
    }
}
```

| Pros | Cons |
|---|---|
| Always correct — every test sees a known-empty schema. | Slow if you have lots of objects to recreate. Re-running migrations per test adds up. |
| Easy to reason about. | Other test classes hitting the same sidecar in parallel will collide. |
| Idiomatic SQL — no clever bookkeeping. | Per-test or per-class scope (xUnit `IClassFixture<T>` recommended; see [README → xUnit fixture-lifecycle](../README.md#xunit-fixture-lifecycle-interaction)). |

### 2. Per-test unique schema name

```csharp
public class AuditFixture : IAsyncLifetime
{
    private string _schema = $"test_{Guid.NewGuid():N}";
    public string SchemaName => _schema;
    public async Task InitializeAsync()
    {
        Pg = await AdjacentPostgres.AcquireAsync(...);
        await using var conn = new NpgsqlConnection(Pg.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA {_schema};";
        await cmd.ExecuteNonQueryAsync();
        // Migrations and queries must qualify with SchemaName or set search_path.
    }
}
```

| Pros | Cons |
|---|---|
| Parallel-safe — every test class can run concurrently against one sidecar with zero collision risk. | Application code under test must accept a schema-name parameter or qualify every reference. Most apps don't. |
| Cleanup is optional (the sidecar gets dropped on next CI agent provisioning anyway). | Hard to retrofit if the app's data-access layer assumes the default schema. |
| Migrations only run once per schema, not per test. | Cumulative schema bloat over long-lived sidecars (`DROP SCHEMA` cleanup loop wanted). |

### 3. Transaction rollback per test

```csharp
public class AuditTests : IClassFixture<AuditFixture>, IAsyncLifetime
{
    private readonly AuditFixture _fixture;
    private NpgsqlTransaction? _tx;
    public async Task InitializeAsync()
    {
        _conn = new NpgsqlConnection(_fixture.Pg!.ConnectionString);
        await _conn.OpenAsync();
        _tx = await _conn.BeginTransactionAsync();
        // All test-method DML happens inside _tx. DisposeAsync rolls it back.
    }
    public async Task DisposeAsync() => await _tx!.RollbackAsync();
}
```

| Pros | Cons |
|---|---|
| Fastest — no schema work between tests, just a transaction rollback (microseconds). | Application code must accept an externally-supplied transaction. Most apps use ambient `using` blocks that open + commit their own. |
| Schema setup happens once (in the class fixture), not per test. | Doesn't reset DDL changes inside the test (a test that runs `ALTER TABLE` is hard to roll back cleanly). |
| Works well for read-heavy tests. | Doesn't help with bug classes that involve the transaction itself (commit/abort behavior, isolation levels). |

## A hypothetical `TampConnection.ResetAsync()`

If the framework shipped a reset hook, it would look something like:

```csharp
public abstract class TampConnection : IAsyncDisposable
{
    public TampConnectionMode Mode { get; }
    public string ConnectionString { get; }

    /// <summary>
    /// Reset the connected resource to a known-empty state. The implementation is per-resource:
    /// Postgres = DROP SCHEMA public CASCADE + CREATE SCHEMA public + (caller-supplied) re-run migrations.
    /// Azurite = delete all blobs in the container (or the storage account if account-scoped).
    /// Service Bus emulator = recreate every queue/topic per the topology fixture.
    /// </summary>
    public virtual Task ResetAsync(CancellationToken ct = default) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not implement ResetAsync. Implement the reset in your test fixture (see README → Schema state in adjacent mode).");
}
```

Adopter shape after:

```csharp
public class AuditFixture : IAsyncLifetime
{
    public TampConnection? Pg { get; private set; }
    public async Task InitializeAsync()
    {
        Pg = await AdjacentPostgres.AcquireAsync(...);
        await Pg.ResetAsync();           // ← the new hook
        await RunMigrationsAsync(Pg);    // adopter-supplied; not framework's concern
    }
}
```

Saves adopters one `NpgsqlConnection + DROP SCHEMA` block of plumbing per fixture. Doesn't help with patterns 2 or 3 above (which are application-design decisions, not framework concerns).

## Why we haven't shipped it

The rule: **2+ adopters must have implemented the manual version before we standardize the hook.** As of TAM-170 filing (2026-05-13):

- HoldFast — postgres adjacent mode in lab QA → uses pattern 1 (DROP SCHEMA)
- Strata — `AuditInterceptorTests` postgres → uses pattern 1 (per ticket TAM-171 friction report)

That's two adopters but with the same pattern, and pattern 1's implementation is small enough that the framework helper doesn't save much (3-4 lines of plumbing).

The framework hook becomes load-bearing when:

- A third adopter implements pattern 1 — confirms it's the dominant shape.
- One of the existing adopters tries to share a sidecar across multiple test classes that need different reset granularity (forcing them to think harder about transaction scope).
- A resource other than Postgres (Azurite blob clear, Service Bus topology recreate) lands as the second canonical reset target — then the value of *one* `ResetAsync` abstraction across resource types pays for itself.

Until then, pattern 1 stays in adopter code, and we revisit on the next ResetAsync friction signal.

## Decision

**Recorded but not shipping.** No v0.x commitment. Revisit when one of the above conditions trips.

Tracked in [YouTrack TAM-170](https://github.com/tamp-build/tamp/issues) (referenced from the [README's Schema state in adjacent mode section](../README.md#schema-state-in-adjacent-mode)).
