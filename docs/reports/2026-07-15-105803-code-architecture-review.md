# FS.GG.Governance — code and architecture review

**Timestamp:** 2026-07-15T10:58:03+02:00
**Author:** Claude Code (Claude Opus 4.8, 1M context)
**Commit reviewed:** `204896c` (branch `main`)
**Method:** Six parallel review agents, each sweeping one slice of the repo end-to-end —
architecture/dependency structure, core domain (kernel/route/ship/verify/config), CLI &
command hosts, the JSON contract layer, adapters/evidence/sensing/process-edges, and
tests/CI/build/packaging. Every finding below was verified against the actual source by
the reviewing agent and, for the load-bearing items, re-verified directly; `file:line`
references point at the tree as of `204896c`. Each agent additionally re-checked every
finding of the prior review (2026-07-02, commit `f4b7836`) against current code to
establish closure.
**Scope:** 82 `src` projects, 84 test projects, ~38.4k lines of F# under `src/` (~53k
under `tests/`), plus `.github/workflows`, `build.fsx`, the packaging/apicheck scripts,
and the MSBuild/CPM configuration.
**Build note:** the full solution could not be restored/built in the review sandbox
(the private `FS.GG.Contracts 2.0.0` package 401s without org-feed auth), so this is a
static review. CI is the source of truth for green: `gate.yml` now runs the whole
Expecto suite in Debug **and** Release on every PR.

---

## Executive summary

The headline of this review is closure. **Every finding of the 2026-07-02 review has been
addressed** — the three High findings (CI never ran tests; the Snapshot git-port pipe
deadlock; the `route --watch` headless crash) are genuinely fixed with regression
coverage, and the entire Medium/Low tail (the M-CI / M-CLI / M-ADPT / M-JSON / M-CORE /
M-ARCH families) was worked through as epic #44 and specs 100–111. The handful that were
*not* code-changed are now explicit, documented decisions (ADR-0007/0008, the deliberate
wire-token divergences, the M-CLI-6 store-read exit divergence recorded in the README),
not silent drift. This is an unusually high remediation rate, and it shows: the same
disciplines the prior review praised — an acyclic 82-project DAG with fsproj dependency
essays, a zero-dependency BCL-only kernel, curated `.fsi` + committed surface baselines
everywhere, determinism engineering (ordinal sorts, injective length-prefixed identity
encodings, no clock/host leaks), and a test methodology that recomputes expected bytes
through the real cores — are now backed by *executable fences* (`DependencyFences.Tests`
over the real `git ls-files` graph) and a real CI gate rather than convention alone.

This pass therefore found fewer and smaller issues, clustered in three themes:

1. **One genuine headless-fragility bug survived the H3 fix.** `fsgg-governance tui` still
   calls `Console.ReadKey` unguarded, so it crashes on redirected stdin / no console —
   exactly the class that `route --watch` was hardened against, but the guard was hoisted
   for `watch` only and never applied to the symmetric `tui` reader. This is the single
   highest-leverage code fix in this review.
2. **Documentation has drifted behind the remediation wave.** The README now materially
   misstates several things the code changed underneath it: "most projects are
   `IsPackable=false`" (69 of 82 are packable); the exit-code table cites a *deleted*
   type (`CommandHost.ExitDecision`) and omits `refresh`'s sixth exit code
   (`ViewsRegenerated=5`); dependency versions are a full major behind
   (`FS.GG.Contracts 1.0.1` → real `2.0.0`). None of this changes behavior, but the README
   is the contract surface consumers read.
3. **A few real-but-narrow correctness/consistency edges remain.** A non-injective
   `GateId` join that can silently drop a gate from the selected set; a same-DU JSON
   token-casing divergence (`"Real"` vs `"real"`) with no divergence warning; a small set
   of fail-open `try … with _ -> []` sensor paths and a couple of taint/rollback edges;
   and residual, safely-hoistable JSON writer duplication that is neither ratified nor
   deferred.

None of the findings are architectural. The architecture is sound and the code matches
the stated design; the remaining work is a documentation-truth sweep, one headless fix,
and a short tail of low-severity hardening.

## Prior-review closure (2026-07-02 → 204896c)

| Prior finding | Status | Landed via |
|---|---|---|
| **H1** CI never runs the test suite | **FIXED** | `gate.yml` `full-test-suite` (Debug) + `full-test-suite-release` (Release), gating on exit code (spec 102, #68/#155) |
| **H2** Snapshot git-port pipe deadlock | **FIXED** | concurrent async drains + bounded waits in Snapshot, GateExecution, and `pack-and-apicheck.fsx` (#58) |
| **H3** `route --watch` crash on redirected stdin | **FIXED** (but see CLI-1 — `tui` was missed) | hoisted `Watch.safeKeyPoll` + guarded watcher (#59) |
| **M-CI-1** ApiCompat can't succeed in CI | **PARTIAL** | tool-resolution + banner fixed (#60); real detection still inert (no baseline seeded — see CI-1) |
| **M-CI-2** baseline picks lowest version | **FIXED** | `baselineFor` max-fold + selftest (#60) |
| **M-CI-3** no NuGet caching | **RESOLVED (deliberate)** | caching stays OFF by design so `--locked-mode` stays cold (ADR-0031, #169) |
| **M-CI-4** ~74 SurfaceDriftTests copies | **FIXED** | shared `Tests.Common.SurfaceDrift` helper (#65) |
| **M-CLI-1** host-boilerplate duplication | **PARTIAL** | shared `CommandHost` leaf (spec 103); thin `Program.fs` triplet still per-host (low value) |
| **M-CLI-2** review-cache key collision | **FIXED** | SHA-256-digest filename + stored-key verify (#77) |
| **M-CLI-3** `--repo --json` swallows next flag | **FIXED** | `MissingValue` guard in all hosts + `requireValue` (#69) |
| **M-CLI-4** arbitrary `.nupkg` pick | **FIXED** | exact-id filter + highest-SemVer (#50) |
| **M-CLI-5** route/cache-eligibility stdout ≠ contract | **FIXED (route) / PARTIAL (cache-elig)** | route emits `route.json` byte-for-byte; cache-eligibility stamped but still bespoke (#52) |
| **M-CLI-6** store-read exit divergence | **DOCUMENTED (still unaligned)** | README records the route/ship/verify-vs-cache-eligibility split |
| **M-CLI-7** `--plain` overrides `--format json` | **FIXED** | Json wins everywhere (#49) |
| **M-CORE-1** parseStatus rename/copy corruption | **FIXED** | trailing NUL field consumed on `R`/`C` (#58) |
| **M-CORE-2** GateExecution unbounded drain + buffer race | **FIXED** | bounded `Task.WaitAll` + `IsCompleted`-gated `ToArray` (#58) |
| **M-CORE-3** unreadable config reported "empty" | **FIXED** | distinct `UnreadableFile` diagnostic (#51/#79) |
| **M-CORE-4** parseExpectations swallows malformed | **FIXED** | `Ok None` (absent) vs `Error` (malformed) (#51/#79) |
| **M-CORE-5** parallel Severity/RunMode/RuleId vocabularies | **PARTIAL (by design)** | edge bridges for profiles; kernel-vs-platform duplication intentional |
| **M-ADPT-1** divergent semver comparators | **FIXED** | single `ReleaseRules.SemVer.compareVersions` (#50) |
| **M-ADPT-2** stale-verdict adapter cache window | **FIXED** | reviewed artifacts injected into `Check.reads` (#50/#60051d3) |
| **M-ADPT-3** DocsChecks boundary prefix bug | **FIXED** | trailing-separator match (#086d6be) |
| **M-ADPT-4** fail-open sensors | **FIXED** | unreadable reified as Blocking; absent≠unreadable (#a51dbe4) |
| **M-ADPT-5** scaffold batch-write debris | **FIXED (files)** | temp+rename + rollback (directories still linger — see ADPT-4) |
| **M-JSON-1** attestation constants hard-coded | **FIXED** | referenced from `AttestationJson.fsi` (#52/c1ee7f9) |
| **M-JSON-2** release-readiness writer duplication | **RATIFIED** | `ofShipDecision` delegates; writer pair kept per ADR-0008 |
| **M-JSON-3** EnvironmentClass token divergence | **FIXED** | mirrored "DO NOT UNIFY" warnings (#c1ee7f9) |
| **M-ARCH-1** YamlDotNet not isolated to Config | **FIXED** | 4-owner bidirectional fence `YamlFenceTests` (spec 100) |
| **M-ARCH-2** exe→exe references | **FIXED** | extracted `RoutePipeline` lib; `ExeLeafTests` fence (spec 100) |
| **M-ARCH-3** three packages claim `fsgg` | **FIXED** | distinct `ToolCommandName`s; `FsggOwnerTests` fence (spec 100) |

## Severity roll-up (new findings)

| Severity | Count | Items |
|---|---|---|
| High | 1 | CLI-1 (`tui` headless crash) |
| Medium | 4 | ARCH-1, ARCH-2, CLI-2, JSON-1 |
| Low–Medium | 3 | CORE-1, CLI-3, JSON-2 |
| Low | 13 | ARCH-3, ARCH-4, CORE-2, CORE-3, CLI-4, CLI-5, JSON-3, JSON-4, JSON-5, ADPT-1, ADPT-2, ADPT-3, ADPT-4 |
| Nit | 1 | JSON-6 |

---

## High-severity findings

### CLI-1 — `fsgg-governance tui` crashes on redirected stdin / no console

`src/FS.GG.Governance.Cli/Program.fs:200-201` — `tuiKeyReader` calls `Console.ReadKey(true)`
with no `try/with`, and `Tui.run` (`Program.fs:221`) loops it. The H3 fix hardened the
symmetric *watch* path (`HumanRender/Watch.fs:64-68` `safeKeyPoll` catches the
`InvalidOperationException` from `Console.KeyAvailable`/`ReadKey` and stops cleanly), but the
guard was never applied to the `tui` reader. On a valid repo with redirected stdin or no
attached console (any CI or piped invocation), `composeRouteView` returns `Some view`,
`Tui.run` is entered, and the first key read throws `InvalidOperationException` → unhandled
crash — instead of the clean `InputUnavailable (66)` the watch path now produces. The
existing headless test (`WatchTuiHostWiringTests.fs:96`) covers `runWatch` over a
nonexistent root; the `tui` tests inject a pure key reader, so the real `tuiKeyReader`
crash path is untested. *(Verified directly during synthesis.)*

**Recommendation:** route `tuiKeyReader` through the same guard used by `watch` (catch and
map to `Tui.Quit`), and add a redirected-stdin test that drives the real reader so the fix
can't regress in only one of the two interactive surfaces — the same lesson H3 taught.

---

## Medium-severity findings

### ARCH-1 — README packaging claim is inverted, and `Directory.Build.local.props` contradicts itself

`README.md:305` states "**Most projects are `IsPackable=false`**", but **69 of 82** `src`
projects are packable (8 explicit `false`, the rest packable by the `.local.props` default);
the api-compat gate packs every one against an org-feed baseline. `Directory.Build.local.props`
also contradicts itself: a comment near line 69 says "the ~70 `IsPackable=false` projects
never pack" while the "SCOPING NOTE (corrects the 094 metadata contract's premise)" a few
lines down says the ~70 projects are `IsPackable=**true**`. The self-correction landed in
the props file but never propagated to the README or the stale sibling comment.
*(Verified: 69 true / 8 false.)*

**Recommendation:** rewrite `README.md:305` to state the packable-by-default reality (most
projects pack for the api-compat baseline; a small explicit set is `false`), and delete the
contradictory `Directory.Build.local.props` comment.

### ARCH-2 — Spectre.Console confinement is asserted but not fenced

`README.md:139,296` present Spectre.Console confinement to `HumanRender` as an equivalent
guarantee to the YamlDotNet confinement, and the invariant currently *holds* (only
`HumanRender.fsproj` carries the real `PackageReference`). But unlike YamlDotNet, there is
**no fence test** — `DependencyFences.Tests` has `YamlFenceTests`, `ExeLeafTests`,
`FsggOwnerTests`, but no Spectre equivalent — so the confinement can drift silently, exactly
the failure mode M-ARCH-1 was about. Presenting a prose-only invariant as machine-enforced
is misleading.

**Recommendation:** add a `spectre-owner` fence mirroring `yamlOwnerViolations` with
allowlist `{ HumanRender }`; it reuses `ProjectGraph.PackageReferences` verbatim (~15 lines).

### CLI-2 — README exit-code section cites a deleted type and omits `refresh`'s sixth code

The README's "fsgg verb hosts … via `CommandHost.ExitDecision`" is doubly wrong: (a)
`CommandHost.ExitDecision`/`exitCode` were **deleted** as dead (spec 103, #71) — each host
declares its own DU; (b) the single `0/1/2/3/4` table is presented for all seven hosts, but
`RefreshCommand` uses a **six-code** scheme — `NothingToRefresh=0`, `StaleUnresolved=1`,
`UsageError=2`, `InputUnavailable=3`, `ToolError=4`, **`ViewsRegenerated=5`**
(`RefreshCommand/Loop.fs:91-96`). A script treating `0` as the sole success reads a
successful regeneration (`5`) as failure. *(Verified: `ViewsRegenerated -> 5`.)*

**Recommendation:** drop the `CommandHost.ExitDecision` reference and give `refresh` its own
row (`0/1/2/3/4/5`) with the `ViewsRegenerated` meaning called out.

### JSON-1 — `EvidenceState` is tokenized with different casing in two JSON contracts, with no divergence warning

The same Kernel `EvidenceState` DU is emitted **Capitalized** by `EvidenceJson.fs:66-73`
(`"Pending"`/`"Real"`/`"Synthetic"`/…) and **lowercase** by `Kernel/Json.fs:208-215`
(`"pending"`/`"real"`/`"synthetic"`/…, which also round-trips). A consumer reading
`evidence.json` sees `"Real"` while a consumer reading the kernel effective-state JSON sees
`"real"`. It is not a round-trip break (separate emit-only contracts), but it is a real
cross-contract inconsistency and — unlike the *deliberate* `localOrCi`/`local-or-ci`
divergence, which now carries mirrored "DO NOT UNIFY" comments on both sides — neither
`stateToken` site warns about the other, so a well-meaning unification would compile cleanly
and silently change one contract's bytes. *(Verified both sites.)*

**Recommendation:** add a divergence-warning comment on both `stateToken` sites naming the
other spelling and the pinning tests (as JsonTokens/EvidenceReuseStore now do), or converge
the casing under an explicit schema bump.

---

## Low–Medium findings

### CORE-1 — `gateIdOf` builds a non-injective `GateId`, so a gate can be silently dropped from `Route.select`

`Gates.fs:45-48` composes `GateId (sprintf "%s:%s" d c)` from `DomainId`+`CheckId` and the
doc-comment calls it "the stable, INJECTIVE gate id" — but a bare `:` join is not injective
when ids contain `:`: `{domain="a"; id="b:c"}` and `{domain="a:b"; id="c"}` both render
`"a:b:c"`. Config places no charset restriction on ids, so this is reachable. `Route.select`
dedups by `GateId` in a `Map` (`Route/Route.fs:71-88`), so a collision silently drops one of
the two gates — a check that should run doesn't. This is precisely the concatenation
ambiguity the repo's own `FreshnessKey` length-prefix discipline exists to prevent. `CheckId`
is already catalog-unique (`Schema.fs:598`), so the domain prefix adds collision surface
without being needed for uniqueness.

**Recommendation:** drop the domain prefix (checkId alone is unique) or length-prefix/escape
the two components as `FreshnessKey.req` does; fix the comment to state the actual guarantee.

### CLI-3 — format-flag vocabulary diverges three ways across hosts

The human-vs-JSON selector is spelled differently per host: route/ship/verify use bare
`--json`; refresh uses `--text`/`--json`; release uses `--format text|json`; evidence and
cache-eligibility use `--format human|json`. The plain-output token is `text` in
release/refresh/dispatcher but `human` in evidence/cache — so `fsgg evidence --format text`
errors while `fsgg release --format text` succeeds.

**Recommendation:** unify on one vocabulary (`--format text|json`, `text` the plain token)
ahead of the planned single-tool multiplexer, or at least accept `text` as a synonym for
`human`. (ADR-0006 records *why* they diverged today; this is the convergence follow-through.)

### JSON-2 — `verify.json` `currency.unresolved[].missing` is a permanently-empty contract field

`VerifyJson/Core.fs:249-263` always emits `"missing": []`; the in-code comment concedes the
cache report alone can't reconstruct the tokens (they live in `Model.Sensed`, reached only by
the text render). The array is structurally dead in `fsgg.verify/v1` — a consumer can never
receive a non-empty value. The same data *is* populated in `EvidenceJson.fs:87-93` from a
richer input, so this is a plumbing choice, not an impossibility.

**Recommendation:** thread the missing-fact tokens into the verify projection input so the
field carries data, or drop `missing` from the `unresolved` shape (schema bump) and document
the omission — an always-empty field misrepresents the contract.

---

## Low-severity findings

- **ARCH-3 — `fsgg-owner` fence checks one literal string, not general uniqueness.**
  `ProjectGraph.fs:110-114 fsggClaimants` flags only `ToolCommandName = Some "fsgg"`; two
  tools colliding on any *other* name (e.g. a second `fsgg-evidence`) pass silently.
  Generalize to "no `ToolCommandName` value is claimed by more than one packable project."
- **ARCH-4 — edge commands over-declare redundant direct `ProjectReference`s.**
  `VerifyCommand.fsproj` declares 43 direct refs, ~32 already transitively reachable. By
  design at the edge (README note) and fenced by *kind* not count, so not drift — but a
  removed transitive dependency won't surface as a leaf build break. Optional prune pass.
- **CORE-2 — non-positive `TimeoutLimit` accepted at the config boundary.**
  `Schema.fs:509` builds `TimeoutLimit s` from `reqInt` with no positivity check, so
  `timeout: 0`/`-5` validates as `Valid` and the gate waits 0 ms → immediate
  `timeoutExitCode 124` every run (a silent always-failing gate from a typo). Spec 110's
  overflow half landed; this boundary-rejection half did not. Reject `timeout <= 0` with a
  `MalformedValue` diagnostic.
- **CORE-3 — `Verdict.combineReasons` splits legitimate reason text on the reserved `"; "`.**
  `Verdict.fs:30-35` treats `"; "` as a delimiter and splits/sorts/rejoins; an adapter reason
  legitimately containing `"; "` is silently fragmented and reordered. Intentional (reserved
  separator), but nothing prevents an adapter emitting it. Document the reservation at the
  probe-authoring surface, or escape the separator in leaf reasons.
- **CLI-4 — cache-eligibility `--json` leaks host output paths into its contract.**
  `CacheEligibilityCommand/Loop.fs:511-517` embeds `"wrote":{cache,unresolved}` (caller-supplied
  paths) in the document; route deliberately removed such leakage (FR-012). Drop the `wrote`
  object from the JSON (keep it in operational lines), matching route.
- **CLI-5 — stray positional reported as "unknown flag" in verb hosts.**
  Ship/Verify/Evidence/Refresh fold a non-`--` positional into `UnknownFlag` ("unknown flag:
  x"); spec 108 only distinguished this in the dispatcher. Add an `UnexpectedArgument` case
  (or reword) for the no-positional hosts; fold into the single-tool cleanup.
- **JSON-3 — `writeRun` + 7 value helpers duplicated verbatim across AttestationJson and
  ProvenanceJson.** `AttestationJson.fs:41-47,62-68` ≡ `ProvenanceJson.fs:35-41,46-52`. Unlike
  the ADR-0008 pair (schema-version-coupled) and the #83 pair (projection-layer symbol), this
  pair is contract-agnostic (`writeRun` depends only on `Audit.*`/`KindedCommandRun`), so
  hoisting into `JsonWriters` inverts no layering and couples no schema versions. Neither
  ratified nor deferred. Lift into `JsonWriters`, guard with the existing byte-identity tests.
- **JSON-4 — generated-view writers duplicated (AuditJson ↔ VerifyJson/GeneratedViews).**
  `AuditJson.fs:171-203` ≡ `VerifyJson/GeneratedViews.fs:23-56`; known and deferred to #83
  (`JsonWriters.fs:68-71`) because both need `RefreshModel.viewKindToken`. Reported for
  completeness; a shared module sited above `RefreshJson` but below the two projections
  resolves it without inversion.
- **JSON-5 — CostBudgetJson synthesizes the `overBudget.reason` string at emit time.**
  `CostBudgetJson.fs:69-76` builds `reason` via `sprintf` inside the projection (deterministic,
  closed-enum tokens, wire-safe) — a mild deviation from the "emit-only, re-derive nothing"
  header. Carry the reason from the `CostBudget` model, or note the exception in the header.
- **ADPT-1 — Verify surface-sensing swallows every exception (and invalid config) to `[]`.**
  `VerifyCommand/Interpreter.fs:164-219` wraps the four-domain dispatch in one
  `try … with _ -> []` and maps an `Invalid` catalog to `[]`; a throw in any single domain
  erases all surface findings and verify can pass with zero surface evidence. Isolate the
  `try` per domain and reify a caught exception / `Invalid` catalog as a Blocking input-state
  finding (fail-closed), mirroring the sensors' own now-fixed discipline.
- **ADPT-2 — SddHandoff silently drops malformed evidence-dependency edges.**
  `Adapters.SddHandoff/Reader.fs:166-179` parses `evidence.dependencies` with `Seq.choose`,
  dropping any non-`[a;b]` tuple with no diagnostic while nodes are parsed strictly. Because
  `AutoSynthetic` taint flows along these edges, a dropped edge can leave a downstream verdict
  resting on a `synthetic` node un-tainted (taint fail-open). Treat a malformed dependency as
  a `Malformed` diagnostic so the handoff is rejected, not partially trusted.
- **ADPT-3 — Snapshot clean-exit drain can yield an empty→"clean" working tree with no
  diagnostic.** `Snapshot/Interpreter.fs:99-110`: if a read task hasn't completed within the
  5 s bounded drain, `resultOf` returns `""`, and for `StatusPorcelain` `""` parses to a
  *clean* tree — a dirty tree could be reported clean. Very low likelihood (accepted H2-fix
  trade-off), but unlike GateExecution it converts lost output into a positive fact. Emit an
  `UnreadableWorkingTree` diagnostic when `WaitForExit` succeeded but a read didn't complete.
- **ADPT-4 — Scaffold rollback leaves empty directories behind.**
  `Scaffold/Interpreter.fs:103-133` deletes the in-flight temp and renamed files on a
  mid-batch throw but never the directories it created, so the header's "ZERO new files"
  holds for files but not directories. Record created directories and remove the now-empty
  ones in reverse order (or stage into one dir and rename the tree).

## Nit

- **JSON-6 — dead `open System.IO` / `open System.Text` in 11 projections.** After the 073
  extraction moved `MemoryStream`/`Encoding` into `JsonText.writeToString`, these opens are
  unused in AttestationJson, AuditJson, CacheEligibilityJson, CostBudgetJson, EvidenceJson,
  GatesJson, ProvenanceJson, RefreshJson, RouteJson, ReleaseJson, ScaffoldManifestJson. Drop
  them (keep `System` where `String.CompareOrdinal` is used).

## Signature-visibility follow-ups (no behavior change)

- `Kernel/Json.fs` public readers (`toExplanation`/`toContract`/`toEvidenceState`/`toEffective`)
  throw on malformed input by design (documented in the `.fs` header, spec 110 B8). Surface the
  throwing contract in `Json.fsi` too, so a caller feeding externally-sourced JSON sees the
  edge at the signature, not only in the implementation.

---

## Strengths worth preserving

- **Remediation discipline.** A prior review's entire finding set was worked to closure with
  regression coverage, and the genuine won't-fixes were converted into ADRs and pinned
  divergence comments rather than left implicit. This is the behavior that keeps a review
  useful across cycles.
- **Fences assert over the real tracked graph.** `ProjectGraph.load` shells `git ls-files`
  and parses every `.fsproj`; the YAML/exe-leaf/fsgg-owner matchers are red-path unit-tested
  over literal nodes and the YAML fence is *bidirectional* (flags an undocumented owner *and*
  an owner that dropped its reference). Invariants can't rot because a hand-maintained list
  went stale.
- **The dependency graph is a clean DAG** — 82 projects, 0 cycles, all 8 executables true
  leaves, a genuinely zero-dependency kernel (0 project + 0 package references), and single
  cross-repo-contract consumption (`FS.GG.Contracts` referenced only by `Config`).
- **The delivery gate is real and layered** — the full Expecto suite runs in Debug **and**
  Release on every PR, gating on exit code, with the Release lane explicitly refusing to trust
  a "0 failed" tally over a discovery crash. Locked restore is *cold* by construction so
  `--locked-mode` validates content hashes against the feed, not a warm record.
- **Determinism across the JSON layer** — no date/float/culture in any projection, all
  escaping via `Utf8JsonWriter`, `schemaVersion` first with fixed field order, ordinal sorts
  on every emitted collection, closed-enum token matches with no wildcards (a new DU case is a
  compile error, never a mis-tokened field), and round-trip symmetry exactly where parsers
  exist.
- **Injective identity/prompt encodings** — `FreshnessKey`, `AgentReviewKey` (count-prefixed),
  and `PromptIsolation` (fixed instruction/data boundary) are consistently length-prefixed; no
  collision or injection window was found (CORE-1's `gateIdOf` is the one place that *doesn't*
  follow this and should).
- **Total process edges** — both spawning ports (Snapshot, GateExecution) and both `.fsx`
  scripts now start concurrent reads before waiting, with bounded process + drain waits and
  race-safe buffer reads; the deadlock/leak class is closed uniformly.
- **Fail-closed sensor discipline is now the norm** — PackageChecks, CurrencySensing, and
  ReleaseFactsSensing reify unreadable/missing/empty inputs as Blocking/Undeterminable findings
  with absent kept distinct from unreadable (ADPT-1 is the remaining fail-open outlier).

---

## Roadmap

Ordered by leverage. Each item is independently shippable as a small Spec Kit feature or a
direct fix; IDs reference the findings above. Suggested tiers follow the repo's own
classification (a doc-only or comment-only change is Tier 0/1; a surface or contract move is
Tier 1+ with `.fsi`/baseline in lockstep).

### Now — correctness & truth (highest leverage)

- [x] **CLI-1 — Guard the `tui` key reader (High).** ✅ Done: `tuiKeyReader`
      (`Cli/Program.fs`) now catches the unreadable-console `InvalidOperationException` → `Tui.Quit`,
      mirroring `Watch.safeKeyPoll`; a redirected-stdin regression test drives the real reader
      (`WatchTuiHostWiringTests.fs`). Both interactive surfaces are now headless-safe.
- [x] **ARCH-1 — Fix the README packaging claim + the self-contradicting props comment
      (Medium).** ✅ Done: `README.md` Packaging section now states the packable-by-default
      reality (74 of 82 packable; a small explicit set is `false`) and names the two actually-
      published packages; the stale "the ~70 `IsPackable=false` projects never pack" line in
      `Directory.Build.local.props` was corrected so it no longer contradicts the SCOPING NOTE.
- [x] **CLI-2 — Correct the README exit-code section (Medium).** ✅ Done: the README exit-code
      section no longer references the deleted `CommandHost.ExitDecision` type (each host declares
      its own DU); `refresh` now has its own `0/1/2/3/4/5` table with `ViewsRegenerated=5` called
      out as a second success code.
- [x] **CI-5 — Refresh README dependency versions (Low).** ✅ Done: README now states
      `YamlDotNet 18.1.0`, `FS.GG.Contracts 2.0.0` (both the key-dependencies list and the
      spec-087 `pinned` line), and `Spectre.Console 0.57.2`, matching `Directory.Packages.local.props`;
      the org `registry/dependencies.yml` `fsgg-contracts` pin was confirmed to equal `2.0.0`.
- [x] **CORE-1 — Make `GateId` injective (Low–Medium).** ✅ Done, via the config boundary rather
      than the `GateId` format: `Schema.reqIdString` now rejects the reserved `:` delimiter in a
      check `id`/`domain` (a located `MalformedValue`), so the existing `<domain>:<checkId>` join is
      injective by construction — no wire/contract/golden churn (tracing showed reformatting the id
      would have reordered and rewritten 6 versioned JSON contracts, since the id is the sort key and
      is embedded in every `ruleId`). The `gateIdOf` "INJECTIVE" comment now states the real
      guarantee (colon-free + catalog-unique); a `malformed-reserved-colon` fixture + `DiagnosticTests`
      case pin the rejection of both the `b:c` id and the colon domain.

### Next — enforce the invariants the docs claim

- [x] **ARCH-2 — Add a `spectre-owner` dependency fence (Medium).** ✅ Done: `ProjectGraph.fs`
      now exposes `spectreOwnerViolations` (a bidirectional owner fence, allowlist `{ HumanRender }`)
      alongside `yamlOwnerViolations` — both delegate to a shared `packageOwnerViolations` matcher, so
      the near-duplicate was factored, not copied. `SpectreFenceTests.fs` asserts the fence over the
      real project graph plus red-path cases (an undocumented owner, and a documented owner that
      dropped its reference). The README prose that presented the confinement as equivalent to
      YamlDotNet's now names the guarding test, so the invariant is machine-enforced rather than
      prose-only.
- [x] **ARCH-3 — Generalize the `fsgg-owner` fence to global `ToolCommandName` uniqueness
      (Low).** ✅ Done: `ProjectGraph.fs` now exposes `toolCommandCollisions` — a group-by over the
      packable `PackAsTool` nodes that flags any `ToolCommandName` claimed by more than one publishable
      project, closing the general collision class (a duplicate `fsgg-evidence`, say) the `fsgg`-only
      `fsggClaimants` let through. The original `fsgg`-specific case is kept. `FsggOwnerTests.fs` adds a
      production assertion over the real graph plus a red-path case (a non-`fsgg` collision is flagged;
      a name shared by a non-`PackAsTool` project is not).
- [x] **JSON-1 — Add divergence warnings to the two `EvidenceState` `stateToken` sites (Medium).**
      ✅ Done, via mirrored `DIVERGENCE — DO NOT UNIFY` comments rather than a schema bump (converging
      would rewrite one contract's bytes). `Kernel/Json.fs stateToken` (lowercase, round-tripping) and
      `EvidenceJson/EvidenceJson.fs stateToken` (Capitalized, emit-only) now each name the other's
      spelling, the reason they diverge, and the pinning test on the far side (Kernel.Tests/JsonTests ↔
      EvidenceJson.Tests/ProjectionTests) — matching the `localOrCi`/`local-or-ci` precedent in
      JsonTokens. A well-meaning unification is now a comment away from being caught, not a silent
      byte change. Comment-only (Tier 0), no `.fsi`/baseline/golden churn.
- [ ] **CORE-2 — Reject non-positive `TimeoutLimit` at the config boundary (Low).** Emit a
      located `MalformedValue` diagnostic instead of a silent always-failing gate.
- [ ] **ADPT-1 — Make Verify surface-sensing fail closed (Low).** Isolate the `try` per domain;
      reify a caught exception / `Invalid` catalog as a Blocking input-state finding.
- [ ] **ADPT-2 — Reject malformed SddHandoff evidence-dependency edges (Low).** `Malformed`
      diagnostic instead of a silent `Seq.choose` drop, closing the taint fail-open.

### Later — hardening & consolidation tail

- [ ] **CI-1 — Wire a real api-compat baseline in CI, or soften the overstated comment (Medium).**
      Seed the baseline feed from the org GitHub Packages feed (`--baseline-feed`) so
      `Checked:met` is reachable, or state that detection is inert until a baseline source
      exists — before promoting the gate to required.
- [ ] **JSON-2 — Populate or remove `verify.json` `currency.unresolved[].missing` (Low–Medium).**
      Thread the tokens through, or drop the field under a schema bump and document the omission.
- [ ] **JSON-3 — Hoist the shared `writeRun` + value helpers into `JsonWriters` (Low).**
      Contract-agnostic and un-deferred; guard with the existing byte-identity tests.
- [ ] **CLI-3 — Converge the format-flag vocabulary (Low–Medium).** One `--format text|json`
      spelling (or accept `text` as a `human` synonym), ahead of the single-tool multiplexer.
- [ ] **CLI-4 — Drop the `wrote` path object from the cache-eligibility JSON contract (Low),**
      matching route's FR-012.
- [ ] **ADPT-3 — Emit an `UnreadableWorkingTree` diagnostic on an incomplete Snapshot drain
      (Low).** Don't convert lost git output into a positive "clean" fact.
- [ ] **ADPT-4 — Roll back empty directories in Scaffold (Low),** so "ZERO new files" holds for
      directories too.
- [ ] **CLI-5 — Distinguish `UnexpectedArgument` from `UnknownFlag` in the no-positional verb
      hosts (Low).**
- [ ] **CORE-3 — Document the reserved `"; "` reason separator at the probe-authoring surface
      (Low),** or escape it in leaf reasons.
- [ ] **JSON-5 — Carry `overBudget.reason` from the model, or document the emit-time synthesis
      (Low).**
- [ ] **JSON-4 — Resolve the generated-view writer duplication if #83 proceeds (Low).**
- [ ] **JSON-6 — Remove the dead `open System.IO` / `System.Text` in the 11 projections (Nit).**
- [ ] **Kernel `Json.fsi` — surface the throwing contract of the public readers (no behavior
      change).**
- [ ] **ARCH-4 / M-CLI-1 tail — optional: prune redundant direct `ProjectReference`s and the
      per-host `Program.fs` triplet during the single-tool consolidation (Low).**

---

*Per-area agent reports (architecture, core domain, CLI/command hosts, JSON contracts,
adapters/sensing/edges, tests/CI/build/packaging) were synthesized into this document;
findings were deduplicated where multiple reviewers touched the same code, and the
load-bearing items (CLI-1, ARCH-1, JSON-1, CLI-2) were re-verified directly against source.*
