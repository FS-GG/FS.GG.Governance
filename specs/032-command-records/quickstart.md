# Quickstart: Command-Record Core (F032)

A validation/run guide for `FS.GG.Governance.CommandRecord` — the pure, total, deterministic command-record
core. It proves, end to end, that the core builds a complete typed record from supplied facts, keeps the
sensed duration apart, and projects a stable canonical identity over the reproducible facts. See
[plan.md](./plan.md), [data-model.md](./data-model.md), and [contracts/](./contracts/) for the full design;
this guide does not duplicate type bodies or test suites.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Repo builds today: `dotnet build FS.GG.Governance.sln`.
- New projects added to the solution (Phase 2 / implementation):
  - `src/FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj`
  - `tests/FS.GG.Governance.CommandRecord.Tests/FS.GG.Governance.CommandRecord.Tests.fsproj`

## 1. Design-first in FSI (Principle I)

Before any `.fs` body exists, the public surface is exercised through `scripts/prelude.fsx` (a new F032
section). Launch:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected `[F32]` lines (illustrative — full asserts live in the tests):

- Build a record from ten literal facts and read each back — all ten present, arguments in order, env delta in
  three classes.
- Two records identical except `SensedDuration` ⇒ **equal** `canonicalId`.
- Flip one reproducible fact (an argument, or the stdout digest) ⇒ **different** `canonicalId`.
- Reorder / duplicate the environment-delta entries ⇒ **unchanged** `canonicalId`.
- `NoCapturedOutput` vs `CapturedAt (CapturedOutputPath "")` ⇒ **different** `canonicalId`.

## 2. Build, with the surface baseline guarded (Principle II)

```bash
dotnet build src/FS.GG.Governance.CommandRecord/FS.GG.Governance.CommandRecord.fsproj
```

The reflective `SurfaceDrift` test compares the assembly's public surface against
`surface/FS.GG.Governance.CommandRecord.surface.txt`. To (re)bless intentionally after a deliberate surface
change:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CommandRecord.Tests/FS.GG.Governance.CommandRecord.Tests.fsproj
```

## 3. Run the semantic tests (Principle V)

```bash
dotnet test tests/FS.GG.Governance.CommandRecord.Tests/FS.GG.Governance.CommandRecord.Tests.fsproj
```

Every input is a real, literally-constructible typed value — no process is spawned and no mock is used (the
facts a host would sense are supplied as literals, exactly the core's contract). The suites map to the success
criteria:

| Suite | Proves | SC |
|---|---|---|
| `RecordTests` | all ten facts carried verbatim; env delta is a three-class partition (a change counted once); failed / timed-out / argument-less / empty-delta runs are ordinary records | SC-001, SC-002 |
| `IdentityTests` | duration reachable as sensed metadata and excluded from identity; duration-only difference ⇒ equal id; any reproducible difference ⇒ different id; captured-output presence/absence/empty are three distinct ids | SC-003, SC-004 |
| `DeterminismTests` | build + identity computed twice are equal; env-delta entry reorder/duplication leaves the id unchanged; argument reorder changes the id | SC-005 |
| `PurityTests` | record + identity identical across changed cwd, time, and unrelated filesystem/repo state | SC-006 |
| `SurfaceDriftTests` | public surface equals the committed baseline; dependency/scope hygiene (Config / BCL / FSharp.Core only) | SC-007 |

The identity laws (duration-invariance, per-field sensitivity, env-delta order/dup invariance) and totality
are FsCheck properties; field-carriage and the edge cases are example tests.

## 4. Confirm the rest of the repo is unchanged (SC-007)

```bash
dotnet build FS.GG.Governance.sln
dotnet test FS.GG.Governance.sln
```

No merged `src/`, `surface/`, or existing test project changes outcome; the new project and its tests are
purely additive.

## What this core does NOT do (by contract)

It spawns no process and captures no bytes, reads no clock/filesystem/git/environment/network, computes no
digest from raw bytes (digests are supplied), persists nothing, renders no `audit.json` or any artifact,
assembles no provenance/attestation, computes no severity/enforcement/freshness/ship verdict, and adds no CLI.
The actual command execution and sensing is a later host edge (Principle IV, the F016 git-sensing precedent);
provenance assembly is Phase-11 row 5; cross-report sensed-metadata policy is Phase-11 row 6.
