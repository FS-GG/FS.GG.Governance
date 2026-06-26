# Contract: `evidence.json` document (`fsgg.evidence/v1`)

**Feature**: `069-evidence-json-projection` | **Date**: 2026-06-26

The deterministic wire contract for the artifact produced by `EvidenceJson.ofReport` and written by
`fsgg evidence` to `readiness/evidence.json`. Companion to the sibling contracts for `route.json` /
`audit.json` / `verify.json` / `cache-eligibility.json`. Field order is **fixed and normative**.

## C1 — Top-level object

Exactly one JSON object. Fields in this fixed order:

```json
{
  "schemaVersion": "fsgg.evidence/v1",
  "graphFailure": null,
  "nodes": [ /* … */ ],
  "dependencies": [ /* … */ ],
  "disclosures": [ /* … */ ]
}
```

- `schemaVersion` — the fixed constant `"fsgg.evidence/v1"` (FR-001), never derived from clock/env/input.
- Exactly one of two mutually exclusive shapes:
  - **Well-formed graph** → `graphFailure` is `null`; `nodes`/`dependencies` are present (possibly `[]`).
  - **Malformed graph** → `graphFailure` is the failure object (C3); `nodes` and `dependencies` are **omitted
    entirely** — never empty arrays, never a partial map (FR-004, SC-003).
- `disclosures` is ALWAYS present (`[]` when none).

## C2 — `nodes[]` (well-formed only)

Each node, in ascending `id` order:

```json
{
  "id": "speckit:T042",
  "declared": "Real",
  "effective": "AutoSynthetic",
  "freshness": { "kind": "stale", "cause": { "kind": "inputsChanged", "categories": ["coveredArtifacts"] } },
  "source": "speckit"
}
```

- `id` — the stable node id, emitted verbatim (never re-parsed across `:`).
- `declared` / `effective` — a `EvidenceState` token from the closed set, both **always present** (FR-002):
  `"Pending"` | `"Real"` | `"Synthetic"` | `"Failed"` | `"Skipped"` | `"AutoSynthetic"`. When taint demotes a
  node, `effective` differs from `declared` and **both are shown** — taint never overwrites `declared`.
  `"Skipped"` is a distinct token from `"Failed"`/`"Pending"` (FR-005).
- `freshness` — the object in C4.
- `source` — the provenance tag (`"speckit"` | `"design-system"` | `"review-cache"` | `"project"`), verbatim.

`nodes: []` is valid and deterministic for a change with no evidence nodes (FR-010) — a success, never an
error or a missing file.

## C3 — `graphFailure` (malformed only)

Renders the named `Kernel.GraphError<string>`; `nodes`/`dependencies` are omitted (FR-004):

```json
{ "schemaVersion": "fsgg.evidence/v1",
  "graphFailure": { "kind": "cycle", "nodes": ["a", "b", "a"] },
  "disclosures": [] }
```

- `kind: "cycle"` → `nodes`: the offending cycle node ids in their `GraphError.Cycle` order.
- `kind: "unknownNode"` → `node`: the offending undeclared id (`GraphError.UnknownNode`).
- `kind: "autoSyntheticDeclared"` → `node`: the illegally directly-declared auto-synthetic id
  (`GraphError.AutoSyntheticDeclared`).

All three kinds MUST appear by name in fixtures (SC-003), and in **0** of them is a guessed per-node effective
map emitted.

## C4 — `freshness` object (no-hide cause)

```json
{ "kind": "fresh" }
{ "kind": "stale", "cause": { "kind": "noPriorEvidence" } }
{ "kind": "stale", "cause": { "kind": "inputsChanged", "categories": ["ruleHash", "coveredArtifacts"] } }
{ "kind": "unresolved", "missing": ["coveredArtifacts", "headRevision"] }
{ "kind": "unknown" }
```

- `fresh` — the node's freshness resolved `Fresh`.
- `stale` — carries a `cause` object (FR-003): `noPriorEvidence`, or `inputsChanged` naming the exact changed
  `InputCategory` tokens (via `categoryToken`) in core order — none dropped, added, or truncated.
  `noPriorEvidence` (no `categories`) is DISTINCT from `inputsChanged` with `categories: []`.
- `unresolved` — carries a **non-empty** `missing` array naming every `MissingFact` (via `missingFactToken`)
  (FR-003).
- `unknown` — the node has no joinable freshness signal; an explicit honest null-equivalent, **never** a
  guessed `fresh`.

## C5 — Determinism and emptiness

- Byte-identical for identical repository state (FR-006, SC-002): `nodes` sorted by `id`, `dependencies` sorted
  by `(dependent, dependency)`, `disclosures` sorted by `(rule, justification)`, category/missing token lists
  in core order, fixed field sequence throughout.
- No wall-clock, absolute/host path, environment value, locale, raw freshness input, computed key/hash,
  numeric exit code, severity, ship verdict, exit-code basis, or provenance/attestation reference.
- `dependencies[]` entries: `{ "dependent": "<id>", "dependency": "<id>" }`.
- `disclosures[]` entries: `{ "rule": "<ruleId>", "justification": "<text>" }`.

## C6 — Additivity (existing artifacts unchanged)

Emitting `evidence.json` changes **0 bytes** of every existing `route.json` / `audit.json` / `verify.json` /
`cache-eligibility.json` golden and the existing `fsgg-governance evidence` output, and changes **no** verdict
or exit-code basis (FR-007, FR-009, SC-005). `schemaVersion` is new at `v1`; no existing schema version is
bumped. The host process exit code reflects only operational outcome — `0` success, `2` usage error, `3`
input-unavailable, `4` tool error — never a ship/merge verdict.
