# Contract delta — `FS.GG.Governance.RouteCommand` (F046)

The `fsgg route` command now senses → resolves → evaluates and passes a **real** `CacheEligibilityReport`
to the F045 embed. This is a **Tier 1** surface change (new `Loop` `Effect`/`Msg`/`Model`/`RunRequest`
members + new `Interpreter.Ports` members). `route.json` keeps schema `fsgg.route/v2` — only its cache
section flips from *not-evaluated* to *evaluated*.

## `Loop.fsi` additions

```fsharp
// RunRequest gains:
StorePath: string                              // --store, default: under repo "readiness/evidence-reuse.json" (D6)

// Effect gains:
| SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
| LoadStore of path: string

// Msg gains:
| FreshnessSensed of Result<SensedFacts, string>
| StoreLoaded of Result<ReuseStore, string>

// Phase gains (between Loaded' and Projected):
| Selected

// Model gains:
Snapshot: RepoSnapshot option                  // D5 — kept to derive base/head
SelectedGates: Gate list
Sensed: SensedFacts option
Store: ReuseStore option
CacheNotes: string list                        // D7 — non-fatal degrade notes
```

`exitCode` is unchanged: `Success 0 | UsageError' 2 | InputUnavailable 3 | ToolError 4` — **no cache code**,
`fsgg route` still always exits 0 on a successful run (FR-008).

## `Loop.fs` behavior

- `Sensed (Ok snapshot)` — also store `Snapshot = Some snapshot` (today it is dropped).
- `Loaded (Valid facts)` — compute `result`/`registry`/`gatesDoc`/`selectedGates` as today, store them, set
  `Phase = Selected`, and emit `[ SenseFreshness(selectedGates, baseHeadOf model); LoadStore request.StorePath ]`.
  **No write is emitted here anymore.**
- `FreshnessSensed (Ok facts)` → `tryProject { m with Sensed = Some facts }`.
- `FreshnessSensed (Error reason)` → `tryProject { m with Sensed = Some emptySensedFacts; CacheNotes = … }`
  (degrade, **no fail** — D2).
- `StoreLoaded (Ok store)` → `tryProject { m with Store = Some store }`.
- `StoreLoaded (Error reason)` → `tryProject { m with Store = Some EvidenceReuse.empty; CacheNotes = … }`
  (degrade, **no fail** — D2).
- `tryProject` (fires when `Sensed` and `Store` both `Some`): build `cacheReport` (data-model §3),
  `routeDoc = RouteJson.ofRouteResult result (Some cacheReport)`, set `Phase = Projected`, emit
  `[ WriteArtifact(GatesArtifact, …, gatesDoc); WriteArtifact(RouteArtifact, …, routeDoc) ]` — the existing
  two-write counter dance in `Wrote(_, Ok())` (`Projected → Persisted → summary`) is preserved.
- `render` (success): add the cache summary — reusable/must-recompute/unresolved gate lines (the F044
  pattern via `CacheEligibility.entries` + `FreshnessResolution.missingFacts`) plus any `CacheNotes`.

## `Interpreter.fsi` / `.fs`

```fsharp
// Ports gains:
Freshness: FreshnessSensing.FreshnessSensor
Store: FreshnessSensing.StoreReader
```

- `step` handles `SenseFreshness(gates, baseHead)` via `FreshnessSensing.senseFreshness ports.Freshness gates baseHead`
  → `FreshnessSensed`, and `LoadStore path` via `FreshnessSensing.loadStore ports.Store path` → `StoreLoaded`.
- `realPorts repo` adds `Freshness = FreshnessSensing.realSensor repo`, `Store = FreshnessSensing.realStoreReader`.

## `.fsproj`

Add `ProjectReference`s: `FreshnessSensing`, `CacheEligibility`, `FreshnessResolution`, `EvidenceReuse`,
`FreshnessKey`. No third-party `PackageReference`. `RouteCommand` stays the packable `fsgg` tool.

## Tests

`Support.fs:259` and `LoopTests.fs:50` recompute the expected `route.json` with `Some expectedReport`
(built through the public chain over the faked sensor facts; empty faked store ⇒ all `mustRecompute
noPriorEvidence`). Add US3 degrade tests and re-bless `surface/FS.GG.Governance.RouteCommand.surface.txt`.
