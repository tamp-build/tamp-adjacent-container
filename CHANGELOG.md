# Changelog

All notable changes to **Tamp.AdjacentContainer** are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/spec/v2.0.0.html).

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
