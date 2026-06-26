# Tasks: Runtime Project Templates (generic template-provider seam)

**Feature**: `071-runtime-project-templates` | **Tier**: 1 (two new packable public surfaces +
two new projects) | **Date**: 2026-06-26

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/provider-contract.md](./contracts/provider-contract.md),
[contracts/scaffold-manifest.schema.md](./contracts/scaffold-manifest.schema.md),
[quickstart.md](./quickstart.md)

## Conventions

- **Status**: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line).
  Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and
  document it.
- **[P]** — no dependency on another incomplete task in this phase (parallel-safe hint).
- **[US1]/[US2]/[US3]** — owning user story. Tasks with no story tag are shared/foundational.
- **Tier annotation** — the whole feature is Tier 1; no per-phase override is needed (omit `[T1]`).
- **Precedent**: this feature is the `RouteCommand` (Loop + Interpreter, pure MVU + edge) + `RouteJson`
  (pure leaf projection) split, with one addition: a `Model` module carrying the contract/manifest
  value types and the in-process provider port. Copy those projects' structure verbatim; do not invent
  new shapes. The leaf projection mirrors `RouteJson`/`EvidenceJson` (`System.Text.Json`
  `Utf8JsonWriter`, fixed field order, stable sorts, exhaustive wildcard-free token matches).
- **Deliverable boundary (research [D0](./research.md))**: this feature ships **libraries only** — the
  generic seam core + the manifest projection. There is **no** CLI subcommand and **no** `Program.fs`;
  `Scaffold.fsproj` is a **library**, not an `Exe`. Host wiring into `fsgg-sdd init` (provider-selection
  flag, lifecycle-skeleton-first ordering, exit-code mapping, manifest persistence) is **deferred** to
  the sibling `FS.GG.SDD` repo and is **out of scope here**. FR-002's "byte-identical today's lifecycle
  skeleton" is a guarantee the SDD host honours; this seam only ever *adds* runtime files.
- **MVU note (Principle IV)**: the scaffold orchestration has multi-step state + I/O
  (invoke → boundary → probe → write → record), so it **MUST** be MVU — pure `Model`/`Msg`/`Effect`,
  pure `init`/`update`, edge `Interpreter` (`Ports`/`realPorts`/`step`/`run`), mirroring `RouteCommand`
  (research [D2](./research.md)). `ScaffoldManifestJson` is a pure total projection → **no** MVU ceremony
  (adding it would violate Principle III).
- **Synthetic disclosure (Principle V, research [D8](./research.md))**: the only synthetic element is the
  deliberately out-of-scope *provider content*. Tests use a disclosed **fake** in-proc provider value
  (`// SYNTHETIC:` use-site comment + a `Synthetic` token + a PR-description note). Every filesystem
  effect is exercised against a **real** temp directory.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the two `src/` projects, two test projects, and `.sln` wiring so every later
phase has a place to land. No behavior yet.

- [X] T001 [P] Create the seam-core project `src/FS.GG.Governance.Scaffold/FS.GG.Governance.Scaffold.fsproj` (copy `src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj` but as a **library** — `OutputType=Library`, **no** `ToolCommandName`, **no** `Program.fs`): `IsPackable=true`, `PackageId=FS.GG.Governance.Scaffold`, `Compile Include` order `Model.fsi`,`Model.fs`,`Loop.fsi`,`Loop.fs`,`Interpreter.fsi`,`Interpreter.fs`, and a `ProjectReference` to **only** `FS.GG.Governance.Kernel`. System.* / FSharp.Core only; **no** new `PackageReference`; **no** git/FS-scan/FAKE/rendering reference (research [D7](./research.md)).
- [X] T002 [P] Create the projection leaf project `src/FS.GG.Governance.ScaffoldManifestJson/FS.GG.Governance.ScaffoldManifestJson.fsproj` (copy `src/FS.GG.Governance.RouteJson/FS.GG.Governance.RouteJson.fsproj`): `IsPackable=true`, `PackageId=FS.GG.Governance.ScaffoldManifestJson`, `Compile Include` order `ScaffoldManifestJson.fsi` then `ScaffoldManifestJson.fs`, and a `ProjectReference` to **only** `FS.GG.Governance.Scaffold` (for the `ScaffoldManifest` type) + the net10.0 shared-framework `System.Text.Json` — **no** command/host/Cli reference, keeping it a leaf with no cycle (research [D7](./research.md)).
- [X] T003 [P] Create the seam-core test project `tests/FS.GG.Governance.Scaffold.Tests/FS.GG.Governance.Scaffold.Tests.fsproj` + `Main.fs` (copy the `RouteCommand.Tests` Expecto entry point), referencing `FS.GG.Governance.Scaffold` (+ `Kernel` transitively). Declare `Compile` placeholders for `Support.fs`, `LoopTests.fs`, `InterpreterTests.fs`, `SurfaceDriftTests.fs`.
- [X] T004 [P] Create the projection test project `tests/FS.GG.Governance.ScaffoldManifestJson.Tests/FS.GG.Governance.ScaffoldManifestJson.Tests.fsproj` + `Main.fs` (copy `RouteJson.Tests` entry point), referencing `FS.GG.Governance.ScaffoldManifestJson` + `FS.GG.Governance.Scaffold`. Declare `Compile` placeholders for `ProjectionTests.fs`, `DeterminismTests.fs`, `SurfaceDriftTests.fs`.
- [X] T005 Add all four new projects to `FS.GG.Governance.sln` (two `src/`, two `tests/`); confirm `dotnet build -c Release FS.GG.Governance.sln` restores and the empty projects compile.

**Checkpoint**: Solution builds with four empty new projects wired in; `Scaffold` is a library (no Exe).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Freeze the two public `.fsi` surfaces (the shared contract every story drives) so no story
body can drift them. **No story work can begin until both surfaces compile and both baselines are
frozen.**

**⚠️ CRITICAL**: The `.fsi` surfaces + baselines block all three user stories.

- [X] T006 Author `src/FS.GG.Governance.Scaffold/Model.fsi` — the **complete** typed contract per [data-model.md](./data-model.md) §1–6: `ProviderId`, `ProviderContractVersion {Major;Minor}`, `ScaffoldRequest {Target;ReservedPaths}`, `EmittedFile {RelativePath;Contents}`, `ProviderEmission {Files}`, `ProviderError` (`Unresolvable`/`EmitFailed`), `TemplateProvider {Id;ContractVersion;Emit}`, `Refusal` (`ContractMismatch`/`ProviderUnavailable`/`OutOfTarget`/`Collision`/`ProviderErrored`), `ScaffoldOutcome` (`NoProvider`/`Scaffolded`/`Refused`), `GeneratedPath {RelativePath;Ownership}` + `PathOwnership = ProviderOwned`, `ScaffoldManifest {Provider;Outcome;Generated;Collisions}`. All closed DUs / immutable records; no access modifiers anywhere in the later `.fs` (Principle II). Exercise the surface in `scripts/prelude.fsx` (FSI) before any `.fs` body (Principle I).
- [X] T007 Author the MVU surfaces `src/FS.GG.Governance.Scaffold/Loop.fsi` (`Model`, `Msg`, `Effect`, `RunRequest`, `init`, `update` — the pure core that performs the version check, path-boundary check, collision decision, and manifest fold per [data-model.md](./data-model.md) §7) and `src/FS.GG.Governance.Scaffold/Interpreter.fsi` (`Ports {Invoke;Probe;Write;Out}`, `val realPorts: target:string -> Ports`, `val step: Ports -> Loop.Effect -> Loop.Msg`, `val run: Ports -> Loop.RunRequest -> Loop.Model`, per [contracts/provider-contract.md](./contracts/provider-contract.md) C4). **Pin the request shape now** (resolves analysis finding I1): `RunRequest` is the bundled `{ Request: ScaffoldRequest; Provider: TemplateProvider option }` that `run`/the interpreter take, and `init` is the 2-arg `init: ScaffoldRequest -> TemplateProvider option -> Model` that the quickstart (`Loop.init request None`) and [data-model.md](./data-model.md) §7 (`init(request, providerOpt)`) call — `run` simply destructures a `RunRequest` into `init`'s two arguments. Fix this in the `.fsi` before freezing the baseline (T009). FSI-exercise the `init`/`update`/`step` shapes before bodies (Principle I).
- [X] T008 Author `src/FS.GG.Governance.ScaffoldManifestJson/ScaffoldManifestJson.fsi` — `val schemaVersion: string` (`"fsgg.scaffold-manifest/v1"`) and `val ofManifest: ScaffoldManifest -> string`, per [contracts/scaffold-manifest.schema.md](./contracts/scaffold-manifest.schema.md). The `.fsi` is the sole surface; FSI-exercise the signature before the body.
- [X] T009 [P] Freeze both surface baselines `surface/FS.GG.Governance.Scaffold.surface.txt` and `surface/FS.GG.Governance.ScaffoldManifestJson.surface.txt` from the new `.fsi` files, using the repo's surface-extraction tooling (same generator as `surface/FS.GG.Governance.Route*.surface.txt`).

**Checkpoint**: Both `.fsi` surfaces compile, both baselines are committed. User stories can begin.

---

## Phase 3: User Story 1 — Scaffold a buildable runtime skeleton from a chosen provider (Priority: P1) 🎯 MVP

**Goal**: A selected provider's emitted runtime files are laid down (tool-side, atomically) under the
operator target alongside the host-owned lifecycle skeleton, and a deterministic manifest records every
generated path, each marked provider-owned, with the provider id + contract version — and every
tool-owned safety refusal (collision, out-of-target, provider error, write fault) leaves the target
untouched with an explicit, named diagnostic (the safety that makes the scaffold trustworthy).

**Independent Test**: `Scaffold.Interpreter.run (realPorts target) request` with a fake provider against
a fresh temp dir ⇒ outcome `Scaffolded`, emitted files exist under `target`,
`ScaffoldManifestJson.ofManifest manifest` lists every path tagged `providerOwned`; and each failure
fixture writes **zero** files with a named refusal (quickstart Scenarios 1, 4, 5).

### Tests for User Story 1 (write first; ensure they FAIL before implementation) ⚠️

- [X] T010 [P] [US1] `tests/FS.GG.Governance.Scaffold.Tests/Support.fs` — disclosed **fake** in-proc provider value(s) (`// SYNTHETIC: stands in for the out-of-scope concrete provider`) emitting a couple of target-relative files, plus `ScaffoldRequest`/`RunRequest` builders and a recording fake `Ports` (in-memory invoke/probe/write) for pure-edge assertions. Per [data-model.md](./data-model.md) §4 / contract C1.
- [X] T011 [P] [US1] `tests/FS.GG.Governance.Scaffold.Tests/LoopTests.fs` — **happy-path** pure transitions + emitted-effect assertions (Principle IV): a compatible provider ⇒ `update` emits `InvokeProvider`; an in-bounds `Ok` emission ⇒ emits `ProbeCollisions(resolved ∪ reserved)`; an empty collision set ⇒ emits `WriteAll`; a write-`Ok` ⇒ `Done(Scaffolded)` folding a `ScaffoldManifest` whose `Generated` lists **every** emitted path ascending, each `ProviderOwned`, with `Some (id,version)` (FR-005, SC-001). **No I/O performed in `update`.** Per [data-model.md](./data-model.md) §7.
- [X] T012 [P] [US1] `tests/FS.GG.Governance.Scaffold.Tests/LoopTests.fs` (failure-mode block) — **pre-write** pure refusals, each terminal, each writing nothing: an emitted `../escape.fs` / rooted path ⇒ `Refused (OutOfTarget …)` after `Emit` but before any probe (FR-009, [D5](./research.md)); a probed pre-existing/reserved path ⇒ `Refused (Collision …)` with **no** `WriteAll` emitted (FR-007, all-or-nothing); `Emit`=`EmitFailed d` ⇒ `Refused (ProviderErrored d)` (FR-008); `Emit`=`Unresolvable d` ⇒ `Refused (ProviderUnavailable d)` (FR-009); a `WriteAll` that returns `Error` ⇒ a recoverable `Refused (ProviderErrored …)` with no partial tree (SC-005). Every refusal folds a manifest with `Some (id,version)` and `Generated = []`.
- [X] T013 [P] [US1] `tests/FS.GG.Governance.Scaffold.Tests/InterpreterTests.fs` — **real temp dir** edge tests (contract C4): a successful `run` writes every emitted file under `target` and yields `Scaffolded`; a pre-existing file at an emitted path refuses with `Collision` and writes/overwrites **nothing** (FR-007); an out-of-target emission rejects with `OutOfTarget` and touches nothing; an injected `Write` port returning `Error` (and a thrown exception) leaves **zero** new files — no partial tree — and is reified to a `Msg` (the interpreter **never throws**, SC-005); re-running over an already-scaffolded target reports the existing files as `Collision` and writes nothing (edge case "re-run after a prior scaffold").
- [X] T014 [P] [US1] `tests/FS.GG.Governance.ScaffoldManifestJson.Tests/ProjectionTests.fs` — `ofManifest` for `outcome="scaffolded"`: fixed field order (`schemaVersion`,`outcome`,`refusal`,`provider`,`generated`,`collisions`), `refusal:null`, `provider={id,contractVersion:"M.m"}`, `generated[]` ascending by `path` with each `ownership:"providerOwned"` (FR-005/FR-006), `collisions:[]`; and a `Refused (Collision …)` manifest renders `outcome="refused"`, the closed `refusal` object (`reason:"collision"`,`paths`), and the `collisions` array — each refusal `reason` token exhaustive and wildcard-free. Per [contracts/scaffold-manifest.schema.md](./contracts/scaffold-manifest.schema.md).
- [X] T015 [P] [US1] `tests/FS.GG.Governance.ScaffoldManifestJson.Tests/DeterminismTests.fs` — the same fake provider over two **fresh empty** temp dirs ⇒ byte-identical `ofManifest` text; a field-exclusion sweep proves **no** absolute target path, clock, or env value reaches the output; 100% of `generated[]` paths are attributable to the manifest's `provider` id + contract version alone (SC-004, SC-006, [D6](./research.md)).

### Implementation for User Story 1

- [X] T016 [US1] Implement `src/FS.GG.Governance.Scaffold/Model.fs` — the immutable record / closed-DU bodies for every type in `Model.fsi` (no access modifiers; Principle II). Pure data only; no helpers with I/O.
- [X] T017 [US1] Implement `src/FS.GG.Governance.Scaffold/Loop.fs` — pure `init`/`update` over `Model`+`Msg` emitting `Effect` data per the [data-model.md](./data-model.md) §7 transition graph: contract-version check (`compatible(declared) ⇔ Major=1 ∧ Minor≤0`, contract C2) **before** invocation; the pure path-boundary predicate over every `RelativePath` (relative, no escaping `..`, not rooted — [D5](./research.md)); the collision decision (all-or-nothing over probed ∪ reserved); and the terminal manifest fold (`Generated` ascending+`ProviderOwned`, `Collisions` ascending). Exhaustive wildcard-free matches throughout. Makes T011/T012 pass.
- [X] T018 [US1] Implement `src/FS.GG.Governance.Scaffold/Interpreter.fs` — the edge: `Ports`, `realPorts target` (invoke the provider, `Probe` the filesystem for the existing subset, `Write` all files **atomically** via temp+rename copying `RouteCommand.Interpreter`'s discipline, `Out`), and total `step`/`run` that catch **every** port `Error` and thrown exception and reify it to the matching `Msg` — never throws, never leaves a partial tree, and re-confirms each resolved absolute path stays under `target` before writing (defence-in-depth, [D5](./research.md)). Makes T013 pass.
- [X] T019 [US1] Implement `src/FS.GG.Governance.ScaffoldManifestJson/ScaffoldManifestJson.fs` — pure total `ofManifest`: `schemaVersion` constant first, fixed field order, `outcome`/`refusal.reason`/`ownership` each rendered through an **exhaustive wildcard-free** token match, `provider` rendered `"<Major>.<Minor>"`, `generated`/`collisions`/`paths` each sorted ascending, `System.Text.Json` `Utf8JsonWriter` only. Never reads clock/env/path; never throws. Makes T014/T015 pass.
- [X] T020 [P] [US1] `tests/FS.GG.Governance.Scaffold.Tests/SurfaceDriftTests.fs` and `tests/FS.GG.Governance.ScaffoldManifestJson.Tests/SurfaceDriftTests.fs` — assert each project's extracted surface equals its frozen baseline from T009.

**Checkpoint**: A selected provider's runtime skeleton is written atomically and recorded in a
deterministic, byte-stable, provider-attributed manifest; every safety refusal leaves the target
untouched with a named diagnostic. MVP complete and independently testable.

---

## Phase 4: User Story 2 — Bring your own template provider without changing the tool (Priority: P2)

**Goal**: A second, differently-emitting provider runs through the **same** seam with no
provider-specific branch — delegation differs only in what the provider emits — and an
incompatible-contract provider is refused **before** invocation with an actionable diagnostic.

**Depends on**: US1's `Loop`/`Interpreter`/projection (T016–T019). Adds parity coverage + the
contract-version-mismatch diagnostic.

**Independent Test**: Run Scenario 1 with a *second* fake provider (different files); the manifest,
safety, and reporting rules are identical to the first. Set `ContractVersion={Major=2}` ⇒
`Refused (ContractMismatch …)`, **no** files written, an actionable diagnostic (quickstart Scenario 2,
US2 AS2/AS3).

### Tests for User Story 2 (write first; ensure they FAIL before implementation) ⚠️

- [X] T021 [P] [US2] `tests/FS.GG.Governance.Scaffold.Tests/ParityTests.fs` — two distinct fake providers (different `Id` + different emissions) each run through the **same** `run`; assert the only difference in the outcome/manifest is the emitted file set and the recorded provider id — identical safety, collision, and reporting behavior, with **no** provider-specific branch anywhere (FR-004, contract C3, US2 AS2).
- [X] T022 [P] [US2] Version block landed in `tests/FS.GG.Governance.Scaffold.Tests/ParityTests.fs` (co-located with the US2 parity tests rather than `LoopTests.fs` — same suite, same public surface): a provider declaring `{Major=2}` (and one declaring `{Major=1;Minor=1}`) ⇒ `init` yields `Done(Refused (ContractMismatch declared))` **without** emitting `InvokeProvider` and with no writes (FR-009, US2 AS3); a `{Major=1;Minor=0}` provider is accepted. The folded manifest renders `refusal.reason="contractMismatch"` with `declaredVersion` (projection assertion in `ProjectionTests.fs`).
- [-] T023 [US2] Fully delivered by T017 + T019, no separate code needed (per this task's own "mark `[-]` rather than padding" instruction): `Loop.fs`'s `ContractMismatch declared` arm carries the declared version (the supported range `1.0` is the fixed schema constant — data-model §5 / contract C2), and `ScaffoldManifestJson.fs` already renders `refusal.reason="contractMismatch"` + `declaredVersion`. Evidence: `ParityTests` (declared version carried, no invocation) + `ProjectionTests` (`declaredVersion` rendering) are green.

**Checkpoint**: US1 **and** US2 both pass independently; any conforming provider runs through the one
seam, and an incompatible contract version is refused pre-invocation with a named, actionable diagnostic.

---

## Phase 5: User Story 3 — No provider: today's behavior, unchanged (Priority: P3)

**Goal**: With no provider selected, the seam is a literal no-op — zero effects, terminal `NoProvider`,
**no** manifest written — so the host's lifecycle-skeleton output stays byte-identical (FR-002).

**Depends on**: US1's `Loop.init` (T017). Independent of US2.

**Independent Test**: `Scaffold.Loop.init request None` ⇒ zero effects, outcome `NoProvider`, no manifest
write (quickstart Scenario 3, FR-002).

### Tests for User Story 3 (write first; ensure they FAIL before implementation) ⚠️

- [X] T024 [P] [US3] `tests/FS.GG.Governance.Scaffold.Tests/NoProviderTests.fs` — `init` with `providerOpt = None` emits **zero** effects and terminates `Done(NoProvider)`; a full `run` over a real temp dir with no provider writes **no** files and produces **no** manifest (the seam never authors the lifecycle skeleton); the host's pre-existing files are untouched and byte-identical (FR-002, [D3](./research.md)). The projection's `ofManifest` for a `NoProvider` value (a totality fixture) renders `outcome="noProvider"`, `provider:null`, `generated:[]`, `collisions:[]` (schema worked example) — even though the host writes no manifest on this path.

### Implementation for User Story 3

- [-] T025 [US3] Fully delivered by T017 (per this task's own "mark `[-]` with that rationale" instruction): `Loop.fs`'s `init request None` arm returns the terminal `Done(NoProvider)` model with **zero** `Effect`s and a folded no-provider manifest value (the seam writes no manifest); `Interpreter.run` short-circuits the edge entirely (`Phase = Done` ⇒ no `step`). Evidence: `NoProviderTests` — `init None` emits zero effects, and a real-temp-dir `run` with no provider adds no files and leaves the seeded host file byte-identical (FR-002) — is green.

**Checkpoint**: All three stories independently functional; the no-provider path is a verified no-op.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Close the quickstart validation, the roadmap row, disclosure, and the full-solution gate.

- [X] T026 Validate [quickstart.md](./quickstart.md) Scenarios 1–5 through FSI (`scripts/prelude.fsx`) and the two test suites (`dotnet test -c Release tests/FS.GG.Governance.Scaffold.Tests` + `…ScaffoldManifestJson.Tests`); confirm the SC-001…SC-006 map holds (buildable-skeleton path, byte-identical no-provider, BYO-provider with zero tool change, deterministic manifest, every failure mode safe+named, 100% path attribution).
- [X] T027 [P] Flip the Phase 9 roadmap rows in `docs/initial-implementation-plan.md` ("Add project templates…" / "Optionally call a template provider…") from ⬜ to the generic-seam-core-landed state, citing `071-runtime-project-templates`, and **explicitly note** host wiring into `fsgg-sdd init` remains deferred to the sibling `FS.GG.SDD` repo (research [D0](./research.md); keep it tracked, not silently omitted — constitution intentional-deferral rule).
- [X] T028 Full-solution gate + disclosure: `dotnet build -c Release FS.GG.Governance.sln` + `dotnet test` green across all projects; both new surface-drift baselines stable; no existing baseline re-blessed; and the synthetic **fake provider** is disclosed in the PR description and at every use site (`// SYNTHETIC:` + `Synthetic` token), per Principle V / [D8](./research.md). Confirm the four "Deferred / Out of Scope" items (host wiring, provider discovery, concrete provider, out-of-process adapter) remain explicitly tracked in [plan.md](./plan.md).

---

## Dependencies & Execution Order

### Phase order

1. **Setup (Phase 1)** — no dependencies; start immediately.
2. **Foundational (Phase 2)** — depends on Setup; **blocks all stories** (freezes both `.fsi` surfaces +
   baselines).
3. **User Stories (Phases 3–5)** — all depend on Foundational. Within them:
   - **US1 (P1)** has no story dependency — the MVP; implement first. Its pure `Loop` (T017) already
     contains the full transition graph, so US2/US3 are largely test-led increments over it.
   - **US2 (P2)** depends on US1's `Loop`/`Interpreter`/projection (T016–T019); **independent of US3**.
   - **US3 (P3)** depends on US1's `Loop.init` (T017); **independent of US2**.
4. **Polish (Phase 6)** — depends on all shipped stories.

### Within each story

- Tests are written first and must FAIL before implementation (Principle V, fail-before/pass-after).
- `Model.fs` (T016) before `Loop.fs` (T017) before `Interpreter.fs` (T018); the projection `.fs` (T019)
  depends only on `Model`.
- Surface-drift tests (T020) after both `.fsi` surfaces are frozen (T006–T009).

### Parallel opportunities

- **Setup**: T001–T004 are `[P]` (different projects); T005 (sln) after them.
- **Foundational**: T009 is `[P]` once T006–T008 exist.
- **US1 tests**: T010–T015 are all `[P]` (different files) and precede T016–T019.
- **US2 tests**: T021, T022 `[P]`. **US3 tests**: T024 `[P]`.
- The two new surface-drift tests (T020) are `[P]` with each other.

---

## Task count per user story

| Story | Phase | Tasks | IDs |
|---|---|---|---|
| Shared / Setup | 1 | 5 | T001–T005 |
| Shared / Foundational | 2 | 4 | T006–T009 |
| **US1 (P1, MVP)** | 3 | 11 | T010–T020 |
| **US2 (P2)** | 4 | 3 | T021–T023 |
| **US3 (P3)** | 5 | 2 | T024–T025 |
| Polish | 6 | 3 | T026–T028 |
| **Total** | | **28** | T001–T028 |

## Suggested MVP scope

**Phases 1 → 2 → 3 (US1)**: a selected provider's runtime skeleton written atomically and tool-owned
(the provider only *describes*, never writes), recorded in a deterministic, byte-stable,
provider-attributed `scaffold-manifest`, with every safety refusal (collision, out-of-target, provider
error, write fault) leaving the target untouched under a named diagnostic. This is the entire point of
the feature — "an empty governed directory" → "a buildable, governed product." US2 (bring-your-own
provider + version negotiation) and US3 (no-provider no-op) are thin, independently-testable increments
on top of the same pure `Loop`.
