# Data Model: Persist The Evidence-Reuse Store To Disk From The Host Commands

This row adds **no new persisted schema** and **no new domain value type**. It adds MVU plumbing
(one request field, one effect, one message, one Model flag) to each host command and reuses F047's pure
operations and F030's `ReuseStore` value verbatim. The on-disk document is the existing
`fsgg.evidence-reuse-store/v1` shape — `EvidenceReuseStore.serialise` output, byte-for-byte the inverse of
`FreshnessSensing.realStoreReader`.

## 1. Reused values (unchanged — referenced, not redefined)

| Value | Source | Role this row |
|-------|--------|---------------|
| `ReuseStore` (`ReuseStore of RecordedEvidence list`) | F030 `EvidenceReuse.Model` | The loaded store (`model.Store`); input to prune/retain/serialise. |
| `RecordedEvidence` (`{ Inputs: FreshnessInputs; Evidence: EvidenceRef }`) | F030 | An opaque entry; removed-whole-only by prune/retain, never mutated. |
| `EvidenceReuse.empty` | F030 | The absent-file load result; persisted as a well-formed empty `v1` document. |
| `EvidenceReuseStore.prune : ReuseStore -> ReuseStore` | F047 | Drops strictly-superseded entries (FR-003). |
| `EvidenceReuseStore.retain : maxEntries:int -> ReuseStore -> ReuseStore` | F047 | Bounds to newest-first head (FR-003). |
| `EvidenceReuseStore.serialise : ReuseStore -> string` | F047 | Produces the `v1` document bytes (FR-002). |
| `EvidenceReuseStore.defaultRetentionBound : int` | F047 | The `retain` argument (D5). |
| `FreshnessSensing.realStoreReader : StoreReader` | F046 | Re-reads the persisted file in tests (round-trip) and in the next run. Unchanged. |
| `writeAtomic : string -> string -> Result<unit,string>` | RouteCommand/ShipCommand `Interpreter.fs` | Atomic temp+rename; the bound `Write` port reused for the store write (FR-001, D8). |

## 2. The persisted document derivation (pure)

A single pure expression, evaluated in `Loop.update` when persistence is enabled and the store is non-degraded:

```
persistedContent (loaded: ReuseStore) : string =
    loaded
    |> EvidenceReuseStore.prune                                   // remove strictly-superseded worlds
    |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound  // bound, newest-first
    |> EvidenceReuseStore.serialise                               // -> fsgg.evidence-reuse-store/v1 bytes
```

Properties (all inherited from F047, asserted here as host-level acceptance):

- **Lossless w.r.t. survivors (FR-002, SC-003)**: every entry in the output is byte-for-byte one of `loaded`'s
  entries; the host removes whole entries only, never mutates or fabricates one.
- **Deterministic / byte-stable (SC-002)**: same `loaded` value → byte-identical output on every run/machine.
- **Bounded + pruned (FR-003, SC-003)**: output is within `defaultRetentionBound` and free of strictly-superseded
  entries, newest-first.
- **Recompute-safe**: no operation turns a `mustRecompute` candidate into a `reusable` one (F047 invariant);
  hence persisting cannot cause a stale reuse in the next run.

## 3. MVU additions per command (RouteCommand and ShipCommand, mirror each other)

### 3a. `RunRequest` — one new field

```fsharp
type RunRequest =
    { // ...existing fields (Repo, Scope, Format, GatesOut/AuditOut, RouteOut, StorePath)...
      PersistStore: bool }   // NEW — opt-in trigger; default false (FR-004, D9)
```

Parsed from a new `--persist-store` boolean flag in each command's existing argv parser; absent ⇒ `false`.
The store **path** is the existing `StorePath` (`--store`, default `<repo>/readiness/evidence-reuse.json`) —
no new path discovery (FR-007).

### 3b. `Effect` — one new case

```fsharp
type Effect =
    | // ...existing (SenseScope, LoadCatalog, SenseFreshness, LoadStore, WriteArtifact, EmitSummary)...
    | PersistStore of path: string * content: string   // NEW — atomic write of the serialised store (D3)
```

`content` is the precomputed `persistedContent` string (decision lives in `update`, FR-010/D2). Emitted only
when `PersistStore = true` **and** the store did not degrade on load (D6).

### 3c. `Msg` — one new case

```fsharp
type Msg =
    | // ...existing (Begin, Sensed, Loaded, FreshnessSensed, StoreLoaded, Wrote, Emitted)...
    | StorePersisted of Result<unit, string>   // NEW — non-fatal store-write ack (D3)
```

Distinct from `Wrote`: `StorePersisted(Error _)` is **non-fatal** (never `ToolError`); it appends a cache note
and leaves `Exit` and emitted artifacts unchanged (FR-006).

### 3d. `Model` — degrade + completeness tracking

```fsharp
type Model =
    { // ...existing fields, incl. Store: ReuseStore option, CacheNotes: string list, Exit: ExitDecision...
      StoreDegraded: bool      // NEW — set true on StoreLoaded(Error _); suppresses the write (D6)
      PersistAcked: bool }     // NEW — gates EmitSummary when persistence is enabled (D10)
```

(Whether these are literal new fields or folded into the existing phase/ack representation is an implementation
choice; the contract is: the write is suppressed on degrade, and `EmitSummary` waits for the persist ack when
enabled.)

## 4. Transition flow (delta over the F046 flow)

```
StoreLoaded(Ok store)      → Store = Some store;  StoreDegraded = false;  tryProject
StoreLoaded(Error reason)  → Store = Some empty;  StoreDegraded = true;
                             CacheNotes += "reuse store unreadable (…); treated as empty …"  (F046, unchanged)

tryProject  (fires when Sensed + Store + Result/Decision present):
    cacheReport = CacheEligibility.evaluate candidates (the LOADED store)     ← verdicts, UNCHANGED (FR-005)
    routeDoc/auditDoc = …embed cacheReport…                                    ← UNCHANGED
    effects = [ WriteArtifact … ; WriteArtifact … (route) | WriteArtifact (ship) ]
    + IF Request.PersistStore && not StoreDegraded:
          effects += PersistStore(Request.StorePath, persistedContent (loaded store))   ← NEW (D2/D4/D6)
      ELSE IF Request.PersistStore && StoreDegraded:
          CacheNotes += "store not persisted: on-disk store failed to parse; left untouched …"  ← NEW (D6)

PersistStore(path, content)   [interpreter] → ports.Write path content (writeAtomic) → StorePersisted result

StorePersisted(Ok ())     → PersistAcked = true                                 (no Exit change)
StorePersisted(Error r)   → PersistAcked = true;
                            CacheNotes += "store not persisted (…); run unaffected"   ← non-fatal (FR-006)

EmitSummary  → emitted only once artifact writes AND (PersistStore=false || PersistAcked) hold  (D10)
```

Exit code (unchanged by this row): route still `Success`(0) on success / existing codes for input/tool errors;
ship still governed solely by `ExitCodeBasis` (`Clean`→0 / `Blocked`→1). **No** path maps a `StorePersisted`
error to a non-zero exit (FR-006, SC-005).

## 5. Effects-boundary addition (impure, per command `Interpreter.step`)

```fsharp
| Loop.PersistStore(path, content) ->
    Loop.StorePersisted(guard (fun () -> ports.Write path content))   // reuse atomic writeAtomic (D8)
```

`guard` already reifies any thrown exception to `Error`, so the interpreter stays total. `ports.Write` is the
existing `writeAtomic` (parent-dir create, unique temp sibling, `File.Move(tmp, path, true)`) — a failed write
leaves no partial/truncated file (FR-001, SC-005).

## 6. What is explicitly NOT changed

- The `fsgg.evidence-reuse-store/v1` schema, `schemaVersion`, and the reader's accepted shape (FR-009).
- The `route.json` / `audit.json` schema, content, and golden baselines (FR-005/FR-009; persistence-off is
  byte-identical, SC-006).
- The F029–F047 cores (FreshnessKey, EvidenceReuse, CacheEligibility, FreshnessResolution, EvidenceReuseStore,
  the Json projections) and their baselines.
- The verdict computation (`CacheEligibility.evaluate`) and its inputs — verdicts read the **loaded** store only.
- The F042/F044 standalone `cache-eligibility.json` sidecar (D7).
