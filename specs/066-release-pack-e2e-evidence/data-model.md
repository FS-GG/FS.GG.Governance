# Phase 1 Data Model — Fixture Entities

This feature adds **no product data model** — no new type, record, schema, or surface. The "entities"
here are the **test fixtures and golden data files** introduced. They consume the existing F26 / `065`
types verbatim; nothing below is a new `.fsi` declaration.

---

## 1. Real pack-boundary fixture (US1)

A self-contained, deterministic temporary tree exercised through the **real** `dotnet pack` and the wired
`065` release host. Constructed in `ReleaseCommand.Tests/Support.fs`; driven in `RealPackTests.fs`.

| Element | Shape | Notes |
|---------|-------|-------|
| Temp repo root | `string` (temp dir) | Generated per test, deleted after; never leaks into asserted output. |
| Packable projects | N × generated `net10.0` `.fsproj`/`.csproj` | Explicit literal names + `<Version>` + package metadata; each declared in `.fsgg/release.yml` `packableProjects` with a `packCommand` (`dotnet pack …`) and a `baseline`. |
| `.fsgg/release.yml` | declaration file | Parsed by the **real** `FS.GG.Governance.ReleaseDeclaration` leaf (the `065` shared leaf), unchanged. |
| Real execution port | `GateExecution.Interpreter.realPort: ExecutionPort` | Replaces the `065` disclosed-synthetic `portsWithPacks.Execute`. |
| Real `PackRead` | `SurfaceId -> ExecutionOutcome -> PackOutcome` | Locates the produced `.nupkg` under `~/.local/share/nuget-local/`, reads packed version + computes `ArtifactHash`; non-zero exit ⇒ `PackFailed` sentinel; zero-exit-no-artifact ⇒ `PackedNoArtifact`; unreadable ⇒ `PackedNoArtifact(ArtifactUnreadable …)`. Real reader, never a replay. |
| Real provenance senses | `SenseHead` / `SenseEnvironment` / `SenseBuilder` | The wired `065` normalized senses (no username/host/clock). |

**Four asserted cases** (consuming the existing `PackOutcome` / `ReleaseDecision` / `ExitCodeBasis`):

1. **Bumped** — every project packs at a bumped version ⇒ pack/version preconditions `Met`, `Exit =
   Success` (0), each project recorded as a `Pack` run, `release.json` v2 + `attestation.json` written.
2. **Failed pack** — one project's pack fails ⇒ `Blocked`, reason names the failing project, the failed
   pack recorded with its non-zero sentinel (never dropped), no fabricated pass.
3. **Unbumped / downgraded** — one project packs at ≤ its baseline ⇒ `Blocked`, reason names the project
   and the offending version.
4. **No baseline** — a packable project with no released-version baseline packs at a first version ⇒
   treated as first release (`NoBaseline`), **not** blocked as a downgrade.

**Determinism** (SC-003): re-running case 1 over unchanged inputs yields byte-identical `release.json` v2
+ `attestation.json`; the comparison excludes the sensed pack `durationNanos`.

---

## 2. Mergeable-vs-releasable fixture pair (US2)

Two product states contrasting the ship/merge boundary against the release boundary; built in
`ReleaseCommand.Tests` (the release run) and run against the real `fsgg ship` host for the ship run.

| State | `fsgg ship` | `fsgg release` | `release.json` v2 preconditions |
|-------|-------------|----------------|----------------------------------|
| Mergeable-but-not-releasable | exit 0 | exit 1 (distinct basis) | publish-plan / trusted-publishing / template-pin — one **unmet** with a named reason |
| Fully-releasable | exit 0 | exit 0 | all three **satisfied** |

Preconditions are read from the existing `ReleaseReport.PreconditionEvidence` projection (`Preconditions`
in the report; rendered as the named entries in `release.json` v2 — `pins` / `publishPlan` /
`trustedPublishing`). No new precondition type is introduced; this fixture asserts the **states** of the
existing ones (FR-004, FR-008).

---

## 3. Frozen byte-identity golden set (US3)

Four committed golden fixture files + four comparison tests (one per producing host, D3).

| Golden file | Producing command | Frozen from | Asserted in |
|-------------|-------------------|-------------|-------------|
| `RouteCommand.Tests/goldens/route.json` | `fsgg route` | pre-wiring anchor `5a0cb28` | `RouteCommand.Tests/PersistenceEdgeTests.fs` |
| `ShipCommand.Tests/goldens/ship.json` | `fsgg ship` | pre-wiring anchor `5a0cb28` | `ShipCommand.Tests/PersistenceEdgeTests.fs` |
| `VerifyCommand.Tests/goldens/verify.no-declaration.json` | `fsgg verify` (no `.fsgg/release.yml`) | pre-wiring anchor `5a0cb28` | `VerifyCommand.Tests/PersistenceEdgeTests.fs` |
| `ReleaseCommand.Tests/goldens/release.empty-v2.json` | `fsgg release` (empty additive v2 fields) | F26-blessed `fsgg.release/v2` contract | `ReleaseCommand.Tests/PersistenceEdgeTests.fs` |

Each comparison: run the **real** producing command over a fixed fixture repo → read the produced JSON →
`Expect.equal` against the committed golden bytes. A mismatch fails loudly (regression caught). The
goldens are plain JSON test data, not surface baselines; no `.fsi` or `surface/*.txt` is touched.

---

## 4. No product-surface changes

| Concern | Status |
|---------|--------|
| New `.fsi` / public surface | **None** — Tier 2, FR-007 |
| New schema / contract version | **None** — pins existing `fsgg.release/v2`, `fsgg.attestation/v1`, `fsgg.verify/v1` |
| New exit code / verdict | **None** |
| New external dependency / project | **None** |
| Surface baselines (`surface/*.txt`) | **Unchanged** |
| Edits | 4 `.Tests` projects, 4 golden files, `065/tasks.md`, `docs/initial-implementation-plan.md` |
