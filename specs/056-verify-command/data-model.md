# Phase 1 Data Model: The `fsgg verify` Host Command

Entities are the pure values the `Loop` owns and the `VerifyJson` projection consumes. All reused types are
referenced by their owning project; this row introduces **no** new persisted schema beyond `verify.json`.
The spec's "Key Entities" map onto these as noted.

## 1. `Loop.RunRequest` — the normalized invocation

Spec entity: **Verification request**.

| Field | Type | Notes |
|-------|------|-------|
| `Repo` | `string` | Governed repository working dir. Default `"."`. |
| `Scope` | `ScopeSelector` | `ExplicitPaths of GovernedPath list \| Since of string \| DefaultRange`. Default `DefaultRange` (locally-changed set). |
| `Profile` | `Enforcement.Profile` | Default `Standard`. Overridable via `--profile` (recognized in `parse`). |
| `Format` | `OutputFormat` | `Text \| Json`. Default `Text`. |
| `VerifyOut` | `string` | `verify.json` output path. Default `under repo "readiness/verify.json"`. |
| `StorePath` | `string` | Evidence-reuse store path. Default `under repo "readiness/evidence-reuse.json"`. |
| `PersistStore` | `bool` | `--persist-store` opt-in. Default `false`. |

**No `Mode` field** — the enforcement mode is fixed to `RunMode.Verify` (research D2, FR-017). The resolved
profile is the only enforcement lever a developer can set.

## 2. `Loop.UsageError` — pure-parser rejections (→ exit 2)

```
UnknownFlag of string | MissingValue of flag: string | PathsAndSinceTogether | EmptyPaths
  | UnrecognizedProfile of string
```

No `UnrecognizedMode` (verify has no `--mode`). Each maps to `UsageError'`/exit 2, decided in `parse`
before any port is built (research D9).

## 3. `Loop.ExitDecision` — the five-way process result

```
Success | Blocked | UsageError' | InputUnavailable | ToolError      ->  0 | 1 | 2 | 3 | 4
```

`Blocked` (1) ⇐ an unmet effective-blocking check (via `Ship.rollup`/`applyExecution` at `RunMode.Verify`);
distinct from every failure-to-run code (research D6, FR-009). Spec entity: **Exit decision**.

## 4. `Loop.Effect` / `Loop.Msg` — the requested I/O and its results

The **exact** ShipCommand vocabulary (the interpreter executes each effect and feeds back a `Msg`):

| Effect | Result `Msg` | Meaning |
|--------|--------------|---------|
| `SenseScope of ScopeSelector` | `Sensed of Result<RepoSnapshot,string>` | Git diff → changed paths (F016). |
| `LoadCatalog of repo` | `Loaded of Validation` | F014 catalog read/validate. |
| `SenseFreshness of Gate list * (Revision option * Revision option)` | `FreshnessSensed of Result<SensedFacts,string>` | F046 freshness facts (degrades). |
| `LoadStore of path` | `StoreLoaded of Result<ReuseStore,string>` | F046 store read (degrades; sets `StoreDegraded`). |
| `ExecuteGates of (GateId * GateCommand) list` | `GatesExecuted of (GateId * CommandRecord) list` | F051 run the must-recompute command-gates. |
| `WriteArtifact of ArtifactKind * path * content` | `Wrote of ArtifactKind * Result<unit,string>` | Atomic `verify.json` write (failure ⇒ ToolError). |
| `PersistStore of path * content` | `StorePersisted of Result<unit,string>` | F048 opt-in store write (non-fatal). |
| `EmitSummary of text` | `Emitted` | stdout sink. |

`ArtifactKind = VerifyArtifact` (one write). Sensing/store `Error`s **degrade** (safe default + non-fatal
currency note), never failing the command or perturbing the verdict/exit (research D7).

## 5. `Loop.Model` — durable workflow state

Same shape as `ShipCommand.Model` (Request, Phase, Candidates, Decision, Snapshot, SelectedGates, Sensed,
Store, Tooling, Outcomes, currency/cache notes, StoreDegraded, PersistAcked, Diagnostics, Exit) with the
`VerifyOut`/no-`Mode` differences from §1. The `Decision : ShipDecision option` is the reused F024 verdict
(at `RunMode.Verify`); `Outcomes : (GateId * GateOutcome) list` are the F052 execution dispositions; the
projected document string is `VerifyDoc`.

Spec entities **Selected check**, **Check execution outcome** map onto the reused `SelectedGate` /
`GateOutcome` (`GateDisposition = Executed | Reused | NotExecuted`, `ExitCode option`, `Passed bool option`)
carried in `Model.SelectedGates` / `Model.Outcomes` — no new types.

## 6. Currency findings — derived, not stored

Spec entity: **Currency finding**. A currency finding is **computed purely** from `Model.Sensed`,
`Model.Store`, `Model.SelectedGates`, and `Model.Outcomes` (the same inputs ship's `cacheLinesOf` uses) —
it is **not** a new persisted field. Per selected check, exactly one disposition:

| Disposition | Source | Carries |
|-------------|--------|---------|
| **Fresh / reused** | cache verdict `Reusable ref` | the opaque `EvidenceRef` (verbatim) |
| **Stale / recomputed** | cache verdict `MustRecompute cause` | `cause`: `NoPriorEvidence` or `InputsChanged categories` (the changed freshness categories — the "stale generated view" signal) |
| **Recompute-by-default** | freshness `Unresolved` | the missing freshness-fact tokens |

The **severity** a currency finding carries is the owning gate's enforcement-assigned severity, read from the
matching `EnforcedItem.Decision` in the `ShipDecision` partition (research D4). Verify never builds a new
`EnforcedItem` and never adds a severity path; a blocking unmet check is already a Blocker, an advisory one a
Warning. Non-fatal degrade notes (sensing failed) are appended as plain strings, exactly like ship.

## 7. `verify.json` — the deterministic projection

Spec entity: **`verify.json` artifact**. Produced by
`VerifyJson.ofVerifyDecision : ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`.

Document shape (fixed field order; see `contracts/verify.schema.md` for the full grammar):

```json
{
  "schemaVersion": "fsgg.verify/v1",
  "verdict": "pass|blocked",
  "exitCodeBasis": "clean|blocked",
  "blockers":  [ <enforced item: id, enforcement{base,maturity,mode,profile,effective,reason}, cache?, execution?> ],
  "warnings":  [ <enforced item> ],
  "passing":   [ <enforced item> ],
  "currency": {
    "fresh":      [ { "gate": "<id>", "evidence": "<ref>" } ],
    "recomputed": [ { "gate": "<id>", "cause": "noPriorEvidence" | { "kind": "inputsChanged", "categories": [ ... ] } } ],
    "unresolved": [ { "gate": "<id>", "missing": [ ... ] } ]
  }
}
```

Determinism: compact `Utf8JsonWriter` (default options), fixed field order, exhaustive token helpers (no
wildcard), arrays in the cores' already-fixed order, no timestamp/abs-path/username/machine-specific content
(research D5; FR-007/FR-008). `--json` stdout is this string verbatim (FR-007, one source of truth).

## 8. Phase progression

`Parsed → Sensed' → Loaded' → Selected → Rolled → Persisted → Done` — the ShipCommand `Phase` ladder.
`Done` is terminal; any further reified `Msg` is inert (FR-013). The exit category is mapped from the
decision's `ExitCodeBasis` at `Emitted` (`Clean → Success`, `Blocked → Blocked`); a sensing/catalog/write
failure short-circuits to `Done` with the mapped tool-failure `ExitDecision` and never as `Blocked`.
