# Phase 0 Research: Runtime Project Templates

**Feature**: `071-runtime-project-templates` · **Date**: 2026-06-26

This file resolves the unknowns surfaced while filling the Technical Context and
records the design decisions that bound the plan. Two decisions were taken with
the user during planning and are recorded first because every other decision
depends on them.

## D0. Deliverable boundary — generic seam core; host wiring deferred

- **Decision**: This Governance feature delivers the **pure, generic
  template-provider seam** — a provider contract, a pure scaffold-orchestration
  MVU core, an edge interpreter over injected ports, and a deterministic
  scaffold-manifest projection — as `FS.GG.Governance.*` libraries. It does **not**
  add a CLI subcommand or wire the seam into a bootstrap host. Wiring into
  `fsgg-sdd init` (which owns the `.fsgg/`/`work/`/`readiness/` lifecycle skeleton)
  lands later in the sibling `FS.GG.SDD` repository.
- **Rationale**: On disk this repo (FS.GG.Governance) owns `route`/`ship`/`verify`/
  `evidence`/`refresh`/`release` but has **no** `init`/bootstrap command — that
  lives in `../FS.GG.SDD`. The constitution's operating rule ("generic code MUST
  NOT assume rendering's package IDs, template names, target names, or directory
  layout") makes the *seam* the natural Governance-owned unit, and the roadmap's
  consistent pattern is "pure core lands here; host wiring follows" (e.g. F041→F044,
  F047→F048). FR-002's "byte-identical *today's* lifecycle skeleton" is a guarantee
  about output owned by the SDD host, so it is honoured *by* that host, not
  re-implemented here.
- **Alternatives considered**: (a) Add a runnable `fsgg scaffold` umbrella host in
  this repo — rejected: introduces a scaffold host Governance did not previously
  own and duplicates the lifecycle-skeleton concern. (b) Implement directly in
  `../FS.GG.SDD` — rejected: the spec, branch, and `feature.json` are filed here,
  and the genericity rule that makes the seam safe is *this* repo's constitution.

## D1. Provider invocation — in-process port; the provider describes, the tool writes

- **Decision**: A provider is an **in-process F# port**: a record carrying its
  identity, its declared contract version, and an `Emit` function
  `ScaffoldRequest -> Result<ProviderEmission, ProviderError>`. Crucially the
  provider **returns declarative data** — a list of target-relative paths with
  contents — and **never touches the filesystem itself**. The tool owns every
  filesystem effect (collision probe, atomic write) and every safety decision.
- **Rationale**: Matches the codebase's injected-interpreter-port idiom
  (`RouteCommand.Interpreter.Ports`) and is fully fakeable in tests. Putting all
  writes tool-side is what makes FR-007 (no overwrite), FR-008 (provider failure
  is recoverable — a failed `Emit` writes nothing), and FR-009 (out-of-target
  rejection) *structurally* true rather than enforced by convention: a provider
  that cannot write cannot overwrite, escape the target, or leave a partial tree.
- **Alternatives considered**: out-of-process executable contract (heavier process
  edge, deferred until a real third-party ecosystem needs language independence);
  "abstract contract + both adapters" (largest surface, premature). Third-party
  providers ship as .NET assemblies implementing the port; **assembly discovery /
  loading is a host concern**, out of scope here (the core is handed an already-
  resolved provider value).

## D2. MVU boundary for the scaffold orchestration

- **Decision**: The scaffold orchestration is modelled as a pure Elmish/MVU core
  (`Loop.fs`/`.fsi`: `Model`, `Msg`, `Effect`, `init`, `update`) with an edge
  `Interpreter` (`Ports`, `realPorts`, `step`, `run`). The version check, the
  path-boundary check, the collision decision, and the manifest fold all happen in
  pure `update`; provider invocation, the collision probe, and writes are `Effect`
  data the interpreter executes.
- **Rationale**: Constitution Principle IV — the workflow has multi-step state and
  I/O (invoke → probe → decide → write → record). Modelling it as MVU makes every
  safety decision a pure, exhaustively-testable transition and keeps I/O at the
  edge. Mirrors `RouteCommand`'s `Loop`+`Interpreter` split exactly.
- **Alternatives considered**: a single impure `scaffold` function — rejected
  (Principle IV; loses the pure failure-mode coverage that SC-005 needs).

## D3. Ordering and the no-provider path

- **Decision**: The seam's `init` accepts an **optional** selected provider. With
  `None` it emits zero effects and terminates with a `NoProvider` outcome and no
  manifest — the host's lifecycle skeleton is untouched (FR-002). With `Some`
  provider the sequence is: contract-version check → invoke `Emit` → path-boundary
  check on every emitted path → collision probe → (refuse on any collision, else
  write all atomically) → fold the manifest.
- **Rationale**: The lifecycle skeleton is host-owned and authored *before* the
  seam runs (spec assumption "lifecycle skeleton is authored first"). The seam only
  ever *adds* runtime files and records what it added, so the no-provider path is a
  literal no-op.

## D4. Safety checks are pre-write and total

- **Decision**: Every failure mode is decided **before any write**: contract
  mismatch and unresolvable-provider refuse before invocation; out-of-target paths
  refuse after `Emit` but before probing; any collision refuses the whole batch
  (no partial writes). The interpreter is **total** — every port `Error` and thrown
  exception is caught and reified to a `Msg`; it never throws and never leaves a
  partial tree (mirrors `RouteCommand.Interpreter`'s temp+rename discipline).
- **Rationale**: SC-005 ("every failure mode leaves the skeleton valid, zero silent
  overwrites or partial failures"). All-or-nothing writes plus pre-write decisions
  give an inspectable, safe re-run (edge case "re-run after a prior scaffold").

## D5. Path-boundary rule

- **Decision**: An emitted path is in-bounds iff, after normalization, it is
  relative, contains no `..` segment that escapes the root, and is not rooted
  (`/...`, drive-qualified, or UNC). The check is a **pure** function over the
  declared relative path string; the interpreter additionally confirms the resolved
  absolute path stays under the target before writing (defence in depth).
- **Rationale**: FR-009 / edge case "provider emits paths outside the target". A
  pure first check keeps the decision testable without a filesystem; the edge
  re-check guards against normalization surprises on the real OS.

## D6. Deterministic manifest projection (the provenance record)

- **Decision**: The manifest is rendered by a pure leaf
  `FS.GG.Governance.ScaffoldManifestJson.ofManifest : ScaffoldManifest -> string`
  with a `schemaVersion` constant (`"fsgg.scaffold-manifest/v1"`), following the
  `EvidenceJson`/`RouteJson` precedent: `System.Text.Json` `Utf8JsonWriter`, fixed
  field order, every collection in a documented stable sort, exhaustive wildcard-
  free token matches over each closed DU. It carries the provider id + contract
  version, the outcome, the generated paths (each marked provider-owned), and the
  collisions — and **no** absolute target path, clock, environment value, or host
  reference.
- **Rationale**: FR-005, FR-010, FR-012, SC-004 (same provider + same empty target
  ⇒ byte-identical manifest), SC-006 (100% of paths attributable from the manifest).
  Excluding the absolute target path is what makes SC-004's determinism hold across
  machines and run directories.

## D7. Project shape and dependency hygiene

- **Decision**: Two new projects. `FS.GG.Governance.Scaffold` holds the contract +
  manifest value types (`Model`), the pure MVU (`Loop`), and the edge
  (`Interpreter`). `FS.GG.Governance.ScaffoldManifestJson` is a **leaf** projection
  referencing only `FS.GG.Governance.Scaffold` (for the `ScaffoldManifest` type) and
  the net10.0 shared-framework `System.Text.Json` — no host/Cli reference, so no
  cycle, mirroring the `RouteJson`/`RouteCommand` split.
- **Rationale**: Keeps the projection a pure, packable leaf consumed by later steps
  and automation; keeps the manifest type free of host coupling. No new third-party
  `PackageReference` (constitution dependency-minimalism). The core takes **no**
  filesystem-scanning, git, FAKE, or rendering dependency — assembly discovery and
  the lifecycle skeleton stay outside it.

## D8. Testing approach (real evidence)

- **Decision**: Pure `update` tests cover every transition and failure mode with a
  fake in-proc provider value; interpreter tests run against a **real temp
  directory** (write, collision-refusal, out-of-target rejection) with a fake
  provider; a determinism test asserts byte-identical manifests for the same
  provider over the same empty target; surface-drift tests pin both new `.fsi`
  surfaces. The fake provider is disclosed per Principle V (`Synthetic` token, use-
  site comment) — it stands in for the deliberately out-of-scope concrete provider.
- **Rationale**: Principle V prefers real dependencies; the only synthetic element
  is the *provider content*, which the spec explicitly defers (Out of Scope:
  "authoring any concrete runtime template content").

## Resolved unknowns

- Provider distribution/registry — **out of scope** (spec assumption; the core is
  handed a resolved provider value, see D1).
- Concrete runtime template content — **out of scope** (spec Out of Scope; tests
  use a disclosed fake, D8).
- Target framework, language, packaging, surface-baseline, and JSON conventions —
  inherited verbatim from the repo (net10.0, F# + `.fsi`, `IsPackable`,
  `surface/*.surface.txt`, `System.Text.Json`); no clarification needed.
