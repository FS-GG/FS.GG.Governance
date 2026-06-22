# Contract — `FS.GG.Governance.CacheEligibilityJson` public API (F042)

The Tier-1 public surface this row commits, with the laws each member upholds. The single `.fsi` file is the
sole declaration of visibility (Principle II); the reflective `SurfaceDrift` test pins this surface to
`surface/FS.GG.Governance.CacheEligibilityJson.surface.txt` and guards the dependency scope. The projection
is **pure, total, and deterministic** (FR-007/FR-008): defined for every well-typed `CacheEligibilityReport`,
never throwing; reading no clock/filesystem/git/environment/network, making no cache lookup against a real
store, computing no freshness key or hash, resolving none of the inputs, never dereferencing the opaque
evidence reference, persisting nothing; identical for identical input regardless of evaluation time,
machine, process, or working directory.

## `CacheEligibilityJson` operations

```fsharp
val schemaVersion: string
val ofReport: report: CacheEligibilityReport -> string
```

Consumes `CacheEligibilityReport` / `CacheEligibilityEntry` / `CacheEligibilityVerdict` from
`FS.GG.Governance.CacheEligibility.Model`; `RecomputeCause` / `EvidenceRef` from
`FS.GG.Governance.EvidenceReuse.Model`; `InputCategory` from `FS.GG.Governance.FreshnessKey.Model`; `GateId`
from `FS.GG.Governance.Gates.Model` — all verbatim. Renders via the public accessors `Gates.gateIdValue`,
`EvidenceReuse.referenceValue`, `FreshnessKey.Model.categoryToken`, and `CacheEligibility.entries`.

### `schemaVersion` — the declared contract version (FR-013)

- **L-V1** — `schemaVersion = "fsgg.cache-eligibility/v1"`, a fixed string constant. It is the value of the
  document's `schemaVersion` field on every output and is never derived from a clock, environment, or input
  value. A change to this constant is a deliberate contract-version bump (and a surface-baseline rebless).

### `ofReport` — the projection (FR-001…FR-012)

Let `doc = ofReport report` and `es = CacheEligibility.entries report`.

- **L-R1 (one entry per report entry, carry, FR-001/FR-005/SC-001)** — `doc`'s `entries` array has exactly
  `List.length es` elements, the i-th rendering `es.[i]`: its `gate` is `gateIdValue es.[i].Gate` and its
  `verdict` renders `es.[i].Verdict`. No entry is dropped, merged, deduplicated, reordered, or invented.
- **L-R2 (verdict echoed verbatim, no recompute, FR-002/SC-004)** — each entry's `verdict` is its own
  `Verdict` rendered structurally; `ofReport` re-runs no reuse decision, re-ranks no evidence, and computes
  no verdict from any other field. `Reusable ref` ⇒ `{ "kind":"reusable", "evidence": referenceValue ref }`;
  `MustRecompute cause` ⇒ `{ "kind":"mustRecompute", "cause": … }`.
- **L-R3 (reusable names its evidence, necessary-not-sufficient, FR-003/SC-004)** — a `reusable` verdict
  carries exactly `kind` + `evidence`, the `evidence` being `referenceValue ref` verbatim — opaque, never
  parsed/dereferenced/validated. It carries no skip action, reuse policy, severity, ship verdict, or
  exit-code basis.
- **L-R4 (mustRecompute names its cause, no-hide, FR-004/SC-005)** — a `mustRecompute` verdict always
  carries a `cause`. `NoPriorEvidence` ⇒ `{ "kind":"noPriorEvidence" }` (no `categories`);
  `InputsChanged cats` ⇒ `{ "kind":"inputsChanged", "categories": [ categoryToken c for c in cats ] }`, the
  categories in the report's order (F041 carried F030's `diff` order) — none dropped, added, or truncated.
- **L-R5 (`noPriorEvidence` ≠ `inputsChanged []`, FR-006/SC-005)** — the two are structurally distinct:
  `noPriorEvidence` has no `categories` field; `inputsChanged []` has `categories: []`. They never collapse
  to one another.
- **L-R6 (gate id verbatim, FR-010)** — `gate` is `gateIdValue es.[i].Gate` verbatim; never re-parsed to
  recover a domain or check, even across a `:` separator.
- **L-R7 (order preserved verbatim, FR-005/FR-007)** — `entries` is `es` in its existing order
  (`GateId`-ordinal with F041's structural duplicate tiebreak), re-sorting nothing. The projection adds no
  ordering decision of its own beyond the fixed field sequence (`schemaVersion`, `entries`; per entry
  `gate`, `verdict`; per verdict `kind` then payload; per cause `kind` then `categories`).
- **L-R8 (duplicates kept, Edge Cases)** — two report entries sharing a `GateId` render as **two** distinct
  `entries` elements under that gate id, each with its own verdict, in the report's order — neither merged
  nor deduplicated.
- **L-R9 (always-present entries; empty is total, FR-009)** — `entries` is always present;
  `ofReport (CacheEligibilityReport [])` is a valid document `{ schemaVersion, entries: [] }` — a success,
  never an error and never a "must recompute by default" placeholder entry.
- **L-R10 (closed token vocabularies, FR-011)** — `verdict.kind ∈ {reusable, mustRecompute}`;
  `cause.kind ∈ {noPriorEvidence, inputsChanged}`; each `categories` element is a `categoryToken`. Every
  rendering `match` is exhaustive over the closed DU with no wildcard, so a new upstream case is a compile
  error here.
- **L-R11 (exclusions, FR-012/SC-007)** — `doc` contains no wall-clock timestamp, host/absolute path, raw
  freshness input, computed freshness key or hash, environment value, numeric process exit code, severity,
  ship verdict, exit-code basis, or provenance/attestation reference — only the schema version, the `gate`
  ids, the closed `verdict`/`cause`/`categories` vocabularies, and the opaque `evidence` reference.

### Cross-cutting laws

- **L-T1 (totality, SC-006)** — `ofReport` returns a document string and never throws across the full space
  of well-typed reports: empty, all-reusable, all-must-recompute, mixed, and duplicate-`GateId`.
- **L-T2 (determinism / purity, SC-002)** — `ofReport report = ofReport report` always (byte-for-byte),
  including under a changed working directory, clock, and filesystem state; no I/O is performed.
- **L-T3 (order-independence at the source, SC-003)** — two reports equal as values but assembled from
  differently-ordered candidate inputs project to byte-identical documents, because F041 already normalized
  the entry order and `ofReport` preserves it verbatim (it introduces no order dependence of its own).

## Worked examples (pinned by tests)

Let `evaluate` be F041 `CacheEligibility.evaluate`, `gid d c = GateId (d + ":" + c)`,
`refA = EvidenceRef "ev-A"`, and `inputs0` any `FreshnessInputs` for gate `("build","tests")`.

| Report (from real `evaluate`) | `ofReport report` `entries` |
|---|---|
| `evaluate [] store` | `[]` |
| `evaluate [ { Gate = gid "docs" "lint"; Inputs = inputs0 } ] (record inputs0 refA empty)` (exact match) | `[ { gate:"docs:lint", verdict:{ kind:"reusable", evidence:"ev-A" } } ]` |
| `evaluate [ { Gate = gid "security" "scan"; Inputs = inputs0 } ] empty` (no prior) | `[ { gate:"security:scan", verdict:{ kind:"mustRecompute", cause:{ kind:"noPriorEvidence" } } } ]` |
| `evaluate [ { Gate = gid "build" "tests"; Inputs = inputs0' } ] (record inputs0 refA empty)` where `ruleHash` + `head` moved | `[ { gate:"build:tests", verdict:{ kind:"mustRecompute", cause:{ kind:"inputsChanged", categories:["ruleHash","headRevision"] } } } ]` |

Ordering. Candidates supplied as `gid "z" "a"`, `gid "a" "b"`, `gid "a" "a"` against any store ⇒ `entries`
ordered `a:a`, `a:b`, `z:a` (the report's `GateId`-ordinal order), independent of supply order — and the
document is byte-identical for any permutation (L-T3). The full document shape, tokens, and samples are in
[cache-eligibility-json-document.md](./cache-eligibility-json-document.md).

## Scope guard (SurfaceDrift test, Principle II)

The `FS.GG.Governance.CacheEligibilityJson` assembly references **only**
`FS.GG.Governance.CacheEligibility` (F041), its transitive pure cores (`EvidenceReuse`, `Gates`,
`FreshnessKey`, `Config`), and `FSharp.Core` / BCL. It references no host/CLI/adapter assembly, no
`RouteJson` / `AuditJson` / `GatesJson` / `Enforcement` / `Ship` / `Snapshot` / `Routing`, and adds no
third-party package — serialization is the net10.0 shared-framework `System.Text.Json`. Any drift in the
rendered public surface or the referenced-assembly set fails the test (with the `BLESS_SURFACE=1`
intentional-rebless path).
