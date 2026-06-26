# Quickstart & Validation: Runtime Project Templates

**Feature**: `071-runtime-project-templates` · Runnable validation that the generic
template-provider seam works end-to-end. This feature ships **libraries only** (no
CLI subcommand — host wiring is deferred to `../FS.GG.SDD`, research D0), so
validation runs through FSI and the test suites, exactly the audience the
constitution's Principle I targets.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Built solution: `dotnet build -c Release FS.GG.Governance.sln`.
- New projects present in `FS.GG.Governance.sln`:
  `FS.GG.Governance.Scaffold`, `FS.GG.Governance.ScaffoldManifestJson`, and their
  `*.Tests`.

## Scenario 1 — Scaffold a runtime skeleton from a (fake) provider (P1)

Validates US1: a selected provider's runtime files are laid down alongside the
host's lifecycle skeleton, and a deterministic manifest records every path.

1. In FSI (`dotnet fsi`, loading the packed libs or `scripts/prelude.fsx`),
   construct a **fake** in-process provider that emits a couple of relative files
   (`// SYNTHETIC: stands in for the out-of-scope concrete provider`).
2. Call `Scaffold.Interpreter.run (realPorts target) request` against a fresh temp
   directory `target`, with the fake provider selected.
3. **Expect**: the terminal model's outcome is `Scaffolded`; the emitted files
   exist under `target`; `ScaffoldManifestJson.ofManifest manifest` lists every
   path, each tagged `providerOwned`, with the provider id + contract version.

See [contracts/provider-contract.md](./contracts/provider-contract.md) (C1, C4) and
[data-model.md](./data-model.md) §7.

## Scenario 2 — Bring your own provider, no tool change (P2)

Validates US2: a second fake provider with a *different* emission runs through the
**same** seam with no provider-specific branch.

- Run Scenario 1 with a second provider value. Delegation differs only in the files
  emitted; the manifest, safety, and reporting rules are identical.
- **Version mismatch**: set the provider's `ContractVersion` to `{ Major = 2 }`.
  **Expect**: outcome `Refused (ContractMismatch …)`, **no** files written, an
  actionable diagnostic (FR-009, US2 AS3).

## Scenario 3 — No provider: today's behaviour, unchanged (P3)

Validates US3 / FR-002.

- Call `Scaffold.Loop.init request None`.
- **Expect**: zero effects, terminal outcome `NoProvider`, and **no** manifest
  write — the host's lifecycle-skeleton output is untouched and byte-identical.

## Scenario 4 — Failure modes leave the target safe (SC-005)

Run each against a real temp dir and assert **zero** new/overwritten files plus an
explicit, named diagnostic:

| Case | Setup | Expected outcome |
|------|-------|------------------|
| Collision | a target file already exists at an emitted path | `Refused (Collision [..])`, nothing written (FR-007) |
| Out-of-target | provider emits `../escape.fs` or `/etc/x` | `Refused (OutOfTarget [..])`, nothing written (FR-009) |
| Provider error | fake `Emit` returns `EmitFailed` | `Refused (ProviderErrored ..)` (FR-008) |
| Unresolvable | fake `Emit` returns `Unresolvable` | `Refused (ProviderUnavailable ..)` (FR-009) |
| Write fault | inject a `Write` port returning `Error` | recoverable refusal, **no partial tree** (SC-005) |

## Scenario 5 — Deterministic manifest (SC-004, SC-006)

- Scaffold the same fake provider over two **fresh empty** temp dirs.
- **Expect**: `ofManifest` text is **byte-identical** across both runs (no absolute
  path, clock, or env leaked); 100% of `generated[]` paths attributable to the
  provider id + contract version from the manifest alone.

## Suite commands

```bash
dotnet test -c Release tests/FS.GG.Governance.Scaffold.Tests
dotnet test -c Release tests/FS.GG.Governance.ScaffoldManifestJson.Tests
```

Surface-drift tests in each suite pin the two new `.fsi` surfaces against
`surface/FS.GG.Governance.Scaffold.surface.txt` and
`surface/FS.GG.Governance.ScaffoldManifestJson.surface.txt` (Constitution II).

## Done when

- Scenarios 1–5 pass through FSI / the test suites.
- Both surface baselines are committed and their drift tests are green.
- The synthetic (fake) provider is disclosed in the PR description and at each use
  site (Constitution V).
- Deferred items (host wiring, provider discovery, concrete provider, out-of-process
  adapter) remain explicitly tracked in [plan.md](./plan.md) "Deferred / Out of Scope".
