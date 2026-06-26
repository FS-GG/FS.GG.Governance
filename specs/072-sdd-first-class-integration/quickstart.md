# Quickstart & Validation: SDD First-Class Reference Integration

**Feature**: `072-sdd-first-class-integration` · Runnable validation that the reference
template provider, the layered worked example, and the tutorials work end-to-end on top of
the **unchanged** 071 seam. Validation runs through FSI and the test suite — the audience
Principle I targets.

## Prerequisites

- .NET `net10.0` SDK (repo standard; also the toolchain the emitted skeleton builds with).
- Built solution: `dotnet build -c Release FS.GG.Governance.sln`.
- New projects present in `FS.GG.Governance.sln`:
  `samples/FS.GG.Governance.Sample.SddReferenceProvider` and its `*.Tests`.
- The 071 libraries (`FS.GG.Governance.Scaffold`, `FS.GG.Governance.ScaffoldManifestJson`)
  built and **unchanged**.

## Scenario 1 — Empty directory → buildable, governed product (US1, P1)

Validates FR-001/FR-003/FR-004 and SC-001/SC-002.

1. In FSI (`dotnet fsi`, loading the packed libs or `scripts/prelude.fsx`), open
   `FS.GG.Governance.Scaffold` and the reference provider; bind
   `let provider = SddReferenceProvider.provider`.
2. Seed a fresh temp `target` with a disclosed lifecycle-layer stand-in and build a
   `ScaffoldRequest` whose `ReservedPaths` are those lifecycle paths (research D4).
3. `Scaffold.Interpreter.run (realPorts target) { Request = req; Provider = Some provider }`.
4. **Expect**: terminal `Outcome = Scaffolded`; the `<App>.sln`, `src/<App>/…`,
   `tests/<App>.Tests/…`, `README.md` exist under `target`, each `ProviderOwned`.
5. `dotnet build <target>/<App>.sln` ⇒ exit 0, no hand-editing.

See [contracts/reference-provider.md](./contracts/reference-provider.md) R2–R3 and
[data-model.md](./data-model.md) §2.

## Scenario 2 — Clone the reference, bring your own provider (US2, P2)

Validates FR-006 and SC-004.

- Copy `SddReferenceProvider` to a minimal custom provider (change `providerId` + the
  emitted files), select it in Scenario 1's run. **Expect**: the seam resolves and invokes
  it through the **same** path; only emitted files differ — **no** edit to `Scaffold`/the
  tool.
- **Version mismatch** (FR-011): clone the provider with `ContractVersion = { Major = 2 }`.
  **Expect**: `Refused (ContractMismatch …)`, **no** files written, actionable diagnostic.

## Scenario 3 — No provider: today's behavior, unchanged (FR-010, SC-007)

- `Scaffold.Loop.init req None`. **Expect**: zero effects, terminal `NoProvider`, no manifest
  write — the lifecycle layer is untouched and byte-identical.

## Scenario 4 — Deterministic manifest golden (FR-008, SC-003, SC-005)

- Run Scenario 1 over two **fresh empty** temp dirs; project each with
  `ScaffoldManifestJson.ofManifest`.
- **Expect**: both equal `fixtures/sdd-reference/scaffold-manifest.golden.json`
  **byte-for-byte** (no absolute path/clock/env). Regenerate intentionally with
  `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests`.

## Scenario 5 — Genericity preserved (SC-006)

- Run the surface-drift test. **Expect**: the reference provider's **own** baseline matches,
  **and** `FS.GG.Governance.Scaffold.surface.txt` /
  `FS.GG.Governance.ScaffoldManifestJson.surface.txt` are **byte-identical** to their
  committed form — the generic core gained no provider knowledge.

## Scenario 6 — Tutorials are anchored, not rotting (FR-008)

- Each tutorial step under `docs/tutorials/` maps to a command/assertion the e2e test runs;
  the adopter/provider tutorials embed/link the §Scenario-4 golden. A provider/seam change
  that alters output fails Scenario 4 before the docs can drift.
- The handoff tutorial's readiness→token table matches ADR 0002 (local, the verifiable
  anchor) 1:1 (SC-008) — see [data-model.md](./data-model.md) §6. The sibling
  `017-governance-handoff` is an **external `FS.GG.SDD`-repo** cross-reference only.

## Edge cases to confirm

| Edge case | Expected |
|---|---|
| Missing .NET SDK | e2e build test **skips with a named prerequisite rationale**, not a failure (D3, Principle VI) |
| Collision in target | `Refused (Collision …)`, nothing overwritten |
| Lifecycle layer skipped | tutorial clarifies ordering; the layer is a sibling-owned precondition (`fsgg-sdd init`) |
| Empty provider output | manifest records an empty generated set; example completes cleanly |

## Suite commands

```bash
dotnet test -c Release tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests
```

## Done when

- Scenarios 1–6 pass through FSI / the test suite.
- The reference provider's additive surface baseline is committed and green; the **two core
  baselines are unchanged** (SC-006).
- The manifest golden is committed and asserted byte-for-byte (FR-008, SC-005).
- The three tutorials exist, are anchored to the executable example, and state the
  `fsgg-sdd init` boundary (FR-013).
- The disclosed lifecycle-precondition stand-in is noted at its use site and in the PR
  description (Principle V).
- Deferred items (host wiring, provider discovery, `governance-handoff.json` consumer,
  running the emitted tests) remain explicitly tracked in [plan.md](./plan.md).
