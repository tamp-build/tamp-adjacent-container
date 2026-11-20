# Changelog

All notable changes to **Tamp.AdjacentContainer** are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-11-20 - .NET 11 in, .NET 8 and .NET 9 out

### Changed

- **Multi-targets `net10.0;net11.0`** instead of `net8.0;net9.0;net10.0` ([ADR 0015](https://github.com/tamp-build/tamp/blob/main/docs/adr/0015-target-framework-strategy.md)). .NET 11 shipped 2026-11-10 and .NET 8 and .NET 9 reached end of support the same day; the rule is to add a TFM the day a release ships and drop one the day Microsoft ends its support. `global.json` pins SDK 11.0.100.

  **If you target `net8.0` or `net9.0`, stay on the previous version** — this one carries no assets your project can resolve.

- **`Tamp.*` pinned to 1.21.0**, which carries a SARIF fix worth knowing about if this project's output is read as evidence: before 1.21.0, a SARIF result whose scanner omitted `level` was read as `none` — the least severe value — instead of the declared `warning`, on every runtime before net11 ([tamp#166](https://github.com/tamp-build/tamp/issues/166)).

- **Publishing moved to nuget.org trusted publishing (OIDC).** The `tamp_nuget_api_key` org secret expired 2026-11-01 along with every other pre-2026-08-17 nuget.org key. The publish job now mints a short-lived key from its own OIDC token, so there is no long-lived credential to rotate or leak.

## [0.2.1] — 2026-09-27

### Added

- Package now ships XML documentation files (`.xml`) alongside the assembly, so consumers get IntelliSense and API docs. (Mirrors [tamp-build/tamp#3](https://github.com/tamp-build/tamp/pull/50).)


## [0.2.0] - 2026-05-15

### Changed (breaking)

- **Split into two NuGet packages.** `Tamp.AdjacentContainer` (this package) now
  contains only the host-mode contracts: `AdjacentContainerBuilder<TSelf>`,
  `TampConnection`, `AdjacentMode`, `TampAdjacentContainerUnavailableException`,
  and the new `TampHostConnection.FromEnvironment` helper. The concrete
  Postgres / Azurite / Service Bus emulator builders, plus the
  `TampAdjacentContainer.ForXxx()` static facade, moved to a sibling package
  **`Tamp.AdjacentContainer.Local`**.

  *Why:* the prior single-package layout dragged `Testcontainers.*` packages
  into the transitive closure of every adopter who referenced
  `Tamp.AdjacentContainer`, even Build.cs orchestration projects that only ever
  read connection-string env vars. `Testcontainers` defines a top-level
  `DotNet` namespace which **shadows** `Tamp.NetCli.V10.DotNet` in any project
  with `using Tamp.NetCli.V10;`, producing
  `error CS0234: The type or namespace name 'Restore' does not exist in the
  namespace 'DotNet'` for every call to `DotNet.Restore(...)` / `DotNet.Build(...)`.
  Adopter workaround was `using DotNetCli = Tamp.NetCli.V10.DotNet;` and renaming
  every call site. Reported by `strata-scott` during the Strata canary; the
  split is the correct framework-level fix. (TAM-XXX)

  **Adopter migration:**

  - **Test projects** (the typical local-fallback consumer): replace the single
    `<PackageReference Include="Tamp.AdjacentContainer" />` with `<PackageReference
    Include="Tamp.AdjacentContainer.Local" />`. `Tamp.AdjacentContainer.Local`
    transitively pulls `Tamp.AdjacentContainer`. **No code changes** — the
    `TampAdjacentContainer.ForPostgres()` facade and the concrete builder types
    keep their namespaces (`Tamp.AdjacentContainer.Postgres` etc.), they just
    ship from a different assembly now.

  - **Build / orchestration projects** that only ever need adjacent (env-var)
    mode: keep referencing `Tamp.AdjacentContainer` and switch from
    `TampAdjacentContainer.ForPostgres().AcquireAsync()` to
    `TampHostConnection.FromEnvironment("TAMP_PG_CONNECTION", "postgres")`. No
    Testcontainers dependency, no namespace shadowing.

### Added

- `TampHostConnection.FromEnvironment(envVarKey, resourceName)` — new host-only
  acquisition helper in the core package. Reads the env var, returns a
  `TampConnection` in `Adjacent` mode, throws
  `TampAdjacentContainerUnavailableException` (with a remediation that mentions
  the `.Local` package as the alternative) if the var is unset.

## [0.1.1] - 2026-05-13

### Added

- README: new "xUnit fixture-lifecycle interaction" section (TAM-171). Documents
  the `IAsyncLifetime` per-test-instance vs `IClassFixture<T>` class-scoped
  decision that adjacent mode forces. Includes the broken pattern, the right
  pattern, the failure-mode error message so adopters can google their way to
  this README, and one-line NUnit / MSTest analogues for cross-framework
  generalization. Patch authored by strata-scott from the Strata canary
  pilot, where the issue surfaced against a real `DROP DATABASE ... WITH (FORCE)`
  setup in Strata's Tamp.AdjacentContainer canary tests.

## [0.1.0] - 2026-05-12

### Added

- Initial release. Fixture-side dual-mode container acquisition for Postgres,
  Azurite, and Service Bus emulator. Resolves the Docker-in-Docker
  sibling-container trap by reading a standardized env var first, falling
  through to a Testcontainers local spawn, and disposing only what it owns.

- `TampAdjacentContainer.ForPostgres()` — Postgres acquisition. Default env
  var `TAMP_PG_CONNECTION`; default local-fallback image `postgres:16-alpine`.

- `TampAdjacentContainer.ForAzurite()` — Azure Storage emulator acquisition.
  Default env var `TAMP_AZURITE_CONNECTION`; default local-fallback image
  `mcr.microsoft.com/azure-storage/azurite:latest`.

- `TampAdjacentContainer.ForServiceBusEmulator()` — Service Bus emulator
  acquisition. Default env var `TAMP_SBUS_CONNECTION`; default local-fallback
  image `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`.

- `TampConnection` — `IAsyncDisposable` value-holder with `ConnectionString`
  and `Mode` (`Adjacent` or `LocalSpawned`). Disposal is a no-op for adjacent
  mode and torn down for locally-spawned; idempotent across multiple calls.

- `TampAdjacentContainerUnavailableException` — actionable diagnostic raised
  when neither the env var nor the local-fallback path succeeds. Carries
  `Resource` and `EnvVarKey` for telemetry.

- `WithEnvironmentOverride(...)` — change the env-var key the builder reads.
  `DisableLocalFallback()` — strict adjacent-only mode for CI agents.

### Notes

- Conceded after design pushback from `strata-scott` on 2026-05-12. The
  pattern (env-var probe → Testcontainers fallback → typed exception) is
  framework-level, not project-level, because every CI adopter hits the same
  Docker-in-Docker wall. Filed as TAM-XXX.

- Companion package `Tamp.AdjacentContainer.Provisioning` (CI-side
  stack-up that generates compose and exports the env vars this package
  consumes) lands in v0.2.0.
