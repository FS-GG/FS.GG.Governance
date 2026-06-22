# Phase 1 Data Model — Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship`

This row introduces **no new domain value type**. It introduces one shared **edge** surface
(`FreshnessSensing`), extends the two host commands' MVU vocabulary, and composes already-merged cores. The
"data" here is the sense → resolve → evaluate → embed pipeline and the two commands' enriched `Model`.

## 1. Reused cores (consumed verbatim — no edits)

| Source | Used for |
|---|---|
| F014 `Config` | `CommandId`, `GovernedPath`, `Validation`/`Valid`/`Invalid` (already used by the commands) |
| F016 `Snapshot` | `RepoSnapshot`, `DiffRange`, `CommitId` — base/head come from `RepoSnapshot.Range` |
| F018 `Gates` | `Gate`, `GateId`, `gateIdValue`; `Gate.FreshnessKey` carries the 5-field identity F043 reads |
| F019 `Route` | `RouteResult`, `SelectedGate`; `result.SelectedGates |> List.map (fun sg -> sg.Gate)` = the gates to sense |
| F029 `FreshnessKey` | `RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision`, `FreshnessInputs`, `InputCategory`/`categoryToken` |
| F030 `EvidenceReuse` | `ReuseStore`, `RecordedEvidence`, `EvidenceRef`, `RecomputeCause`, `empty`, `referenceValue` |
| F041 `CacheEligibility` | `evaluate : CandidateGate list -> ReuseStore -> CacheEligibilityReport`; `entries`; `CacheEligibilityReport`/`Verdict`/`CandidateGate` |
| F043 `FreshnessResolution` | `SensedFacts`; `resolve : Gate list -> SensedFacts -> FreshnessResolutionReport`; `entries`; `candidate : entry -> CandidateGate option`; `isResolved`; `missingFacts`; `missingFactToken` |
| F045 `RouteJson` / `AuditJson` | `ofRouteResult : RouteResult -> CacheEligibilityReport option -> string`; `ofShipDecision : ShipDecision -> CacheEligibilityReport option -> string` (the embed; called with `Some report`) |

## 2. New shared edge surface — `FS.GG.Governance.FreshnessSensing`

A pure-`.fsi` impure edge library (BCL crypto + `System.IO` + `System.Text.Json`; no third-party dep). It
owns the sensing the two commands previously lacked, extracted from F044's interpreter (D1). See
[contracts/FreshnessSensing.fsi](./contracts/FreshnessSensing.fsi) for the exact signature.

| Member | Shape | Meaning |
|---|---|---|
| `FreshnessSensor` | record of four `option`-returning accessors | `SenseRuleHash`, `SenseGeneratorVersion`, `SenseCoveredArtifacts: Gate -> ArtifactHash list option`, `SenseCommandVersion: CommandId -> CommandVersion option`. `Some []` = sensed-empty (resolves); `None` = unsensed (unresolved on that input). Never fabricated. |
| `StoreReader` | `string -> Result<ReuseStore option, string>` | `Ok None` = file ABSENT; `Ok (Some store)` = present + well-formed; `Error reason` = present + malformed. |
| `realSensor` | `repo:string -> FreshnessSensor` | Real SHA-256 over `.fsgg/*.yml` (rule hash) + `src/**` (covered artifacts), assembly version (generator), coarse command-version digest — the F044 MVP, carried verbatim. |
| `realStoreReader` | `StoreReader` | Reads + deserializes `fsgg.evidence-reuse-store/v1` read-only. |
| `senseFreshness` | `FreshnessSensor -> Gate list -> (Revision option * Revision option) -> Result<SensedFacts, string>` | The `SensedFacts` assembly (F044's `SenseFreshness` handler body): present Map key = sensed (even if empty); absent key = unsensed. Base/head passed through from the caller. Total/guarded. |
| `loadStore` | `StoreReader -> string -> Result<ReuseStore, string>` | `Ok None ⇒ Ok EvidenceReuse.empty`; `Ok (Some s) ⇒ Ok s`; `Error e ⇒ Error e` (the caller decides degrade policy, D2). |

**Note**: `senseFreshness`/`loadStore` return faithful `Result`s; the **degrade-to-safe-default policy lives
in the command `update`** (D2), not here, so it is a pure, testable transition.

## 3. The sense → resolve → evaluate → embed join (the pure `tryProject`, D4)

Identical in both commands (modulo the projected document):

```text
selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)        // verbatim F044
baseHead      = model.Snapshot |> Option.bind (fun s -> s.Range)            // D5
                |> function Some r -> Some(Revision r.Base), Some(Revision r.Head) | None -> None, None

-- effects emitted by Loaded(Valid):  [ SenseFreshness(selectedGates, baseHead); LoadStore storePath ]

-- tryProject fires when BOTH model.Sensed and model.Store are Some:
report   = FreshnessResolution.resolve selectedGates sensed
candidates = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
cacheReport = CacheEligibility.evaluate candidates store                    // : CacheEligibilityReport

-- route:  routeDoc = RouteJson.ofRouteResult result (Some cacheReport)
-- ship:   auditDoc = AuditJson.ofShipDecision decision (Some cacheReport)
```

A gate that resolves → a `CandidateGate` → an F041 verdict in the report → F045 renders `reusable` /
`mustRecompute`. A gate that **does not** resolve (missing facts) → dropped by `candidate` → absent from
the report → F045 renders `notEvaluated`; the summary names its missing facts.

## 4. Enriched command `Model` / `Msg` / `Effect`

Added to **both** `RouteCommand.Loop` and `ShipCommand.Loop` (see the contract deltas for exact `.fsi`):

```fsharp
// Effect (new cases)
| SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
| LoadStore of path: string

// Msg (new cases)
| FreshnessSensed of Result<SensedFacts, string>
| StoreLoaded of Result<ReuseStore, string>

// Model (new fields)
Snapshot: RepoSnapshot option        // D5 — kept to derive base/head
SelectedGates: Gate list             // the gates to sense, set at Loaded(Valid)
Sensed: SensedFacts option           // join input A
Store: ReuseStore option             // join input B
CacheNotes: string list              // D7 — non-fatal degrade notes for the summary

// RunRequest (new field)
StorePath: string                    // D6 — `--store`, default readiness/evidence-reuse.json
```

`RouteCommand` keeps `Result`/`GatesDoc`/`RouteDoc`; `ShipCommand` keeps `Decision`/`AuditDoc`. A `Phase`
case is added between `Loaded'` and the projection phase (`Selected`) in each.

## 5. The degrade transitions (D2 — the divergence from F044)

| Msg | F044 (standalone) | F046 route/ship |
|---|---|---|
| `FreshnessSensed (Ok facts)` | `tryProject { m with Sensed = Some facts }` | same |
| `FreshnessSensed (Error reason)` | `fail ToolError` | `tryProject { m with Sensed = Some emptySensedFacts; CacheNotes = m.CacheNotes @ [note] }` — **no fail** |
| `StoreLoaded (Ok store)` | `tryProject { m with Store = Some store }` | same |
| `StoreLoaded (Error reason)` | `fail ToolError` | `tryProject { m with Store = Some EvidenceReuse.empty; CacheNotes = m.CacheNotes @ [note] }` — **no fail** |

where `emptySensedFacts = { RuleHash=None; GeneratorVersion=None; Base=None; Head=None;
CoveredArtifacts=Map.empty; CommandVersions=Map.empty }`.

The command's existing fatal paths (`Sensed (Error _)` git failure ⇒ `InputUnavailable`, `Loaded (Invalid)`
⇒ existing category, `Wrote (_, Error _)` ⇒ `ToolError`) are **unchanged**. Cache degrades; the command's
own I/O still fails honestly.

## 6. Laws / invariants (each maps to a test — see research D9)

- **L1 (information, not verdict)** — for any input, the non-cache subtree of `route.json` / `audit.json` and
  the command's exit code are identical with `Some report` vs the pre-wiring `None` projection of the same
  routed change (modulo the now-populated section). Ship verdict/partition/enforcement/`ExitCodeBasis`/exit
  byte-or-value identical (SC-003, SC-004; FR-008, FR-009).
- **L2 (no new failure)** — a `None`-returning sensor accessor or a malformed store never changes the exit
  code and never throws; the document still emits (FR-010, FR-011; SC-006).
- **L3 (no-hide)** — every `mustRecompute` names its cause; an unresolved gate is `notEvaluated` (never
  silent `reusable`) and its missing facts appear in the summary (FR-010, FR-015; SC-005).
- **L4 (recompute-by-default)** — empty store ⇒ every resolved gate `mustRecompute noPriorEvidence`; no gate
  `reusable` unless its inputs fully resolved AND the read-only store matched (SC-005).
- **L5 (determinism)** — identical repo state ⇒ byte-identical documents including the cache section; cache
  verdicts follow the document's existing gate order (the F045 embed owns ordering) (FR-012; SC-007).
- **L6 (no derivation)** — the commands compute no freshness key/hash/cache decision, render no raw freshness
  input, and never dereference the opaque evidence reference (FR-013).
- **L7 (untouched)** — F041/F042/F043/F045 cores, F044 standalone command/sidecar, and the F045/F028 golden
  baselines are byte-unchanged; no schema bump (FR-014; SC-008; D10).
