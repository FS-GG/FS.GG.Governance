# Phase 0 Research — Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship`

All Technical Context items were known from the merged cache-eligibility thread (F029–F045) and the two
merged host commands (F022 `RouteCommand`, F026 `ShipCommand`) plus the standalone cache command (F044
`CacheEligibilityCommand`). No `NEEDS CLARIFICATION` remained. The decisions below resolve the two
plan-time mechanism points the spec deferred and pin the wiring rules.

## D1 — Share the freshness-sensing edge via a NEW library `FS.GG.Governance.FreshnessSensing`

**Decision**: Extract the freshness-sensing edge — the `FreshnessSensor` / `StoreReader` port types, the
real SHA-256 catalog/source sensing (`senseCatalogHash`, `senseSrcHashes`, `toolVersion`, `sha256Hex`), the
read-only `fsgg.evidence-reuse-store/v1` deserializer (`parseStore`/`parseEntry`/`realStoreReader`), and the
`SensedFacts` assembly (the body of F044's `SenseFreshness` handler) — into a new shared edge library
`FS.GG.Governance.FreshnessSensing`. `RouteCommand` and `ShipCommand` reference it. **F044 is left untouched
this row** (FR-014, SC-008); converging its inline copy onto the shared library is an explicit, bounded
deferred follow-up.

**Rationale**: Maintainer-confirmed this session (AskUserQuestion, over duplicating into both commands).
Without extraction the ~80-line sensing edge would exist in three copies (F044 + route + ship) of code the
spec itself flags as an MVP "documented later refinement" — exactly the duplication the constitution's DRY
and dependency-minimalism guidance discourages. A shared library gives the two new consumers one canonical
home and one place to refine sensing later. FR-014 forbids editing F044, so the shared library + F044's
inline copy temporarily coexist; that bounded duplication is the cost of keeping F044 byte-stable this row.

**Alternatives considered**: (a) **Duplicate into both commands** — no new project, lowest upfront risk, but
yields three copies; rejected by the maintainer. (b) **Reference F044's `CacheEligibilityCommand` from
route/ship** — a host→host dependency, and F044 exposes no "build just the sensor" function; rejected as an
inverted, ugly coupling. (c) **Refactor F044 onto a shared library in THIS row** — the cleanest end state but
edits F044, violating FR-014/SC-008; deferred to a bounded follow-up.

## D2 — Degrade, don't fail, on sense/store errors (the one divergence from F044) — IN the pure `update`

**Decision**: Unlike F044 (a standalone cache command where a freshness-sense failure or a malformed store is
legitimately a fatal `ToolError`), the route/ship pure `update` **degrades**:

- `FreshnessSensed (Error reason)` → substitute an **empty `SensedFacts`** (`RuleHash=None`,
  `GeneratorVersion=None`, `Base=None`, `Head=None`, `CoveredArtifacts=Map.empty`,
  `CommandVersions=Map.empty`) so every gate resolves *unresolved* (⇒ absent from candidates ⇒ F045
  `notEvaluated`), record a non-fatal cache note, and continue to `tryProject`.
- `StoreLoaded (Error reason)` → substitute `EvidenceReuse.empty` so every resolved gate is `mustRecompute
  noPriorEvidence`, record a non-fatal cache note, and continue.

Neither path calls `fail`; the command's `ExitDecision` is untouched. The faithful `Result` is still
produced by the interpreter (`FreshnessSensing.senseFreshness` / `loadStore` return `Error` on a real
failure) — only the **policy** of what to do with it differs, and that policy lives in the pure `update` so
it is a testable value transition.

**Rationale**: Spec FR-010/FR-011 require that wiring cache sensing into the safety-critical commands
introduces **no new failure mode** and never changes the exit code; cache eligibility is information, not a
verdict. Keeping the degrade in `update` (not the interpreter) matches Principle IV (policy is pure,
testable) and Principle VI (the failure is surfaced as a note, never swallowed).

**Alternatives considered**: degrade inside the interpreter `step` (hides policy from pure tests; rejected);
keep F044's `fail ToolError` (violates FR-010/FR-011; rejected).

## D3 — route/ship ALWAYS pass `Some report` after this row

**Decision**: Both commands always reach the cache step and always pass `Some report` to the F045 embed; the
`None` (not-evaluated) callsite is removed. `cacheEligibilityEvaluated` is therefore always `true` in
route/ship output. The degraded paths (D2) still produce a `Some report` (an evaluated report whose gates
are all `notEvaluated` / `mustRecompute`), not `None`.

**Rationale**: FR-006/FR-007 require `Some report` on a successful sense; the cache step now always runs, so
there is no route/ship path that legitimately means "no cache step ran." `None` remains a supported F045
input (its own tests keep exercising it, SC-008) but is unused by route/ship.

## D4 — Build the report in a pure `tryProject` join (the exact F044 shape)

**Decision**: Mirror F044's `tryProject`: a pure helper that fires once **both** `model.Sensed` and
`model.Store` are `Some`, computing
`report = FreshnessResolution.resolve selectedGates sensed |> FreshnessResolution.entries |> List.choose FreshnessResolution.candidate |> fun cands -> CacheEligibility.evaluate cands store`,
then projecting with `Some report`. `selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)`
(verbatim F044). Unresolved gates drop out of `candidate` ⇒ absent from the report ⇒ F045 renders them
`notEvaluated`; the summary additionally names their missing facts (FreshnessResolution `missingFacts` /
`missingFactToken`, the F044 pattern).

**Rationale**: This is the already-proven composition; reusing its exact shape minimizes risk and keeps the
cores consumed verbatim (FR-013, no re-derivation). The join's two-input barrier (`Some, Some`) is the
F044-tested way to wait for both async effects without a counter.

## D5 — Keep the `RepoSnapshot` in the command `Model` to derive base/head

**Decision**: Add a `Snapshot: RepoSnapshot option` field to both command `Model`s and store it on
`Sensed (Ok snapshot)` (today RouteCommand/ShipCommand extract `Candidates` and drop the snapshot). Derive
base/head exactly as F044's `baseHeadOf` does: `model.Snapshot |> Option.bind (fun s -> s.Range)` →
`Some (Revision r.Base, Revision r.Head)` else `None, None`. `ExplicitPaths` scope ⇒ no snapshot ⇒ base/head
`None` (the F044 L2 path) ⇒ gates unresolved on base/head.

**Rationale**: F043 `resolve` needs base/head; they come from the snapshot range, not the freshness sensor
(D4/F044 invariant: base/head are passed through, never re-sensed or fabricated). Keeping the snapshot is the
minimal change to make them available.

## D6 — Store path / CLI: mirror F044 verbatim (`--store`, default `readiness/evidence-reuse.json`)

**Decision**: Add `StorePath: string` to both `RunRequest`s, a `--store <path>` flag to both `parse`
functions, and a default of `under repo "readiness/evidence-reuse.json"` (the F044 default), loaded
**read-only**. Absent file ⇒ `EvidenceReuse.empty` (FR-005).

**Rationale**: Consistency with the standalone F044 command (same flag, same default path, same store schema
`fsgg.evidence-reuse-store/v1`) so a store written by a future store-writing row is found identically by all
three commands. The safe default (absent ⇒ empty ⇒ recompute-by-default) holds whether or not `--store` is
passed.

## D7 — Surface non-fatal cache degradations via a `CacheNotes` field, rendered in the success summary

**Decision**: Add `CacheNotes: string list` to both command `Model`s. The degrade transitions (D2) append a
note (e.g. `"cache store unreadable: <reason> — treating as empty (recompute-by-default)"`,
`"freshness sensing failed: <reason> — all gates recompute-by-default"`). The **success** summary renders
these notes plus the F044-style unresolved-gate lines (gate id + missing facts). The existing `Diagnostics`
field (rendered only on the `None`-result/failure path) is unchanged and stays fatal-only.

**Rationale**: FR-015 requires the summary to reflect the cache outcome including any store-read failure and
any unresolved gates; FR-011 requires the read failure surfaced. A dedicated non-fatal channel keeps the
fatal `Diagnostics` semantics intact (they still map to a non-zero exit) while honestly reporting the
degrade (Principle VI, no swallowed failure).

## D8 — MVU phase additions (minimal, mirroring F044)

**Decision**:
- **Both commands** gain `Effect` cases `SenseFreshness of Gate list * (Revision option * Revision option)`
  and `LoadStore of path: string`; `Msg` cases `FreshnessSensed of Result<SensedFacts,string>` and
  `StoreLoaded of Result<ReuseStore,string>`; `Model` fields `Snapshot`, `SelectedGates`, `Sensed`,
  `Store`, `CacheNotes`.
- **RouteCommand**: `Loaded (Valid facts)` computes `result`/`registry`/`gatesDoc`/`selectedGates`, stores
  them, and emits `[SenseFreshness(selectedGates, baseHead); LoadStore storePath]` (Phase → `Selected`) —
  it **no longer writes** in this step. `tryProject` (on the second of the two responses) builds the report,
  `routeDoc = ofRouteResult result (Some report)`, and emits the **two** writes (gates + route) together —
  the existing two-write `Wrote(_,Ok())` counter dance (`Projected → Persisted → summary`) is preserved.
- **ShipCommand**: `Loaded (Valid facts)` computes `result`/`decision`/`selectedGates`, stores them, emits
  the same two sensing effects (Phase → a pre-projection phase). `tryProject` builds the report,
  `auditDoc = ofShipDecision decision (Some report)`, and emits the **single** audit write (Phase →
  `Rolled`). The `Wrote(_,Ok())` → summary and the `Emitted` → **exit-from-basis** mapping are **unchanged**
  (FR-009).

**Rationale**: The smallest extension that inserts the sense+store barrier before projection while preserving
each command's existing write/exit choreography. ShipCommand's exit semantics are the safety-critical part
and are deliberately left byte-for-byte intact.

## D9 — Test strategy (real evidence; faked ports disclosed `Synthetic`)

**Decision**:
- **New `FreshnessSensing.Tests`**: drive `realSensor`/`loadStore` against a real temp directory — real
  `.fsgg/*.yml` + `src/**` bytes (assert stable SHA-256 ordering), store absent ⇒ `Ok None` ⇒ caller maps to
  empty, present well-formed ⇒ parsed, present malformed ⇒ `Error`.
- **RouteCommand.Tests / ShipCommand.Tests**: add faked `Freshness`/`Store` ports (fixed literal hashes —
  `Synthetic`, disclosed). Update the four `None` callsites (`RouteCommand` `Support.fs:259`,
  `LoopTests.fs:50`; `ShipCommand` `Support.fs:265`, `LoopTests.fs:50`) to recompute the expected document
  **live** with `Some expectedReport`, where `expectedReport` is built through the same public
  `resolve`→`candidate`→`evaluate` chain over the faked facts (empty faked store ⇒ every resolved gate
  `mustRecompute noPriorEvidence`). Add US3 degrade tests (a `None`-returning sensor accessor ⇒ a
  `notEvaluated` gate + a cache note, exit unchanged; a malformed-store reader ⇒ degrade-to-empty + a note,
  exit unchanged). Add the **SC-003 ship-invariant** test: the verdict, partition, every enforcement field,
  the `ExitCodeBasis`, and the numeric exit are value-identical to the pre-wiring projection of the same
  input (i.e. comparing the non-cache subtree + the exit, which is what `Some report` must not perturb).
- **Untouched**: `RouteJson.Tests`, `AuditJson.Tests` (F045's own, stay `None`), and the F028
  `EnforcementFixtures.Tests` generator (stays `None`; golden snapshots unaffected). SC-008.

**Rationale**: Principle V — real typed values and a real temp-dir sensor; the only synthetic surface is the
faked sensor's literal hashes, disclosed per the constitution. The SC-003 invariant test is the explicit
guard that the safety-critical ship path is unperturbed.

## D10 — No schema bump; F044, F045, and F028 baselines untouched

**Decision**: `route.json` stays `fsgg.route/v2` and `audit.json` stays `fsgg.audit/v2` (F045 already
bumped them). This row changes **no** projection-core byte; it only supplies a populated report where the
commands previously supplied `None`. F041/F042/F043/F045 cores, the F044 standalone command, the F042/F044
sidecar, and the F045 + F028 golden baselines are all unedited and un-re-blessed.

**Rationale**: The wire contract did not change shape — F045 already designed the v2 section to carry exactly
these verdicts. Populating it is not a contract change, so no version bump and no golden re-bless are
warranted (and SC-008 forbids them). The only re-blessed baselines are the three **surface** baselines whose
public `.fsi` genuinely changed (the new library + the two commands).

## Resolved unknowns

None outstanding. The MVP sensing coverage (`src/**` + `.fsgg/*.yml`, repo-wide per gate) and the coarse
command-version digest are carried verbatim from F044 as a documented later refinement (out of scope here),
and the real cache-store write/evict/expire row remains deferred.
