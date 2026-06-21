# Phase 1 Data Model: Deterministic audit.json Projection (F025)

This row introduces **no new domain types**. It consumes the F024 `ShipDecision` (an already-validated,
already-ordered typed value) and produces a single JSON **string**. The wire shape — field order,
tokens, exclusions — is fixed in [`contracts/audit-json-document.md`](./contracts/audit-json-document.md);
the public surface in [`contracts/AuditJson.fsi`](./contracts/AuditJson.fsi).

## 1. Consumed value (read-only; nothing re-derived)

The F024 `ShipDecision` (`src/FS.GG.Governance.Ship/Model.fsi`) and everything it transitively carries:

```fsharp
type Verdict        = Pass | Fail
type ExitCodeBasis  = Clean | Blocked

type EnforcedItemId =
    | GateItem    of GateId                    // Gates.Model
    | FindingItem of FindingId * GovernedPath  // Findings.Model * Config.Model

type EnforcedItem =
    { Id: EnforcedItemId
      Decision: EnforcementDecision }          // Enforcement.Enforcement

type ShipDecision =
    { Verdict: Verdict
      Blockers: EnforcedItem list
      Warnings: EnforcedItem list
      Passing:  EnforcedItem list
      ExitCodeBasis: ExitCodeBasis }
```

The six-field F023 enforcement detail (`src/FS.GG.Governance.Enforcement/Enforcement.fsi`):

```fsharp
type Severity = Advisory | Blocking
type Maturity = Observe | Warn | BlockOnPr | BlockOnShip | BlockOnRelease   // Config.Model
type RunMode  = Sandbox | Inner | Focused | Verify | Gate | Release
type Profile  = Light | Standard | Strict | Release

type EnforcementDecision =
    { BaseSeverity: Severity
      Maturity: Maturity
      Mode: RunMode
      Profile: Profile
      EffectiveSeverity: Severity
      Reason: string }
```

The projection **reads** these; it re-derives nothing, re-validates nothing, re-partitions nothing, and
recomputes neither the verdict nor the exit-code basis (FR-002, FR-003, FR-006).

## 2. Produced value

A single JSON **string**: one top-level object, field order `schemaVersion`, `verdict`,
`exitCodeBasis`, `blockers`, `warnings`, `passing`. Compact (non-indented) UTF-8 from a default
`Utf8JsonWriter`. The full field/token contract is in `contracts/audit-json-document.md`.

## 3. Rendering rules (per field) and the tokens this row owns

| Upstream value | Rendered as | Token source |
|---|---|---|
| `AuditJson.schemaVersion` | `schemaVersion` string `"fsgg.audit/v1"` | local public constant |
| `decision.Verdict` | `verdict` (`pass`\|`fail`) | local **hidden** `verdictToken` |
| `decision.ExitCodeBasis` | `exitCodeBasis` (`clean`\|`blocked`) | local **hidden** `basisToken` |
| `GateItem g` | `{ kind:"gate", id, enforcement }` | `Gates.gateIdValue` (reuse, public) |
| `FindingItem (fid, GovernedPath p)` | `{ kind:"finding", id, path, enforcement }` | `Findings.findingIdToken` (reuse, public); `GovernedPath` unwrapped by match |
| `d.BaseSeverity` / `d.EffectiveSeverity` | `baseSeverity` / `effectiveSeverity` (`advisory`\|`blocking`) | local **hidden** `severityToken` |
| `d.Maturity` | `maturity` (`observe`…`blockOnRelease`) | local **hidden** `maturityToken` |
| `d.Mode` | `mode` (`sandbox`…`release`) | local **hidden** `modeToken` |
| `d.Profile` | `profile` (`light`…`release`) | local **hidden** `profileToken` |
| `d.Reason` | `reason` (free text) | writer string API (JSON-escaped) |

Each local token helper is an **exhaustive** `match` over its closed DU with **no wildcard** ([research
D3](./research.md)), so a future case is a compile error, never a mis-tokened field. The two identity
renderers are **reused** from their public upstream homes so the document re-derives nothing (FR-010);
Enforcement's own token helpers are hidden in `Enforcement.fs` and cannot be reused across the assembly
boundary, so AuditJson rolls its own — exactly the GatesJson precedent.

## 4. Determinism (FR-007, SC-002, SC-003)

- **Field order** is the writer's fixed call order at every level (top-level, item entry, `enforcement`
  object) — part of the contract.
- **Collection order** is inherited verbatim from the `ShipDecision`'s `Blockers`/`Warnings`/`Passing`
  lists, which F024 already sorted by the stable composite key (gates before findings, gates by
  `GateId`, findings by `(path, finding-id token)`). The projection **re-sorts nothing** ([research
  D6](./research.md)).
- **No `Map` iteration** — the inputs are F# `list`s in fixed order.
- **Compact output** — default `Utf8JsonWriter` options (no indentation, deterministic).
- **No clock / environment input** — output depends only on the `ShipDecision` value.

Consequence: identical decisions project byte-for-byte identically (SC-002); two decisions equal as
values but assembled from differently-ordered route inputs project identically (SC-003).

## 5. Totality (FR-008, FR-009, SC-006)

The projection pattern-matches only **closed** DUs (`Verdict`, `ExitCodeBasis`, `EnforcedItemId`,
`Severity`, `Maturity`, `RunMode`, `Profile`) exhaustively; it has no partial pattern, no array
indexing, no parsing, and no I/O. It therefore **never throws** for any well-typed `ShipDecision`. The
empty/clean decision (no items; `Pass`; `Clean`) projects to a valid document with three present, empty
arrays and `verdict:"pass"` / `exitCodeBasis:"clean"` — a success, never an error and never a
"fail by default" fallback (FR-009).

## 6. Exclusions (FR-011, FR-012, SC-007)

The document carries **no** numeric process exit code (the later `fsgg ship` host edge maps the basis
to a number), **no** provenance/attestation reference or artifact digest (the `ShipDecision` carries
none — the later Release phase), **no** cache-eligibility/freshness verdict (Phase 11), **no** gate
registry metadata (cost/timeout/owner/prerequisites/freshnessKey), and **no** raw YAML, host/absolute
path, wall-clock timestamp, or environment-derived value. Only the declared schema version, the closed
verdict/basis/severity/maturity/mode/profile vocabularies, the declared id strings, the governed path,
and the carried free-text `reason` appear.
</content>
