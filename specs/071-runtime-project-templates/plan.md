# Implementation Plan: Runtime Project Templates

**Branch**: `071-runtime-project-templates` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/071-runtime-project-templates/spec.md`

## Summary

Add an opt-in, pluggable **template-provider seam** so an operator can delegate
runtime-skeleton creation to a selected provider, while the lifecycle tool never
hardcodes any provider's template names, package identifiers, target names, or
directory layout. This Governance feature delivers the **generic seam core only**
— a provider contract, a pure scaffold-orchestration MVU core, an edge interpreter
over injected ports, and a deterministic scaffold-manifest projection — as
`FS.GG.Governance.*` libraries. Host wiring into `fsgg-sdd init` (which owns the
`.fsgg/`/`work/`/`readiness/` lifecycle skeleton) is **deferred** to the sibling
`FS.GG.SDD` repository (research [D0](./research.md)).

Technical approach (research [D1](./research.md)–[D8](./research.md)): a provider is
an **in-process F# port** that *describes* its output (target-relative paths +
contents) and **never writes**; the tool owns every filesystem effect and every
safety decision. The pure `update` performs the contract-version check, the
path-boundary check, the collision decision, and the manifest fold; the edge
interpreter executes provider invocation, the collision probe, and atomic writes,
and is total (never throws, never leaves a partial tree). The manifest is rendered
by a pure, deterministic leaf projection that omits any absolute path/clock/env so
the same provider over the same empty target yields a byte-identical record.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: `FS.GG.Governance.Kernel` (shared value types/JSON
helpers); net10.0 shared-framework `System.Text.Json` (`Utf8JsonWriter`) for the
manifest projection. **No** new third-party `PackageReference`; **no** filesystem-
scanning, git, FAKE, or rendering dependency in the core.

**Storage**: Files only, at the edge — the interpreter writes provider-emitted
files atomically (temp + rename) under the operator-chosen target. The pure core
and the manifest projection touch no storage.

**Testing**: Expecto (repo standard). Pure `update` transition/failure-mode tests
with a fake in-proc provider; interpreter tests against a **real** temp directory;
a manifest determinism test; surface-drift tests for both new `.fsi` surfaces.

**Target Platform**: Cross-platform .NET CLI/library (Linux/macOS/Windows).

**Project Type**: Single repository of F# library projects (no web/mobile split).

**Performance Goals**: Not a hot path. The only constraint is determinism, not
throughput: identical inputs ⇒ byte-identical manifest (SC-004).

**Constraints**: Pure core (no I/O/clock/git); total, never-throwing edge
interpreter with all-or-nothing writes (SC-005); deterministic, version-stamped
manifest carrying no absolute path/clock/env (SC-004, SC-006); the seam adds no
provider-specific knowledge to the tool (FR-003).

**Scale/Scope**: Two new `src/` projects + two new `tests/` projects; two new
surface baselines. No change to existing commands; the no-provider path is a no-op
that emits nothing (FR-002).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after
Phase 1 design — still passing; no violations, Complexity Tracking empty.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. Tasks sequence
  `.fsi` first (drafted in `scripts/prelude.fsx` FSI), semantic tests through the
  packed/public surface, then `.fs`. See [quickstart.md](./quickstart.md).
- **II. Visibility lives in `.fsi`**: PASS. Every public module
  (`Scaffold.Model`, `Scaffold.Loop`, `Scaffold.Interpreter`,
  `ScaffoldManifestJson`) ships a curated `.fsi`; `.fs` files carry no access
  modifiers; both new surfaces get a `surface/*.surface.txt` baseline + drift test.
- **III. Idiomatic Simplicity**: PASS. Records, closed DUs, pure functions,
  exhaustive wildcard-free matches. No SRTP/reflection/type-providers/custom
  operators. Provider *discovery* (which could need reflection/assembly loading) is
  a deferred host concern — the core receives a resolved provider value, so the
  core stays plain.
- **IV. Elmish/MVU boundary**: PASS. The stateful/I/O scaffold workflow is a pure
  `Model`/`Msg`/`Effect`/`init`/`update` core with an edge `Interpreter`
  (`Ports`/`realPorts`/`step`/`run`) — research [D2](./research.md).
- **V. Test Evidence Is Mandatory**: PASS. Real-filesystem interpreter tests +
  pure failure-mode tests; the only synthetic element is the deliberately
  out-of-scope provider *content*, supplied by a disclosed fake (`Synthetic` token
  + use-site comment + PR note) — research [D8](./research.md).
- **VI. Observability and Safe Failure**: PASS. Each failure mode (contract
  mismatch, unresolvable provider, out-of-target path, collision, provider error)
  is an explicit, named, actionable outcome; the interpreter fails fast and never
  swallows — it reifies every error to a `Msg`. Diagnostics distinguish a provider
  defect from a tool defect.
- **Change Classification**: **Tier 1** (new public API surface + new projects) —
  requires the full chain: spec, plan, `.fsi`, surface baselines, test evidence,
  docs. Declared accordingly.
- **Engineering Constraints**: PASS. net10.0; `.fsi` per public module; surface
  baselines; dependency-minimalism (no new third-party dep; core free of
  git/FS-scan/FAKE/rendering); `FS.GG.Governance.*` identity; pack output to
  `~/.local/share/nuget-local/`.
- **Genericity (operating rule)**: PASS — this is the feature's central design
  driver. The core hardcodes no provider names, package ids, target names,
  toolchain, or layout (FR-003); all such knowledge lives behind the provider port.

**Result**: No violations. Complexity Tracking below is intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/071-runtime-project-templates/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D0–D8
├── data-model.md        # Phase 1 — entities & state transitions
├── quickstart.md        # Phase 1 — runnable validation guide
├── contracts/           # Phase 1 — provider contract + manifest schema
│   ├── provider-contract.md
│   └── scaffold-manifest.schema.md
├── checklists/
│   └── requirements.md  # (existing) spec-quality checklist
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.Scaffold/                 # NEW — the generic seam core
│   ├── Model.fsi  / Model.fs                  #   contract + manifest value types + in-proc provider port
│   ├── Loop.fsi   / Loop.fs                   #   PURE MVU: init/update/Effect; version/boundary/collision/fold decisions
│   ├── Interpreter.fsi / Interpreter.fs       #   EDGE: Ports/realPorts/step/run — invoke, probe, atomic write (total)
│   └── FS.GG.Governance.Scaffold.fsproj
└── FS.GG.Governance.ScaffoldManifestJson/     # NEW — pure deterministic manifest projection (LEAF)
    ├── ScaffoldManifestJson.fsi / .fs         #   schemaVersion + ofManifest : ScaffoldManifest -> string
    └── FS.GG.Governance.ScaffoldManifestJson.fsproj

tests/
├── FS.GG.Governance.Scaffold.Tests/           # NEW — pure update + real-temp-dir interpreter tests
│   ├── Support.fs                             #   fake provider value(s), builders
│   ├── LoopTests.fs                           #   pure transition + every failure mode
│   ├── InterpreterTests.fs                    #   real temp dir: write, collision-refusal, out-of-target reject, no-partial
│   ├── SurfaceDriftTests.fs
│   └── Main.fs
└── FS.GG.Governance.ScaffoldManifestJson.Tests/   # NEW
    ├── ProjectionTests.fs                     #   field order, outcomes, provider-owned marking, collisions
    ├── DeterminismTests.fs                    #   byte-identical for same provider/empty target (SC-004); no abs-path/clock/env
    ├── SurfaceDriftTests.fs
    └── Main.fs

surface/
├── FS.GG.Governance.Scaffold.surface.txt              # NEW baseline
└── FS.GG.Governance.ScaffoldManifestJson.surface.txt  # NEW baseline
```

**Structure Decision**: Single-repo F# library layout (the repo's only shape). The
feature mirrors the established `RouteCommand` (Loop + Interpreter) + `RouteJson`
(leaf projection) split: the **seam core** `FS.GG.Governance.Scaffold` carries the
contract, the pure MVU, and the edge; the **leaf projection**
`FS.GG.Governance.ScaffoldManifestJson` renders the deterministic manifest and
references only `FS.GG.Governance.Scaffold` + the shared-framework
`System.Text.Json` (research [D7](./research.md)). No CLI subcommand or `Program.fs`
is added — host wiring is deferred to `../FS.GG.SDD` ([D0](./research.md)). Both new
projects are added to `FS.GG.Governance.sln`.

## Deferred / Out of Scope (tracked)

Per spec Out of Scope and research [D0](./research.md)/[D1](./research.md), these are
explicit, bounded follow-ups — **not** silent omissions (constitution: intentional
deferral MUST be explicit in the plan):

- **Host wiring** of the seam into `fsgg-sdd init` (provider selection flag,
  lifecycle-skeleton-first ordering, exit-code mapping, manifest persistence) —
  sibling `FS.GG.SDD` repo.
- **Provider discovery/resolution** (registry, assembly loading) — host concern;
  the core receives an already-resolved provider value.
- **A concrete built-in runtime provider** — separate decision; tests use a
  disclosed fake provider.
- **Out-of-process provider adapter** — only the in-process port ships now.
- **Human-readable scaffold report** (FR-010's human-facing half) — the seam ships
  only the deterministic *machine-readable* `scaffold-manifest` JSON projection
  (`ScaffoldManifestJson.ofManifest`); a human-readable rendering is a host affordance
  deferred to `FS.GG.SDD` (mirrors the cache-eligibility/evidence host `--format
  human` precedent). The seam's `Ports.Out` is the host's injection point, not a
  report renderer.
- **Manifest persistence** (FR-012's "discoverable after bootstrap") — the seam
  produces the `ScaffoldManifest` value and its JSON projection; *writing* it to a
  discoverable on-disk location (so later lifecycle/Governance steps can read
  provenance) is host wiring, deferred to `FS.GG.SDD`. The seam writes only the
  provider-emitted runtime files, never the manifest.

## Complexity Tracking

> No constitution violations. No entries.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
