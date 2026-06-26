# Phase 0 Research — Release-Provenance End-to-End Pack Evidence and Byte-Identity Goldens

This row has **no NEEDS CLARIFICATION** in the spec — it is a bounded test-evidence-and-goldens feature
that closes three deferred `065` tasks. Research concentrates on the few mechanical decisions that make
the evidence honest and deterministic. All decisions are grounded in a reconnaissance of the wired `065`
hosts, their `.Tests` projects, and the repo's existing real-process test pattern.

---

## D1 — How to make the pack-boundary evidence *real* without touching product code

**Decision.** Reuse the already-wired `065` release host verbatim and drive `Interpreter.run` over a
real temporary multi-project tree, swapping the disclosed-synthetic `portsWithPacks`
(`ReleaseCommand.Tests/Support.fs`) for the **real** F51 execution port
`FS.GG.Governance.GateExecution.Interpreter.realPort: ExecutionPort` and a **real** `PackRead` that
locates the produced `.nupkg`, reads its packed version, and computes its digest. The pure cores
(`evaluatePack` / `factContributions` / `evaluateRelease` / `summarize` / `assemble`) and the host's
`update` transition are unchanged and already proven by the `065` `LoopTests`; the only new code is
fixture construction + assertion.

**Rationale.** `065` deliberately built the host with injectable `Execute` / `PackRead` / `SenseHead` /
`SenseEnvironment` / `SenseBuilder` ports and proved the transition with fakes (T013–T017, all `[X]`).
The deferred T018 is precisely "now run it through the real port." The repo already has the real-process
pattern: `GateExecution.Interpreter.realPort` drives `System.Diagnostics.Process` with ordered arguments,
and `ShipCommand.Tests/ExecutionTests.fs` exercises real/near-real execution. So the real-pack fixture is
a port swap + a real `.nupkg` reader, not new product behaviour (FR-007).

**Alternatives considered.** (a) Add a new "real pack" code path to the host — rejected: `065` already
made the port injectable for exactly this; a second path would be redundant and would change surface.
(b) Keep the synthetic fake and just rename it "real" — rejected: vacuous, violates Constitution V and
the spec's FR-001 "real `dotnet pack`" requirement.

---

## D2 — Freezing genuinely *pre-wiring* byte-identity goldens after `065` already landed

**Decision.** The pre-wiring anchor is commit **`5a0cb28`** (F25/`064`, the parent of `065`'s single
commit `1ddf169`). Freeze the three construction-identical goldens — `route.json`, `ship.json`, and a
**no-declaration** `verify.json` — by running their producing commands from a `5a0cb28` checkout (a
throwaway `git worktree`) over the fixed fixture repo, and commit the captured bytes. The **empty-v2**
`release.json` golden is the exception: `release.json` v2 is *introduced* by F26/`065`, so there is no
pre-wiring v2 to freeze — its golden is the F26-blessed empty-additive `fsgg.release/v2` contract
captured from current code (exactly as `065` T024 scoped it).

**Rationale.** The spec's edge case is explicit: "re-deriving them from the post-wiring code would make
the check vacuous." Since `065` already landed, "current `main`" is post-wiring. Two facts make the
freeze honest: (1) RouteCommand and ShipCommand were **untouched** by `065`, and the `verify.json`
`releaseReadiness` block is emitted **only** when a declaration is present (`ofVerifyDecisionWithPreview`
writes nothing for `None`) — so for these three, `5a0cb28` output and `main` output are byte-identical
*by construction*, and freezing from `5a0cb28` proves the wiring left them untouched rather than asserting
it. (2) For `release.json` v2 there is no honest "pre-wiring" bytes to compare against — the additive v2
contract *is* the new thing being pinned, so freezing the F26-blessed empty-additive shape pins the
contract going forward, which is the strongest non-vacuous guarantee available.

**Alternatives considered.** (a) Freeze all four from current `main` — rejected for route/ship/no-decl
verify: vacuous self-comparison, fails the edge case. (b) Reconstruct pre-wiring `release.json` v1 and
compare a v2 run to it — rejected: v1 and v2 differ by the additive fields by design; the meaningful pin
is the empty-additive v2 shape, not a v1→v2 diff. (c) Skip the goldens entirely — rejected: SC-005 and
`065` T009/T024 require them.

---

## D3 — Where each byte-identity golden test lives

**Decision.** Place each golden's comparison test in its **producing** host's `.Tests` project, extending
the existing `PersistenceEdgeTests.fs` in each: `route.json` → `RouteCommand.Tests`, `ship.json` →
`ShipCommand.Tests`, empty-v2 `release.json` → `ReleaseCommand.Tests`, no-declaration `verify.json` →
`VerifyCommand.Tests`. Each test runs the **real** producing command over a fixed fixture repo and
asserts byte-equality with the committed golden. This supersedes `065` T024's note that placed
route/ship/release goldens centrally in `ReleaseCommand.Tests`.

**Rationale.** RouteCommand and ShipCommand produce `route.json` / `ship.json`; the release host cannot
produce them without a cross-host reference (the hosts are separate exes). Running each command in its own
`.Tests` project is the honest, reference-free way to prove byte-identity, and every host already has a
`PersistenceEdgeTests.fs` plus the temp-repo `Support.fs` harness to do it. Centralizing in
`ReleaseCommand.Tests` (the `065` note) would require either faking the route/ship producers (vacuous) or
a host→host reference (forbidden by the genericity/operating rules).

**Alternatives considered.** (a) One central golden-comparison test project — rejected: it would have to
reference four host exes. (b) Keep `065`'s central placement — rejected: cannot produce route/ship output
from the release test project without faking the producer.

---

## D4 — Determinism and SDK gating for the real-`dotnet pack` fixture

**Decision.** Generate the temporary project tree from explicit literals (fixed project names, fixed
`<Version>` values, fixed package metadata) so no machine path, username, or wall-clock enters any
**asserted** output. Pack duration is sensed metadata only (`durationNanos`) and is **excluded** from the
byte-identity and re-run assertions (compare the normalized `release.json` v2 / `attestation.json`, not
the raw run record's duration). Probe for a working `dotnet pack` before the real-pack tests; when absent,
surface a disclosed Expecto `ptestList` / skip with a clear diagnostic message (`FR-008`), never a silent
pass.

**Rationale.** FR-006 forbids path/username/clock/environment/pack-duration leakage into asserted
contract output; the F26 cores already normalize `release.json` v2 / `attestation.json`, so the fixture's
obligation is to feed deterministic inputs (literal tree) and to exclude duration from comparisons. SC-003
requires re-run byte-identity, which holds once duration is excluded. FR-008 forbids a silent green on a
missing SDK — Expecto's skip mechanism with a written rationale (Constitution V) is the disclosed signal.

**Alternatives considered.** (a) Assert over the raw run record including duration — rejected: duration is
wall-clock-sensitive, breaks SC-003. (b) Let the test hard-fail when no SDK is present — acceptable per
FR-008's "skip-or-fail," but a disclosed skip is friendlier in mixed CI; either is allowed, skip is
preferred with a loud diagnostic. (c) Mock `dotnet pack` — rejected: defeats the entire purpose (FR-001).

---

## D5 — Closing the `065` deferrals and the roadmap note

**Decision.** When the evidence is green, mark `065` tasks.md **T009 / T018 / T023 / T024** complete
(citing `066` as the row that landed them), and rewrite the F26 "Partial follow-ups" note in
`docs/initial-implementation-plan.md` (currently: "the real-filesystem `dotnet pack` E2E (T018), the
mergeable-vs-releasable + FR-008 precondition fixture (T023), and the frozen byte-identity host goldens
(T009/T024) … deferred") to record them as closed by `066`.

**Rationale.** FR-007 and SC-005 make flipping the deferrals and the roadmap note a first-class
acceptance criterion. The note is the single roadmap-level record of the deferral; leaving it stale after
the evidence lands would misreport the project state (Constitution VI — faithful reporting).

**Alternatives considered.** (a) Leave the `065` tasks partial and only track in `066` — rejected: the
deferrals are explicitly owned by `065`'s task list; SC-005 names them. (b) Delete the roadmap note —
rejected: rewrite-to-closed preserves the audit trail.

---

## Summary of resolved decisions

| ID | Decision | Closes |
|----|----------|--------|
| D1 | Real-pack via `GateExecution.Interpreter.realPort` + real `PackRead`; reuse the wired host, swap only the faked edge | T018 / SC-001 / SC-003 |
| D2 | Freeze route/ship/no-decl-verify goldens from pre-wiring anchor `5a0cb28`; empty-v2 release from the F26-blessed contract | T009 / T024 / SC-004 |
| D3 | Each byte-identity golden test in its producing host's `.Tests` project | T024 / SC-004 |
| D4 | Deterministic literal project tree; pack duration excluded from identity; SDK-gated disclosed skip | SC-003 / FR-006 / FR-008 |
| D5 | Flip `065` T009/T018/T023/T024 and rewrite the F26 roadmap "Partial follow-ups" note | FR-007 / SC-005 |

No NEEDS CLARIFICATION remain. Proceed to Phase 1.
