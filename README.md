# Tamp.AdjacentContainer

> Fixture-side dual-mode container acquisition for Tamp adopters. Tests reach an adjacent Postgres / Azurite / Service Bus emulator via a standardized env-var contract on CI, and spawn a local Testcontainers instance on dev workstations — same connection-string shape, automatic disposal of locally-spawned containers only.

| Package | Status | What it ships |
|---|---|---|
| `Tamp.AdjacentContainer` | 0.2.0 | Host-mode contracts: `AdjacentContainerBuilder<T>`, `TampConnection`, `TampHostConnection.FromEnvironment(...)`. **No Testcontainers dep.** Pick this for build / orchestration projects that only read connection-string env vars. |
| `Tamp.AdjacentContainer.Local` | 0.2.0 | Local-fallback dual-mode: the concrete Postgres / Azurite / Service Bus emulator builders + `TampAdjacentContainer.ForXxx()` facade. Pulls Testcontainers. Pick this for test projects that spawn containers on dev workstations and consume sidecars on CI. |

> **Upgrading from 0.1.x?** See the [migration note in CHANGELOG.md](CHANGELOG.md#020---2026-05-15). Test projects switch their `<PackageReference>` to `Tamp.AdjacentContainer.Local` with no code changes ; build-side projects can drop the dep entirely if they only need env-var acquisition.

## Why this exists

You have integration tests that need a real Postgres / Azurite / Service Bus to run against. Locally on your dev machine, Testcontainers spins one up — fine. Then your CI agent runs Docker-in-Docker without a mounted host socket, the agent can't spawn sibling containers, and your test class fails at fixture init with `DockerUnavailableException` — or worse, hangs for 90 seconds before timing out.

The escape valve every adopter eventually reaches: provision a sidecar Postgres on the agent host, export a connection string via env var, refactor the test fixtures to consume that env var when present and fall back to Testcontainers when absent. The pattern is identical across every project that hits this wall.

`Tamp.AdjacentContainer` is that pattern, expressed once in the framework so every adopter doesn't get the disposal-ownership semantics subtly wrong.

This package pairs with — but does not depend on — two siblings:

- **[Tamp.Testcontainers.V4](https://github.com/tamp-build/tamp-testcontainers)** — a synchronous Docker reachability probe (`Testcontainers.Probe()`) that fails fast with an actionable diagnostic before testcontainers-dotnet hangs. Use as a `.OnlyWhen(...)` gate on a `Target IntegrationTests`.
- **Tamp.AdjacentContainer.Provisioning** *(planned)* — the CI-agent side. Generates compose, brings up the sidecar stack, exports the env vars this package reads, tears down in the build target's `Finally`. Not in 0.2.0 ; 0.2.0 was the package-split release.

## Install

Pick the package that matches the project consuming it:

```bash
# Test projects — full dual-mode (env-var probe → Testcontainers fallback).
# Transitively pulls Tamp.AdjacentContainer + Testcontainers.*.
dotnet add package Tamp.AdjacentContainer.Local

# Build / orchestration projects — host-mode only (no Docker, no Testcontainers).
# Use TampHostConnection.FromEnvironment(...) for the acquisition.
dotnet add package Tamp.AdjacentContainer
```

Both packages multi-target net8 / net9 / net10.

### Why the split?

The `Testcontainers.*` packages define a top-level `DotNet` namespace. Any
project that pulled `Tamp.AdjacentContainer` AND did `using Tamp.NetCli.V10;`
(typical Build.cs shape) hit a namespace collision:

```
error CS0234: The type or namespace name 'Restore' does not exist
              in the namespace 'DotNet'
```

…because C# resolves the `DotNet` namespace ahead of the `Tamp.NetCli.V10.DotNet`
static class. Splitting the local-fallback path into its own package keeps
build-side projects clean ; test projects opt in explicitly via
`Tamp.AdjacentContainer.Local`.

## Minimal adoption snippet

```csharp
using Tamp.AdjacentContainer;

// One handle for the entire test/build process. Disposes the docker-compose
// stack at end of build if it was launched locally; reads env vars and skips
// the stack-up step entirely in CI.
await using var pg = await TampConnection.ForPostgres(
    composeFile: RootDirectory / "docker-compose.yml",
    serviceName: "postgres");

// pg.ConnectionString now points at the live database — either the local
// container that just started, or the CI-provided TAMP_PG_CONNECTION value.
using var conn = new NpgsqlConnection(pg.ConnectionString);
await conn.OpenAsync();
```

**Same code, two environments.** Locally: `docker compose up -d postgres` runs at startup, `docker compose down` at dispose. In CI (where `TAMP_PG_CONNECTION` is preset): the env var is honored and no docker is touched. Adopters write one path; the satellite handles the dual-mode flow. Companion factories: `ForAzurite`, `ForServiceBusEmulator`.

### Host-only acquisition (`Tamp.AdjacentContainer` alone)

Build / orchestration projects that only ever run against an adjacent sidecar
(never a locally-spawned container) skip `Tamp.AdjacentContainer.Local` entirely
and use `TampHostConnection.FromEnvironment`:

```csharp
using Tamp.AdjacentContainer;

// Throws TampAdjacentContainerUnavailableException with a remediation message
// (env-var name + pointer at .Local) if TAMP_PG_CONNECTION is unset.
await using var pg = TampHostConnection.FromEnvironment(
    environmentVariableKey: "TAMP_PG_CONNECTION",
    resourceName: "postgres");

// pg.ConnectionString is the env-var value verbatim. pg.Mode == Adjacent.
// pg.DisposeAsync() is a no-op — host-only never owns the resource lifecycle.
```

No Testcontainers dependency, no Docker, no namespace shadowing in any Build.cs
that does `using Tamp.NetCli.V10;`.

## The acquisition contract

Every resource follows the same dual-mode flow:

1. Probe the resource-specific env var (default keys below; override with `.WithEnvironmentOverride(...)`).
2. If the env var is set → return a `TampConnection` with `Mode = Adjacent`. Disposal is a no-op (you don't own the resource).
3. If absent and local fallback is enabled (default) → spawn the resource via Testcontainers, return a `TampConnection` with `Mode = LocalSpawned`. Disposal stops and removes the container.
4. If neither path works → throw `TampAdjacentContainerUnavailableException` with an actionable diagnostic.

## Examples

### Postgres

```csharp
using Tamp.AdjacentContainer;

await using var pg = await TampAdjacentContainer
    .ForPostgres()
    .WithDatabase("strata_test")
    .AcquireAsync();

// pg.ConnectionString — Npgsql-compatible regardless of mode
// pg.Mode — Adjacent (env var supplied) or LocalSpawned (Testcontainers)
```

Default env var: **`TAMP_PG_CONNECTION`**. Override with `.WithEnvironmentOverride("STRATA_TEST_PG_CONNECTION")` if your pipeline already uses a different name.

Default local-fallback image: `postgres:16-alpine`. Override with `.WithLocalFallback("postgres:15-alpine")` for older schema-version testing.

### Azurite (Azure Storage emulator)

```csharp
await using var az = await TampAdjacentContainer
    .ForAzurite()
    .AcquireAsync();

var blob = new BlobServiceClient(az.ConnectionString);
```

Default env var: **`TAMP_AZURITE_CONNECTION`**. Local-fallback image: `mcr.microsoft.com/azure-storage/azurite:latest`.

### Service Bus emulator

```csharp
await using var sb = await TampAdjacentContainer
    .ForServiceBusEmulator()
    .AcquireAsync();

var client = new ServiceBusClient(sb.ConnectionString);
```

Default env var: **`TAMP_SBUS_CONNECTION`**. Local-fallback image: `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`. The builder accepts Microsoft's EULA by default — use `.WithEulaAcceptance(false)` to opt out (the spawn then fails per Microsoft's contract).

## The env-var contract

| Resource | Default env var | Connection-string shape |
|---|---|---|
| Postgres | `TAMP_PG_CONNECTION` | Npgsql (`Host=...;Database=...;Username=...;Password=...`) |
| Azurite | `TAMP_AZURITE_CONNECTION` | Azure Storage (`DefaultEndpointsProtocol=...;AccountName=...;...`) |
| Service Bus emulator | `TAMP_SBUS_CONNECTION` | Service Bus (`Endpoint=sb://...;SharedAccessKeyName=...;...`) |

Adopters pin these conventions in their CI pipeline step — the test process inherits the env vars, fixtures pick them up automatically, no per-test wiring needed.

## How configuration resolves

When `AcquireAsync()` runs, the builder resolves the connection in this order:

1. **Explicit env-var override** — if `.WithEnvironmentOverride("CUSTOM_KEY")` was chained AND `CUSTOM_KEY` is set non-empty, its value is the connection string. `Mode = Adjacent`.
2. **Default env var** — `TAMP_PG_CONNECTION` / `TAMP_AZURITE_CONNECTION` / `TAMP_SBUS_CONNECTION` per resource. If set non-empty, its value is the connection string. `Mode = Adjacent`.
3. **Local fallback** — unless `.DisableLocalFallback()` was chained, the resource is spawned via Testcontainers. `Mode = LocalSpawned`.
4. **Failure** — `TampAdjacentContainerUnavailableException` is thrown with `Resource` and `EnvVarKey` populated, and a message naming the env-var key and remediation.

**Resource-specific builder options (`WithDatabase`, `WithUsername`, `WithPassword`, `WithLocalFallback(image: ...)`) apply ONLY to the local-fallback spawn.** In adjacent mode they are ignored — the env-var-supplied connection string is authoritative end-to-end. If your env var encodes `Database=strata_dev` but you chained `.WithDatabase("strata_test")`, your tests hit `strata_dev` silently. Pin the database via the env-var value, not the builder, when running on a sidecar.

## Schema state in adjacent mode

`Tamp.AdjacentContainer` does NOT reset state between acquisitions. In `LocalSpawned` mode you get a fresh container per acquisition by construction, but in `Adjacent` mode the sidecar Postgres / Azurite / Service Bus survives across test runs and carries whatever schema and data the previous run left behind.

This is fixture-side responsibility, not the framework's. Three common patterns, picked per your tradeoffs:

| Pattern | Speed | Caveat |
|---|---|---|
| `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` per test | Slow (full schema recreate per test) | Always correct; the safe default |
| Per-test unique schema name (e.g. `test_${Guid.NewGuid():N}`) | Fast | Fixtures must scope all DDL/DML to the schema; reference-data seeders must too |
| `BEGIN; ... ROLLBACK;` per test | Fastest | DDL (CREATE TABLE, etc.) doesn't roll back in Postgres; works only for DML-only tests |

Pick once per test class and stay consistent. v1.0+ may surface a `pg.ResetAsync()` hook to standardize pattern 1, but no v0.x commitment. See [docs/reset-strategies-and-future-resetasync-hook.md](docs/reset-strategies-and-future-resetasync-hook.md) for the design exploration: full pros/cons of each pattern, the hypothetical `ResetAsync` shape, and the 2-adopter rule we're applying before standardizing.

## xUnit fixture-lifecycle interaction

Practical addendum to "Schema state in adjacent mode" for xUnit consumers. The other test frameworks have the same issue with different attribute names ; see the bottom of this section.

xUnit creates a fresh test-class instance per test method. The instinctive pattern is to implement `IAsyncLifetime` directly on the test class and put `AcquireAsync()` + destructive setup (`DROP DATABASE ... WITH (FORCE)`, schema reset, seed reload) in `InitializeAsync`. That works fine in `LocalSpawned` mode because each test gets its own freshly-spawned container ; per-test resets are part of the lifecycle by construction.

In `Adjacent` mode the resource is shared across all test-class instances. Per-test destructive setup terminates connections the *prior* test instance still has in flight. The next test fails with:

```
Npgsql.NpgsqlException: Exception while reading from stream
---- System.IO.IOException: Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.
```

It looks like a transport / pool issue, not a fixture-pattern issue. The actionable signal is buried.

**Wrong (per-test `IAsyncLifetime` with destructive setup):**

```csharp
public class AuditTests : IAsyncLifetime
{
    private TampConnection? _pg;

    public async Task InitializeAsync()
    {
        _pg = await TampAdjacentContainer.ForPostgres().AcquireAsync();
        // DROP DATABASE WITH (FORCE) per-test kills the prior instance's pooled connections.
        await CreateFreshDbAsync(_pg.ConnectionString, "audit_test");
    }
    // ...
}
```

**Right (class-scoped `IClassFixture<T>`):**

```csharp
public class AuditFixture : IAsyncLifetime
{
    public TampConnection? Pg { get; private set; }
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        Pg = await TampAdjacentContainer.ForPostgres().AcquireAsync();
        ConnectionString = await CreateFreshDbAsync(Pg.ConnectionString, "audit_test");
    }

    public async Task DisposeAsync()
    {
        if (Pg is not null) await Pg.DisposeAsync();
    }
}

public class AuditTests : IClassFixture<AuditFixture>
{
    private readonly AuditFixture _fixture;
    public AuditTests(AuditFixture fixture) { _fixture = fixture; }
    // ...
}
```

The destructive setup runs once per test class, not once per test, and never races a still-in-flight prior instance.

For tests inside a *collection* (cross-class state), use `ICollectionFixture<T>` with the same shape ; the fixture is then shared across every test class in the collection.

**Same shape, other frameworks:**

- **NUnit** ; put the destructive setup in `[OneTimeSetUp]` (class-scoped), not `[SetUp]` (per-test).
- **MSTest** ; put the destructive setup in `[ClassInitialize]`, not `[TestInitialize]`.

Per-test setup is the wrong scope for *any* destructive operation against an adjacent resource, regardless of test framework.

## CI-only enforcement: disable the local-fallback

On a CI agent, "Docker daemon unreachable → spawn fails → cascade of timeouts" is a *worse* failure mode than "env var missing → fail fast with a clear remediation message." Disable the fallback on CI:

```csharp
await using var pg = await TampAdjacentContainer
    .ForPostgres()
    .DisableLocalFallback()  // Adjacent mode only — no Docker spawn attempts
    .AcquireAsync();
```

When the env var is absent in this mode, the builder throws immediately with the env-var key, resource name, and remediation text in the exception message.

## Disposal semantics

`TampConnection` is `IAsyncDisposable`. Tests pair it with `await using` so resources tear down at scope exit:

- **`Adjacent` mode** — `DisposeAsync` is a no-op. You never tear down resources you didn't provision.
- **`LocalSpawned` mode** — `DisposeAsync` stops and removes the Testcontainers instance.

`DisposeAsync` is idempotent. Multiple calls are safe.

## Errors

| Exception | When | Caller action |
|---|---|---|
| `TampAdjacentContainerUnavailableException` | env var absent AND (fallback disabled OR Docker unreachable) | Read the message — it contains the env-var key and resource name. Wire the env var in your pipeline, or make Docker reachable, or fix the fallback toggle. |
| `ArgumentException` | empty / null env-var key passed to `WithEnvironmentOverride(...)` | Pass a real key. |

## What this package is not

- **It is not a Postgres / Azurite / Service Bus client.** Bring your own (`Npgsql`, `Azure.Storage.Blobs`, `Azure.Messaging.ServiceBus`) and pass the connection string in.
- **It does not run schema migrations or seed data.** That's project-side. The connection is ready to use; what you put in it is your job.
- **It does not handle test isolation** (per-test transactions, snapshot/restore, per-class containers). That's xUnit / NUnit / collection-fixture territory.
- **It does not provision the sidecar Postgres on your CI agent.** That's `Tamp.AdjacentContainer.Provisioning` (v0.2.0). Today you write a compose step in your pipeline that exports the env var; this package does the consumption half.

## Companion: `Tamp.Testcontainers.V4`

When the local-fallback path is enabled and Docker is unreachable, the spawn fails with whatever message testcontainers-dotnet produced — usually a 30-second timeout. Add `Tamp.Testcontainers.V4`'s probe as a `Target` gate to fail at build-target boundaries instead of fixture-init boundaries:

```csharp
Target IntegrationTests => _ => _
    .OnlyWhen(() => Testcontainers.Probe().IsAvailable,
              "Docker unreachable — skipping integration tests on this agent.")
    .DependsOn(Compile)
    .Executes(() => DotNet.Test(...));
```

The probe and `Tamp.AdjacentContainer` are independent — use either, both, or neither depending on your CI shape.

## Releasing

Releases follow the [Tamp dogfood pattern](MAINTAINERS.md): bump `<Version>` in `Directory.Build.props`, tag `v<X.Y.Z>`, GitHub Actions runs `dotnet tamp Ci` then `dotnet tamp Push`.

## License

MIT. See [LICENSE](LICENSE).
