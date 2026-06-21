---
description: "Task list for F023 - 023-enforcement-effective-severity: the first Phase-5 pure core — the typed enforcement vocabulary (six-value `RunMode`, four-value `Profile`, base/effective `Severity`, `Recognized<'T>`, `EnforcementInput`/`EnforcementDecision`) plus the single pure, total `deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision` that maps (base severity, maturity, run mode, profile) to (effective severity, reason). REUSES F014 `Config.Model.Maturity`/`ProfileId` verbatim; takes ONE inward reference (Config); adds NO new third-party dependency. TOTAL over the full cross-product, DETERMINISTIC (byte-identical for identical input), echoes base severity unchanged, never drops a finding, never escalates base-advisory. Computes NO ship/merge verdict, blockers, exit code, or cross-finding rollup; does NO I/O, NO `.fsgg/policy.yml` parsing, NO per-class dial map; emits NO CLI/route.json/audit.json."
---

# Tasks: Enforcement Levers and Effective Severity

**Feature branch**: `023-enforcement-effective-severity` (active spec; git branch currently `main`)
**Spec**: [`specs/023-enforcement-effective-severity/spec.md`](./spec.md)
**Plan**: [`specs/023-enforcement-effective-severity/plan.md`](./plan.md)

**Input**: Design documents from `/specs/023-enforcement-effective-severity/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/Enforcement.fsi](./contracts/Enforcement.fsi), [contracts/enforcement-decision.md](./contracts/enforcement-decision.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. This is a **Tier 1** feature (new public, packable surface; new public `.fsi`; new surface baseline). Credible evidence is **public-surface** testing only: `deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile`, `profileToProfileId`, `profileOfProfileId`, and `runModeOrdinal` exercised through the packed library over **real typed lever values** (the four lever DUs + the reused F014 `Maturity`/`ProfileId`), never private helpers and never mocks (Principle V). The hidden `maturityFloor`/`profileTighten` maps and the four reason builders are tested **only through** `deriveEffectiveSeverity`. Every input is a real, enumerable typed value — **no synthetic evidence is anticipated** (the full input domain is the finite cross-product of base severity × maturity × run mode × profile, all literally constructible); if any `Synthetic`-tokened test ever appears it carries a use-site `// SYNTHETIC:` disclosure and is listed in the PR.

**Tier**: the whole feature is **Tier 1** (plan Constitution Check). Every task matches the feature tier; no per-task `[T1]`/`[T2]` annotations needed. **No existing project's public surface is touched** — `FS.GG.Governance.Config` is referenced as-is for `Maturity`/`ProfileId`; the only new baseline is `surface/FS.GG.Governance.Enforcement.surface.txt`.

**Elmish/MVU (Principle IV)**: **NOT APPLICABLE** — this feature is a pure, total, side-effect-free value-to-value computation (FR-005, FR-014): no I/O, no git sensing, no clock, no multi-step state, no retries, no effect. It is exactly the "single pure function" case Principle IV exempts from MVU ceremony (plan Constitution Check; the same call F015/F017/F018/F019/F021 made). The boundary is the pure module `Enforcement` — closed DUs, two records, `deriveEffectiveSeverity` + the recognition/mapping helpers — no `Model`/`Msg`/`Effect`/`update`/interpreter.

**Determinism minimums (FR-006, SC-004)**: the derivation reads only the four typed lever inputs — no clock, environment, ordering, or host-path value enters it (none exist on `EnforcementInput`). `runModeOrdinal`, the hidden `maturityFloor`/`profileTighten` maps, the `clamp`, and the four reason builders are all total `match`/arithmetic over closed inputs. Consequence: identical inputs always yield byte-identical `EffectiveSeverity` **and** byte-identical `Reason` text (SC-004), proven by evaluating each input twice and comparing.

**Carry/exclusion minimums (FR-009, FR-012, FR-013, SC-003, SC-006)**: `EnforcementDecision` echoes `BaseSeverity`/`Maturity`/`Mode`/`Profile` **byte-identical** from the input (only `EffectiveSeverity` and `Reason` are derived). The profile may relax or tighten effective enforcement but NEVER changes the reported base severity, the maturity, or the mode (FR-009). Reclassification is not suppression: mapping the derivation over an N-finding list yields exactly N decisions (FR-012, SC-006) — no finding dropped. The surface exposes **no** rollup/ship-verdict/blockers/exit-code member (FR-013) and **no** I/O/CLI member (FR-014) — asserted by the surface baseline.

**Totality minimums (FR-005, FR-007, FR-008, SC-001)**: `deriveEffectiveSeverity` pattern-matches only closed DUs (the six `RunMode`, four `Profile`, two `Severity`, five `Maturity`) exhaustively — **no wildcard** that would hide a future case — unwraps single-case newtypes, and does no partial function, division, parse, or I/O, so it cannot throw for any well-typed `EnforcementInput`. `Observe`/`Warn` always derive `Advisory` regardless of mode/profile (FR-007); a base-blocking finding blocks iff `runModeOrdinal mode ≥ effectiveFloor` (FR-008); a base-advisory finding always stays `Advisory` (this core never escalates — research D4). Every result carries a non-empty `Reason` (FR-010).

**Scope-guard minimums (FR-013/FR-014/FR-015)**: pure decision only — **no** ship/merge verdict, blockers list, exit code, or cross-finding rollup (FR-013); **no** I/O, **no** `.fsgg/policy.yml` parsing, **no** artifact persistence, **no** CLI (FR-014); **no** per-class profile dial map (`unknownPaths`/`staleEvidence`/…) — the four canonical profiles carry strictness intrinsically; the dial layer is deferred (FR-015). The recognition of an unknown lever string is a total `Unrecognized` value carrying the offending string — never an exception, never a silent default (FR-011). The library takes **one** inward reference (`FS.GG.Governance.Config`) and adds **no** new third-party `PackageReference` (`System.*`/FSharp.Core only — the derivation needs no serialization).

## Status Legend

- `[ ]` pending
- `[X]` done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build — narrow the scope and document it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase.
- **[Story]**: `[US1]`..`[US3]`; omitted for setup/foundation/polish.
- Every task names an exact file path.

---

## Phase 1: Setup

**Purpose**: stand up the new optional pure-leaf library `FS.GG.Governance.Enforcement`, its test project, the public contract (copied verbatim), the enumerated lever domains + FsCheck generators, the prelude sketch, and the readiness note. **No new third-party dependency** — the library references **only** `FS.GG.Governance.Config` (F014, for `Maturity`/`ProfileId`); its own code is `System.*` + FSharp.Core (no serialization).

- [X] T001 Create `src/FS.GG.Governance.Enforcement/FS.GG.Governance.Enforcement.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Governance.Enforcement`, `RootNamespace=FS.GG.Governance.Enforcement`, with exactly **one** `<ProjectReference>` — `../FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj` — and **no** `<PackageReference>` (research D1; the derivation needs no `System.Text.Json`). Compile order: `Enforcement.fsi` → `Enforcement.fs`. Add an fsproj header comment (mirroring the F021 fsproj) noting this is the first Phase-5 pure core — the pure, total derivation of a finding's effective severity from its base severity, rule maturity, run mode, and profile; a pure leaf (`Enforcement → Config`), reaching no git/filesystem/clock and adding no dependency.
- [X] T002 Copy `specs/023-enforcement-effective-severity/contracts/Enforcement.fsi` → `src/FS.GG.Governance.Enforcement/Enforcement.fsi` verbatim as the curated public surface (Principle II — this `.fsi` is the SOLE public surface: the four lever/severity DUs, `Recognized<'T>`, `EnforcementInput`/`EnforcementDecision`, `runModeOrdinal`, `profileToProfileId`/`profileOfProfileId`, `recognizeMode`/`recognizeProfile`, `deriveEffectiveSeverity`; the matching `Enforcement.fs` carries no top-level access modifiers and keeps the `maturityFloor`/`profileTighten` maps and the four reason builders hidden — absent from the `.fsi`, the `Kernel/Json.fs` + `GatesJson.fs` precedent).
- [X] T003 Add `failwith "F023"` stub bodies in `src/FS.GG.Governance.Enforcement/Enforcement.fs` for every `val` in the contract (`runModeOrdinal`, `profileToProfileId`, `profileOfProfileId`, `recognizeMode`, `recognizeProfile`, `deriveEffectiveSeverity`) plus the DU/record definitions, so the library compiles against the contract before any real derivation logic lands (Principle I).
- [X] T004 Create `tests/FS.GG.Governance.Enforcement.Tests/FS.GG.Governance.Enforcement.Tests.fsproj` with centrally pinned Expecto/Expecto.FsCheck/FsCheck/YoloDev.Expecto.TestSdk packages (from `Directory.Packages.props`), `IsPackable=false`, `GenerateProgramFile=false`, and `ProjectReference`s to `src/FS.GG.Governance.Enforcement` and `src/FS.GG.Governance.Config` (the tests construct real typed lever values and the reused F014 `Maturity`/`ProfileId`, and call the public surface directly).
- [X] T005 [P] Add empty Expecto test modules in compile order in `tests/FS.GG.Governance.Enforcement.Tests/`: `Support.fs`, `DerivationTests.fs`, `RecognitionTests.fs`, `CarryTests.fs`, `DeterminismTests.fs`, `TotalityTests.fs`, `SurfaceDriftTests.fs`, `Main.fs` (Main runs the assembly).
- [X] T006 Add `src/FS.GG.Governance.Enforcement` and `tests/FS.GG.Governance.Enforcement.Tests` to `FS.GG.Governance.sln`.
- [X] T007 [P] Implement the enumerated lever domains + FsCheck generators in `tests/FS.GG.Governance.Enforcement.Tests/Support.fs` over **real** typed values (no mocks): (a) the four total domain lists — `allModes` (the six `RunMode`), `allProfiles` (the four `Profile`), `allSeverities` (`Advisory`/`Blocking`), `allMaturities` (the five F014 `Maturity`); (b) `allInputs : EnforcementInput list` = the full cross-product (2 × 5 × 6 × 4 = 240 inputs) used to drive enumeration-based totality/determinism/carry tests; (c) FsCheck generators/arbitraries over each lever domain and over `EnforcementInput`, all drawing from the finite enumerations (so every generated input is a real, constructible lever value); (d) the canonical token tables for recognition assertions — the six mode tokens (`sandbox`..`release`), the four profile tokens (`light`..`release`), and a representative invalid set (`"Gate"`, `"ship"`, `""`, `"  inner "`, `"lite"`, `"normal"`) per [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §3. These produce REAL inputs, never fakes.
- [X] T008 [P] Extend `scripts/prelude.fsx` with an F023 design sketch that `#r`s the built `FS.GG.Governance.Enforcement` assembly, opens `FS.GG.Governance.Config.Model` + `FS.GG.Governance.Enforcement`, and exercises the [quickstart.md](./quickstart.md) §FSI smoke: the worked example (`{ BaseSeverity = Blocking; Maturity = BlockOnShip; Mode = Inner; Profile = Light }` ⇒ `Advisory`, with the exact reason naming run mode + profile), the same finding at `Gate` ⇒ `Blocking`, the `Observe` withhold case under `Release`/`Release` ⇒ `Advisory`, a determinism check (derive twice, assert byte-identical), and the recognition cases (`recognizeMode "gate"` ⇒ `Recognized Gate`; `recognizeMode "ship"` ⇒ `Unrecognized "ship"`; `recognizeProfile "strict"` ⇒ `Recognized Strict`). **Design-first, like F021's prelude sketch**: written here as the design record, it will *throw at runtime* while the surface is the `failwith "F023"` stub (T003) and only runs green once Foundation/US1 land; T025 re-runs it end-to-end against the real body.
- [X] T009 [P] Create `specs/023-enforcement-effective-severity/readiness/README.md` listing the required FSI transcripts (the worked example showing `Advisory` + the exact reason; the same finding at `gate`/`release` showing `Blocking`; an `observe`/`warn` withhold transcript; a twice-identical determinism run; the canonical recognition set + a representative `Unrecognized`) and an SC-traceability note mapping SC-001…SC-006 to the test files that prove them (per [quickstart.md](./quickstart.md) acceptance→evidence map).

**Checkpoint**: `dotnet build src/FS.GG.Governance.Enforcement` and `dotnet test tests/FS.GG.Governance.Enforcement.Tests` compile against the stub; the solution lists the two new projects; the single `Config` reference resolves; `Support.fs` enumerates the full 240-input cross-product and exposes generators over real lever values.

---

## Phase 2: Foundation (Blocking Prerequisites)

**Purpose**: the shared enforcement vocabulary every story specializes — the intrinsic run-mode ordinal, the `Profile`↔`ProfileId` mapping, and the **hidden** maturity-floor / profile-tighten maps the derivation reduces against. **No user-story work begins until this phase is complete.**

- [X] T010 Implement `runModeOrdinal : RunMode -> int` in `src/FS.GG.Governance.Enforcement/Enforcement.fs` as the total ordinal map `Sandbox`→0, `Inner`→1, `Focused`→2, `Verify`→3, `Gate`→4, `Release`→5 ([data-model.md](./data-model.md) ordinal table, research D3) — an **exhaustive** `match` over the closed `RunMode` with **no wildcard** (a future mode is a compile error here). Exposed because the ordering IS the enforcement semantics.
- [X] T011 Implement `profileToProfileId : Profile -> ProfileId` and `profileOfProfileId : ProfileId -> Recognized<Profile>` in `src/FS.GG.Governance.Enforcement/Enforcement.fs`: the canonical tokens `Light`↔`"light"`, `Standard`↔`"standard"`, `Strict`↔`"strict"`, `Release`↔`"release"` (FR-003, [data-model.md](./data-model.md)). `profileOfProfileId` is **total** — a non-canonical id yields `Unrecognized` carrying the id's string (FR-011, [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §3), never an exception or default. Unwrap the `ProfileId` newtype at the use site.
- [X] T012 Implement the **hidden** floor/tighten maps in `src/FS.GG.Governance.Enforcement/Enforcement.fs` (absent from `Enforcement.fsi`, mirroring the hidden token helpers in `Kernel/Json.fs`/`GatesJson.fs`): `maturityFloor : Maturity -> int option` (`Observe`/`Warn`→`None`; `BlockOnPr`→`Some 4`; `BlockOnShip`→`Some 4`; `BlockOnRelease`→`Some 5` — research D3, the `block-on-pr`/`block-on-ship` coincidence at the `gate` floor deliberate in this Governance-only slice) and `profileTighten : Profile -> int` (`Light`→0; `Standard`→0; `Strict`→1; `Release`→2 — research D4). Each `match` is **exhaustive over the closed DU with no wildcard**, so a future maturity/profile case is a compile error here, never a silently mis-floored finding.

**Checkpoint**: the library builds with the real `runModeOrdinal`, the `Profile`↔`ProfileId` mapping, and the hidden floor/tighten maps; `deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile` are still `failwith "F023"` stubs (filled by the stories); the surface compiles against `Enforcement.fsi`.

---

## Phase 3: User Story 1 - Derive a finding's effective severity and explain it (Priority: P1) 🎯 MVP

**Goal**: `deriveEffectiveSeverity` maps `(base severity, maturity, run mode, profile)` to a `(effective severity, reason)` decision over the **complete** cross-product, never throwing: `Observe`/`Warn` always advisory (FR-007); a base-blocking finding blocks iff the run mode reaches the profile-adjusted maturity floor (FR-008); a base-advisory finding stays advisory (research D4); every result carries all six fields with a non-empty, lever-naming reason (FR-010); identical inputs are byte-identical (SC-004). This is the feature's reason to exist — the MVP.

**Independent Test**: derive the worked example (`Blocking`, `BlockOnShip`, `Inner`, `Light`) and assert `Advisory` + the exact reason naming the run mode and profile; derive the same finding at `Gate`/`Release` and assert `Blocking`; sweep `Observe`/`Warn` × every mode × profile and assert all `Advisory`; enumerate the full 240-input cross-product and assert each returns a defined decision (six fields, non-empty reason) without throwing; derive each input twice and assert byte-identical effective severity + reason.

### Tests for User Story 1 (write first; must FAIL before implementation)

- [X] T013 [P] [US1] In `tests/FS.GG.Governance.Enforcement.Tests/DerivationTests.fs`, add the derivation tests over real typed lever inputs: (1) the **worked example** — `{ Blocking; BlockOnShip; Inner; Light }` ⇒ `EffectiveSeverity = Advisory`, `BaseSeverity = Blocking`, and `Reason` exactly the relaxed sentence naming the `'light'` profile, the `'block-on-ship'` maturity, the `'gate'` boundary, and the `'inner'` run mode (US1 AS1, **SC-002**, [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §1/§2); (2) the same finding under `Gate` **and** under `Release` ⇒ `Blocking`, with the blocking-reason sentence naming the boundary reached (US1 AS2, FR-008); (3) **withhold** — `Observe` and `Warn` × every `RunMode` × every `Profile` (base `Blocking`) all ⇒ `Advisory` with the withhold-reason sentence (US1 AS3, FR-007); (4) **boundary** — for each `(maturity ∈ {BlockOnPr; BlockOnShip; BlockOnRelease}, profile)` cell, assert `Blocking` exactly when `runModeOrdinal mode ≥ clamp(maturityFloor − profileTighten, 0, 5)` and `Advisory` below it, matching the full base-blocking truth table in [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §1 (FR-008); (5) **base-advisory** — base `Advisory` × every maturity/mode/profile ⇒ `Advisory` with the base-advisory reason ("does not escalate it; per-class strictness dials deferred"), proving this core never escalates (research D4, US3 AS3 reason branch); (6) every reason is one of the four fixed sentence shapes and is **non-empty** (FR-010, [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §2).
- [X] T014 [P] [US1] In `tests/FS.GG.Governance.Enforcement.Tests/TotalityTests.fs`, add the **totality** coverage: (1) an enumeration test mapping `deriveEffectiveSeverity` over `Support.allInputs` (the full 2 × 5 × 6 × 4 = 240 cross-product) asserting each returns a value and **none throws** (US1 AS4, **SC-001**); (2) an FsCheck **totality** property over the `EnforcementInput` generator asserting `deriveEffectiveSeverity` always returns a decision and never throws (**SC-001**); (3) every decision carries all six fields populated and a **non-empty** `Reason` (US1 AS4, FR-010). All inputs are real enumerated lever values — **no synthetic evidence** (the domain is finite and literally constructible).
- [X] T015 [P] [US1] In `tests/FS.GG.Governance.Enforcement.Tests/DeterminismTests.fs`, add the **determinism** coverage: for every input in `Support.allInputs` (and via an FsCheck property over the generator), derive **twice** and assert the two decisions are byte-identical — equal `EffectiveSeverity` **and** equal `Reason` string (FR-006, **SC-004**). The derivation reads no clock/environment/ordering/host-path value (none exist on `EnforcementInput`), so this must hold for 100% of inputs.

### Implementation for User Story 1

- [X] T016 [US1] Implement `deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision` in `src/FS.GG.Governance.Enforcement/Enforcement.fs` ([data-model.md](./data-model.md) branch order, first-match-wins): (1) **withhold** — `Maturity ∈ {Observe; Warn}` ⇒ `EffectiveSeverity = Advisory` (overrides mode and profile, FR-007); (2) **base-advisory** — `BaseSeverity = Advisory` ⇒ `EffectiveSeverity = Advisory` (never escalates, research D4); (3) **blocking-eligible** — `BaseSeverity = Blocking` and `maturityFloor` is `Some f`: compute `effectiveFloor = clamp(f − profileTighten Profile, 0, 5)` and emit `Blocking` iff `runModeOrdinal Mode ≥ effectiveFloor`, else `Advisory` (FR-008). Build the `EnforcementDecision` echoing `BaseSeverity`/`Maturity`/`Mode`/`Profile` **byte-identical** from the input (FR-009) with `EffectiveSeverity` derived and `Reason` filled by T017. **Exhaustive** closed-DU matches, no wildcard, no partial function/parse/division/I/O — total, never throws (FR-005). (Depends on T010–T012.)
- [X] T017 [US1] Implement the four **hidden** reason builders in `src/FS.GG.Governance.Enforcement/Enforcement.fs` (absent from `Enforcement.fsi`) — `withhold`, `base-advisory`, `blocking`, `relaxed` — each the one fixed sentence in [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §2, interpolating **only** the lower-case canonical tokens of the typed inputs (`<m>` maturity, `<mode>` run mode, `<profile>` profile, `<floor-mode>` = the run-mode token at `effectiveFloor`). Wire each into the matching branch of `deriveEffectiveSeverity` so every result carries a deterministic, non-empty reason naming the responsible levers (FR-010). No clock, host path, or environment value enters any reason (SC-004). Use `runModeOrdinal`'s inverse (a total `match` from the clamped ordinal back to the boundary `RunMode` token) for `<floor-mode>`.

**Checkpoint**: the worked example reproduces exactly (blocking→advisory + the lever-naming reason), the same finding blocks at `gate`/`release`, `observe`/`warn` withhold under any mode/profile, the full 240-input cross-product is total and deterministic with non-empty reasons, and every base/effective pair traces to the truth table — the MVP. US1 stands alone.

---

## Phase 4: User Story 2 - Name the canonical enforcement vocabulary as closed, total values (Priority: P2)

**Goal**: a later host edge and the policy loader can turn caller/file-supplied strings into the typed levers, and reject anything outside the canonical sets without crashing: every canonical mode/profile name recognizes to its typed value; any other string yields a total `Unrecognized` carrying the offending value; the recognized sets are **exactly** the six modes and four profiles (FR-011, SC-005).

**Independent Test**: recognize each of the six canonical mode tokens and four profile tokens to its typed value; recognize a representative invalid set (`"Gate"`, `"ship"`, `""`, `"  inner "`, `"lite"`, `"normal"`) to `Unrecognized "<input>"`; assert exact-token matching (no trim, no case-fold, no default); assert the recognized set sizes are exactly 6 and 4; recognize `profileOfProfileId (ProfileId "strict")` ⇒ `Recognized Strict` and `(ProfileId "experimental")` ⇒ `Unrecognized "experimental"`.

### Tests for User Story 2 (write first; must FAIL before implementation)

- [X] T018 [P] [US2] In `tests/FS.GG.Governance.Enforcement.Tests/RecognitionTests.fs`, add the recognition tests over `Support`'s canonical/invalid token tables: (1) each canonical run-mode token (`sandbox`/`inner`/`focused`/`verify`/`gate`/`release`) ⇒ `Recognized` of the corresponding `RunMode` (US2 AS1, **SC-005**); (2) each canonical profile token (`light`/`standard`/`strict`/`release`) ⇒ `Recognized` of the corresponding `Profile` (US2 AS2); (3) every representative invalid string (`"Gate"` case-variant, `"ship"`, `""`, `"  inner "` whitespace, `"lite"`, `"normal"`) ⇒ `Unrecognized "<exact input>"` — exact-token match, **no** trim, **no** case-fold, **no** silent default, never an exception (US2 AS3, FR-011, [contracts/enforcement-decision.md](./contracts/enforcement-decision.md) §3); (4) the recognized sets are **exactly** the six modes and four profiles — recognizing every canonical token and round-tripping `profileToProfileId`→`profileOfProfileId` covers each case once, no more, no fewer (SC-005); (5) `profileOfProfileId` recognizes the four canonical `ProfileId`s and yields `Unrecognized` (carrying the string) for a non-canonical id.
- [X] T019 [P] [US2] In the same file, add a **round-trip** test: for every `Profile`, `profileOfProfileId (profileToProfileId p) = Recognized p` (the mapping is a total bijection over the four canonical profiles); and assert `profileToProfileId` emits exactly the four canonical tokens (FR-003).

### Implementation for User Story 2

- [X] T020 [US2] Implement `recognizeMode : string -> Recognized<RunMode>` and `recognizeProfile : string -> Recognized<Profile>` in `src/FS.GG.Governance.Enforcement/Enforcement.fs`: an **exact-token** `match` mapping each canonical token to its typed value and **every** other string to `Unrecognized` carrying the input verbatim (FR-011) — no `Trim`, no `ToLower`/case-fold, no default fallthrough, never an exception. Total over all strings. (`profileOfProfileId` from T011 already provides the `ProfileId`-keyed recognition.)

**Checkpoint**: every canonical mode/profile name recognizes to its typed value, every non-canonical string yields a total `Unrecognized` with the offending value, and the recognized sets are exactly six modes + four profiles — the shared dictionary US1's computation and the later CLI both depend on. US2 is independently testable.

---

## Phase 5: User Story 3 - Guarantee profiles explain enforcement without hiding truth (Priority: P3)

**Goal**: turning a profile up or down only ever changes *enforcement* — it can never erase a verdict, rewrite base severity, or drop a finding. The output's base severity is byte-identical to the input's across the full sweep (FR-009, SC-003); the most-relaxed and strictest profiles report the same base severity/maturity/mode and differ only in effective severity + reason (US3 AS2); mapping the derivation over N findings yields N decisions — no drop (FR-012, SC-006).

**Independent Test**: across the full 240-input sweep, assert `decision.BaseSeverity = input.BaseSeverity` and `decision.Maturity/Mode/Profile = input.*` for 100% of inputs; for a fixed finding, derive under `Light` (most relaxed) and `Release` (strictest profile) and assert both report the same base severity + maturity + mode, differing only in `EffectiveSeverity`/`Reason`; map the derivation over an N-element finding list and assert the output count equals N.

### Tests for User Story 3 (write first; must FAIL before implementation)

- [X] T021 [P] [US3] In `tests/FS.GG.Governance.Enforcement.Tests/CarryTests.fs`, add the carry/no-drop tests over real fixtures: (1) an FsCheck property (and an enumeration over `Support.allInputs`) asserting `decision.BaseSeverity = input.BaseSeverity` for 100% of inputs — profiles never alter base severity (US3 AS1, **SC-003**, FR-009); (2) the echoed `Maturity`/`Mode`/`Profile` equal the input's for every input (FR-009 — the levers carry through unchanged); (3) **relax-vs-strict** — for each `(base, maturity, mode)`, derive under `Light` and under `Profile.Release` and assert both decisions report the **same** `BaseSeverity`/`Maturity`/`Mode`; only `EffectiveSeverity` and `Reason` may differ (US3 AS2); (4) **no-drop** — mapping `deriveEffectiveSeverity` over an N-element `EnforcementInput list` yields exactly N `EnforcementDecision`s, one per input in order, none dropped (FR-012, **SC-006** — reclassification is not suppression).

### Implementation for User Story 3

- [X] T022 [US3] Confirm carry/no-drop is intrinsic to `deriveEffectiveSeverity` (`src/FS.GG.Governance.Enforcement/Enforcement.fs`): the `EnforcementDecision` record echoes `BaseSeverity`/`Maturity`/`Mode`/`Profile` directly from the input in every branch (no branch recomputes or omits them), and the function is a pure per-finding map with no aggregation/filtering — so mapping it over a list is inherently 1:1. Verify there is **no** code path that mutates base severity, derives a verdict/rollup, or could drop a finding (FR-009/FR-012/FR-013). Note explicitly that no change is needed beyond US1 (the record echo wired in T016 already satisfies this) — or fix any residual recompute if found.

**Checkpoint**: every decision carries the input base severity/maturity/mode byte-identical, relaxed and strict profiles differ only in effective severity + reason, and no lever combination drops a finding — Governance's central safety promise holds. US3 is independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: lock the public surface, prove the dependency boundary, and finish the docs/evidence.

- [X] T023 [P] Generate `surface/FS.GG.Governance.Enforcement.surface.txt` capturing exactly the public `Enforcement` module (the four lever/severity DUs, `Recognized<'T>`, `EnforcementInput`/`EnforcementDecision`, `runModeOrdinal`, `profileToProfileId`/`profileOfProfileId`, `recognizeMode`/`recognizeProfile`, `deriveEffectiveSeverity` — the `.fsi` surface), nothing private (no `maturityFloor`/`profileTighten` maps, no reason builders).
- [X] T024 In `tests/FS.GG.Governance.Enforcement.Tests/SurfaceDriftTests.fs`, add the surface-drift test asserting the built public surface matches `surface/FS.GG.Governance.Enforcement.surface.txt` (Principle II, with `BLESS_SURFACE=1` regen path), assert "exactly the `Enforcement` module, nothing private" (no floor/tighten maps, no reason builders), assert the surface exposes **no** rollup/ship-verdict/blockers/exit-code/IO/CLI member (FR-013/FR-014 exclusions, [quickstart.md](./quickstart.md) acceptance→evidence map), and assert the `Enforcement → Config` one-way dependency (no kernel/host/adapters/route/snapshot/CLI edge; no new third-party `PackageReference`) — mirroring the F021 `SurfaceDriftTests` dependency assertion.
- [X] T025 [P] Verify [quickstart.md](./quickstart.md) end-to-end: run the documented `dotnet test` for the new project **and** the full solution (no regression across existing projects), run the prelude FSI smoke (T008) against the real body, confirm the acceptance→evidence map holds, and fill `specs/023-enforcement-effective-severity/readiness/README.md` (T009) with the real FSI transcripts and the SC-001…SC-006 traceability note.
- [X] T026 [P] Update [`specs/023-enforcement-effective-severity/plan.md`](./plan.md) with an **Implementation Progress** header (status table + evidence summary, mirroring the F021 plan) once the suite is green, and confirm `CLAUDE.md`'s SPECKIT block points at this plan.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundation (Phase 2)** — depends on Setup; **BLOCKS all user stories** (`runModeOrdinal`, the `Profile`↔`ProfileId` mapping, and the hidden floor/tighten maps the derivation reduces against).
- **User Stories (Phases 3–5)** — all depend on Foundation. US1 (P1) is the MVP — the derivation itself. US2 (P2) is the recognition vocabulary (independent of the derivation). US3 (P3) asserts the safety properties over the document US1 produces.
- **Polish (Phase 6)** — depends on all desired user stories being complete.

### User-story dependencies

- **US1 (P1)** — after Foundation; the core derivation + reason builders. No dependency on other stories.
- **US2 (P2)** — after Foundation; `recognizeMode`/`recognizeProfile` are independent of the derivation (they share only the lever DUs). Independently testable.
- **US3 (P3)** — after the derivation exists (US1); its carry/no-drop properties are assertions over `deriveEffectiveSeverity`'s output. Independently testable once US1 lands.

### Within each user story

- Tests are written first and MUST FAIL before implementation (Principle I/V).
- `runModeOrdinal` + `Profile`↔`ProfileId` + the hidden floor/tighten maps (Foundation) before any derivation.
- Each story is independently completable and testable; complete a story before moving to the next priority.

### Parallel opportunities

- **Setup**: T005, T007, T008, T009 are `[P]` (distinct files) once T001–T004 exist.
- **Tests across stories**: T013–T015 (US1), T018–T019 (US2), T021 (US3) are `[P]` — distinct test files (`DerivationTests`/`TotalityTests`/`DeterminismTests`/`RecognitionTests`/`CarryTests`), no shared state.
- **Stories**: once Foundation is done, US1–US3 test-writing can proceed in parallel by different developers; the implementation tasks (T016/T017, T020, T022) all touch `Enforcement.fs`, so serialize those edits (or have one owner sweep them in phase order — T022 is a "confirm/complete" since the T016 record echo already covers it).
- **Polish**: T023, T025, T026 are `[P]`; T024 depends on T023.

---

## Parallel Example: cross-story test authoring

```bash
# After Foundation (Phase 2), launch the per-story test files together (distinct files):
Task: "DerivationTests.fs  — US1 worked example + withhold + boundary + base-advisory + reason shapes (T013)"
Task: "TotalityTests.fs    — US1 full 240-input cross-product + FsCheck totality + non-empty reason (T014)"
Task: "DeterminismTests.fs — US1 twice-identical effective severity + reason (T015)"
Task: "RecognitionTests.fs — US2 canonical maps + invalid → Unrecognized + round-trip (T018–T019)"
Task: "CarryTests.fs       — US3 base carry + relax-vs-strict + no-drop (T021)"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: the worked example reproduces (blocking→advisory + lever-naming reason), the same finding blocks at `gate`/`release`, `observe`/`warn` withhold under any mode/profile, the full cross-product is total and deterministic with non-empty reasons.

### Incremental delivery

1. Setup + Foundation → foundation ready.
2. US1 → the derivation classifies and explains every finding → the MVP.
3. US2 → the canonical vocabulary recognizes strings into typed levers (the shared dictionary).
4. US3 → profiles proven to reclassify, never hide truth (base carry + no-drop).
5. Polish → surface baseline + dependency assertion + readiness/quickstart.

---

## Notes

- `[P]` = different files, no dependencies.
- `[Story]` label maps a task to its user story for traceability.
- The `Enforcement.fs` implementation tasks (T010–T012, T016–T017, T020, T022) edit one file — serialize them in phase order; T022 is "confirm/complete," since the T016 record echo already satisfies the carry/no-drop properties.
- Tests drive the **public** surface (`deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile`, `profileToProfileId`, `profileOfProfileId`, `runModeOrdinal`) over real typed lever inputs — never the hidden `maturityFloor`/`profileTighten` maps or reason builders (Principle V).
- **No synthetic evidence is anticipated** — the entire input domain is the finite cross-product of base severity × maturity × run mode × profile (240 inputs), all literally constructible; the FsCheck properties (T014, T015, T021) generate `EnforcementInput`s from the same finite enumerations. Any unavoidable literal carries `Synthetic` in the test name + a use-site `// SYNTHETIC:` disclosure and is listed in the PR.
- Scope guards (FR-013/FR-014/FR-015): no ship/merge verdict, blockers, exit code, or cross-finding rollup; no I/O, `.fsgg/policy.yml` parsing, artifact, or CLI; no per-class profile dial map (deferred) — the derivation stops at the typed `EnforcementDecision`, takes one inward reference (`Config`), and adds no third-party dependency.
