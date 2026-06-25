# Phase 0 Research: The `fsgg refresh` Host Command

All NEEDS CLARIFICATION from the Technical Context are resolved below. Each decision records what was
chosen, why, and the alternatives rejected. The spec deferred four things explicitly to planning: the
generation-manifest representation, the precise reuse of the freshness/provenance machinery, the precise
reuse of the rendering modules, and the precise numeric exit-code assignment.

## D1 — How is staleness decided (currency comparator)?

**Decision**: A view is stale iff `FreshnessKey.matches recorded current = false`, where `recorded` and
`current` are `FS.GG.Governance.FreshnessKey.FreshnessInputs` values built **per view** that differ only in
the **source-digest set** (`CoveredArtifacts`) and the **`GeneratorVersion`**; the revision fields
(`Base`/`Head`) are held **equal** between the two. `FreshnessKey.diff recorded current` yields the changed
`InputCategory` list (`CoveredArtifactsCat`, `GeneratorVersionCat`) used for the human/`--dry-run` reason
and the `refresh.json` `drifted` field. `recorded` is reconstructed from the view's recorded provenance
(see D4); `current` is built from freshly-sensed digests of the view's *declared* sources (see D2).

**Rationale**:
- This **reuses the existing machinery rather than introducing a new staleness mechanism** (FR-002): the
  comparator (`compute`/`matches`/`diff`) and its `InputCategory` vocabulary are F029, used verbatim.
- It decides currency **by digest and generator version, not by file presence** (FR-002, SC-002): the
  inputs carry hashes and a version string, never an mtime or an existence flag.
- **Holding `Base`/`Head` equal is the crux** that separates *view currency* from *gate-evidence reuse*.
  `EvidenceReuse.decide`/`FreshnessInputs` are correctly keyed *per change* (Base/Head vary every commit),
  which is right for "can I reuse a gate's evidence for THIS diff" but **wrong** for "is this generated view
  current with its sources" — a view does not go stale merely because `HEAD` advanced. Neutralizing the
  revision fields makes currency depend only on *sources and generator*, which is the spec's currency basis.

**Alternatives rejected**:
- *Reuse `EvidenceReuse.decide` + `ReuseStore` verbatim, revisions included.* Rejected: it would mark every
  view stale after any commit (Base/Head drift), contradicting SC-002's "re-run regenerates nothing."
- *A brand-new digest-set comparison type.* Rejected: it would be the "new staleness mechanism" FR-002
  forbids; `FreshnessKey` already expresses exactly digest-set + generator-version equality with a
  category-level `diff`.
- *Compare on the full F030 `Provenance` record (git revisions, builder, environment).* Rejected: F030
  `Provenance` is *change*-provenance (it demands `SourceCommit`/`Base`/`Head`/`Builder`/`Environment`),
  over-specified for view currency, and would re-introduce the revision-coupling D1 exists to avoid.

## D2 — How are per-view source digests sensed (without editing frozen cores)?

**Decision**: A thin **row-local edge helper** in `Interpreter` computes a content digest (SHA-256, the
same notion `FreshnessSensing.realSensor` already uses for `CoveredArtifacts`) over each *declared* source
path of a manifest entry, in declared order, yielding the `ArtifactHash list` that becomes the view's
`CoveredArtifacts`. The `GeneratorVersion` is sensed from the declared generator-version basis (e.g. the
generator command's reported version, or a declared static version token in the manifest — product-neutral,
read from the repository). An absent/unreadable declared source ⇒ the helper returns a sense failure that
the pure `update` turns into a `stale-unresolved` finding (FR-010), never a fabricated digest.

**Rationale**: Per-view source digesting is genuinely row-local — the frozen `FreshnessSensing.realSensor`
senses *repository-wide* facts (`RuleHash` over all `.fsgg/*.yml`, `CoveredArtifacts` over all `src/**`),
not the digest of one declared source file. Adding a per-path digest helper at the **edge** keeps the
frozen cores untouched (the spec's "row-local surface, frozen cores untouched") while reusing the identical
hash primitive, so the currency notion stays consistent with the rest of the freshness machinery.

**Alternatives rejected**:
- *Edit `FreshnessSensing` to expose per-path digesting.* Rejected: edits a frozen core for a row-local
  need; the spec freezes F046 and reuses it verbatim.
- *Digest by file mtime / size.* Rejected: violates FR-002 "by digest, not presence/mtime."

## D3 — How are views regenerated product-neutrally (which renderers are reused)?

**Decision**: Regeneration is an **injected edge effect** (`RegenerateView of GenerationEntry`). The pure
`update` decides *which* views to regenerate and emits the effect; the edge `Interpreter` realizes it by
running the view's **declared generator** (a command in the manifest entry) through the F051 `GateExecution`
/ F052 `GateRun` process port, then committing the produced bytes with the atomic temp-then-rename writer.
The pure core names **no renderer and no product** (FR-011). The concrete renderers the spec lists — gate
metadata (`GatesJson`), rule catalogs, capability/API-surface docs, route projections (`RouteJson`),
committed surface baselines (the `BLESS_SURFACE=1 dotnet test` path) — are *examples a repository declares
in `refresh.yml`*, not symbols refresh references.

**Rationale**: The renderers are heterogeneous pure functions with incompatible input types
(`GateRegistry` vs `RouteResult` vs reflective surface walks); a product-neutral command cannot call them
uniformly without naming them, which FR-011 forbids. Driving regeneration through a *declared command* (a)
keeps the core product-neutral, (b) reuses the **same execution port** `ship`/`verify` already use to run
declared commands, and (c) matches how the repository regenerates its own views today (surface baselines via
`BLESS_SURFACE=1`, the CI guidance template via a script). On success the edge re-senses the view's source
digests + generator version and records refreshed provenance (D4); a non-zero generator exit or write
failure is a `ToolError` (exit `4`) with no partial view left behind.

**Alternatives rejected**:
- *An in-process renderer registry mapping a manifest `renderer-id` to `GatesJson.ofGateRegistry` et al.*
  Rejected: it would either name the product renderers in core (violating FR-011) or require the edge to
  build each renderer's bespoke input (a `GateRegistry`, a `RouteResult`) — large, product-coupled, and
  redundant with the existing execution port.
- *Refresh shells out to a hardcoded set of `fsgg` sub-commands.* Rejected: hardcodes view/product
  identity (FR-011) and assumes a dispatcher that does not exist.

## D4 — Where does recorded provenance live, and what does it record?

**Decision**: Each view's recorded provenance is a **generated companion record** — the source digests, the
generator version, and the output digest captured at the last successful generation — written by refresh on
regeneration and **never authored by hand**. It is keyed by view identity. The store is persisted next to
the manifest (a row-local generated lock, e.g. `.fsgg/refresh.lock.json`, written atomically and
deterministically), **reusing the F048 `EvidenceReuseStore` serialization if its record shape fits the
view-currency triple, otherwise a minimal row-local lock**. The recorded triple is exactly what D1 needs to
reconstruct the `recorded` `FreshnessInputs` and what the SDD `GenerationManifest` precedent records
(`Sources: SourceIdentity list` with `Digest`, `Generator`, `OutputDigest`).

**Rationale**: Separating the **authored** manifest (`refresh.yml` — declares relationships; never mutated)
from the **generated** recorded provenance (the lock — written by refresh) keeps refresh from rewriting an
authored file (cleaner FR-013 story) and makes the recorded provenance itself a generated view governed by
the same currency rules. Writing the lock is the "recorded provenance … written atomically" FR-013(a)
explicitly permits, and the "opt-in persistence the shared cores already perform" FR-013(c) anticipates.

**Alternatives rejected**:
- *Record provenance inside `refresh.yml` (the SDD GenerationManifest places sources+digests in one file).*
  Rejected here: it forces refresh to deterministically rewrite an authored YAML file in place, mixing
  authored and generated content in one artifact and complicating the no-mutation-of-authored-input story.
- *Recompute from git history each run.* Rejected: requires git sensing this row deliberately avoids (D1)
  and cannot express "generator version changed."

## D5 — Exit-code contract (six distinguishable outcomes)

**Decision**: Six distinguishable codes, extending the `release`/`verify`/`ship` five-way family with a
second success shade (FR-009; the spec authorizes the precise numeric assignment as a planning decision):

| Code | Outcome | Meaning |
|---|---|---|
| `0` | nothing-to-refresh | every in-scope view current (or empty manifest) — the canonical "clean" success (SC-002) |
| `1` | stale-unresolved | ≥1 view stale and could not be brought current — the blocking analogue; CI fails on it |
| `2` | UsageError | unknown flag, missing value, mutually exclusive selectors |
| `3` | InputUnavailable | absent/malformed/unreadable manifest or declared source — cannot evaluate currency |
| `4` | ToolError | a genuine generator/IO defect during regeneration or write |
| `5` | views-regenerated | success; ≥1 stale view was brought current (write mode) **or** would be (`--dry-run`) |

**Rationale**: Distinguishability is FR-009/SC-005's explicit ask — automation must tell *all-current* from
*regenerated* from *unresolved*. Keeping `0` for nothing-to-refresh satisfies SC-002 verbatim ("re-run …
exits with the 'nothing to refresh' success code"). Mapping `2`/`3`/`4` identically to the sibling family
preserves muscle memory and the shared `categoryToken` story. `5` (a *success* that is non-zero) is
deliberate and documented: a `--dry-run` in CI returning `5` means "stale views exist — commit a refresh,"
the same ergonomics as a formatter's "would reformat" code; `1` (stale-**un**resolved) is the genuinely
*failing* outcome a CI job blocks on. The SDD `fsgg-sdd refresh` precedent distinguishes the same shades
via `outcome` (`noChange`/`succeeded`/`succeededWithWarnings`/`blocked`) — this row projects that
distinction onto the Governance five-way exit family.

**Alternatives rejected**:
- *Fold both success shades into `0`, distinguish only in the report.* Rejected: FR-009 says the **codes**
  must distinguish them, and SC-002 names a specific "nothing to refresh" code.
- *Map views-regenerated to `1`.* Rejected: collides with the blocking/unresolved analogue and would make
  an ordinary successful refresh look like a failure to naive `if cmd; then` CI.

## D6 — Scoping the refresh to a subset of views

**Decision**: An optional, documented selector narrows the in-scope view set — `--view-kind <kind>` and/or
`--view <id>` (and the spec's "work-item id" form where a manifest entry declares one). No selector ⇒ the
documented default scope: **all declared views**. Mutually exclusive or unparseable selectors ⇒ `UsageError`
(exit `2`). Out-of-scope views are reported as **not-evaluated** (a distinct per-view status), never
silently assumed current (FR-015, edge "Partial scope selection").

**Rationale**: Directly satisfies FR-015 and the "Partial scope selection" edge case. Mirrors the
sibling commands' flag-parsing discipline (closed `Result<RunRequest, UsageError>` parse).

**Alternatives rejected**: *No scoping (always all views).* Rejected: FR-015 mandates a documented scoping
mechanism. *Silently skipping out-of-scope views as current.* Rejected: FR-015 forbids it.

## D7 — Determinism of regenerated views and `refresh.json`

**Decision**: `refresh.json` is emitted by a pure `RefreshJson.ofRefreshDecision` via a hand-driven
`Utf8JsonWriter` walk (the `AuditJson`/`ReleaseJson` precedent): no timestamps, no absolute paths, no
usernames, stable key/array order, a versioned `schemaVersion = "fsgg.refresh/v1"`. The printed machine
output is the verbatim persisted file (one source of truth — `--json` prints exactly what is written).
Regenerated view *bytes* are deterministic to the extent the *declared generator* is deterministic; the
contract asserts byte-identical regeneration across runs over identical state for the in-repo fixture
generators (which are deterministic), and the recorded-provenance lock is written deterministically.

**Rationale**: FR-007/FR-008/SC-004 verbatim. The pure projection + `Utf8JsonWriter` precedent is proven
five times over in this repo (`AuditJson`…`VerifyJson`).

**Alternatives rejected**: *`JsonSerializer` reflection-based serialization.* Rejected: key order and
formatting are not guaranteed byte-stable across runtimes; the repo standard is the explicit writer walk.

## Reused-core inventory (named functions)

| Concern | Reused symbol | Project |
|---|---|---|
| Read `.fsgg/refresh.yml` | `Loader.FileReader` / `Loader.fileSystemReader` | F014 `Config` |
| Currency comparator | `FreshnessKey.compute` / `matches` / `diff` / `categoryToken` | F029 `FreshnessKey` |
| Run a declared generator | `GateExecution` port + `GateRun` | F051 / F052 |
| Recorded-provenance store (if it fits) | `EvidenceReuseStore` serialization | F048 |
| Deterministic JSON walk pattern | (precedent only) `AuditJson` / `ReleaseJson` | — |
| Atomic temp-then-rename write | (precedent only) `ReleaseCommand.Interpreter.writeAtomic` | — |

**Not taken**: F016 `Snapshot` (no git sensing — D1), F030 `Provenance` (over-specified — D1), the
gate-selection/rollup chain (F015–F024 — refresh selects no gates and rolls up no verdict), YamlDotNet is
*reused not added*.
