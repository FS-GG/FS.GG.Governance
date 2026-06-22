# Research: Persist The Evidence-Reuse Store To Disk From The Host Commands

Phase 0 decisions. Every NEEDS CLARIFICATION from the spec is resolved here. Each decision is a *mechanism*
choice; the *requirements* are fixed by spec.md.

## D1 — Where the persistence wiring lives: extend the two existing command MVU boundaries

- **Decision**: Add the store write to the existing `FS.GG.Governance.RouteCommand` and
  `FS.GG.Governance.ShipCommand` `Loop`/`Interpreter` pairs — a new effect emitted from the pure `update`,
  interpreted at the edge. No new command, no new library, no edit to the F047 core.
- **Rationale**: F046 wired the **read** side (`LoadStore` effect → `StoreLoaded` msg → `CacheEligibility.evaluate`)
  into exactly these two boundaries; the write is the symmetric edge and belongs in the same place. Both
  commands already load the store, already discover the path (`--store`), and already own an atomic `writeAtomic`
  port. The F047 library exists precisely to be referenced here.
- **Alternatives rejected**: (a) A new `fsgg cache-persist` command — adds CLI surface and a second store-path
  discovery scheme for no benefit; the spec says the write belongs where the store is already loaded. (b)
  Putting the write inside the F047 library — F047 is pure-by-contract (FR-009 of F047); pushing I/O into it
  breaks that and re-opens its frozen surface.

## D2 — The decision is a pure transition; only the byte write is impure (FR-010)

- **Decision**: The pure `update` computes the **persisted document string** (when persistence is enabled and
  the store is non-degraded) and emits `PersistStore(path, content)`. `Interpreter.step` does nothing but call
  the existing atomic `ports.Write path content` and reify the `Result` to `StorePersisted`.
- **Rationale**: FR-010 requires all decision logic (whether to write, what to write, how to degrade) to be
  testable without I/O. Computing `content` in `update` makes the entire decision a pure `Model`+`Msg` →
  `Model`+`Effect` function asserted in unit tests; the edge holds only the unavoidable file write. This mirrors
  how `route.json`/`audit.json` content is computed in `update` (`tryProject`) and only written at the edge.
- **Alternatives rejected**: Passing the loaded `ReuseStore` value through the effect and calling
  `prune/retain/serialise` in the interpreter — moves decision logic to the impure edge, violating FR-010 and
  making the bound/prune choice untestable without a filesystem.

## D3 — A **separate** effect/msg pair, distinct from `WriteArtifact`/`Wrote` (FR-006)

- **Decision**: Add `PersistStore of path: string * content: string` (effect) and `StorePersisted of
  Result<unit, string>` (msg) — **not** reuse `WriteArtifact`/`Wrote`.
- **Rationale**: The existing `Wrote(_, Error _)` transition maps to `ToolError` (route exit 4 / ship exit 4),
  and route/ship count `Wrote` acks to sequence `EmitSummary`. Routing the store write through `Wrote` would
  (a) make a store-write failure fatal — directly violating FR-006 — and (b) corrupt the artifact ack count. A
  distinct `StorePersisted` msg keeps the store write **non-fatal** (its `Error` only appends a cache note) and
  keeps it out of the artifact ack arithmetic.
- **Alternatives rejected**: A third `ArtifactKind` case (`StoreArtifact`) on `WriteArtifact` — inherits the
  fatal `Wrote(Error)→ToolError` path; rejected.

## D4 — The persisted document is derived from the **loaded** store, fully decoupled from verdicts (FR-005)

- **Decision**: `content = EvidenceReuseStore.serialise (EvidenceReuseStore.retain
  EvidenceReuseStore.defaultRetentionBound (EvidenceReuseStore.prune loadedStore))`, where `loadedStore =
  model.Store` (the value F046 loaded). The existing `CacheEligibility.evaluate candidates store` join that
  feeds `route.json`/`audit.json` is **not touched** and continues to read the same loaded store value.
- **Rationale**: FR-005/SC-004 require the current run's verdicts to be identical whether persistence is on or
  off. Because the verdict path and the persist path both read the immutable `model.Store` and neither mutates
  it, the prune/retain/serialise cannot perturb the verdicts. The persisted file affects only the **next** run.
- **Alternatives rejected**: Persisting a store that includes any newly `record`-ed evidence from this run —
  out of scope (no gate executes this row; no new evidence exists to record). The persisted store is the loaded
  store, pruned + bounded (a maintenance write).

## D5 — Retention bound: F047 `defaultRetentionBound`, no new flag

- **Decision**: Use `EvidenceReuseStore.defaultRetentionBound` as the `retain` argument. No `--store-retain`
  flag in this row.
- **Rationale**: The spec lists the exact bound as an F047/plan mechanism detail and requires only that the
  persisted store is within *a* deterministic bound and recompute-safe — both guaranteed by F047's default.
  Adding a tuning flag is surface the row does not need; it can be a later additive row if a real workload
  demands it.
- **Alternatives rejected**: Reading a bound from config/env — adds a discovery scheme and a non-determinism
  surface for no current requirement.

## D6 — Don't clobber a store that failed to parse (spec Edge Cases / Assumptions)

- **Decision**: When the load **degraded** (the F046 `StoreLoaded(Error _)` path that substitutes
  `EvidenceReuse.empty` and appends a note), the pure transition **suppresses** the `PersistStore` effect and
  appends a second note (`"store not persisted: on-disk store failed to parse; left untouched to avoid data
  loss"`). A genuinely **absent** file is *not* a parse failure and **is** persisted (writes a well-formed empty
  `v1` document, creating parent dirs).
- **Rationale**: The spec's default (Assumptions, "Malformed-load handling unchanged") is to not silently
  overwrite a possibly-recoverable malformed file with an empty document. Distinguishing absent (load returns
  `Ok None` → empty, persist OK) from malformed (load returns `Error` → degrade, suppress write) is exactly the
  existing F046 reader distinction (`realStoreReader`: missing file → `Ok None`; parse failure → `Error`), so we
  carry one extra boolean in the Model (`StoreDegraded`) set on the `StoreLoaded(Error _)` branch.
- **Alternatives rejected**: Always persisting the post-degrade empty store — silently destroys a malformed but
  potentially hand-recoverable file; rejected by the spec default. Failing the command on malformed-load —
  violates the F046 non-fatal invariant.

## D7 — Scope: `fsgg route` and `fsgg ship` only; `fsgg cache-eligibility` (F044) does not gain the write

- **Decision**: Wire persistence into the two commands that own a route/ship run lifecycle and already persist
  `route.json`/`audit.json`. The standalone `fsgg cache-eligibility` command keeps its read-only,
  sidecar-projection behavior.
- **Rationale**: The spec targets "the host command(s) that already load the store … (`fsgg route` and `fsgg
  ship`)" and explicitly mirrors F046's two-command scope. F044 emits a standalone `cache-eligibility.json`
  projection; it is a read-only diagnostic, not a store-lifecycle owner, and adding a write there is a
  separately-scoped decision the spec defers.
- **Alternatives rejected**: Adding the write to all three commands now — broadens the diff to a third
  command and its baseline for a capability the spec does not require there.

## D8 — Reuse each interpreter's existing `writeAtomic`; do **not** extract a shared writer module now

- **Decision**: The `PersistStore` interpreter arm calls the existing `ports.Write` (bound to `writeAtomic`,
  the temp-write-then-`File.Move(tmp, path, true)` helper already in `RouteCommand/Interpreter.fs` and
  `ShipCommand/Interpreter.fs`). No new shared atomic-writer module.
- **Rationale**: `writeAtomic` already gives the FR-001 guarantee (parent-dir create, unique temp sibling,
  atomic rename, failed write leaves no partial file) and is already the bound `Write` port in both commands —
  so the store write reuses it with zero new code at the edge. Extracting the (identically-duplicated) helper
  into a shared module would additionally touch `CacheEligibilityCommand/Interpreter.fs` and create a new
  public surface, exceeding this row's additive scope. Deduplication is a clean, separable later refactor.
- **Alternatives rejected**: A dedicated `StoreWriter` port distinct from `Write` — the bytes-to-path contract
  is identical; a second port is redundant ceremony. A shared `AtomicWrite` library — worthwhile but
  out-of-scope here (touches three commands).

## D9 — Flag spelling and default: `--persist-store`, default **off**

- **Decision**: A boolean flag `--persist-store` parsed in each command's existing argv parser into
  `RunRequest.PersistStore: bool`, defaulting to `false`. No value argument.
- **Rationale**: FR-004 mandates explicit opt-in defaulting off so the default run is byte-identical to the
  pre-row baseline. A plain boolean flag is the minimal mechanism; the name states intent and matches the
  existing `--store <path>` flag's vocabulary. When absent, the parser yields `PersistStore = false` and the
  `update` emits no `PersistStore` effect → no store write → byte-identical artifacts and goldens (SC-006).
- **Alternatives rejected**: An env var (`FSGG_PERSIST_STORE`) — a non-discoverable, non-deterministic trigger;
  rejected in favor of an explicit flag. A `--store-write=on|off` value flag — more surface than a boolean
  needs.

## D10 — Summary must reflect the persist outcome: gate `EmitSummary` on the persist ack when enabled

- **Decision**: When persistence is enabled, `EmitSummary` is emitted only after **both** the artifact writes
  and the `StorePersisted` ack have been folded into the Model, so the rendered summary deterministically
  includes any non-fatal persist note. This is tracked by explicit completeness in the Model (the artifact ack
  state plus a `PersistAcked`/`PersistStore = false` short-circuit), not by the brittle raw ack counter.
  When persistence is **off**, the set of expected acks is exactly the artifacts → the existing sequencing and
  summary are unchanged (byte-identical).
- **Rationale**: SC-005 requires the induced-failure note to surface in the summary; that requires the persist
  result to be known before the summary renders. Making the "all writes done" condition explicit (rather than
  counting `Wrote` acks) accommodates the extra, differently-typed `StorePersisted` ack without fragile
  arithmetic, and is a no-op when persistence is off.
- **Alternatives rejected**: Emitting `EmitSummary` on the last `Wrote` ack regardless of persist — the persist
  note could be missing or arrive after the summary (nondeterministic ordering in the batch drive loop);
  rejected against SC-005. Firing the persist write **after** the summary — same ordering hazard, and a late
  failure would never be reported.

## D11 — Testing strategy: pure transitions without I/O + real interpreter at the edge (SC-008)

- **Decision**: Two test layers per command. (1) **Pure transition tests** in the existing `*.Tests` project
  drive `Loop.update` directly: assert the `PersistStore` effect's `content` equals
  `EvidenceReuseStore.serialise (retain default (prune loaded))` for a real upstream-assembled store; assert
  `PersistStore` is **absent** when the flag is off and when the store degraded; assert `StorePersisted(Error
  _)` leaves `Exit`, the emitted `route.json`/`audit.json`, and (ship) the verdict unchanged. **No filesystem.**
  (2) **Effects-boundary tests** drive the real `Interpreter.run`/`realPorts` against a temp repo and re-read
  the persisted file through the **real** `FreshnessSensing.realStoreReader` (lossless round-trip, byte-identity,
  bounded/pruned, induced-failure no-partial-file + non-fatal note).
- **Rationale**: SC-008 explicitly demands the decision logic be exercised with no filesystem access and the
  atomic write be exercised at the boundary. Using the real reader on the write output proves the round-trip end
  to end against the actual accepted shape (SC-001), not a re-implemented parser.
- **Alternatives rejected**: A mock writer that records bytes only — would not prove the atomic temp+rename or
  the real reader's acceptance; kept only as an optional induced-failure injector (an unwritable path) for SC-005.

## Resolved unknowns

| Spec clarification point | Resolution |
|--------------------------|------------|
| Exact opt-in flag name/spelling | `--persist-store`, boolean, default off (D9). |
| Whether `fsgg cache-eligibility` (F044) also gains the write | No — route & ship only (D7). |
| Writer shared code or per command | Reuse each command's existing `writeAtomic` via `ports.Write`; no shared module this row (D8). |
| Retention bound / per-gate fairness | F047 `defaultRetentionBound`, global newest-first; no fairness, no flag (D5). |
| Overwrite a degraded-to-empty (malformed) store on write? | No — suppress the write, note it; absent file still persisted (D6). |
| How the non-fatal write integrates with exit codes / summary | Separate non-fatal `StorePersisted` msg (D3); `EmitSummary` gated on the persist ack when enabled (D10). |
