# FS.GG.Governance — code quality and architecture review

**Timestamp:** 2026-07-02T14:10:08+02:00
**Author:** Claude Code (Claude Fable 5)
**Method:** Six parallel review agents, each sweeping one slice of the repo end-to-end:
architecture/dependency structure, core domain, CLI/command layer, JSON contract layer,
tests/CI/build infrastructure, and adapters/evidence/sensing. All findings below were
verified against the actual source by the reviewing agent; file:line references point at
the code as of commit `f4b7836`.
**Scope:** 81 src projects, 87 test projects, ~82,500 lines of F#, plus `.github/workflows`,
`build.fsx`, packaging scripts, and MSBuild/CPM configuration.

---

## Executive summary

This is an unusually disciplined codebase, and every reviewer independently reached that
conclusion. The 81-project granularity is deliberate and documented (each `.fsproj` carries
a design essay naming its allowed dependencies), the ProjectReference graph is fully acyclic
with clean domain → `*Json` projection → `*Command` edge layering, dependency fences are
mostly real (pure BCL-only Kernel; Spectre.Console confined to `HumanRender`), every library
ships a curated `.fsi` plus a committed surface-drift baseline, and the test methodology —
expected bytes recomputed through the genuine pure cores, real-git end-to-end proofs,
explicit determinism assertions — is a model of its kind. Error handling is consistently
total at the edges (exceptions reified to values), artifact writes are atomic everywhere,
and identity/cache-key encodings are injective by construction.

The most consequential findings cluster in four themes:

1. **CI does not run the test suite.** The per-PR gate workflow only restores and builds;
   just 2 of 87 test projects execute in any CI path. The repo's ~5,000 assertions run only
   on developer machines, so a regression in any core merges green as long as it compiles.
   This is the single highest-leverage fix in the repo.
2. **Process-spawning edges have hang/deadlock windows.** The Snapshot git port drains
   stdout to EOF before touching stderr (classic pipe-buffer deadlock the GateExecution
   port explicitly avoids); GateExecution's clean-exit path has an unbounded drain wait;
   the pack-and-apicheck script has the same sequential-drain shape.
3. **Duplication has started producing real divergence bugs.** The clearest case: an
   unguarded `Console.KeyAvailable` crashes `fsgg route --watch` under redirected stdin —
   the identical code in the CLI dispatcher was already fixed, but the fix landed in only
   one of the two copies. Ship↔Verify hosts are ~73% common text; `writeAtomic` exists in
   7 files; ~74 near-identical copies of `SurfaceDriftTests.fs`; a ~105-line JSON writer
   block is copied between ReleaseJson and VerifyJson; two divergent semver comparators
   feed the same release-fact family and can produce contradictory verdicts.
4. **A few stated invariants have quietly drifted.** README claims YamlDotNet is isolated
   to `Config` (five projects reference it); three published tool packages all claim the
   `fsgg` command name; route/cache-eligibility `--json` stdout violates the repo's own
   "Json is the contract" rule; the advisory ApiCompat gate cannot succeed in CI as wired.

None of the findings are architectural — the architecture is sound and the code matches
the stated design with only minor drift. The work ahead is consolidation (a second
CommandHost-style extraction pass) and closing the gap between the discipline the repo
declares and the handful of places it isn't yet enforced.

## Severity roll-up

| Severity | Count | Themes |
|---|---|---|
| High | 3 | CI never runs tests; Snapshot git-port pipe deadlock; `--watch` crash on redirected stdin (+ the host-boilerplate duplication that caused it) |
| Medium | 18 | ApiCompat gate broken in CI; inverted baseline comparator; divergent semver comparators; stale-verdict cache window in adapters; path-boundary prefix bug; fail-open sensors; Json-contract violations; fixture backdoors in production code; flag-parsing value swallowing; nondeterministic nupkg pick; exit-code policy divergence; token-vocabulary drift risks; tool-name collision; exe→exe references |
| Low | ~35 | Residual duplication, dead code, stale comments, missing timeouts/caching in CI, version metadata inconsistencies, minor API-shape issues |

---

## High-severity findings

### H1. The per-PR gate never runs the test suite (`.github/workflows/gate.yml`)

The `gate` job does a locked restore + `dotnet build` only. The only tests executed
anywhere in CI are `ReferenceGateSet.Tests` (gate.yml:121) and `Cli.Tests`
(publish.yml:151, and only on publish events). The other ~85 test projects — roughly
5,000 `Expect.*` assertions, including every core's byte-identity and determinism suite —
run only on developer machines. The repo already has the right tool: `dotnet fsi build.fsx
test` (bounded parallelism, exit-code preserving).

**Recommendation:** add a `test` job to gate.yml running `dotnet fsi build.fsx test -c
Debug --no-restore` after locked restore; shard into 2–3 jobs if wall-time demands it.
This is what makes the 87 test projects actually earn their keep.

### H2. Snapshot git port: sequential `ReadToEnd` pipe-buffer deadlock

`src/FS.GG.Governance.Snapshot/Interpreter.fs:78-80` drains stdout to EOF, then stderr,
with no timeout. If git writes more than the OS pipe buffer (~64 KB) to stderr before
exiting — e.g. thousands of `warning: LF will be replaced by CRLF` lines on
`diff`/`status` in an autocrlf repo — git blocks on stderr, never closes stdout, and
`ReadToEnd` blocks forever. This contradicts the file's own "never throws / never hangs"
contract, and the repo already contains the correct pattern: concurrent async drains in
`GateExecution/Interpreter.fs`. The same sequential-drain shape exists in
`pack-and-apicheck.fsx:85-87`.

**Recommendation:** drain both streams concurrently (as GateExecution does) and add a
bounded `WaitForExit` in both the Snapshot port and the pack script.

### H3. `fsgg route --watch` crashes when stdin is redirected — a duplication-drift bug

`src/FS.GG.Governance.RouteCommand/Program.fs:62` calls `Console.KeyAvailable`
unguarded; it throws `InvalidOperationException` when stdin is redirected or no console
is attached, and nothing in `Watch.run`'s poll loop catches it. The identical code in the
dispatcher was already fixed with `try … with _ -> false`
(`src/FS.GG.Governance.Cli/Program.fs:184-188`) — the guard landed in one of the two
copies. Relatedly, `new FileSystemWatcher(root)` in `HumanRender/Watch.fs:70` throws
unguarded for a nonexistent root.

This is the concrete evidence for the broader duplication finding (M-CLI-1 below): the
copy-pasted host boilerplate is no longer just a maintenance tax; it is producing
headless-fragility regressions of exactly the class that specs 091/#32 fixed.

**Recommendation:** hoist a single `safeKeyPoll` helper into `HumanRender` next to
`Watch.run`, use it from both call sites, and guard the watcher construction so watch
entries exit 3/66 instead of crashing.

---

## Medium-severity findings

### CI, build, and release engineering

- **M-CI-1. ApiCompat tool invocation cannot succeed in CI.** `pack-and-apicheck.fsx:137`
  runs `dotnet tool run apicompat`, which resolves local-manifest tools only;
  `.config/dotnet-tools.json` contains only `fake-cli` (drift-locked), while CI installs
  apicompat globally. Every baselined package therefore grades Indeterminate — fail-safe,
  but the `Checked:met` promotion path (SC-005) is unreachable. Invoke the binary
  directly or install with `--tool-path`.
- **M-CI-2. Baseline selection picks the *lowest* version.** The comparator at
  `pack-and-apicheck.fsx:124-129` sorts descending and `List.tryLast` then selects the
  smallest valid baseline, contradicting the comment ("the last published") — once
  M-CI-1 is fixed, breaks would be graded against the oldest-ever published version.
  Flip the comparator (and add a selftest for baseline choice among multiple candidates).
- **M-CI-3. No NuGet caching despite 166 committed lockfiles.** Seven jobs cold-restore
  the graph; `actions/setup-dotnet` with `cache: true` +
  `cache-dependency-path: '**/packages.lock.json'` is a perfect fit.
- **M-CI-4. ~74 near-identical copies of `SurfaceDriftTests.fs`** (~7,200 lines of
  scaffold, several re-implementing `findRepoRoot` that already exists in
  `Tests.Common.RepositoryHelpers`). Add `SurfaceDrift.testsFor …` to `Tests.Common`
  and shrink each copy to a ~5-line instantiation.

### Correctness at the edges

- **M-CORE-1. `parseStatus` mishandles worktree renames/copies.**
  `Snapshot/Snapshot.fs:179-190` checks only the X (index) column for `'R'`/`'C'`;
  porcelain v1 also emits Y-column worktree renames whose `-z` records carry the extra
  original-path field. Such a record corrupts the parse: the original path is consumed as
  the next record with its first three chars swallowed as `"XY "`. Skip the extra field
  when either column is `'R'`/`'C'`.
- **M-CORE-2. GateExecution clean-exit drain is unbounded.**
  `GateExecution/Interpreter.fs:104-108` — on a within-timeout exit, `Task.WaitAll` on
  the drain tasks has no bound; a gate that spawned a background child inheriting
  stdout/stderr hangs the port forever despite the gate having exited. The timeout branch
  already bounds its wait to 5000 ms; do the same here, and fix the buffer race in the
  timeout branch (`stdoutBuf.ToArray()` while `CopyToAsync` may still be writing).
- **M-CORE-3. Unreadable config files are reported as "empty".**
  `Config/Loader.fs:29-32` maps read failure to `Present ""` → `EmptyFile` diagnostic.
  Fail-closed direction is right; the diagnostic is factually wrong and discards the
  error message. Add an `UnreadableFile` diagnostic.
- **M-CORE-4. `parseExpectations` silently swallows malformed values.**
  `ReleaseDeclaration/Declaration.fs:205-225` — present-but-malformed criteria degrade to
  `None`, indistinguishable from "not declared", violating the file's own "never partial
  facts" header (which the sibling section parsers honor). Distinguish `Ok None` from
  `Error`.
- **M-ADPT-1. Two divergent version comparators produce contradictory `VersionBump`
  verdicts.** `PackEvidence/Pack.fs:106-110` implements full semver (pre-release lower
  than release, build metadata stripped); `ReleaseFactsSensing/Sensing.fs:49-62` splits
  on `.` and coerces non-numeric segments to 0. For `2.0.0-alpha.1` vs `2.0.0` one says
  bumped/Met, the other Downgraded — and both feed the same release family. Extract one
  shared comparator (Pack's is correct).
- **M-ADPT-2. Stale-verdict window in the SpecKit/DesignSystem adapters.** The kernel's
  `AgentReviewed` cache key covers judge + check hash + artifact hashes + question, but
  both adapters stub `ArtifactHash = fun _ _ -> ""` (`Adapters.SpecKit/SpecKit.fs:146`,
  `Adapters.DesignSystem/DesignSystem.fs:81`) and register `Opaque` checks declaring no
  `Reads`. Net: the cache key for e.g. `plan-satisfies-spec` is identical before and
  after `plan.md` changes, so a stale `Reviewed` fact still decides. Wire real content
  hashing into the bridges and document the invalidation split with F036.
- **M-ADPT-3. DocsChecks link-boundary check has the sibling-prefix bug.**
  `DocsChecks/Interpreter.fs:63` uses `combined.StartsWith rootFull` without a trailing
  separator, so `../<repoName>-something/file.md` resolves as "inside" — despite the
  function's own FR-016 comment, and despite `Scaffold/Interpreter.fs:41-47` implementing
  the guard correctly. Append the separator or share `resolveUnder`.
- **M-ADPT-4. Fail-open sensors.** `PackageChecks/Interpreter.fs:207` maps an unreadable
  transcripts directory to `[]` (zero findings → verify passes exactly when evidence
  could not be gathered); `CurrencySensing.fs:97-98,157-158,193-194` swallows an
  unreadable/malformed `.fsgg/refresh.yml` to an empty finding list, silently dropping a
  configured `block-on-ship` enforcement dial. Both contradict the sibling sensors'
  reify-to-finding policy (`BaselineUnreadable`, `Unreadable`) and FreshnessSensing's
  Result contract. Add unreadable-fact channels.
- **M-ADPT-5. Scaffold batch write can leave debris.**
  `Scaffold/Interpreter.fs:98-119` — if a write or `File.Move` throws mid-batch, the
  in-flight `.tmp-<guid>` file and created directories are never rolled back,
  contradicting the header's "leaving ZERO new files (SC-005)". Track the tmp path before
  writing and delete it in rollback.
- **M-CLI-2. Test-fixture backdoors keyed off path substrings in production code.**
  `Cli/Program.fs:75`, `Cli/ReviewStore.fs:41,63` — any real repo path containing
  `review-store-unavailable` or `review-dispatch-failed` gets injected failures in the
  shipped binary. Move fixture behavior behind the ports seam; delete the substring checks.
- **M-CLI-3. All argv parsers swallow a following flag as an option value.**
  `fsgg verify --repo --json` silently sets `Repo = "--json"` and drops JSON mode
  (same shape in all seven hosts plus `Cli.requireValue`). In the shared parser, treat a
  `--`-prefixed next token as `MissingValue`.
- **M-CLI-4. Release attestation picks an arbitrary "newest" `.nupkg`.**
  `ReleaseCommand/Interpreter.fs:130-136` scans the shared `~/.local/share/nuget-local`
  and takes the most-recently-written `*.nupkg` with no name filter — with multiple
  packables or stale files, the attested digest/version can belong to a different
  package, and mtime ordering contradicts the file's own FR-011 determinism claim.
  Filter by the surface's package id and/or diff the listing before/after the pack run.

### Contract-consistency drift

- **M-ARCH-1. README's "YamlDotNet isolated to Config" claim is false.** Five projects
  reference it (Config, CurrencySensing, RefreshCommand, ReleaseDeclaration,
  ReleaseCommand) — and uniquely in this repo, the four extra references carry no
  justifying fsproj comment. Either re-fence through a single YAML owner or update
  README/fsprojs and add a confinement guard test (the Spectre one is the template).
- **M-ARCH-2. Executable-to-executable ProjectReferences invert the two CLI lineages.**
  `Cli.fsproj` references `RouteCommand` (an Exe/PackAsTool project);
  `EvidenceCommand.fsproj:48` references `Cli` itself. The longest dependency chain
  (depth 14) runs through two composition roots, and the installed `fsgg evidence` tool
  bundles two other executables. Extract the reused pipeline/fold into library projects;
  keep all eight Exe projects as leaves.
- **M-ARCH-3. Three published tool packages all claim `ToolCommandName` `fsgg`**
  (RouteCommand, EvidenceCommand, CacheEligibilityCommand) — installing any two as global
  tools collides. Known state (README:161, ADR-0003 defers unification), but a
  distribution hazard the moment more than one reaches a shared feed. Let exactly one
  package own `fsgg` until the multiplexer lands.
- **M-JSON-1. Attestation schema-version and compliance tokens hard-coded in two other
  projections.** `ReleaseJson.fs:285,287` and `VerifyJson/ReleaseReadiness.fs:135,137`
  write the literals `"fsgg.attestation/v1"` / `"compatible-shape-not-formal-compliance"`
  instead of referencing `AttestationJson` constants (the compliance token is `let
  private`, so it *cannot* be referenced today). A v2 bump would silently strand the
  embedded references. Expose both through `AttestationJson.fsi`.
- **M-JSON-2. ~105-line verbatim duplication of the release-readiness writers** between
  `ReleaseJson.fs:172-289` and `VerifyJson/ReleaseReadiness.fs:26-139`; nothing keeps
  them mirrored and the naming has already forked. Lift into `JsonWriters` (they meet
  the 073 extraction criterion exactly) or add an element-identity test.
- **M-JSON-3. Undocumented `EnvironmentClass` wire divergence.** `JsonTokens.fs:44`
  emits `localOrCi`; `EvidenceReuseStore.fs:38` emits `local-or-ci` (with a matching
  parser). Unlike the disposition/verdict divergences, neither site carries the
  "do not unify — bytes diverge" warning, so a well-meaning dedup would compile cleanly
  and break the store round-trip at runtime. Add the warning comment both sides.
- **M-CLI-5. "Json is contract" not honored by Route and CacheEligibility stdout.**
  `RouteCommand/Loop.fs:623-652` and `CacheEligibilityCommand/Loop.fs:469-506` emit
  bespoke, schema-version-less summary JSON that differs from the persisted artifact,
  while Ship/Verify/Refresh emit the artifact byte-for-byte. Emit the artifact document
  (or stamp and document the divergence).
- **M-CLI-6. Exit-code policy diverges across hosts for the same failure class.**
  Unreadable store/freshness: Route/Ship/Verify degrade to 0 + note, CacheEligibility
  fails 4. Exit 2 means "governed blocking" from the `fsgg-governance` dispatcher
  (sysexits) but "usage error" from every verb host. Document the two families in
  `cli.md` and align the store-failure policy.
- **M-CLI-7. EvidenceCommand `--plain` silently overrides `--format json`**
  (`EvidenceCommand/Loop.fs:106`), contradicting the convention stated at
  `Cli/Cli.fs:222-224`; the parsed `ExplicitPlain` field is consumed by nothing.
- **M-CORE-5. Parallel vocabularies with no bridge.** Two `Severity` types (byte-identical),
  two `RunMode`s (three overlapping case names), two `RuleId`s — no conversion functions,
  so correspondence is enforced nowhere. Document the intentional split at the type
  definitions or add `ofKernel`/`toKernel` mappers.

---

## Low-severity findings (grouped)

**Duplication / drift-prone copies**
- Ship↔Verify hosts ~73% common text (~497 changed lines of ~1,840 after token
  neutralization); interpreter `step` bodies byte-identical across Route/Ship/Verify;
  `writeAtomic` ×7, `guard` ×9, drive loop ×7, `realHandoffs` ×4 (one with a subtly
  different sort); `EvidenceCommand/Interpreter.fs:33-357` is a ~325-line self-described
  verbatim copy of `Cli/ArtifactReading.fs` even though the project already references
  `Cli` (and the copies have already diverged — the dead `"present"` check exists in one).
- `CommandHost.ExitDecision`/`exitCode` are dead: every host re-declares an identical DU.
- Four JSON writer pairs (~95 lines) still byte-identical across projections
  (`writeFreshnessKey`/`writePrerequisite`, attestation/provenance unwrappers,
  `writeCacheEligibility`, `writeGeneratedView(s)`); `AuditJson.ofShipDecision` duplicates
  `ofShipDecisionWithGeneratedViews` instead of delegating (provably byte-identical fix).
- Four copies of `mkFinding`, five+ of the `safe` exception-reifier, two of `valuesFor`
  across the *Checks packs; `sha256Hex` and Kernel `digest` each defined twice;
  `Severity -> string` three times in Kernel alone; `Route.stakesOf` re-implements
  `Verdict.combineReasons` because the `.fsi` hides it; SddHandoff `Consumer` copies
  `Readiness.internalIdOf`/`buildGate` for the same reason.
- ViewCurrency fold duplicated between ShipCommand and VerifyCommand; Ship/Verify
  `Wrote(Ok)` micro-drift (pre- vs post-update model passed to `emitEffect`).

**Correctness edges and API shape**
- `Cost` comparisons ride on DU declaration order (`RouteExplain.fs:51,63`,
  `CostBudget/Budget.fs:67`) — reordering cases silently inverts "cheaper"; the repo's
  own `generatedProductTierRank` pattern is the fix.
- `FixedPoint.evaluate` has no round cap/fuel (documented precondition, but a fuel guard
  would harden a kernel that promises totality elsewhere).
- `TimeoutLimit` accepts non-positive/overflowing values end-to-end; timeout > 2,147,483 s
  overflows `seconds * 1000` to negative and misreports as a start failure while leaking
  the started process.
- `GateOutcome` makes invalid states representable (`Executed` with no exit code);
  `lexCommandLine` accepts unterminated quotes; `commandFor` collapses four failure modes
  into one `None`.
- `Option.get`/`.Value` invariant coupling in `Config/Schema.fs` parsers rests on a
  non-local convention; restructure `finish` to pattern-match the options.
- `CalibrationEvidence.ObservedAgreement` taken on faith; the per-sample `Agreement`
  field is never read by production code.
- `Matrix.decideMatrix` carries a dead `boundary` parameter (`ignore boundary`).
- Kernel `Json.fs` throws (`failwith`) where the rest of the repo returns Result —
  fine for trusted kernel-emitted text, but undocumented at the public boundary.
- Snapshot's `GitUnavailable` path hand-builds a `RepoSnapshot` inline (with its own
  local `sortDigests`), bypassing `assemble`.
- ReviewStore `safeFileName` collisions can return the wrong cached verdict (distinct
  keys collide onto one file; the file doesn't record its key).
- `senseCatalogHash` concatenates name+content bytes without length prefixes (the exact
  ambiguity the repo's own FreshnessKey discipline exists to prevent, and it's a
  `RuleHash` cache input); `senseSrcHashes` is rename/move-insensitive.
- SkillChecks `claimed.Contains ".."` over-rejects filenames like `notes..md`; a
  mirror-read error mislabels a declared mirror as `NoMirrorDeclared`.
- DesignChecks sensor wraps only one of five port calls in `safe`.
- DocsChecks `ExampleFact`/`exampleFindings` unreachable (`Examples = []` always);
  VerifyCommand `SurfacesPending` set at three sites, read nowhere; dead `"present"`
  check in `Cli/ArtifactReading.fs:257-259`; dead loop in `pack-and-apicheck.fsx:36-46`.
- `VerifyJson` `currency.unresolved[].missing` is a permanently-empty contract field;
  quadratic dedup in `VerifyJson/Core.fs:193-200`; O(n²) list-append folds in
  `SddHandoff/Reader.fs:153-156`.
- EvidenceCommand `update` lacks the Done-inertness guard every sibling documents.

**CI / packaging hygiene**
- No `timeout-minutes` on any workflow job (default 360).
- Non-semver `v*` tags (e.g. `vNext`) fall through publish.yml's regex to `push="true"`
  with no tag-vs-fsproj cross-check.
- Hardcoded fallback NuGet user `'Paradigma11'` duplicated in two publish jobs.
- The advisory api-compatibility job builds the 166-project solution twice per PR while
  (per M-CI-1/M-CI-2) producing near-zero signal — move to schedule/dispatch until fixed.
- Version metadata scattered: 13 packable fsprojs have no `<Version>` (default 1.0.0,
  inconsistent with 0.1.x siblings); centralize a `VersionPrefix` for baseline-only
  projects.
- Command hosts carry heavily redundant direct ProjectReferences (VerifyCommand declares
  43, of which 32 are transitively reachable) with no documented convention.
- ADR references (0007/0012/0013) resolve only in `FS-GG/.github`; add a local index in
  `docs/decisions/`.
- `HumanText` sits on every executable's critical path and rebuilds whenever the release
  domain moves; consider per-domain text projections if build latency bites.
- Only `HumanRender` and `SurfaceChecks.Dispatch` lack mirrored test projects — worth
  confirming HumanRender (the sole Spectre/IO owner, subject of spec 091) is deliberate.
- Stale headers: `AttestationJson.fs:22-23` claims "no access modifiers" while using
  `let private`; ~8 projections keep dead `System.IO`/`System.Text` opens from the 073
  extraction; `ProductSurfaces.fs`/`ReleaseReport/Report.fs` contradict the stated
  no-modifiers convention; dispatcher `MissingCommand` help text omits `watch`/`tui`.
- Format-flag vocabulary spelled four ways across the verb family (`--json`;
  `--format human|json`; `--text|--json|--text-and-json`; `--format text|json|both`);
  `--repo` required only in release.

---

## Strengths worth preserving

- **Dependency discipline as executable documentation.** Acyclic graph, two roots
  (Kernel, Config), fsproj essays naming allowed dependencies, third-party surface of
  essentially four packages, Spectre genuinely confined to one project with a guard test.
- **Surface governance:** 70 committed `.surface.txt` baselines with drift tests,
  curated `.fsi` files everywhere, RenameGuard, API-compat gating (once M-CI-1/2 land).
- **Determinism engineering:** ordinal sorts on every emitted collection, canonicalized
  commutative check hashing with prefix-free pre-images, injective length-prefixed
  identity encodings (`FreshnessKey`, `AgentReviewKey`, `Provenance.canonicalId`,
  `PromptIsolation.render` — a genuinely good prompt-injection defence), no
  clock/username/hostname leaks in artifacts.
- **MVU command hosts with total edges:** pure `Loop`s that never throw, interpreters
  that reify every exception, atomic writes everywhere, usage errors decided before any
  port exists, degrade-don't-fail cache senses with explicit operational notes and
  verdict/health separation.
- **JSON layer fundamentals:** no manual escaping anywhere (everything through
  `Utf8JsonWriter`), culture-invariant by construction (no floats, no dates), fixed
  field order, `schemaVersion` first field of every document, exhaustive wildcard-free
  token matches, round-trip symmetry where parsers exist.
- **Test methodology:** expectations recomputed through real cores rather than
  snapshot-blessed; one real-git end-to-end proof per command suite; explicit
  determinism tests forbidding `/tmp/`, `/home/`, timestamps in artifacts; FsCheck
  properties for order-independence; the headless-render fix (pinned Spectre profile +
  derived fold bound) done at the root rather than by re-blessing.
- **Release engineering:** OIDC Trusted Publishing with least-privilege `id-token`,
  locked-mode restores everywhere, committed lockfiles, org-feed-first byte-identical
  dual publish, enforcement smoke that installs and drives the real packed tool, and
  build scripts that are small, self-testable (`--print-command`, `--print-version`),
  and self-gating.
- **Decision hygiene:** ADRs that take costed positions (ADR-0003's explicit deferral),
  specs cross-referenced from fsprojs, corrective notes owning past mistakes
  (`Directory.Build.local.props` SCOPING NOTE).

## Prioritized recommendations

1. **Run the tests in CI** (H1). One job, existing script. Everything else in this
   report matters less while regressions can merge green.
2. **Fix the process-edge hangs** (H2, M-CORE-2): concurrent drains + bounded waits in
   Snapshot, GateExecution's clean-exit path, and pack-and-apicheck.fsx.
3. **Fix the watch crash and hoist the guard** (H3) — small, user-visible, and the
   proof-case for consolidation.
4. **Repair the ApiCompat gate** (M-CI-1, M-CI-2): direct invocation + comparator flip,
   with a selftest; until then move the job off the per-PR path.
5. **Second extraction pass on the command hosts** (M-CLI family): shared impure leaf for
   `writeAtomic`/`guard`/drive/`realHandoffs`/sense helpers + shared argv parser (fixing
   M-CLI-3 once), have EvidenceCommand call `ArtifactReading` instead of its copy, adopt
   or delete `CommandHost.ExitDecision`. ~600–800 lines deleted; fixes land once.
6. **Single-source the release-verdict comparator** (M-ADPT-1) and wire real artifact
   hashing into the adapter review keys (M-ADPT-2).
7. **Close the fail-open sensor paths** (M-ADPT-4) and the DocsChecks boundary check
   (M-ADPT-3).
8. **Contract hygiene sweep** (M-JSON-1/2/3, M-CLI-5): expose attestation constants via
   `.fsi`, extract or identity-test the release-readiness writers, add divergence
   comments to the environment tokens, make route/cache-eligibility stdout emit the
   artifact document.
9. **Re-fence or re-document YamlDotNet** (M-ARCH-1) and de-invert the exe→exe references
   (M-ARCH-2); resolve the `fsgg` tool-name collision before publishing further tools
   (M-ARCH-3).
10. **Consolidate the surface-drift test scaffold into Tests.Common** (M-CI-4) and adopt
    NuGet caching + job timeouts in the workflows (M-CI-3).

---

*Full per-area agent reports (architecture, core domain, CLI/command, JSON contracts,
tests/CI/build, adapters/sensing) were synthesized into this document; findings were
deduplicated where multiple reviewers independently flagged the same code.*
