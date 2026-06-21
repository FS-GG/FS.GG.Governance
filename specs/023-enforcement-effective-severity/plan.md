# Implementation Plan: Enforcement Levers and Effective Severity

**Branch**: `023-enforcement-effective-severity` (active spec; git branch currently `main`) | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-enforcement-effective-severity/spec.md`

## Summary

Land the **first Phase-5 pure core**: the typed enforcement vocabulary and the single total, deterministic
function that derives a finding's **effective severity** (and a human-readable **reason**) from its **base
severity**, its rule **maturity**, the active **run mode**, and the Governance **profile**. It is the
design's *Modes, profiles, and maturity* section (`docs/initial-design.md:501`) and the first four
checkboxes of *Phase 5: Route Parity, Profiles, and Enforcement Fixtures* — the pure decision the later
`fsgg ship` and `audit.json` rows will reuse, exactly as F018's registry and F019's selection were pure
values consumed by F022's host edge.

The core models three closed levers — run **mode** (`sandbox`/`inner`/`focused`/`verify`/`gate`/`release`,
six values, ordered), Governance **profile** (`light`/`standard`/`strict`/`release`, four values, ordered),
and the base/effective **severity** value (`advisory`/`blocking`) — and **reuses F014 `Config`'s `Maturity`
and `ProfileId` verbatim** (FR-003) rather than redefining them. It exposes one total derivation,
`deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision`, defined over the **complete**
cross-product of (base severity × maturity × run mode × profile) that never throws (FR-005, SC-001), is
deterministic (FR-006, SC-004), echoes the input base severity byte-for-byte into its output (FR-009,
SC-003), and pairs the derived effective severity with a non-empty reason naming the responsible levers
(FR-010). It also exposes a total **recognition** of caller-supplied strings into typed modes/profiles with
an explicit `Unrecognized` outcome (FR-011, US2/SC-005).

Because the feature is a **pure, total, side-effect-free value-to-value computation** — no multi-step state,
no I/O, no clock — it is a **pure leaf** like F015/F017/F018/F019/F021, **not** an Elmish/MVU edge
(Constitution Principle IV applies only once behavior includes stateful workflow or I/O; spec "Boundary
discipline" assumption, FR-014). The work lands as a new optional, packable project
**`FS.GG.Governance.Enforcement`** plus its test project, continuing the one-row-one-project rhythm of
F014–F021. It takes **exactly one inward project reference** — `FS.GG.Governance.Config` (F014), for
`Maturity` and `ProfileId` — and adds **no new third-party `PackageReference`** (`System.*`/FSharp.Core
only; the derivation needs no serialization, so even `System.Text.Json` is unused here).

It computes **no** ship/merge verdict, blockers list, exit code, or any cross-finding rollup (FR-013); does
**no** I/O and parses **no** `.fsgg/policy.yml` (FR-014); and adds **no** CLI. The four canonical profiles
carry the design's documented strictness **intrinsically**; project-authored per-class profile dial
overrides (the `unknownPaths`/`staleEvidence`/… map in `policy.yml`) are a later Config + integration layer
and are explicitly **out of scope** (FR-015) — the same deferral discipline F022 used when it dropped the
`<id>` segment from `readiness/<id>/route.json` because the SDD work model does not yet exist here.

**Confirmed during planning (the plan-time reconciliations the spec deferred — research D1, D3, D4, D6):**

- **Project home (D1)**: a new pure-leaf packable project `FS.GG.Governance.Enforcement` (one
  `Enforcement.fsi`/`.fs` module pair, `IsPackable=true`), referencing only `FS.GG.Governance.Config`. It is
  a pure leaf — *not* an extension of the older kernel-era `Route`/`CheckRule` lineage (distinct lineage; see
  D2). The module exposes `deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile`, plus the ordinal
  and `ProfileId` mapping helpers.
- **Severity / RunMode are NEW here, not the kernel's (D2)**: the kernel already carries a `Severity`
  (`Advisory|Blocking`, `Kernel/CheckRule.fsi`) and a three-value `RunMode` (`Sandbox|Inner|Gate`,
  `Kernel/Route.fsi`). This feature does **not** reuse them: the Phase-2/5 line (`Config`/`Routing`/`Gates`/
  `Route`/…) references the kernel nowhere, and FR-001/FR-004 require a **six-value** run mode and a
  base/effective severity this kernel three-value ladder cannot express. New `RunMode` and `Severity` DUs
  are defined in this project; the relationship to the kernel sublanguage is documented, no kernel reference
  is taken.
- **Maturity → run-mode floor (D3)**: each maturity names the minimum run-mode ordinal at which a
  base-blocking finding may block. `observe`/`warn` → never; `block-on-pr` → `gate` (4); `block-on-ship` →
  `gate` (4); `block-on-release` → `release` (5). `block-on-pr` and `block-on-ship` **deliberately coincide
  at the `gate` floor** in this Governance-only slice — the design runs ship at `--mode gate` and no distinct
  PR vs ship run mode exists yet; a later feature splits them when a distinct PR run-position is introduced.
  This reproduces the design's worked example exactly (SC-002).
- **Profile strictness (D4)**: the four canonical profiles tighten (never relax below) the maturity floor by
  a documented adjustment: `light` 0, `standard` 0, `strict` −1, `release` −2 (clamped to `[sandbox,
  release]`). `light`/`standard` honour the maturity floor (the worked example requires `light` to still
  block `block-on-ship` at `gate`); `strict`/`release` block one/two boundaries earlier. Per-class
  relaxation that would further distinguish `light` from `standard` lives in the **deferred** `policy.yml`
  dial layer (FR-015); within this core `light` and `standard` differ only in reason text, which is
  disclosed. This core **never escalates a base-advisory finding** — base `advisory` ⇒ effective `advisory`
  always (escalation is reserved for the deferred per-class strictness rules; US3 scenario 3's "or escalated"
  branch is satisfied by a reason that explains the non-escalation).
- **Reason text (D6)**: deterministic, non-empty, names the active levers and the governing boundary; built
  from the typed inputs only (no clock, path, or environment). The `observe`/`warn` withhold-blocking case,
  the below-floor relaxed case, the at/above-floor blocking case, and the base-advisory case each render a
  distinct, stable sentence.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (from `Directory.Build.props`), `LangVersion latest`.

**Primary Dependencies**: One project reference only — `FS.GG.Governance.Config` (F014), for the reused
`Maturity` and `ProfileId` types. **No new third-party `PackageReference`.** The derivation is pure value
logic over closed DUs and records; it needs no serialization, git, or filesystem primitive — `System.*`/
FSharp.Core only.

**Storage**: None. The feature performs no I/O and persists no artifact (FR-014).

**Testing**: `dotnet test` (Expecto + FsCheck via the VSTest adapters, the F021 test shape). Tests drive the
**public** surface (`deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile`) through the packed
library / prelude, never private helpers (Principle V). FsCheck property tests assert **totality** over the
full enumerated cross-product (SC-001), **determinism** by evaluating twice and comparing bytes (SC-004),
**base-severity carry** (output base ≡ input base, SC-003), and **no-drop** (mapping the derivation over a
finding list is 1:1, SC-006). Example-based tests pin the design's worked example (SC-002) and the
recognition canonical/invalid sets (SC-005). All inputs are real typed lever values — no mocks, no synthetic
evidence needed; if any `Synthetic` token ever appears it carries a use-site disclosure (Principle V).

**Target Platform**: Cross-platform .NET library; validated on the Linux dev host.

**Project Type**: Optional packable F# library (pure leaf) plus one test project — the F015/F017/F018/F019/
F021 shape (a `.fsi`/`.fs` pair referenced by its test project), not the `Host`/`RouteCommand` edge shape.

**Performance Goals**: Not throughput-bound. The derivation is O(1) over four closed inputs. Determinism and
totality, not latency, are the contract.

**Constraints**: The derivation is **total** (defined for every one of the finite input combinations) and
**deterministic** (no clock, environment, ordering, or host-path influence — FR-006, SC-004). It **never
throws** (FR-005). It echoes base severity unchanged (FR-009, SC-003), never drops a finding (FR-012,
SC-006), computes no rollup/verdict/exit code (FR-013), and performs no I/O or `policy.yml` parsing
(FR-014). Recognition of an unknown string is a total `Unrecognized` value carrying the offending string —
never an exception, never a silent default (FR-011).

**Scale/Scope**: One new production project (`FS.GG.Governance.Enforcement`: a single `Enforcement` module —
`.fsi` then `.fs`) + one test project. One inward project reference (`Config`); zero new packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle / Constraint | Status | Notes |
|---|---|---|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | Public surface drafted as `Enforcement.fsi` and exercised in `scripts/prelude.fsx` before any `.fs` body; semantic tests call the packed surface (`deriveEffectiveSeverity`, `recognizeMode`, `recognizeProfile`), not internals. |
| II. Visibility in `.fsi` | PASS | The single public module gets a curated `Enforcement.fsi`; the `.fs` carries no `private`/`internal`/`public` modifiers; a surface-drift baseline (`surface/FS.GG.Governance.Enforcement.surface.txt`) is added and validated by a drift test. |
| III. Idiomatic Simplicity | PASS | Plain closed DUs, a small record, and pipeline/`match` logic; reuses F014 `Maturity`/`ProfileId` verbatim. No SRTP, reflection, type providers, custom operators, or non-trivial CEs. The ordinal map is a total `match`. |
| IV. Elmish/MVU is the boundary for stateful/I/O | **PASS — not applicable** | The feature has no multi-step state and no I/O; it is a pure, total leaf (spec "Boundary discipline" assumption, FR-014). Principle IV's MVU obligation triggers only once behavior includes stateful workflow or I/O — like F015/F017/F018/F019/F021, this slots beside them as a pure leaf with no `Model`/`Msg`/`Effect`. |
| V. Test Evidence Is Mandatory | PASS | FsCheck property sweeps (totality, determinism, base-carry, no-drop) + example tests (worked example, recognition sets) through the public surface, failing before / passing after. Real typed lever inputs throughout; no synthetic evidence required. |
| VI. Observability & Safe Failure | PASS | The derivation never throws and is total; the one "failure-shaped" output — an unrecognized lever string — is a distinct typed `Unrecognized` value carrying the offending string (FR-011), never an exception or silent default, keeping a bad input distinguishable from a tool defect. |
| Change Classification | **Tier 1** | New public API surface (a new project with a public `.fsi` module). Full chain: spec, plan, `.fsi`, surface baseline, tests, docs. No new dependency. |
| Engineering Constraints | PASS | `net10.0`; curated `.fsi`; surface baseline + drift test; pure leaf (no MVU needed); one inward reference (`Config`), no new package; generic — no rendering package IDs/paths assumed; honors the `~/.local/share/nuget-local/` pack location if/when packed. |

**Gate result: PASS — no unjustified violations. Complexity Tracking remains empty.**

## Project Structure

### Documentation (this feature)

```text
specs/023-enforcement-effective-severity/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — the plan-time reconciliations (D1–D6) + the enforcement truth table
├── data-model.md        # Phase 1 — RunMode/Profile/Severity, EnforcementInput/Decision, the derivation rule
├── quickstart.md        # Phase 1 — build/run/test validation guide + acceptance→evidence map
├── contracts/           # Phase 1 — public .fsi contract + the derivation/recognition decision contract
│   ├── Enforcement.fsi               # pure surface: types, deriveEffectiveSeverity, recognize*, helpers
│   └── enforcement-decision.md       # the effective-severity truth table + reason-text + recognition contract
└── tasks.md             # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.Enforcement/             # NEW — the first Phase-5 pure core (pure leaf)
├── FS.GG.Governance.Enforcement.fsproj       # IsPackable=true; PackageId FS.GG.Governance.Enforcement;
│                                              #   single ProjectReference: Config (F014)
├── Enforcement.fsi / Enforcement.fs           # PURE: RunMode, Profile, Severity, EnforcementInput,
│                                              #   EnforcementDecision, Recognized<'T>; deriveEffectiveSeverity;
│                                              #   recognizeMode/recognizeProfile; runModeOrdinal;
│                                              #   profileToProfileId/profileOfProfileId

tests/FS.GG.Governance.Enforcement.Tests/      # NEW
├── FS.GG.Governance.Enforcement.Tests.fsproj
├── Support.fs                                 # the enumerated lever domains (all modes/profiles/maturities/
│                                              #   severities) + FsCheck generators over them
├── DerivationTests.fs                         # worked example (SC-002), observe/warn withhold (FR-007),
│                                              #   boundary at/above/below floor (FR-008), base-advisory (D4)
├── TotalityTests.fs                           # full cross-product evaluated, never throws (SC-001)
├── DeterminismTests.fs                        # twice-run byte-identical effective severity + reason (SC-004)
├── CarryTests.fs                              # output base ≡ input base (SC-003); no-drop over a list (SC-006)
├── RecognitionTests.fs                        # canonical names recognized; invalid → Unrecognized (SC-005)
├── SurfaceDriftTests.fs                       # surface baseline for Enforcement
└── Main.fs                                    # Expecto entry

surface/FS.GG.Governance.Enforcement.surface.txt   # NEW public-surface baseline
```

**Structure Decision**: A single new pure-leaf project mirroring the F021 shape — one curated `.fsi` + `.fs`
module, `IsPackable=true`, referenced by its test project, taking one inward reference (`Config`). It sits as
a leaf in the dependency graph (`Enforcement → Config`); the kernel and the host/edge tier stay untouched. It
is the pure decision the later `fsgg ship` / `audit.json` rows will compose, never composing anything itself.

## Implementation Progress

**Status: ✅ COMPLETE — 2026-06-21.** All 26 tasks (T001–T026) done with real evidence; the full
solution is green (no regression). The pure-leaf project `FS.GG.Governance.Enforcement` ships the
typed enforcement vocabulary and the total `deriveEffectiveSeverity` derivation.

| Area | Status | Evidence |
|---|---|---|
| Setup (T001–T009) | ✅ | New `FS.GG.Governance.Enforcement` (`.fsi`/`.fs`, `IsPackable`, one `Config` reference, no new package) + test project, both in the solution; `Support.fs` enumerates the full 240-input cross-product; `scripts/prelude.fsx` F023 sketch; `readiness/README.md`. |
| Foundation (T010–T012) | ✅ | `runModeOrdinal` (Sandbox 0..Release 5), `profileToProfileId`/`profileOfProfileId`, hidden `maturityFloor`/`profileTighten` maps — all exhaustive closed-DU matches, no wildcard. |
| US1 — derivation (T013–T017) | ✅ | Worked example reproduces (`Blocking/BlockOnShip/Inner/Light` ⇒ `Advisory` + exact reason, SC-002); withhold (FR-007); full base-blocking truth table (FR-008); base-advisory non-escalation (D4); totality over 240 inputs + FsCheck (SC-001); determinism twice-run byte-identical (SC-004). |
| US2 — recognition (T018–T020) | ✅ | Canonical six modes / four profiles recognize; invalid ⇒ `Unrecognized` verbatim (exact-token, no trim/case-fold/default, FR-011, SC-005); `Profile`↔`ProfileId` bijection (FR-003). |
| US3 — carry/no-drop (T021–T022) | ✅ | Base severity byte-identical out=in (SC-003); maturity/mode/profile carried; relax-vs-strict differ only in effective severity + reason; mapping over N findings yields N decisions (SC-006). Intrinsic to the record echo — no extra code. |
| Polish (T023–T026) | ✅ | `surface/FS.GG.Governance.Enforcement.surface.txt` baseline + drift test (hidden helpers absent; no rollup/verdict/IO/CLI member; `Enforcement → Config` one-way leaf); quickstart + prelude FSI smoke run green against the real body. |

**Test evidence:** `dotnet test tests/FS.GG.Governance.Enforcement.Tests` → 28 passed, 0 failed.
Full-solution `dotnet test FS.GG.Governance.sln` → all projects green (no regression). No synthetic
evidence used — the entire input domain is the finite, literally-constructible cross-product.

## Complexity Tracking

> No Constitution violations to justify — this section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
