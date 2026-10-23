# GOV-423 — Language-neutral capability bindings

Executable source roadmap, 2026-10-05. Original item and delivery chain: [FS-GG/FS.GG.Governance#423](https://github.com/FS-GG/FS.GG.Governance/issues/423). Proposed owning document: `docs/roadmaps/governance-423-neutral-capability-bindings.md` in Governance, landed with the first source PR. This is a bounded extension of the delivered [058 plan](https://github.com/FS-GG/FS.GG.Governance/blob/bfcab71bb322687306134276c816eab95bcb85a5/specs/058-generated-product-capabilities/plan.md), not a replacement for its completed product-surface work.

Named Unified part: [§9.8 Language-independent workspaces and agent integration — V2-LANG-01](https://github.com/FS-GG/.github/blob/064d27897f720709a567493d039d8379ab13395e/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md#98-feature-parts-and-subroadmap-index), narrowed to the Governance producer requested by ADR-0092. Stage: additive producer contracts, then enforcement and publication; no extension to the frozen R5 cohort. Routine delivery is selected. The root programme integrator owns dispatch, shared-contract joins, PR admission and publication boundaries.

## Outcome and exact starting facts

Each selected neutral obligation has a stable meaning, a declared executable binding, exact ordered arguments, bounded cost/environment/timeout, and explicit tool and evidence references. Required missing/unsupported bindings refuse before execution. A successful declaration validation never claims execution or evidence acceptance. F# and gameplay evidence-only obligations retain their distinct existing routes.

Fresh read-only `git ls-remote origin refs/heads/main` and local `git rev-parse HEAD` both returned Governance `bfcab71bb322687306134276c816eab95bcb85a5`; worktree status was empty. Observation: 2026-10-05, before 06:28:50Z. Protected `.github/main` fresh-read as `064d27897f720709a567493d039d8379ab13395e`; ADR-0092 and its linked design are the architecture authority. Architecture #3009 is already delivered; this plan does not reopen it. The source-start packet reports no original #423 implementation PR/owner; this planner does not turn that observation into a new issue or board write.

Source trace at that Governance SHA:

| Capability | Actual source and status |
|---|---|
| Stable gate IDs | `Gates/Gates.fs:gateIdOf` composes domain/check, already supporting `build:build`, `test:test`, `evidence:evidence`. Implemented; no new router needed. |
| Typed config | `Config/Model.fsi`, `Schema.fs` accept capabilities schema 2; tooling schema 1 has command strings and minimum tool versions. `Schema.supportedVersionFor` reads SDD-owned `Fsgg.Schemas` constants. No complete neutral binding contract exists. |
| Execution | `GateRun/Plan.fs:commandFor` lexes legacy command strings; `GateExecution/Interpreter.fs` ultimately uses executable plus argument list. Reuse the latter representation without stringifying new argv. |
| Missing command | `CommandHost/CommandHost.fs` classifies every `commandFor` error as `NoCommand`. It does not distinguish a required executable neutral binding from a legitimate semantic-only gate. This is a later runtime join, not solved by a catalog. |
| Floors | `Inheritance/ReferenceProfile.fs` delegates `fsharp-constitution` to `SurfaceChecks.Profile`; the game profile embeds `gameplay:fr-covered` and `gameplay:production-journey`, commandless, block-on-ship. Keep this authority. |
| Product routing | 058's `ProductSurfaces` and additive `RouteJson` path are delivered; unchanged legacy output remains `fsgg.route/v2`. No route-output change is needed for C1. |
| Distribution | Root `pack-reference-gate-set.fsx` pins 1.7.0; packaging project emits content-only package, four YAMLs plus controlled-import assets. Existing tests pack/install it and guard source/archive equality. Source inspection is not fresh installed qualification. |

## Contract decision for the first window

Implement C1 as an additive public module in the existing `FS.GG.Governance.Config` project. Do not add fields to `Model.Check`, `CommandSpec`, `TypedFacts` or `Schema.RawSource`; do not change the four YAML schemas, loader, existing signatures or default files. A record extension would break all record constructors and require a coordinated schema producer release for no benefit to this bounded slice.

`CapabilityBindings.fsi/.fs` owns the eight selected meanings and a pure normalized resolver. The SDD descriptor is the owner of provider/catalog wire data. Governance consumes normalized input; it does not create a competing provider descriptor or a fifth auto-loaded `.fsgg` file. Reuse `Fsgg.Provider.DeclaredCommand` from the already referenced Contracts package (Executable plus ordered Arguments), and existing Config `Cost`, `EnvironmentClass`, `TimeoutLimit`, `GovernedPath` types where appropriate. Add local records/DUs only for the new API.

The semantic inventory is exactly:

| ID | Meaning | Required executable policy |
|---|---|---|
| `build:build` | Build selected product components | Command plus execution-evidence binding |
| `test:test` | Execute selected tests and functional journeys | Same |
| `lint:lint` | Enforce declared formatting/static analysis | Same |
| `evidence:evidence` | Validate required evidence and provenance | Same |
| `package:package` | Produce and verify the declared distributable | Same |
| `security:security` | Verify declared dependency/code security policy | Same |
| `public-surface:public-surface` | Validate declared API/CLI/module surface | Same |
| `release:release` | Verify release identities, coherence and applicable publication gates | Same; declaration grants no publication authority |

The table says how an obligation is satisfied when required. It does not automatically make all eight checks organization floors, change their maturity, or make CLI packs games. Requiredness is the union of caller-supplied trusted requirements and provider-declared required capabilities; a provider cannot clear a trusted requirement with `required:false`.

Public API shape to specify first (names can follow local conventions; semantics are fixed):

- `SemanticCapability` describes ID, meaning and executable requirement; `catalog` is the single authoritative list.
- `BindingKind = SemanticOnly | Command of Provider.DeclaredCommand`.
- `CapabilityBinding`: capability ID, required flag, platform IDs, tool IDs, evidence IDs, binding kind; executable metadata is explicit working directory, timeout, cost and environment. Missing executable metadata is representable as invalid input, not filled from ecosystem defaults.
- `ToolBinding`: stable ID and declared exact version; `EvidenceBinding`: stable ID, explicit format identity/version and governed relative output path. Exact pins are declarations, not observations of installed tools. SDD owns ecosystem-specific version and platform grammar; no Governance language-name switch.
- `ResolutionRequest`: selected platform; trusted required capability IDs; trusted semantic-only obligation IDs; known declared tools/evidence/platforms; supported evidence format IDs; provider bindings. The trusted policy and supported-format inputs are supplied independently of provider-owned declarations.
- `resolve : ResolutionRequest -> Resolution` produces either a complete deterministic resolved set or located diagnostics with no partial executable success. An optional unsupported capability is explicitly listed as unsupported and cannot produce a satisfied verdict.
- Diagnostics distinguish malformed input, duplicate identity, missing required binding, unknown required semantic ID, illegal semantic-only neutral obligation, unresolved tool/evidence/platform reference, unsupported evidence format, invalid path and missing/invalid execution limit. Sort deterministically by capability, field, code; preserve command argument order.
- `catalogJson : unit -> string` projects the canonical semantic table into the catalog payload below. Do not create a second handwritten semantic table in JSON.

Recognized semantic-only obligations come from the existing profile authority at the caller, never a provider's self-assertion. A trusted list cannot turn any of the eight neutral IDs into semantic-only. Do not map `public-surface:public-surface` to `fsharp:public-surface`, and do not replace F# or game checks with neutral aliases. Unknown required semantics refuse. Only resolved executable obligations require commands; genuine semantic-only floors remain commandless.

Required executable bindings need at least one evidence reference, all references must resolve uniquely, and all required formats must be supported. Declaration-only resolution cannot establish freshness, provenance, digest, actual tool version, process success, TRX/JUnit/SARIF/coverage semantics or package integrity. Those remain Unknown until the existing evidence/evaluator boundary observes them; no fabricated normalizer or cross-format success is permitted.

Path validation uses normalized governed paths and rejects absolute/escaping paths before normalization can hide intent. Reject blank executables, non-positive limits, duplicate references and mismatched selected platforms. Accept spaces, empty arguments and shell metacharacters as literal argv data; no shell expansion or legacy command-string roundtrip.

## Version and compatibility decision

| Surface | Decision and supported range |
|---|---|
| Existing YAML | Keep capabilities `2`; governance/policy/tooling `1`. Existing inputs and route JSON stay byte-compatible. C1 changes neither SDD `Schemas` nor governance handoff `2.0.0`. |
| Neutral catalog | New `fsgg.neutral-capability-catalog/v1`, `contractVersion:"1.0.0"`; C1 accepts/declares exactly 1.0.0. Future versions require explicit compatibility tests, not optimistic major-range parsing. This is semantic vocabulary, not provider wire data. |
| ReferenceGateSet | Select **1.8.0**: additive package shape with a new independently located catalog, per pack script's documented minor rule. Keep historical 1.7.0 bytes immutable. Proposed consumer compatibility window `[1.8.0,2.0.0)` applies to this additive catalog contract, while first adoption pins exactly 1.8.0 plus immutable artifact evidence. No evidence currently proves publication. |
| Legacy reference route | Legacy schema-2 consumers can retain exact 1.7.0. In 1.8.0 the original six `.fsgg/` files, schema manifest and default resolution behavior remain unchanged; the new catalog is outside that tree. Existing F# providers remain on the current compatibility route without forced migration. |
| Config API | Pure additive .fsi surface; proposed project version **0.3.0** from inspected 0.2.0. This is a source/API version decision, not a promise that Config is currently independently publicly distributed. Existing public delivery is principally the CLI plus content package; C3 resolves the actual coherent publish set. |
| SDD producer | SDD#928 planner proposes catalog schema 2/new descriptor protocol exactly 3.0.0, leaving descriptor 1.x/2.x distinct. C1 consumes only the existing DeclaredCommand type and does not pin an unpublished new SDD package. SDD3-to-Governance adapter and exact package compatibility must be qualified before Templates adoption. |

Catalog payload path: source `reference-gates/neutral-capabilities.json`, archive `contentFiles/any/any/neutral-capabilities.json`. Include schema, contractVersion and sorted capability objects from `catalog`. Leave `schema-manifest.json`'s existing four keys unchanged. Expose a read-only `FsggNeutralCapabilityCatalog` path in the existing buildTransitive targets; do not copy this file into old `.fsgg` trees automatically. Downstream SDD/Templates explicitly read the pinned package artifact, not a sibling checkout.

## Milestones and first executable window

- [x] **GOV-423-C1 — Additive semantic binding contract and distributable vocabulary** — route: routine.
  Source delivered in [PR #442](https://github.com/FS-GG/FS.GG.Governance/pull/442), main `09a20140c2a6cf0694b4a14ce896abf8c4674234` (2026-10-05). Additive resolver, surface and content catalog are landed; native/provider acceptance and public availability are separate.
  Prerequisite: accepted ADR-0092 and parent acceptance of this contract join. Outcome: public pure resolver, deterministic catalog projection, tested content-only 1.8.0 candidate. The initial implementation window, delivered by one owner in one coherent source PR. Specify Tier-1 shape, .fsi, meaningful tests and compatibility notes before implementation; retain 058 completion unchanged. Source tests can run before any native provider, source publication or installed consumer exists.
  Acceptance scope retained: all eight exact IDs; executable/semantic-only distinction; missing, duplicate, unsupported and malformed cases refuse; literal argv/order preserved; deterministic output across unordered input permutations; no legacy schema or route-golden drift; actual packed catalog equals the authoritative generated bytes; original six content files and manifest unchanged; real package resolution preserves prior consumer behavior. A local candidate does not close #423 or Templates gates.

- [ ] **GOV-423-C2 — Required binding admission reaches actual callers** — route: routine; bounded extension after C1.
  Depends on: accepted C1 API and SDD#928's concrete invocation/provenance adapter window. Outcome: the new provider route calls Governance resolver before target mutation/invocation, carries the complete declared metadata into effective provenance, and projects validated bindings into actual governance execution. `CommandHost`/`GateRun` must refuse required unresolved/unsupported executable bindings rather than classifying them as harmless `NoCommand`; old entrypoints retain behavior for legacy callers. Keep existing inheritance and maturity floors authoritative. Before dispatch, trace the actual SDD caller and its output into the selected Governance host; derive the narrow touch-set from that implementation, not every named semantic join.
  Acceptance boundary: required binding deletion, missing output, stale/mismatched provenance, unsupported evidence format and conflicting declarations yield refusal/Unknown at their owning boundary, never success; recognized game/F# command-free checks remain command-free. Real executable/argv fixture via GateExecution proves the resolver result reaches a process without a lexer roundtrip; no ecosystem provider qualification claimed. A freshness result may reuse existing evidence contracts but cannot be invented by this resolver.

- [ ] **GOV-423-C3 — Compatible producers are published and independently read back** — route: routine source/release preparation; publication is a separate protected effect.
  Depends on: C1+C2 coherent qualification and SDD928 compatible producer publication plan. An initial independently qualified Config-only distribution step may precede the remaining producer set after actual C1+C2/C2.3 acceptance; it does not consume historical ReferenceGateSet custody or complete C3. Publish the actual Governance runtime distribution containing the resolver/admission and ReferenceGateSet1.8.0; pin the new SDD Contracts/reference-set association only in the owning SDD release work, not by editing organization synced package props incidentally. Pack once; verify identical artifacts from both required feeds, exact tags/source, package manifests and catalog bytes. The runtime CLI successor is selected against its then-current version and public delta at this milestone; current source is 1.12.1, and C1 does not reserve a stale future CLI version. A content-only package cannot substitute for published enforcement code.

- [ ] **GOV-423-C4 — Templates441 consumes published contracts; receiver qualification closes the producer chain** — route: routine; later outline, not dispatched by this plan.
  Depends on C3 and compatible SDD928 publication. Templates adopts exact producer versions and emits four explicit provider bindings; install once-packed artifacts without sibling sources. Qualify actual build/lint/test/entry-point/package/security/public-surface/release evidence and negative binding controls for TypeScript CLI, JavaScript CLI, Rust CLI and Go CLI. Templates publication/readback then gates .github3010 wizard/registry adoption. Governance#423 closure must state the native issue's full acceptance and distinguish producer completion from provider/public receiver acceptance; no checkbox infers installed support.

## C1 touch-set and checks

The implementation owner may create/edit only the following paths for the ready window (new files marked `+`). These are reservations, not instructions to make unnecessary edits:

```
+ docs/roadmaps/governance-423-neutral-capability-bindings.md
+ docs/governance-design/neutral-capability-bindings.md
+ src/FS.GG.Governance.Config/CapabilityBindings.fsi
+ src/FS.GG.Governance.Config/CapabilityBindings.fs
  src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj
  surface/FS.GG.Governance.Config.surface.txt
+ tests/FS.GG.Governance.Config.Tests/CapabilityBindingsTests.fs
  tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj
+ reference-gates/neutral-capabilities.json
  reference-gates/README.md
  packaging/FS.GG.Governance.ReferenceGateSet/FS.GG.Governance.ReferenceGateSet.fsproj
  packaging/FS.GG.Governance.ReferenceGateSet/buildTransitive/FS.GG.Governance.ReferenceGateSet.targets
  pack-reference-gate-set.fsx
  tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetPackageTests.fs
  tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetDerivationTests.fs
  tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetResolutionTests.fs
  docs/tutorials/adopter-onboarding.md
```

Use the owning design document for the Tier-1 specification, API semantics and migration notes; use this roadmap for sequential tasks. Do not create a new numbered Spec Kit artifact family solely for routine ceremony. Source technical signature/spec/test obligations remain. No C1 edits to Config Model/Schema/Loader, Gates, Inheritance, ProductSurfaces, RouteJson, CommandHost, Scaffold, sample `.fsgg` YAML, shared package props, SDD source or workflows are expected. A discovered required expansion comes back to the integrator with evidence before implementation.

Focused future validation: Config semantic/surface tests; ReferenceGateSet guard, derivation, package and resolution tests; existing Gates/Inheritance controls and RouteJson goldens as compatibility regressions. The package test must independently compare catalog semantic IDs/requirements to the exported canonical table and compare archive bytes to source; changing implementation and a mirrored expected JSON alone is insufficient. Add catalog derivation to the existing `ReferenceGateSetGuardDerivation` test list, which the pack script already selects with `FullyQualifiedName~ReferenceGateSetGuard`; the ordinary pack path must refuse stale catalog content, and this test must not invoke pack recursively. Reuse the existing explicit `BLESS_REFERENCE_GATE_SET=1` regeneration route for the new projection. The test project already references Config, so no project reference change is required. CLR/native resources must be admitted by root; run pack/restore controls serially as existing fixtures require. This planning assignment executed none of these commands.

Run repository-required qualification, routine eligibility/operation fixtures and coherent validation under ADR-0084 for the exact candidate head. Root admits PR creation and controls its queue. Public API baseline change must be reviewed as additive, not blessed blindly. Do not publish or update consumer pins as an incidental result of source tests.

## §9.9 workspace behavior and downstream gates

C1 changes no generated workspace or lifecycle default. C2 changes the explicitly selected new descriptor route only after its producer is installed; fresh creation first changes at Templates441's published profile adoption. New TypeScript/JavaScript/Rust/Go CLI families then receive explicit product-owned commands and evidence with Governance-owned semantic IDs, rather than F# defaults. Existing `console`, `web`, `fable-game`, `fable-bindings` remain compatible and unchanged. Omitted lifecycle remains `sdd`; neither typed-sdd nor V2 activation is selected here.

Clean creation must consume exact SDD, Governance runtime, ReferenceGateSet and Templates packages, then independently prove no sibling checkout dependency, required binding refusal before invocation, actual tool/provenance evidence, reserved-tree ownership and no partial state on failure. Existing workspace upgrade is separate: preview conflicts, retain original bytes/owner content, record Migrated/Ambiguous/Unsupported using the existing SDD migration contract, preserve rollback evidence where applicable. After V2 activation, repair forward under ADR-0091; package rollback does not restore V1 authority.

SDD928 planner confirmed the additive resolver route. Its C1 implementation owner confirmed the source-level declaration join: open capability IDs, Required flag, platform/tool/evidence refs, and `SemanticOnly | Command of Provider.DeclaredCommand * CommandLimits`; limits carry positive `TimeoutSeconds`, declared `WorkingDirectory`, `CostClass` and `EnvironmentIds`. Governance maps the cost token to its existing cheap/medium/high/exhaustive vocabulary and resolves environment IDs against independently supplied supported environments; unsupported/missing values refuse, and the adapter must preserve declarations in provenance. These are structural declarations, not runtime enforcement or observations. This settles the bounded API join; final signatures must agree at integration and neither side claims a published protocol yet.

Proposed §9.8 edit: append the owning Governance subroadmap link and original #423 identity to the existing V2-LANG-01 row, preserving all current links and completed milestones. While unmerged cite this private draft in the dispatch packet; after delivery use `https://github.com/FS-GG/FS.GG.Governance/blob/<merge>/docs/roadmaps/governance-423-neutral-capability-bindings.md`. Do not add a parallel architecture row or change the R5 population. Root owns the asynchronous source-closure projection and its progression fence.

## Boundaries, invalidation and accounting

Stop C1 at a coherent locally prepared source candidate and parent handoff, or at a demonstrated shared-contract conflict. Do not expand into native language runtimes, compile/model/browser experiments, remote publication, board state or retained upgrade. A requirements decision that demands in-place YAML/record mutation, changes neutral command-free semantics, shifts floors or changes package compatibility invalidates this additive plan and needs a narrow replan. No such decision is assumed.


## C3 Config release-source extension

The existing publisher now selects Config as an independent library output using its evaluated source
version, preserving CLI-led dispatch semantics. The [094 publication contract](../../specs/094-nuget-org-publish/contracts/publish-workflow-nuget-org.md#config-resolver-library-extension-gov-423-c3)
and [package qualification controls](../../tests/config-package-smoke/README.md) define the full Config
suite, one archive, explicit package-only consumer, retention and ordered feed effects. The cheap static
preflight validates the actual job before restore and rejects seven publication mutations; synthetic
archive controls cover malformed identity, closure, assembly/API, payload and candidate count.

This source extension leaves C2/C3/C4 open. Actual Config pack/manifest/dependency closure, CLR suite,
package consumption and hosted exact-head/coherent qualification remain unrun in the source-only
implementation window. No package/public pin is inferred from source 0.3.0. A locally qualified archive
may enable SDD C2.2 source preparation only; its normal pin follows independent public readback.
Config's current committed closure selects published Contracts 7.6.0; a package pin alone does not
prove candidate-bound compatibility or the actual SDD/Verify join. Root must qualify C1+C2/C2.3,
select an independent Config-only operation and qualify its exact archive/consumer closure before
collision admission, org-first same-byte publication and independent both-feed/public readback.
That initial Config distribution may proceed without consuming the retained ReferenceGateSet 1.8.0
candidate. Its original custody and operation holds remain reserved for historical recovery,
ReferenceGateSet publication/first adoption, broader publication and C3 completion. The earlier local archive
cannot automatically qualify a later rebuilt release archive. Existing runtime versions remain as
inspected until the real coherent release delta selects their successors.

## C2 selected source phases

The accepted explicit Verify context join is phased within existing GOV-423-C2. Phase 1 prepares
additive complete-request Config-typed planning in GateRun/CommandHost concurrently with SDD928 C2.2.
Its public pure API resolves the whole request before selected gates, blocks required gate omission
and executable-floor downgrade, preserves literal argv/limits/admitted environment, keeps unsupported
and deferred outcomes non-passing, and bypasses legacy cache reuse. The owning design supplies the
Tier-1 specification; signatures/API exercise, focused semantic tests and additive surface baselines
precede implementation/qualification. No C2 completion is recorded for this slice.

Phase 2 uses actual qualified SDD Commands/Artifacts libraries for the explicit schema2-provenance /
independent-policy Verify loader and existing GateExecution edge, after root selects the exact local
package closure. No placeholder loader or public package pin is admitted. Phase 3 joins real SDD
C2.2 output into actual Verify/GateExecution with source/evidence revalidation and bounded execution.
That actual caller acceptance, coherent source delivery and the separate C3 release decision remain
open. Original ReferenceGateSet1.8 custody and existing native operation holds are unchanged.

Local phase-1 validation: GateRun 27/27 and CommandHost 30/30 passed, with no failed or skipped
cases; the 25 new controls use synthetic normalized declarations, effective gates and execution
inputs through actual Config. Surface/dependency checks matched 3 and 2 cases respectively, and
both reflected baselines are additive. An initial build failed on strict F# nullness checks; the
corrected source passed both complete suites in a separately selected qualification window. The
original failure and successful offline restore evidence remain retained separately. Source merge,
full Debug/Release coherent CI, actual Verify integration, provider/evidence acceptance and release
remain pending; GOV-423-C2 stays open.

## C2 bounded direct-child source window

The additive GateExecution CurrentHost/direct-child component has local qualification for its selected
weaker scope. Callers explicitly accept UncontainedUnobserved descendants and observed paths without
atomic binding; stronger requirements refuse. One-use caller custody precedes launch, original monotonic
work/cleanup ends do not renew, concurrent stream capture is capped before retention, and observations
separate direct exit, EOF/closure, failure and settlement. Legacy entrypoints/cache behavior remains.
Runtime signal identity is unqualified; pending/ambiguous ownership is retained rather than signaled.

Qualification: 29 synthetic public API exercise controls, actual Release compilation, three
surface/dependency/scope checks, ten synthetic workflow controls and ten real direct-process controls.
A separately admitted held-pipe test passed with direct exit0, streams pending, retained session and
work/cleanup failures; its private guardian observed/adopted/reaped the finite holder, then reaped the
runner after EOF. The missing-executable control separately recorded LaunchOutcomeUnknown/no exit/
Retained and retained its actual CLR caller until selected external SIGKILL retirement. Exact adopted
runtime and final runner statuses were consumed; this is external-control acceptance, not a normal
unit-test pass or public Released. Private fixture reaping grants no production descendant guarantee.

Native cases are pending by default, with separate exact opt-ins that schedule only. Default and
nonexact direct-case routes each skipped ten cases; corrected-source default discovery skipped the two
remaining cases. These skipped outcomes are not native passes. Positive private qualification requires
an independently admitted real owner; ordinary hosted full-suite CI supplies no such owner.

Three compile failures and the original held-readiness runtime failure remain retained separately.
The original held launch/expiry cause remains unknown because its run snapshot was not retained before
assertion. Successful successors used new original 1-second work/3-second cleanup fixture budgets and
bounded preassertion snapshots, preserving the finite 10-second holder and original outer deadlines.

This closes the selected component's local fixture window only. Coherent source delivery, actual Verify
incoming-context/package-backed SDD caller acceptance, stronger enforcing profiles and GOV423-C2 remain
open. C3 coherent producer publication/readback and C4 public receivers remain separately gated; no
release, installed support, scope-empty or general reaping claim follows. Original native/installed
custody holds and the attributed telemetry gap remain unchanged.


### GOV423-C2 explicit selector source slice (2026-10-09)

Tier1 additive host API: `ProviderContext.select` validates the five explicit provenance/policy/path-digest/platform options together; `Loop.parseInvocation` wraps the existing `RunRequest` without changing legacy `parse`, constructors or execution. Raw locators, digest spelling, platform and legacy argv are retained; no reads, discovery, schema fallback or recorded policy authority are added. Duplicate/incomplete/missing/empty/malformed selection returns located provider usage errors. Legacy diagnostics remain separately typed and token boundaries remain intact, including `--paths` and missing legacy values.

Order: specification and signatures, full additive API/semantic exercise, then production body and focused legacy/parser/public-surface checks. This slice leaves Program on legacy parsing pending the actual SDD package loader. It is not provider-mode availability, actual Verify caller acceptance or completion of C2. Commands/Artifacts once-packed coherent closure and physical capture/execution-profile joins remain pending; CurrentHost observed-path/uncontained guarantees cannot silently replace stronger loader requirements. C2.3 real producer/Verify journey and C3 publication/carrier gates remain separate. No generated defaults or installed behavior change; existing callers require no migration.

Local qualification passed the API-first exercise's 62 synthetic selector/wrapper controls, actual
Release compilation, all three surface/dependency checks and the complete Verify suite: 164/164
passed, including all 62 selector cases. The reflected baseline is additive; all previous exported
members remain. Cold locked restore also verified all 21 normalized package content hashes against
the committed locks, using the separately retained raw archives. No package publication follows.

Three earlier API compiler failures remain retained separately. The first full-suite attempt stopped
at an observed resource bound without retaining its dimension or test outcomes. A sequenced successor
recorded a fourth CLR exceeding the three-CLR limit; its retirement exit0 was not a test pass. The
successful successor retained the same source, compiled outputs and reviewed baseline, explicitly
sequenced Expecto scheduling, and allowed four CLR processes for CLI/VSTest/testhost plus the real
FCS SDK resolver. Other resource bounds remained unchanged. The resolver path is supported by exact
package-commit source; the failed fourth process's historical argv remains unknown.

This validates the additive selector/wrapper and legacy semantics only. Program remains on legacy
parsing; no loader, filesystem discovery, provider profile acceptance, SDD package availability or
installed behavior is claimed. Exact-head coherent hosted validation and delivery remain separate.
The real C2 caller, C2.3 producer journey and C3 release gates stay open. Original operation/custody
unknowns and the attributed telemetry gap remain unchanged.


### GOV423-C2 retained provider document capture (Tier 1; local qualification)

The independently selected capture slice adds `ProviderContextCapture` to the existing Verify host.
Its caller holds an opaque one-use session before any acquisition, including original monotonic work
and cleanup ends, cancellation, fixed byte caps and explicit cooperative namespace custody. It
captures provenance under the selected repository root and policy under its independently selected
absolute parent. Policy bytes never derive authority from workspace provenance or digest equality.

Linux-x64 acquisition requires qualified `openat2`/`statx` and procfs: hold directory roots, resolve
regular files beneath them without links or mount crossings, acquire `O_PATH`, check actual type,
then reopen the same held object through an internally formed owned-descriptor binding. There is no
BCL pathname fallback, process-global cwd change or data open of an unverified device/FIFO.

Pure Model/Msg/Effect transitions precede the interpreter. Fixed buffers charge per-file and aggregate
bytes before storage. After capturing the complete set, compare original held files and current
pathname bindings again; revalidation repeats that comparison before dependent use without accepting
replacement bytes. Immutable observations distinguish sticky first/distinct secondary failures,
original deadlines/cancellation and actual resource settlement. Pending activities retain the same
caller owner. Later retirement can release settled resources without retroactive bounded success.

This observes held objects under explicitly cooperative file/namespace custody. It does not freeze
contents or prove simultaneous multi-file consistency, absence of earlier ABA, hostile-writer safety,
execution containment or atomic cwd launch binding. Stronger namespace requirements refuse. No
Loop/Program wiring, SDD package pin, provider profile or whole GOV423-C2 acceptance is supplied.

Local qualification passed 36 synthetic public API controls, a separate 35-control Linux primitive
gate, the complete Verify Release compile and three surface controls. The default Verify suite passed
200 tests and left all 15 physical capture cases pending. A separately admitted, supervised invocation
then passed all 15 physical cases: held-root/file acquisition, immutable bytes and revalidation,
per-file/aggregate caps, digest refusal, symlink/type refusal, file and root changes, cancellation,
original deadline expiry and one-use retirement. Both managed invocations observed actual terminal
exit, EOF and known-leader-only retirement within their original selected windows. Two earlier managed
compile failures remain preserved; the successful successor does not reclassify them.

Physical cases remain pending by default. The environment opt-in schedules only; the separate local
fixture route retains the actual caller session and unsignaled runtime/outer owner on incomplete
settlement, including retirement or projection exceptions. These 15 cases do not force a blocked
native read or uncertain descriptor close: those physical retention paths remain unqualified.
API/primitive checks and static review do not substitute for that coverage. Full loader/Program wiring,
strong provider profile and coherent SDD package joins remain open, so GOV423-C2 is not complete.
Original custody unknowns and the attributed telemetry gap remain retained.


### GOV423-C2 Verify planning transition (2026-10-09)

The [provider Loop join](../governance-design/neutral-capability-bindings.md#gov423-c2-verify-planning-transition-2026-10-09)
is locally validated. Eight synthetic normalized-input tests exercise actual Verify selection and
complete-request refusal. Actual Release compilation and the full Verify suite passed: 208 tests,
zero failures/errors; 15 physical capture cases remain pending. Legacy goldens and three surface/
dependency checks passed. Reflected changes add two entry points and two records while preserving
legacy constructors.

The shared catalog runtime harness observed stable declared product source/test/surface/config
identities and process exit/stream closure. Full-suite tests used its explicitly selected PID
namespace backend; this is test-process custody, not provider environment qualification. Two
preceding test-source compiler failures, a malformed runner invocation and two identity refusals
from generated readiness sidecars remain retained separately. The final identity scope excludes
runtime readiness output; earlier failed results remain failed.

Source delivery/coherent hosted checks, actual package-backed loader/runtime/evidence joins and
producer acceptance remain open. Program stays legacy and valid provider plans remain explicitly
non-passing. GOV423-C2, publication/carrier selection and installed adoption remain incomplete;
existing custody unknowns and the attributed telemetry gap are unchanged.

## C3 bounded Config forward recovery

The existing Config publisher accepts a complete original run/artifact/package-hash selection only
for explicit manual Config recovery at the same immutable source/version. Native first-attempt
workflow/job/step and artifact observations establish original qualification and retention; archive
identity and selected dependency closure are checked by the existing reader. Recovery skips every
restore/test/pack/consumer-generation step and reuses the original archives and native evidence.
Unknown original writes, conflicts, incomplete retention and inaccessible observations refuse;
root observation remains necessary before selecting recovery. Original candidate bytes are retained
before effects and copied to durable private custody while available. A recovery-uploaded copy is
not a new candidate, and no-push candidates cannot be promoted by this route.

This source amendment does not qualify a release archive, dispatch, publish, reconcile original
holds or complete C2/C3/C4. Runtime carrier publication, exact ReferenceGateSet 1.8.0 adoption,
Templates/public receivers and broader release completion retain their applicable evidence gates.

### GOV423-C2 controlled retention source window (2026-10-15)

Owner: Governance source lane. Status: [source window delivered](https://github.com/FS-GG/FS.GG.Governance/pull/459)
at `8a6a74c701ccc713f28723fad5eea539acaada38`; C2 remains unchecked. This extends the existing capture milestone and preserves all earlier failures and custody
unknowns. The historical 15-case report above describes #449; #453 subsequently added held-root lease
coverage and reported 20 physical controls. Those reports do not qualify blocked kernel reads or
uncertain physical closes.

The bounded seven-path change adds internal per-session native/clock operations, immutable ownership
facts and ten finite controls through the actual capture interpreter. Native result/errno association,
original task registration, one-use acquisition, byte caps, cooperative comparisons and the public
signature remain preserved. A post-read work check prevents a late read from accepting new bytes.
Synthetic sessions cannot duplicate native leases; production's outstanding lease fences stay intact.
The surface guard explicitly rejects exported operation/test types and entry points.

Local validation covered source, exact touch set, diff and relative links; no local CLR or native
fixture ran. [Hosted qualification](https://github.com/FS-GG/FS.GG.Governance/actions/runs/38692065699)
passed the required source matrix and advisory API job at `8c242c6133b36206f9ff83fc287684840788a777`.
VerifyCommand passed 219 tests in each of Debug and Release, with all 20 physical cases skipped.
The first `b086c148` build failed on two nullable reflection accesses; its successor repaired the
guard. After rebase, the first routine event refused the stale head marker; the subsequent authentic
edited event passed after rebinding. Both failures remain preserved. The protected delivery helper
accepted routine eligibility and coherent validation, then one guarded apply delivered #459.
This establishes source qualification and delivery only; publication, installed adoption, physical
blocked-read/uncertain-close acceptance and the rest of C2 remain open.

The remaining physical gate needs an accountable supplying owner, an authentic regular-file blocked
read accepted by production acquisition, independently observed uncertain close and demonstrated
whole-worker/resource teardown. No backend is qualified by this window. Keep those physical cases
pending and retain the original caller owner and failure evidence. The existing provider loader,
Program/Verify integration, stronger profiles, coherent SDD consumer joins and C3/C4 milestones remain
open. The owning Atlas archive records `governance/provider-capture-retention-boundary`; retrieve it
with the Atlas helper and verify its immutable source citations before reuse.


### GOV423-C2 phase-2 package loader preparation (2026-10-15)

Source preparation targets the retained SDD Commands/Artifacts 2.3.0 candidate from
run `37951954780`, artifact `11626413573`, source
`c8aea9289a6cc410f076823bf1219a02314f00a7`. As of 2026-10-22 SDD 2.3.0 is published from that
exact candidate (FS-GG/FS.GG.SDD#1113) and a cold locked restore of the org feed matches the
committed lock hashes and the retained archive digests for Commands, Artifacts and Knowledge. The package-backed adapter decodes immutable observations from the
original Ready capture session, strictly parses schema-2 provenance and independently selected
policy, binds policy/platform/admission correspondence, and recomputes the whole Config request
before catalog selection, cache or execution. Literal argv stays typed throughout.

The provider interpreter drives the existing non-passing Verify planning transition. Program
parses all explicit provider selectors and supplies a caller-owned-session adapter; it never
acquires or releases that session. Standalone `main` explicitly returns input-unavailable for
provider mode because the outer capture lifetime and teardown boundary is not yet implemented.
The owner is decided in the 2026-10-22 section below; the production entry is still a
prerequisite, not usable provider-mode availability. Legacy invocations retain the existing
interpreter path.

Focused source controls cover actual package codecs, raw digests, strict UTF-8/schema refusal,
independent policy/platform correspondence and the Program-to-planning join. Fixtures are
synthetic declarations and observations, not execution evidence. Local CLR, restore/lock
regeneration, compilation and test execution were held under the programme resource limit when
this section was first written; they have since run (2026-10-22: locked restore, build, and the
Verify suite in Debug and Release, with the hosted gate set on the reviewed head). The
`ProviderContextLoader.load` capture-readiness selection and the `CapabilityBindings.Rejected`
branch of `decode` were untested at the PR #462 merge (`c80e8903`); the section below records
their direct controls. Package-only qualification, physical capture acceptance, production owner integration, source/evidence revalidation and
the real SDD-to-Verify/GateExecution journey remain open. C2 remains unchecked.

### GOV423-C2 direct loader controls and capture-lifetime owner decision (2026-10-22)

Owner: Governance source lane, Delivery [#465](https://github.com/FS-GG/FS.GG.Governance/issues/465),
the remainder named by the closed [#463](https://github.com/FS-GG/FS.GG.Governance/issues/463).
Source base: PR #462 merge `c80e8903`. C2 remains unchecked. This section changes tests and this
document only; `Program.main` still returns input-unavailable for provider mode.

**Direct controls.** `ProviderContextCaptureTests.fs` now drives `ProviderContextLoader.load` and
`Program.runWithCapturedProvider` over a ready session produced by the real capture interpreter with
the existing inert per-session operations. That ready-capture fixture stays private to the capture
tests; it serves the coherent schema-2 provenance and policy bytes and has two finite gates, the
existing read gate and a new metadata-read gate held inside a revalidation window. Nothing is acquired from the kernel. Load cases:
decode from the captured bytes and leave the caller's session unchanged (model, bytes, roots,
ownership and close attempts), case-insensitive digest selection, selected platform, undecodable
captured bytes, never-captured, failed-capture, drifted-binding, released, in-flight revalidation,
work end, cleanup end and cancellation refusals, and selected path, digest and role mismatches. The
adapter cases cover a bound session reaching nonpassing planning, supplier refusal, a context for
another repository, a repository that is not an original captured root, a root without freshly
confirmed binding, loader refusal retention, and that the adapter never revalidates or releases the
caller's session. Three `decode` cases reach the `CapabilityBindings.Rejected` branch (missing tool,
missing evidence, semantic-only downgrade of an independently required obligation).

**Negative-control scope.** Each control was mutated in a scratch copy of the guarded source
(the matrix is recorded in PR #467; the list below is the durable statement). The guards proven independently load-bearing are: the
in-flight revalidation phase, work end, cancellation, selected path, selected digest, role, the
case-insensitive digest comparison, the captured-byte routing into `decode`, the `Rejected`
mapping, the adapter's context/root/binding checks, its diagnostic and supplier-message
retention, and its no-release/no-revalidate property. Several readiness guards (captured-once,
first/secondary failure, settlement, cleanup end, non-empty roots, current root binding,
comparison count, exactly-two documents) overlap the phase, retirement or work-end state of the
real interpreter, so no real session isolates them; they are defense in depth, not separately
proven. Removing the phase and root guards together shows the settlement guard load-bearing.

**Decision: the Verify invocation owns the capture; the outer admitted runtime owns teardown.**
For one `fsgg verify` provider invocation, the Verify host composition root in
`FS.GG.Governance.VerifyCommand` (the entry that today supplies the standalone refusal) is the
outer capture-lifetime owner. It must build the capture request from the original work and cleanup
ends the outer admitted runtime gives the invocation, prepare the single `CaptureSession` before any acquisition, keep it for the
whole invocation (capture, `load`, planning and any later `borrowRepositoryRoot` execution lease)
and be the only code that calls `release`. It never disposes, finalizes, replaces, renews or
signals around an unsettled session. If `release` is not `Ok` by the original cleanup end, it keeps
the session, reports a non-passing result and stays resident rather than exiting while a read may
be pending; whole-worker termination and resource reclamation belong to the outer admitted runtime
that supplied those ends (today the root-admitted supervised route; for portable execution the
workspace executor carrier). The exit mapping for any return is open prerequisite (a).

Basis: (1) the capture contract already requires the caller to hold the actual owner before
acquisition and after any incomplete return, forbids any disposal, finalizer or replacement owner
from abandoning descriptors or pending activities, and fixes original ends in the process monotonic
domain that cannot renew; whoever forms the request owns the session. (2) `runWithCapturedProvider`
already assumes the supplying caller retains every returned session and performs no acquisition,
renewal or release, so the owner must sit above the adapter, exactly where `main` stubs it.
(3) The borrowed repository-root lease and direct-execution borrowers fence `release` on the same
session, so one owner must span planning and execution; a pre-step or separate process would
create the replacement owner the contract forbids. (4) SDD and providers supply declarations and
packages; the independent policy and its capture belong to the Governance verifier, and workspace
provenance grants no policy authority. SDD#928 C3.1-03 consumes this join and does not own it.
(5) #423 makes Governance the supplier of semantic obligations and checks to the workspace
execution contract; process lifecycle and whole-worker teardown stay with the runtime carrier, as
the physical capture fixture retains the session with an unsignaled outer owner, and the
GateExecution held-pipe control has an independent guardian observe and reap. Rejected: a library finalizer or `IDisposable` auto-release, a
`main` that exits on timeout while a read may be pending, an SDD- or provider-owned capture, and a
detached Verify background supervisor.

This decision implements nothing. It is a Governance source-owner decision; acknowledgement by the
SDD#928 and [.github#4648](https://github.com/FS-GG/.github/issues/4648) owners and by the carrier
owner is not recorded here.

**Open prerequisites (unchanged by this section).** (a) Host composition source and its non-zero
exit mapping for an unsettled session. (b) An authentic regular-file blocked read accepted by
production acquisition, with an independently observed uncertain physical close. (c) Demonstrated
whole-worker and resource teardown by the outer runtime for those two cases, with a named
accountable supplier. (d) Real SDD to Verify to GateExecution acceptance with source and evidence
revalidation. (e) Stronger profiles, C3/C4 and the original custody holds. The inert controls above
and hosted source qualification discharge none of these.


### GOV423-C2 Verify capture-lifetime host composition (2026-10-22)

Owner: Governance source lane, Delivery [#468](https://github.com/FS-GG/FS.GG.Governance/issues/468),
implementing the decision recorded above (#465, PR #467 `fef152ac`). C2 remains unchecked. This section
supersedes the earlier statements that `Program.main` returns input-unavailable for provider mode: provider mode
now runs through the host below. Legacy invocations never reach it.

**Composition.** `ProviderHost` (internal, `FS.GG.Governance.VerifyCommand`) is the outer capture-lifetime owner,
and `Program.main` is `runHosted` over the real clock, environment, `ProviderContextCapture.prepare` and
signal-driven cancellation. For one provider invocation it: reads the original work and cleanup ends the outer
admitted runtime supplies (`FSGG_VERIFY_WORK_SECONDS` and `FSGG_VERIFY_CLEANUP_SECONDS`, whole seconds from host
start, cleanup not before work; absent or inconsistent ends admit nothing and start nothing); prepares the single
`CaptureSession` and retains it before any acquisition; captures once; hands the same session to
`runWithCapturedProvider` (loader and planning); and then is the only caller of `release`. It derives the planning
context from the observed `CI` variable, an empty environment delta and the whole seconds left in the work window.
Exceptions during the invocation are contained and still reach settlement.

**Settlement and exit mapping.** After the invocation the host calls `release` until it returns `Ok`. It never
disposes, finalizes, replaces, renews or signals around a session whose release is refused.

| Outcome | Host behaviour | Exit |
|---|---|---|
| `release` returns `Ok` without a prior unsettled report | none | the invocation's own exit (1 blocked, 3 input unavailable); a passing 0 on a session with a recorded failure becomes 4 |
| Not released, original cleanup end not reached | wait and retry, silent | pending |
| Not released at or after the original cleanup end, a read may be pending (activity, uncertain close or borrowed lease) | report once; keep the session; stay resident; keep retrying | none until the outer runtime ends the worker, or 4 if the session is released after that report |
| Not released after the cleanup end, nothing pending (retained descriptors only) | report once; exit | 4 |

The exit is the existing tool-error code. Before declaring a session retained with nothing pending, the owner makes
one last `release` attempt so a session that did settle is never reported as retained. Signals (SIGINT, SIGTERM) in
provider mode request cancellation of the capture and do not terminate the host (cancellation reaches only the
capture: once planning starts, a signal is swallowed until planning returns); whole-worker termination and resource reclamation remain with the outer
admitted runtime. If the outer runtime never ends a worker whose read is pending, the host stays resident: that is
the recorded decision, not a defect of this slice.

**Controls.** `ProviderContextCaptureTests.fs` drives the host over the real capture interpreter with finite
synthetic descriptors and gates: settled, refused (unadmitted ends, invalid request, failed capture), cancellation
(before capture and with a read in flight), deadline (a read returning inside the window, and one pending past
the cleanup end), uncertain close, a fault during planning, and legacy isolation, plus pure tests of admission,
the planning context and the exit mapping. Each guard was mutated in a scratch copy (matrix in the PR).

**Not claimed.** The `FSGG_VERIFY_WORK_SECONDS` / `FSGG_VERIFY_CLEANUP_SECONDS` interface is a Governance source-owner choice;
acknowledgement by the carrier or runtime owner that supplies the ends is not recorded. Nothing here is a real blocked regular-file read, an independently observed uncertain physical
close, demonstrated whole-worker teardown, the named accountable supplier for it, or the real SDD to Verify to
GateExecution journey. The 20 physical cases remain pending. Prerequisite (a) of the decision (host composition
source and its non-zero exit mapping) is implemented by this slice; (b) through (e) are unchanged. The
`borrowRepositoryRoot` execution lease is not used because provider planning executes nothing.

### GOV423-C2 Verify host shipped through the published tool (2026-10-22, #470)

`FS.GG.Governance.VerifyCommand` was `IsPackable=false` and in no published artifact, so no release
(1.13.0 included) carried the #462 loader or the #469 host. #470 ships it through the existing
`FS.GG.Governance.Cli` tool, with no new package id or tool command: `fsgg-governance verify <args>`
forwards every argument after the verb to `VerifyCommand.Program.run`, before the dispatcher's own parser.

- `VerifyCommand` is now a library (an executable may not reference another executable, `ExeLeafTests`);
  `Program.main` became `Program.run`, which holds the `SignalCancellation` owner and calls `runHosted` exactly as
  `main` did. The host, loader, capture, settlement and exit mapping are unchanged (Verify suite 290 passed,
  20 physical pending).
- The CLI tool closure now bundles `VerifyCommand` and the SDD `Commands`/`Artifacts`/`Knowledge` 2.3.0 pair with
  their transitive packages (locked in `Cli/packages.lock.json`); the tool package still declares no dependency.
- Controls: `VerifyVerbTests` spawn the built host binary (usage grammar and exit 2 versus the dispatcher's 64 for
  the same bad flag, legacy input-unavailable 3, provider mode unadmitted/inconsistent/admitted ends); the
  `cli-publish-smoke` and `PackagingTests` installed-tool runs assert the host assemblies are in `tools/**` and
  `verify` answers from the installed tool. Removing the dispatch fails all six spawned-host tests.
- Release: Cli and FSharpSurfaceCommand 1.14.0 (new shipped surface through the CLI package).

**Not claimed.** Provider mode being reachable from a published tool is not the 20 physical cases, whole-worker
teardown, the named carrier of the outer ends or the real SDD to Verify to GateExecution journey; those remain open.

### GOV423-C2 provider execution and evidence acceptance join (2026-10-23)

**Owner and trigger.** Owner: the Governance source lane (fdev). FS-GG/FS.GG.SDD#928 C3.1-03 ran published SDD 2.4.0
against Governance 1.14.0 ([evidence](https://github.com/FS-GG/FS.GG.SDD/issues/928#issuecomment-6359021512)).
Provider-mode `fsgg-governance verify` stopped after planning: the positive case exited 1 with
`provider planning completed; execution and evidence acceptance are not connected`.

**This slice.** It connects that planning to execution and to evidence acceptance. The design is in
[the design document](../governance-design/neutral-capability-bindings.md#gov423-c2-provider-execution-and-evidence-acceptance-2026-10-23).

* Each admitted executable binding runs once:
  * as a real child process through the existing GateExecution held-cwd direct-child host;
  * under the Verify host's single capture owner, after revalidating the captured documents through
    `borrowRepositoryRoot`;
  * within the original work and cleanup ends, with the work end capped by the declared timeout.
* Governance accepts the run only on evidence it observes itself:
  * the evidence is `fsgg.governance-handoff@2.x`;
  * it was absent before launch;
  * it is strictly parsed with `sources[]`;
  * its sources equal the workspace bytes;
  * it declares passing content.
* The refusals already in place are preserved:
  * provenance digest, policy identity and platform refuse at capture or admission;
  * an unrouted path and a required capability without a selected gate refuse at planning;
  * missing time limits refuse at resolution.
* A required capability that is not accepted blocks whatever its maturity. The legacy Verify route is unchanged.

**Controls (local, this host: Linux-x64, glibc 2.44, SDK 10.0.401).**

| Suite | Result |
|---|---|
| `VerifyCommand.Tests` | 333 passed. The 26 skipped are the 20 existing physical capture cases and the 6 new opt-in native cases. |
| Opt-in native journey (`FSGG_GOV_PROVIDER_EXECUTION_NATIVE=1`) | 6 of 6 passed through the real `Program.runHosted`, real capture and a real POSIX sh fixture launched by `prepareHeldCwd`. |
| Cli.Tests | 80 passed |
| DesignChecks.Tests | 57 passed |
| `tests/cli-publish-smoke/run.sh` | passed on packed CLI 1.15.0 |

The native journey covered these cases:

* positive: the fixture's `run` writes the declared handoff, and Verify exits 0;
* missing evidence: exits 1;
* stale (pre-existing) output: never launched over, left untouched, exits 1;
* mismatched sources: exits 1;
* real exit 2: exits 1;
* provenance digest, platform, unrouted path and policy identity: exit 3 with nothing launched.

The rest of the evidence:

* **Mutation matrix.** Eight guards were each mutated in place and every mutation was killed: the evidence
  refusals, the pre-launch absence check, source validation, the required floor, the Governance-computed digest, the
  non-zero exit, content acceptance and the format check.
* **Full Release solution suite.** 3000 passed. It had two failures, both outside this change. One was the
  DesignChecks packed-tool case, built before the version bump in this branch; it passed after rebuilding. The other
  was `ReferenceGateSetResolution.R7`. That case compares against the locally installed `fsgg-sdd` 2.2.0 and fails
  on its extra files, `governance-resolution.proj` and `knowledge-guide.md`. Hosted CI has no such tool and uses the
  fixture.

**Decisions recorded with this slice.**

* *SDD version skew.* Verify keeps the bundled SDD `Commands`/`Artifacts` 2.3.0 and accepts SDD 2.4.0 provenance.
  The wire contract is the closed schema-2 provenance codec, the policy parser and `ProviderCapabilityAdmission`, not
  the producer package version. Between source `c8aea928` (2.3.0) and `c1fee66a` (2.4.0) none of
  `ScaffoldProvenanceDocument`, `CatalogScaffoldProvenance`, `CatalogScaffoldPolicy`, `ProviderCapabilityAdmission`
  or the `Fsgg` contracts changed. The 2.4.0 delta (#1116) changes only how scaffold encodes template override argv,
  and that argv is recorded as data. A future codec change is refused by the strict parsers rather than misread.
  Moving the bundled pair is a separate release decision.
* *Workspace Governance catalog.* Provider-mode gates come from the workspace's own `.fsgg` catalog: routing, owners
  and maturity. Provider provenance never supplies them, and Governance does not synthesize gates from declarations,
  because that would let provider data create its own floors. A scaffolded workspace therefore needs a Governance
  catalog that routes its verify scope to a gate whose id equals each required capability id. The planning refusal
  now says so. Supplying that catalog at creation belongs to workspace composition: Templates#441 and the SDD
  scaffold composition (C4). It is not part of Verify.

**Still open.** C2 stays unchecked, and none of the following is supplied by this slice:

* Governance 1.15.0 publication with both-feed readback.
* The installed re-run of the SDD#928 C3.1-03 / SDD928-C2.3 journey, with a fixture whose `run` writes the declared
  evidence plus its missing, stale and mismatched variants.
* Prerequisites (b) and (c) above: the physical blocked read, the observed uncertain close, and whole-worker teardown
  with a named supplier.
* Stronger execution profiles, which this cooperative held-cwd profile does not provide. Held-cwd execution needs
  glibc 2.40 or later, so hosted runners with an older glibc report not-launched and block.
* Required-input coverage for evidence `sources[]` (`requiredPaths`), which is not imposed.
* C3 and C4.

The release preparation is in [the 1.15.0 report](../reports/2026-10-23-governance-1.15.0-release-preparation.md).
