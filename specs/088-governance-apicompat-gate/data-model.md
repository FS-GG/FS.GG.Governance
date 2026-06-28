# Phase 1 Data Model: Breaking-Change (API-Compat) Gate

Pure, total, deterministic types (Constitution Principle III/V; the existing release-core invariant). No type carries raw bytes of a `.nupkg`, host paths, timestamps, or a process exit code — the sensor at the edge produces these values as data, the pure core only grades them. Names below are indicative `.fsi` shapes to be drafted in FSI first (Principle I).

## Reused verbatim (no change)

- `FS.GG.Governance.ReleaseRules.Model.FactState` = `Met | Unmet | Unrecoverable`
- `FS.GG.Governance.ReleaseRules.Model.ReleaseRule` = `{ Kind; Surface; BaseSeverity; Maturity }` — the `Maturity` lever is the advisory→required ratchet (D3)
- `FS.GG.Governance.ReleaseRules.Model.ReleaseFacts` = `{ States: Map<ReleaseRuleKind, FactState> }`
- `ReleaseFinding` / `EnforcedReleaseFinding` / `ReleaseDecision` — the rollup, unchanged
- `FS.GG.Governance.Config.Model.SurfaceId` — the governed package identity
- The semantic-version comparator behind `Pack.versionPolicy` (numeric core segments; build metadata ignored; pre-release < release) — reused for the version delta

## New / extended types

### 1. `ReleaseRuleKind` — additive case (`FS.GG.Governance.ReleaseRules.Model`)

```fsharp
type ReleaseRuleKind =
    | VersionBump
    | PackageMetadata
    | TemplatePins
    | PublishPlan
    | TrustedPublishing
    | Provenance
    | ApiCompatibility        // NEW (additive) — break-vs-bump adequacy for a published package
```

- Additive only; existing cases and their `factFor` keys unchanged.
- A declared `ApiCompatibility` rule reads `facts.States.[ApiCompatibility]`; absent ⇒ `Unrecoverable` ⇒ `Violated` (existing semantics).

### 2. `ApiBreakSignal` — the sensor's per-package result (new; sensor-facing)

```fsharp
/// What the assembly/package comparison concluded for ONE package, as DATA.
/// Produced by the ApiCompat sensor at the I/O edge; consumed by the pure verdict helper.
type ApiBreakSignal =
    | NoBreakingChanges
    | BreakingChanges of breaks: ApiBreak list   // non-empty
    | NoBaseline                                  // never published / baseline absent (FR-009)
    | Indeterminate of reason: string             // feed unreachable / unreadable / tool error (FR-008)
    | NotPackable                                 // not an IsPackable target (no fact emitted)

/// One detected break — enough to name it in a finding (FR-003). Product-neutral text.
type ApiBreak =
    { Member: string        // the removed/changed public member, as ApiCompat reports it
      Kind: ApiBreakKind }  // removed | signature-changed | …(closed, mirrors ApiCompat categories we surface)

type ApiBreakKind =
    | MemberRemoved
    | MemberSignatureChanged
    | TypeRemoved
    | OtherIncompatibility of label: string
```

### 3. `VersionDelta` — magnitude of the bump vs baseline (new; pure)

```fsharp
/// The semantic magnitude of packed-vs-baseline, derived from the existing comparator.
type VersionDelta =
    | MajorBump
    | MinorOrPatchBump
    | NoForwardChange     // equal or downgrade (a break here is also under-bumped)
    | NoBaselineDelta     // no baseline to compare
```

### 4. `ApiCompatVerdict` → `FactState` (new pure helper)

```fsharp
/// Combine the sensor signal with the version delta into the governing FactState (D4 table).
/// PURE, TOTAL, deterministic. Lives next to Pack.versionPolicy (PackEvidence) so the version
/// comparator is reused; emits the value the sensing layer puts into ReleaseFacts.States.[ApiCompatibility].
val apiCompatibilityFact:
    signal: ApiBreakSignal -> delta: VersionDelta -> FactState option
//  None  ⇒ NotPackable (no rule fact emitted; the package is reported "not covered", FR-007)
//  Some Met / Unmet / Unrecoverable per the D4 table
```

**Verdict mapping (D4), authoritative:**

| `signal` | `delta` | result |
|---|---|---|
| `NoBreakingChanges` | any | `Some Met` |
| `BreakingChanges _` | `MajorBump` | `Some Met` |
| `BreakingChanges _` | `MinorOrPatchBump` / `NoForwardChange` | `Some Unmet` |
| `NoBaseline` | `NoBaselineDelta` | `Some Met` (vacuous, FR-009) |
| `Indeterminate _` | any | `Some Unrecoverable` (fail-safe, FR-008) |
| `NotPackable` | any | `None` (not covered, FR-007) |

### 5. Coverage report (new; for FR-007 / SC-001)

```fsharp
/// The explicit per-package coverage outcome so "not covered" is reported, never silently clean.
type ApiCompatCoverage =
    { Surface: SurfaceId
      Outcome: ApiCompatCoverageOutcome }

type ApiCompatCoverageOutcome =
    | Checked of fact: FactState          // a baseline existed and was compared
    | NoBaselineYet                       // vacuously Met but flagged as "not yet enforcing"
    | NotCovered of reason: string        // not packable / tool could not analyze (FR-007)
```

The coverage list feeds both the human/JSON projection and SC-001's "100% covered-or-reported, zero silent passes."

## Validation / invariants (carried from the spec & constitution)

- **Determinism**: identical `(signal, delta)` ⇒ identical `FactState`; identical inputs ⇒ byte-identical findings & coverage ordering (sorted by `SurfaceId`).
- **Fail-safe**: `Indeterminate` and absent fact ⇒ `Violated` (never silently `Met`).
- **Vacuous-safe**: `NoBaseline` ⇒ `Met` but surfaced as `NoBaselineYet` in coverage (not hidden).
- **Additivity**: existing rule kinds, `Pack.versionPolicy`, and all current `surface.txt` baselines are unchanged except for the additive `ApiCompatibility` case and the touched projects' refreshed baselines.
- **No product vocabulary** in the pure core beyond declared ids (genericity / operating rule).

## State transitions (advisory → required)

The only "state" is the declared `ApiCompatibility` rule's `Maturity`, changed by a human maintainer:

```
Advisory (violations → Warnings, Verdict unaffected)
   │  (maintainer flips Maturity when SC-005 met: zero breaking-under-bump across covered packages)
   ▼
BlockOnRelease (violations → Blockers, Verdict = Fail; CI job added to required checks)
```

No runtime state machine, no persistence — the transition is a reviewed change to the declared rule + branch-protection settings.
