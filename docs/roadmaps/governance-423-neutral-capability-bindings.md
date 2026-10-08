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
  Depends on: C1+C2 coherent qualification and SDD928 compatible producer publication plan. Publish the actual Governance runtime distribution containing the resolver/admission and ReferenceGateSet1.8.0; pin the new SDD Contracts/reference-set association only in the owning SDD release work, not by editing organization synced package props incidentally. Pack once; verify identical artifacts from both required feeds, exact tags/source, package manifests and catalog bytes. The runtime CLI successor is selected against its then-current version and public delta at this milestone; current source is 1.12.1, and C1 does not reserve a stale future CLI version. A content-only package cannot substitute for published enforcement code.

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
Contracts 7.5.2 in Config's committed lock does not prove the SDD Contracts 7.6.0 cross-repository join.
Root must qualify C1+C2, choose the coherent release set, resolve collision/visibility observations and
preserve original ReferenceGateSet 1.8.0 custody before dispatch/publication. The earlier local archive
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
