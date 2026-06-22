# Phase 0 Research: Cache-Eligibility Host Command

**Feature**: `044-cache-eligibility-command` | **Date**: 2026-06-22

All NEEDS CLARIFICATION from the Technical Context are resolved below. Two decisions (D7 unresolved
representation, D8 verb) were maintainer-confirmed at plan time via the clarifying questions; the rest follow
from the merged-core surfaces and the `RouteCommand` (F022) precedent established in the codebase.

---

## D1 — One new host project mirroring `RouteCommand`, not an edit to any merged project

**Decision**: Deliver a new `Exe`/tool project `FS.GG.Governance.CacheEligibilityCommand` with the
`Loop.fsi/fs` (pure MVU) + `Interpreter.fsi/fs` (edge ports) + `Program.fs` shape of the merged
`FS.GG.Governance.RouteCommand` (F022). It owns the `cache-eligibility` verb of the shared `fsgg`
`ToolCommandName`.

**Rationale**: F022 already established this is the repo's edge-composition shape (pure `Loop` total `update`,
edge `Interpreter` with injected fakeable `Ports`, thin `Program`). The spec mandates a standalone command that
does not modify the merged `fsgg route` command or the F020/F025 cores (FR-011). A new project is purely
additive (SC-007/SC-008).

**Alternatives rejected**: (a) Add a subcommand inside `RouteCommand` — couples cache-eligibility to the route
artifact lifecycle and edits a merged project's surface. (b) Add it to the kernel-era `Cli`/`Host` projects —
different lineage (kernel MVU), and `Cli` already routes its own command family; reusing it would mean threading
the cache pipeline through an unrelated host.

---

## D2 — Reuse the F022 selection by **replicating its call-sequence**, not by referencing `RouteCommand`

**Decision**: Obtain the selected gates by calling the same merged cores `RouteCommand.Loop.update` calls, in
the same order, against the same `Snapshot`/`Config` ports: sense scope (`Snapshot`) → load+validate catalog
(`Config`) → `Routing.route facts candidates` → `Gates.buildRegistry facts` →
`Findings.findUnknownGovernedPaths facts report` → `Route.select registry report findings`. Take a
`ProjectReference` on each of `Config`, `Snapshot`, `Routing`, `Findings`, `Gates`, `Route`.

**Rationale**: `RouteCommand` exposes **no reusable selection function** — its composition lives inside its own
private `Loop.update`. Replicating the call-sequence reuses the **cores verbatim** (FR-001/FR-012: "no
selection logic of its own"); the ordering of merged-core calls is not new logic, it is the same calls. This is
exactly how F042/F043 reused upstream cores (call them, do not re-implement).

**Alternatives rejected**: `ProjectReference FS.GG.Governance.RouteCommand` — it is an `Exe` whose selection is
not surfaced; consuming it would couple this command to `RouteCommand`'s `RunRequest`/`Effect`/argv and pull its
`route.json`/`gates.json` projection into our reference graph for no benefit.

---

## D3 — Selected gates come as full `Gate` records off `RouteResult.SelectedGates`

**Decision**: After `Route.select` returns its `RouteResult`, the selected gates F043 needs are
`result.SelectedGates |> List.map (fun sg -> sg.Gate)` — full F018 `Gate` records, each carrying its five-field
`FreshnessKey`. `Gates.buildRegistry` returns `{ Gates: Gate list }` already in `GateId`-ordinal order, so the
selected list inherits that order.

**Rationale**: `RouteResult.SelectedGates : SelectedGate list` and `SelectedGate = { Gate: Gate;
SelectingPaths: SelectingPath list }` — the full record is present, no registry re-lookup needed. `resolve :
Gate list -> SensedFacts -> FreshnessResolutionReport` consumes exactly this. Duplicate selections (same
`GateId` via multiple paths) are preserved as the route result presents them — F043/F041 preserve duplicates
(completeness contract, Edge Cases).

**Verification**: `src/FS.GG.Governance.Route/Model.fs` (`RouteResult`/`SelectedGate`),
`src/FS.GG.Governance.Gates/Model.fs` (`GateRegistry = { Gates: Gate list }`), confirmed in exploration.

---

## D4 — A new injected `FreshnessSensor` port; base/head come **free** from `RepoSnapshot.Range`

**Decision**: Add one new edge port, `FreshnessSensor`, that senses the facts not carried on the gate's
`FreshnessKey` and not already produced by scope sensing: the rule-pack hash, the generator version, each
selected gate's covered-artifact hashes, and each declared command's command version. **Base and head
revisions are NOT a new port call** — they are read from the already-sensed `RepoSnapshot.Range : DiffRange
option` (`{ Base; Head; MergeBase }`, each a `CommitId`) produced by the reused `Snapshot` scope sensing, and
converted to F029 `Revision` newtypes at the boundary. When `Range = None` (e.g. a scope that produced no
range), `Base`/`Head` are left unsensed (`None`) and gates resolve unresolved on that basis (no-hide).

**Rationale**: The `Snapshot` git port already runs `RevParse`/`MergeBase` and exposes `DiffRange` — re-sensing
base/head would duplicate a git call and risk divergence. The remaining four facts have no existing producer, so
they are the genuine new sensing work, isolated behind one port so the pure `Loop` requests a single
`SenseFreshness of Gate list` effect and receives a `SensedFacts` bundle (or a failure). All hashing uses BCL
`System.Security.Cryptography` **inside the interpreter** (FR-013: the command computes no hash itself; the
injected boundary does).

**No-hide sensing rule** (FR-003/FR-005, US2): the `FreshnessSensor` returns `option`/`Map`-shaped facts where
**a fact it cannot sense is left `None` (repo-wide) or its key absent (per-gate/per-command)** — never
defaulted, zero-filled, or fabricated. A **sensed-empty** value (e.g. a gate that genuinely covers no artifacts
→ a present `Map` key with an empty `ArtifactHash list`) is distinguished from **unsensed** (absent key). This
is the exact `SensedFacts` option/Map contract F043 consumes.

**Scope of sensing in this row**: per the spec assumptions, command-version sensing is allowed to be minimal —
"where a declared command's version cannot be sensed cheaply at the host boundary, the corresponding fact is
left unsensed and the gate resolves unresolved on that basis (no-hide) rather than invoking arbitrary tools."
Likewise the per-gate covered-artifact derivation (which repo paths a gate covers) is encapsulated behind the
sensor; where it cannot be derived the key is left absent (unresolved, honestly), never invented. Richer
command-version / covered-artifact sensing is a later refinement; the contract this row fixes is the honest
no-hide boundary, not exhaustive sensing.

---

## D5 — `SensedFacts` assembled at the boundary; the pure `Loop` calls `resolve`/`candidate`/`evaluate`

**Decision**: The interpreter assembles the `FreshnessResolution.SensedFacts` record
(`{ RuleHash; GeneratorVersion; Base; Head; CoveredArtifacts: Map<GateId,_>; CommandVersions: Map<CommandId,_>
}`) from the `RepoSnapshot.Range` (Base/Head) plus the `FreshnessSensor` output, and hands it to the pure
`Loop` as the `SenseFreshness` result `Msg`. The pure `update` then: calls `FreshnessResolution.resolve gates
sensed`; for each entry, takes `candidate entry` (→ `CandidateGate option`); runs `CacheEligibility.evaluate
(candidates |> List.choose id) store`; renders the resolved verdicts via `CacheEligibilityJson.ofReport`; and
builds the unresolved sidecar from the entries whose `outcome` is `Unresolved` using
`missingFacts`/`missingFactToken` and `gateIdValue`.

**Rationale**: Keeps the pure `Loop` a total composition of merged cores over already-sensed opaque values (no
I/O, no hash, no clock) — the F022 discipline. `candidate` is F043's recompute-safe bridge: `Some CandidateGate`
for `Resolved`, `None` for `Unresolved`, so an unresolved gate **cannot** reach `evaluate` (FR-005). The
`evaluate`/`ofReport` path carries only resolved verdicts — the F042 schema is untouched.

---

## D6 — A minimal **read-only** `ReuseStore` deserializer, authored from scratch; absent ⇒ `empty`

**Decision**: The repo has **no** evidence-reuse store deserializer today (confirmed: `EvidenceReuse` is pure;
no `*Json` reader; the only on-disk codec anywhere is the `Host.Tests` review-store TSV). Author a minimal,
deterministic, **read-only** parser behind a `StoreReader` port that deserializes a documented on-disk format
into F030 `ReuseStore` (`ReuseStore of RecordedEvidence list`, each `{ Inputs: FreshnessInputs; Evidence:
EvidenceRef }`). An **absent** store file is treated as `EvidenceReuse.empty` (not an error). The format is the
ten opaque `FreshnessInputs` fields + the `EvidenceRef` string per record; a strict, total parser that fails the
run (`ToolError`, no partial artifact) on a malformed present file (Constitution VI: malformed input is named,
not silently treated as empty).

**Rationale**: FR-006 requires loading a read-only store from a declared location; US1 scenario 1 (a reusable
gate) needs a non-empty store to be loadable for testing. The spec defers **writing/evicting/expiring** evidence
to the cache-storage row, so this row authors only the reader. The format is intentionally minimal and
documented as **superseded** by the canonical format the cache-storage row will define — this row commits to the
*behavior* (absent ⇒ empty; present-malformed ⇒ named failure; present-valid ⇒ F030 store), not to a
long-lived wire format.

**Alternatives rejected**: Treat the store as always-empty for this row — fails US1 scenario 1 / SC-002 (cannot
demonstrate a reusable gate end to end). Reuse the `Host.Tests` TSV codec — it serializes a *review* store, not
an evidence-reuse store, and lives in a test project.

---

## D7 — Unresolved gates → a companion `cache-eligibility.unresolved.json` sidecar (maintainer-confirmed)

**Decision**: The F042-rendered `cache-eligibility.json` is written **verbatim** and carries only the resolved
gates' verdicts (its `fsgg.cache-eligibility/v1` schema, unchanged). Gates F043 reports `Unresolved` are written
to a **companion sidecar** `cache-eligibility.unresolved.json` with a new schema
`fsgg.cache-eligibility.unresolved/v1`: one entry per unresolved gate in `GateId` order, each naming the gate
and exactly its missing facts (via `missingFactToken`). Both files are deterministic and byte-stable; the
sidecar is written even when empty (an empty `unresolved` array) so its presence is unconditional and diffable.

**Rationale (maintainer-confirmed)**: Keeps **both files honest on disk** — the disk artifact never silently
omits an unresolved gate (Edge: "never silently dropped"), while F042 stays untouched (FR-007/SC-008: "reused
verbatim", schema unchanged). The sidecar is a small deterministic render over F043's **public** accessors
(`missingFacts`/`missingFactToken`/`gateIdValue`) — it computes no freshness key, hash, or cache decision
(FR-013), so it needs no new core.

**Alternatives rejected**: (a) Stdout-summary-only — the on-disk `cache-eligibility.json` would silently omit
unresolved gates, weakening the honesty contract for any consumer that reads only the file. (b) An envelope that
adds an `unresolved` section *inside* `cache-eligibility.json` — modifies the on-disk F042 schema, contradicting
"reused verbatim / schema unchanged" (FR-007).

---

## D8 — Verb `fsgg cache-eligibility`; flags mirror `RouteCommand` + store/out (maintainer-confirmed)

**Decision**: The verb is **`fsgg cache-eligibility`** (project `FS.GG.Governance.CacheEligibilityCommand`).
Flags mirror `RouteCommand`'s `RunRequest` scope shape plus the two cache paths:

| Flag | Meaning | Default |
|---|---|---|
| `--repo <dir>` | repository root to sense | current directory |
| `--paths <p…>` / `--since <rev>` / (default range) | the `ScopeSelector` (explicit paths / since-rev / default range), reused from `RouteCommand` | default range |
| `--store <file>` | read-only evidence-reuse store location | a declared default; absent ⇒ empty store |
| `--out <file>` | `cache-eligibility.json` output path (sidecar derived: `<out>` → `<out-stem>.unresolved.json` in the same dir) | a declared default under the readiness/output dir |
| `--format human\|json` | summary rendering on stdout | `human` |

**Rationale (maintainer-confirmed)**: The verb matches the artifact and schema names
(`cache-eligibility.json` / `fsgg.cache-eligibility/v1`) — most discoverable. Reusing `RouteCommand`'s scope
flags keeps the two commands' scope semantics identical (FR-001). The spec fixes behavior, not spelling; the
exact default paths are a plan decision recorded in the CLI contract.

---

## D9 — Determinism, exit codes, and no surfaced wall-clock

**Decision**: Both artifacts and the summary depend only on the sensed repo facts + loaded store, never on cwd,
process, ambient order, or clock (FR-008). Per-gate entries are in `GateId` order (inherited from the registry
and preserved by F041/F042/the sidecar render). **No wall-clock value is surfaced** in this MVP — neither
artifact has a timestamp field (F042 has none; the sidecar adds none), and the summary prints none — so F034
`SensedMetadata` is **not referenced**. Exit codes mirror `RouteCommand`: `Success = 0`, `UsageError = 2`,
`InputUnavailable = 3`, `ToolError = 4`; the **information** outcomes (must-recompute, unresolved) are all
exit 0 (FR-009). Genuine sensing/catalog/write failures are non-zero and write **no partial artifact** (atomic
temp-write-then-`File.Move`), keeping a tool defect distinct from missing/malformed input (Constitution VI,
FR-010).

**Rationale**: Byte-stability is the artifact contract (US3/SC-004); surfacing no clock is the simplest way to
guarantee it. If a later row needs a sensed timestamp in the summary, it MUST be F034-marked and excluded from
the reproducible artifact content (FR-008) — flagged here so the door stays open without taking the dependency
now.

---

## Resolved unknowns summary

| Unknown (Technical Context) | Resolution |
|---|---|
| Project shape & lineage | D1 — new `CacheEligibilityCommand`, the `RouteCommand` MVU shape |
| How to reuse F022 selection | D2 — replicate the call-sequence over `Config`/`Snapshot`/`Routing`/`Findings`/`Gates`/`Route` |
| Getting selected `Gate` values | D3 — `RouteResult.SelectedGates |> List.map (.Gate)`, GateId order |
| Where freshness facts come from | D4 — new `FreshnessSensor` port; base/head free from `RepoSnapshot.Range`; no-hide |
| How the pipeline composes | D5 — `SensedFacts` at the edge; pure `Loop` calls `resolve`/`candidate`/`evaluate`/`ofReport` |
| Reuse-store loading | D6 — from-scratch read-only deserializer; absent ⇒ `empty`; malformed ⇒ named failure |
| Unresolved-gate representation | D7 — companion `cache-eligibility.unresolved.json` sidecar (`fsgg.cache-eligibility.unresolved/v1`) |
| Verb & flags | D8 — `fsgg cache-eligibility`; `RouteCommand` scope flags + `--store`/`--out`/`--format` |
| Determinism, exit codes, clock | D9 — GateId order, no surfaced wall-clock (no F034), `RouteCommand` exit codes, information ⇒ exit 0 |
