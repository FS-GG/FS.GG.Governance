# Phase 1 Data Model — shared JSON helpers

This feature introduces no runtime data entities (it is pure de-duplication). The "data
model" here is the **public surface of the shared helpers** — the members that become the
single source of truth and the domain types they consume. All helpers are pure and total
unless noted; none performs I/O.

## Entity 1 — `Kernel.Json.writeToString` (promoted to public)

- **Home**: `FS.GG.Governance.Kernel`, module `Json` (already defined at `Json.fs:23`).
- **Change**: add to `Json.fsi` only.
- **Signature**: `val writeToString: emit: (Utf8JsonWriter -> unit) -> string`
- **Behaviour**: emit compact (non-indented) UTF-8 JSON through the callback; deterministic.
- **Consumed types**: `System.Text.Json.Utf8JsonWriter` only.
- **Consumers**: the 12 `*Json` projections + `EvidenceReuseStore` + `RefreshCommand`
  (interpreter) — all current local copies deleted.

## Entity 2 — `FS.GG.Governance.JsonTokens` (NEW pure leaf)

Module `JsonTokens`. Each helper is an exhaustive `match` over a closed DU with **no
wildcard** (a future enum case must be a compile error, never a silently mis-tokened field —
the existing projection convention).

| Member | Signature | Token strings (verbatim, byte-identical to current) |
|---|---|---|
| `costToken` | `Cost -> string` | `cheap` / `medium` / `high` / `exhaustive` |
| `maturityToken` | `Maturity -> string` | `observe` / `warn` / `blockOnPr` / `blockOnShip` / `blockOnRelease` |
| `severityToken` | `Severity -> string` | `advisory` / `blocking` |
| `environmentToken` | `EnvironmentClass -> string` | `local` / `ci` / `localOrCi` / `release` |
| `dispositionToken` | `GateDisposition -> string` | `executed` / `reused` / `notExecuted` |
| `basisToken` | `ExitCodeBasis -> string` | `clean` / `blocked` |
| `profileToken` | `Profile -> string` | (verbatim from current `Enforcement`/projection copy — confirmed at extraction) |

- **Consumed domain types**: `Cost`, `Maturity`, `GateDisposition` (`Gates`/`GateRun`);
  `EnvironmentClass` (`Config`/`FreshnessKey`); `Severity`, `ExitCodeBasis`, `Profile`
  (`Findings`/`Enforcement`). Referenced via the enum-owning projects.
- **Out of scope** (stay local): single-use `factStateToken`, `outcomeToken` (ReleaseJson
  only); and the `Verdict` token (`verdictToken`/`rrVerdictToken`) whose copies emit
  divergent strings (`Fail` → `blocked` vs `fail`) and so cannot be unified (research D3).

## Entity 3 — `FS.GG.Governance.JsonWriters` (NEW pure leaf)

Module `JsonWriters`. References `JsonTokens`. Sub-object writers emit a fixed field order
verbatim; map helpers are first-by-list-order-wins folds keyed on the gate-id string.

| Member | Signature | Notes |
|---|---|---|
| `writeCause` | `Utf8JsonWriter -> RecomputeCause -> unit` | tagged `kind` object; `noPriorEvidence` / `inputsChanged` + `categories[]`. Unifies the 6 copies incl. `VerifyJson.writeCauseValue`. |
| `verdictByGate` | `CacheEligibilityReport -> Map<string, CacheEligibilityVerdict>` | first-by-report-order-wins lookup |
| `outcomeByGate` | `(GateId * GateOutcome) list -> Map<string, GateOutcome>` | first-by-list-order-wins lookup |
| `writeExecution` | `Utf8JsonWriter -> GateOutcome -> unit` | `disposition` (+ optional `exitCode`, `passed`) |
| `writeEnforcement` | `Utf8JsonWriter -> EnforcementDecision -> unit` | the six F023 enforcement fields verbatim |

- **Consumed domain types**: `RecomputeCause`, `CacheEligibilityReport`,
  `CacheEligibilityVerdict` (`CacheEligibility`/`EvidenceReuse`); `GateId`, `GateOutcome`,
  `GateDisposition`, `ExitCode` (`GateRun`/`Gates`/`CommandRecord`); `EnforcementDecision`
  (`Enforcement`); token helpers from `JsonTokens`.
- **Note**: helpers that genuinely vary per projection (e.g. a writer that embeds a
  projection-specific field set) stay local; only the byte-identical copies move. The
  single-use `writeNullableString`/`writeNullableInt` writers (ReleaseJson only) are **not**
  duplicated and therefore stay local — out of scope (research D3).

## Dependency placement (acyclic)

```text
Kernel  (System.* only; owns writeToString)
  ▲
domain enum/record owners: Config, Gates, Findings, FreshnessKey, Enforcement,
  GateRun, CacheEligibility, EvidenceReuse, CommandRecord
  ▲
JsonTokens  ──referenced by──▶  JsonWriters
  ▲                                  ▲
  └──────────── the 12 *Json projections ────────────┘
```

## State transitions

None. All members are pure functions; there is no mutable state, lifecycle, or persistence.
