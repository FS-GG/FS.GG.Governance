# FS.GG.Governance replacement capability analysis for FS-Skia-UI

**Timestamp:** 2026-06-18T23:37:18+02:00
**Revision:** 2026-06-19T00:13:40+02:00
**Author:** Codex
**Status:** Research update, no implementation changes
**Scope:** Research the current FS-Skia-UI repository and define what FS.GG.Governance must own so the new FS.GG system replaces FS-Skia-UI's governance/build/product-generation capability envelope, not merely Spec Kit.

## Executive summary

The target should be a full FS.GG governance and generation framework that makes FS-Skia-UI unnecessary as a capability provider. The prior direction, "replace Spec Kit," was correct but too narrow. FS-Skia-UI is not just a Spec Kit wrapper. It combines:

- a governed F# desktop UI/product template;
- a compiled F# build/governance engine;
- route selection from git diffs;
- typed gate identities and target metadata;
- synthetic-evidence tracking and merge audit;
- generated product validation;
- capability catalogs, package/API surface baselines, and FSI transcript checks;
- design-token, control-catalog, contrast, docs, and fidelity checks;
- synchronized agent skills;
- single-source generated views with drift gates;
- CI documentation, docs deployment, and NuGet trusted publishing.

Replacing FS-Skia-UI in capability means FS.GG.Governance must become the product-neutral rule and evidence platform, while FS.GG.Rendering supplies the rendering/UI packages, generated product templates, samples, and product skills. Governance alone should not reimplement the renderer, controls, or window host. It must, however, govern those surfaces well enough that an FS.GG generated product no longer needs FS-Skia-UI's `FS.Skia.UI.Build`, template, skill tree, route selector, evidence engine, package-surface checks, or release gates.

The replacement architecture is:

- Keep the FS.GG.Governance kernel as the reusable rule algebra: reified `Check`, `CheckRule`, evidence, route, contract, explanation, JSON, adapters, host loop, and CLI.
- Add an FS.GG-owned workflow/product adapter that replaces the SpecKit adapter as the main path.
- Add concrete rule packs for workflow, package/API surface, generated product/template, skill quality, docs/examples, design/rendering, distribution, provenance, and CI.
- Replace FS-Skia-UI's FAKE-bound gates with stable CLI/library contracts. FAKE can remain an optional runner, not the architecture.
- Make generated readiness files machine-readable first, with Markdown only as rendered views.
- Make governance cost proportional to change risk: cheap local checks, focused PR gates, full ship/release checks only at the right boundary, and evidence freshness instead of needless reruns.
- Make `fsgg ship --mode gate --json` the branch-protection status check.

The local FS.GG.Governance codebase already has the harder reusable core: 39 non-generated source/interface files under `src/`, 50 non-generated F# test files, 12 feature specs, a reified check algebra, an adapter SPI, SpecKit and design-system adapters, a host loop, and a CLI that can emit route, contract, explain, and evidence output. The missing work is not theory. It is parity engineering against FS-Skia-UI's capability surface.

Equally important: the replacement must not recreate FS-Skia-UI's oppressive development loop. FS-Skia-UI's later governance could turn slight changes into hundreds of tests and broad generated-product checks. That is a design failure even when every individual gate is useful. FS.GG must treat "small safe changes stay cheap" as a first-class requirement.

## Research snapshot

Online source of truth inspected:

- GitHub repository: <https://github.com/EHotwagner/FS-Skia-UI>
- Fresh research clone at commit `f943406829a9218b2882e09eb88d4ddb539c5e72` (`2026-06-16 10:01:40 +0200`, subject `docs: add design-system governance domain detailed design`)
- GitHub repository metadata on 2026-06-18: `created_at=2026-05-12T13:18:59Z`, `pushed_at=2026-06-16T08:16:52Z`, `archived=false`, default branch `main`, 2 stars, 0 forks, 0 open issues
- FS-Skia-UI governance docs:
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/index.md>
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/routing-and-gates.md>
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/evidence-and-audit.md>
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/single-source-generation.md>
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/speckit-placement.md>
- FS-Skia-UI architecture/distribution docs:
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/architecture/governance.md>
  - <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/distribution.md>
- FS-Skia-UI implementation files in the research clone:
  - `README.md`
  - `AGENTS.md`
  - `.github/workflows/docs.yml`
  - `.github/workflows/publish.yml`
  - `build/Governance/**`
  - `validation.contract.yml`
  - `template/capabilities.yml`
  - `.agents/skills/**`
  - `.claude/skills/**`
  - `specs/**`
  - `readiness/**`
  - `src/**/*.fsi`
  - `tests/**`
- External references used as comparison points:
  - GitHub status checks / branch protection: <https://docs.github.com/articles/about-status-checks>
  - NuGet trusted publishing: <https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing>
  - F# signature files: <https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files>
  - SLSA v1.2 requirements and provenance: <https://slsa.dev/spec/v1.2/requirements>, <https://slsa.dev/spec/v1.2/provenance>
  - in-toto attestation framework: <https://github.com/in-toto/attestation>
  - Cedar: <https://docs.cedarpolicy.com/>
  - Open Policy Agent: <https://www.openpolicyagent.org/docs>

Local sources inspected:

- This repository: `src/FS.GG.Governance.Kernel`, `src/FS.GG.Governance.Host`, `src/FS.GG.Governance.Adapters.Spi`, `src/FS.GG.Governance.Adapters.SpecKit`, `src/FS.GG.Governance.Adapters.DesignSystem`, `src/FS.GG.Governance.Cli`, `docs/governance-design`, `specs/**`, and CLI tests.
- Local generated-product sibling: `/home/developer/projects/SkiaViewer`, especially `.specify/memory/constitution.md`, `CLAUDE.md`, `README.md`, `specs/**`, `src/**/*.fsi`, surface-area tests/baselines, and scripts.

## FS-Skia-UI capability inventory

The FS-Skia-UI repository currently carries a broad, integrated capability surface:

| Area | Observed evidence | Replacement implication |
|---|---|---|
| Repository scale | 11,538 tracked files in the shallow research clone; GitHub page reports 438 commits | Replacement must be a migration path, not a hand rewrite. |
| Feature workflow | 120 `specs/*/tasks.md` files and 8,969 files under `specs/*/readiness/**` | FS.GG needs import and durable readiness support, not just new greenfield schemas. |
| Compiled governance | 106 `.fs`/`.fsi` files under `build/Governance` | FS.GG.Governance must absorb behavior into reusable libraries/adapters. |
| Target model | Closed `Targets.Target` union with routeable gates, metadata, prerequisites, costs, failure owners | FS.GG needs typed gate ids and generated target metadata. |
| Routing | `Routing.Diff`, tiers, path globs, default-deny, dogfood forcing, expected artifacts, `Route --enforce` | FS.GG must provide diff-sensitive route selection with JSON and explanation. |
| Evidence graph | Five authored task states plus computed `AutoSynthetic` / `[S*]`, dependency graph, cycle detection | FS.GG evidence model already maps conceptually; it needs product workflow sensing. |
| Merge audit | `EvidenceAudit` combines synthetic taint, SEH classification, accepted deferrals, and diff-scan blockers | FS.GG `ship` must produce a blocking audit and machine-readable verdict. |
| Single-source generation | `validation.contract.yml`, `.claude` skills, docs, API-surface docs, tokens, controls catalog, baselines | FS.GG must make generated views output, not peer sources. |
| Capability catalog | `template/capabilities.yml` maps packages, projects, `.fsi` contracts, tests, skills, fragments, evidence, baselines | FS.GG.Rendering needs an equivalent catalog consumed by FS.GG.Governance. |
| Public API control | 65 `.fsi` files; package surface checks, baselines, FSI transcripts, per-package diffs | FS.GG package adapters must govern `.fsi` and generated reference surfaces. |
| Tests/samples | 217 F# test/interface files under `tests/`; 13 sample apps | Replacement needs generated-product and sample smoke gates, not just library tests. |
| Skills | 37 Codex `.agents/skills/**/SKILL.md` files mirrored to 37 Claude skills; 8 template/product skill files | FS.GG must govern skill contracts, paths, sync/mirroring, and generated product skills. |
| Docs/design | 72 files under governance, architecture, controls, controls-design, and examples docs | Docs/examples are product-facing surfaces and must route to checks. |
| Distribution | Docs workflow, publish workflow, 11 libraries plus template, NuGet trusted publishing, pre-publish checks | FS.GG must define release/publish governance if it replaces generated-product distribution. |

FS-Skia-UI's README says the repository is "archived for now" in prose, but GitHub metadata reports `archived=false`. Treat the project as active source material frozen at the inspected commit, not as a dead repository whose behavior can be ignored.

## What "replace FS-Skia-UI in capability" means

FS-Skia-UI is two things at once:

1. A UI/runtime stack: Scene, SkiaViewer, Elmish, Input, KeyboardInput, Layout, Controls, Testing, SkillSupport, samples, docs, and a `dotnet new` template.
2. A governed development system: route, gates, evidence, generated products, capability catalog, skills, single-source generation, surface governance, CI, and publishing.

FS.GG.Governance should replace the second category directly. FS.GG.Rendering should replace the first category directly. The new system only replaces FS-Skia-UI in capability when the two are integrated:

- FS.GG.Rendering publishes or generates the runtime/product surfaces.
- FS.GG.Governance senses those surfaces, routes changes, checks contracts, validates generated products, emits readiness, and blocks ship when evidence is insufficient.
- Generated products consume FS.GG packages and FS.GG governance commands, not `FS.Skia.UI.Build`, `FS.Skia.UI.Template`, or FS-Skia-UI skills.

Therefore the replacement target is not "rename Spec Kit" and not "port `build/Governance`." It is "deliver every externally useful governance, generation, evidence, and distribution capability that made FS-Skia-UI usable."

## Capability parity matrix

| FS-Skia-UI capability | Current FS.GG.Governance position | Required replacement work |
|---|---|---|
| Compiled typed policy | Stronger kernel exists: reified `Check`, `CheckRule`, verdict, contract, explanation, evidence | Add product-level target/gate registry and rule packs. |
| Diff route selection | Kernel has route model; CLI has `route`; SpecKit adapter is not enough | Add git/CI snapshot sensor, changed-path facts, default-deny, `--paths`, branch/base support, and JSON route trace. |
| Cost-proportional validation | No explicit cost budget, evidence cache, or rule maturity model yet | Add rule cost metadata, impact scopes, freshness keys, scoped local checks, and advisory-first promotion. |
| Tiers and modes | FS.GG has `RunMode` and rule severity concepts | Define FS.GG-specific tiers/modes: inner, focused, verify, ship, release. |
| Target identities | No FS-Skia-style target union for products yet | Add typed `GateId`/`TargetId` registry with metadata, prerequisites, cost, timeout, failure owner. |
| Evidence graph | Kernel evidence supports authored and effective states | Add workflow/product evidence schemas and import from old `tasks.md`/`tasks.deps.yml`. |
| Merge audit | Kernel can report failures; CLI can emit evidence | Add `fsgg ship` audit: synthetic taint, blocking findings, stale generated views, missing product evidence, exit codes. |
| Single-source generated views | Kernel renders contract/explain; no broad regeneration command yet | Add generation manifest, currency checks, and `fsgg refresh` for contracts, skill views, docs views, surface baselines. |
| Capability catalog | No product catalog schema in Governance | Define `.fsgg/capabilities.yml` for FS.GG.Rendering packages, skills, tests, surfaces, samples, docs, evidence. |
| Public `.fsi` governance | Spec and design adapters do not own F# package surfaces | Add package/API adapter for `.fsi`, surface baselines, compatibility notes, FSI transcripts. |
| Template/generated product checks | CLI can inspect a root but does not instantiate products | Add generated product adapter: instantiate template, run product Dev/Test/Verify, verify generated guidance and capabilities. |
| Skill quality/sync | Skills exist outside current repo model | Add skill adapter for `SKILL.md` metadata, required references, product-skill paths, mirror/currency if multiple agent surfaces are supported. |
| Design/rendering checks | Design-system adapter exists as pure facts/rules | Connect sensors to FS.GG.Rendering: tokens, captures, contrast, interaction states, catalog/docs fidelity. |
| Docs/examples | Not yet a first-class rule pack | Add docs/examples adapter for fsdocs/literate scripts, links, examples, public API docs, generated docs currency. |
| Distribution/publish | Out of current kernel scope | Add package/release rule pack: version bump, packability, metadata, trusted-publishing readiness, provenance artifact. |
| CI/branch protection | CLI can exit; no prescribed status contract | Make `fsgg ship --mode gate --json` the required GitHub status check. |

## What to keep from FS-Skia-UI

Keep the ideas, not the coupling:

| Concept | Keep | Notes |
|---|---:|---|
| Typed gate identities | Yes | FS-Skia-UI uses a closed F# union. FS.GG should do the same or use an equally typed registry. |
| Changed-path route selection | Yes | This is essential. Governance must start from repository facts, not from which command an agent remembered. |
| Default-deny for unknown paths | Yes | Unknown blast radius should route to stronger proof. |
| Minimal gate selection | Yes, but strengthen | The idea is right, but FS.GG must route by precise capability impact, not coarse directories. |
| Evidence states and synthetic propagation | Yes | FS.GG already has matching kernel vocabulary; make it workflow/product-visible. |
| Merge-time audit | Yes | `ship` must be a hard gate with machine-readable reasons. |
| Single-source generation | Yes | Generated files are views and must be currency-checked. |
| Capability catalog | Yes | It is the bridge from packages/templates/skills/tests to governance facts. |
| Public `.fsi` surface discipline | Yes | F# signature files are a natural package contract boundary. |
| Skill path/quality governance | Yes | Generated products need reliable agent capabilities. |
| Design-token/control checks | Yes | FS.GG.Rendering needs these as domain adapters. |
| NuGet trusted-publishing flow | Yes, if FS.GG publishes templates/packages | Release governance should cover publish credentials, metadata, pack outputs, and idempotency. |
| Spec Kit as primary workflow | No | Treat `.specify` and `specs/**` as import/migration input, not the future contract. |
| FAKE as required architecture | No | Use CLI/library contracts; FAKE can call them. |
| Broad path-glob escalation | No | Avoid rules like `src/Controls/**` selecting every controls/template/generated-product gate for trivial edits. |
| Full-suite local defaults | No | Exhaustive validation belongs at ship, release, nightly, or explicit verify boundaries. |
| Plain-text-only route output | No | FS.GG should emit JSON and explain traces from day one. |
| Presence-only `--enforce` | No | FS.GG should include freshness, source hash, commit/base, and provenance where practical. |

## Anti-oppression requirements

FS.GG should be strict at product boundaries and cheap in the inner loop. A governance system that is technically correct but routinely turns a one-line low-risk edit into hundreds of tests will be bypassed, disabled, or feared. Cost control is therefore part of correctness.

| Requirement | Design rule | FS.GG incorporation |
|---|---|---|
| Separate local, PR, ship, and release gates | Do not make the local authoring loop pay release-level cost | Add modes such as `local`, `pr`, `ship`, and `release`, with explicit promotion rules. |
| Route from precise impact facts | Broad path globs are too crude for mature products | Map changed paths to capabilities, public surfaces, generated views, docs pages, controls, tests, and evidence artifacts. |
| Support scoped authoring | Whole-worktree routing is right for merge safety but bad while editing | Add `fsgg route --paths ...`, `fsgg check --paths ...`, and `fsgg check --since <rev>`; reserve full base/head routing for `ship`. |
| Cache fresh evidence | Expensive proof should rerun only when its inputs changed | Store rule hash, artifact hashes, command version, generator version, base/head commit, environment class, and output digest. |
| Split expensive gates | A generated-product umbrella gate hides cost and cause | Separate structural scans, restore/build, focused product tests, visual checks, full generated-product verify, and release checks. |
| Explain broad routes | A large gate list must justify itself | `route.json` should name matched rule, changed path, affected capability, required evidence, estimated cost, and cheaper local alternative. |
| Budget every rule | Runtime and blast radius are governance data | Add cost, timeout, expected-test-count, historical-runtime, and failure-owner metadata to gates/rules. |
| Promote rules gradually | New heuristic gates are noisy until proven | Support `observe`, `warn`, `block-on-pr`, `block-on-ship`, and `block-on-release` maturity levels. |
| Run exhaustive checks asynchronously | Full sweeps still matter, but not on every small edit | Use nightly/scheduled full validation, release-only publish checks, and explicit broad `verify` commands. |
| Prefer contract checks first | Small public API edits need high signal before broad integration | Run `.fsi`, FSI transcript, baseline, docs, and focused semantic tests before generated-product or visual matrices. |

Concrete examples:

- A prose-only docs edit should route to docs/link/generated-docs checks, not package tests or generated-product validation.
- A private `.fs` implementation edit should normally route to affected project tests, not every template/profile/sample check.
- A single `.fsi` edit should route to that package's surface baseline, FSI transcript, compatibility note, and focused semantic tests.
- A token-source edit should route to token generation, contrast checks for affected roles, and selected visual captures, not all samples.
- Template, package pin, or capability catalog edits can still route broad because they affect generated consumers.

The final rule: if a slight change selects a high-cost path, the route explanation must make the risk obvious. If it cannot, the rule is too broad or belongs in advisory/scheduled validation.

## Proposed FS.GG replacement model

### Product split

| Concern | Owner |
|---|---|
| Rule algebra, verdicts, evidence propagation, route/contract/explain/evidence JSON | `FS.GG.Governance.Kernel` |
| Adapter composition and cross-domain lifting | `FS.GG.Governance.Adapters.Spi` |
| Workflow state, work graph, evidence declarations, ship audit | New `FS.GG.Governance.Adapters.Workflow` |
| Package/API surfaces, `.fsi`, baselines, FSI transcripts | New package/API adapter |
| Generated product/template validation | New generated-product adapter, consuming FS.GG.Rendering templates |
| Skills, skill lists, skill path contracts, mirrors | New skill adapter |
| Design/rendering facts: tokens, captures, contrast, interaction states | Existing design-system adapter plus FS.GG.Rendering sensors |
| Docs/examples/reference generation | New docs/examples adapter |
| Release/publish/provenance | New distribution adapter |
| CLI user/CI surface | `FS.GG.Governance.Cli` |
| Rendering/UI packages/templates/samples | `FS.GG.Rendering` |

### Lifecycle

Replace Spec Kit phases with FS.GG stages. These stages are facts in the workflow adapter, not command folklore.

| Stage | Purpose | Primary command | Gate posture |
|---|---|---|---|
| `Charter` | Establish project policy, package surfaces, domains, default gates, branch policy | `fsgg init` / `fsgg charter` | Advisory until branch protection is configured |
| `Intent` | Capture user value, scope, non-goals, acceptance criteria | `fsgg work intent <id>` | Advisory |
| `Design` | Decide architecture, public contracts, dependencies, evidence plan | `fsgg work design <id>` | Advisory with route preview |
| `WorkGraph` | Produce typed tasks, dependencies, owners, required evidence | `fsgg work graph <id>` | Advisory or early fence |
| `Implement` | Execute tasks and declare evidence | `fsgg work update <id>` | Advisory local checks |
| `Verify` | Run product tests, surface checks, docs checks, generated-view currency checks | `fsgg verify <id>` | Blocking in selected CI contexts |
| `Ship` | Recompute from base/head, enforce blocking rules, publish readiness | `fsgg ship <id> --mode gate` | Blocking |
| `Release` | Pack/publish after ship, with provenance and metadata checks | `fsgg release <id>` | Blocking for package publication |

This preserves the useful intent/design/work separation while removing Spec Kit as the governing artifact model.

### Artifacts

Use Markdown where it helps humans author, but make structured files authoritative for gates.

| Artifact | Source/generated | Purpose |
|---|---|---|
| `.fsgg/project.yml` | Source | Project id, package surfaces, capability catalog pointer, domains, default modes |
| `.fsgg/policy.yml` | Source | Enforcement dial, blocking rules, review budgets, generated-view policy |
| `.fsgg/capabilities.yml` | Source | FS.GG.Rendering package/template/skill/test/surface/doc/evidence catalog |
| `.fsgg/gates.yml` or generated `gates.json` | Generated view | Human/tool view of typed gate registry |
| `.fsgg/rules.md` | Generated view | Rendered rule catalog from `Check.render` |
| `work/<id>/intent.md` | Source | User value, scope, non-goals, acceptance criteria |
| `work/<id>/design.md` | Source | Architecture, API impact, dependency decisions, migration notes |
| `work/<id>/contracts/` | Source | `.fsi`, OpenAPI, gRPC, package-surface contracts or links |
| `work/<id>/graph.yml` | Source | Typed work items, dependencies, owners, skills, required evidence |
| `work/<id>/evidence.yml` | Source | Declared evidence state per work item and evidence URI |
| `readiness/<id>/route.json` | Generated | Matched rules, tiers, gates, paths, default-deny, trace |
| `readiness/<id>/contract.json` | Generated | Rendered rule contract and input reads |
| `readiness/<id>/explain.json` | Generated | Proof trees for applicable rules |
| `readiness/<id>/evidence.json` | Generated | Effective evidence states, taint propagation, graph failures |
| `readiness/<id>/audit.json` | Generated | Ship verdict, blockers, warnings, provenance references |
| `readiness/<id>/summary.md` | Generated view | PR-friendly human summary rendered from JSON |
| `readiness/<id>/attestations/` | Generated | Optional SLSA/in-toto-style provenance and verification summaries |

Migration rule: `.specify/memory/constitution.md`, `specs/<feature>/spec.md`, `plan.md`, `tasks.md`, `tasks.deps.yml`, and `readiness/**` are import sources. They are not the future source of truth.

## Rules and adapters needed

| Adapter/rule pack | Facts it owns | Example rules |
|---|---|---|
| Workflow | Stage, work artifacts, graph nodes, dependencies, evidence declarations, policy dial | `workGraphWellFormed`, `designSatisfiesIntent`, `evidenceNotSynthetic`, `shipFence` |
| Git/CI | Base/head, changed paths, branch, PR labels, status checks, dirty worktree, unknown paths | `changedPathsRouted`, `unknownPathDefaultsSafe`, `requiredStatusPresent` |
| Cost/cache | Rule cost, historical runtime, freshness keys, prior evidence digests, rule maturity | `evidenceFresh`, `ruleWithinBudget`, `expensiveGateJustified`, `advisoryRuleNotBlocking` |
| Package/API | Public `.fsi`, package projects, surface baselines, compatibility notes, FSI transcripts | `publicSurfaceHasSignature`, `surfaceBaselineCurrent`, `breakingChangeHasMigration` |
| Generated product | Template profile, generated root, generated package pins, product tests, generated guidance | `templateInstantiates`, `generatedProductVerifies`, `generatedGuidanceCurrent` |
| Skills | Skill ids, paths, references, capability mappings, mirrors, loaded-skill evidence | `skillExists`, `skillContractPathValid`, `declaredSkillLoadedBeforeWork` |
| Docs/examples | FsDocs pages, examples, reference docs, API docs, links | `examplesRun`, `publicApiDocumented`, `generatedDocsCurrent` |
| Design/rendering | Token source, generated token surface, captures, contrast, controls catalog, interaction states | `tokensCurrent`, `contrastPasses`, `interactionStatesCovered`, `captureMatchesPolicy` |
| Build/package | Test commands, pack commands, package metadata, versioning, local/staging feeds | `testsPassed`, `packableProjectsPacked`, `versionBumpedWhenPacked`, `publishMetadataComplete` |
| Provenance | Source commit, base/head, command, artifacts, digests, environment, generator identity | `readinessHasProvenance`, `artifactDigestMatches`, `attestationCurrent` |

The kernel should remain generic. Product vocabulary belongs in adapters and capability catalogs.

## Current FS.GG.Governance strengths

FS.GG.Governance already improves on FS-Skia-UI in several important ways:

- `Check<'fact>` is a reified algebra with six folds: evaluate, render, hash, explain, reads, and reified-ness. FS-Skia-UI's routing and evidence are typed, but its gates are not generalized into a reusable rule algebra.
- `CheckRule` and bridge concepts separate deterministic checks, agent-reviewed checks, and human-only rules.
- Evidence is already domain-neutral and can represent synthetic and auto-synthetic propagation.
- The adapter SPI lets domains keep their own vocabulary while reusing kernel behavior.
- The CLI already exposes route, explain, contract, and evidence commands with text/JSON output.
- The design-system adapter proves the approach is not Spec Kit-specific.

That means the replacement should not copy FS-Skia-UI's `build/Governance` library. It should mine it for requirements and implement them as FS.GG adapters, sensors, generated views, and CLI commands.

## Current gaps

| Gap | Why it blocks replacement |
|---|---|
| No FS.GG workflow adapter | The current main adapter is SpecKit-shaped. Replacement needs FS.GG-owned stages/artifacts. |
| No git/CI snapshot adapter with path facts | Route parity requires base/head diff, dirty paths, unknown-path handling, and CI context. |
| No scoped authoring mode | Whole-worktree routing would recreate FS-Skia-UI's heavy local loop. |
| No evidence freshness cache | Expensive checks would rerun even when their rule inputs and artifacts are unchanged. |
| No rule cost/maturity model | New or high-cost gates could become blocking before their signal/noise ratio is known. |
| No typed product gate registry | FS-Skia-UI's target union/metadata is a major capability; FS.GG needs equivalent target identity. |
| No generated-product/template adapter | FS-Skia-UI validates generated consumers; FS.GG cannot replace the template workflow without this. |
| No capability catalog schema | Package/tests/skills/docs/evidence need a central catalog analogous to `template/capabilities.yml`. |
| No package/API surface adapter | `.fsi` and baselines are central to F# product governance. |
| No skill-quality adapter | Product generation depends on skills as artifacts, not incidental files. |
| No single regeneration entry point | FS-Skia-UI's `RefreshSurfaceBaselines` pattern needs an FS.GG equivalent. |
| No release/publish/provenance adapter | Replacement of distribution capability needs version/pack/publish checks. |
| No branch-protection recipe | The final interface should be a required `fsgg ship` status check. |

## Implementation roadmap

### Phase 1: Route parity

- Add a git/CI snapshot sensor that records base ref, head ref, changed paths, uncommitted paths, untracked paths, and path classifications.
- Add typed `GateId`/`TargetId` metadata: name, prerequisites, timeout class, cost, failure owner, product-check flag.
- Add path-glob route rules as reified checks or a typed route table rendered into `route.json` and `contract.json`.
- Implement default-deny for unknown paths and a `--paths` authoring mode for scoped local checks.
- Emit JSON route traces naming matched rules, unmatched paths, selected tier, gates, expected artifacts, estimated cost, cache eligibility, and why each gate is present.
- Add rule maturity levels so new checks can run as observe/warn before becoming blocking.

### Phase 2: Workflow and evidence parity

- Add `.fsgg/project.yml`, `.fsgg/policy.yml`, `work/<id>/graph.yml`, and `work/<id>/evidence.yml` schemas.
- Add import from `.specify` and `specs/**` with explicit uncertainty warnings.
- Port task DAG validation, dependency propagation, synthetic taint, SEH/accepted-deferral concepts, and diff-scan blocking into FS.GG terms.
- Add evidence freshness records keyed by rule hash, artifact hashes, command version, base/head, environment class, and output digest.
- Make `fsgg evidence` and `fsgg ship` produce `evidence.json`, `audit.json`, and `summary.md`.

### Phase 3: Single-source generation parity

- Add `fsgg refresh` as the one regeneration entry point.
- Define a generation manifest listing source, generated view, renderer, and currency gate.
- Generate rule catalog, gate metadata, capability docs, skill references, API-surface docs, and surface baselines.
- Fail on stale generated views with diagnostics that name the source and the regeneration command.

### Phase 4: Capability catalog and generated-product parity

- Define `.fsgg/capabilities.yml` for FS.GG.Rendering packages, contracts, tests, product skills, template fragments, profiles, evidence tags, and baselines.
- Add generated-product checks in cost tiers: structural scan, restore/build, focused product tests, full verify, and release/publish validation.
- Ensure generated products can run FS.GG governance locally without depending on the monorepo.

### Phase 5: Package/API, design, docs, and skills parity

- Add `.fsi` surface baseline generation and drift checks.
- Add FSI transcript checks for public examples and package contracts.
- Wire the design-system adapter to real FS.GG.Rendering token/capture/control facts.
- Add docs/examples checks for FsDocs, literate scripts, public API docs, and link/reference currency.
- Add skill-quality checks for local product skills, task skill lists, path contracts, and optional multi-agent mirrors.

### Phase 6: Ship, release, and provenance

- Define `fsgg verify` and `fsgg ship --mode gate --json` exit codes and stable output schema.
- Add GitHub Actions workflow guidance so `fsgg ship` is the required protected-branch status.
- Add scheduled/nightly exhaustive validation for broad matrices so the local and PR loops stay focused.
- Add release rules for version bumps, packable projects, package metadata, template pins, dry-run publish plans, and NuGet trusted publishing.
- Emit optional SLSA/in-toto-style attestations for readiness artifacts, package outputs, and generated products.

### Phase 7: Migration and retirement

- Import FS-Skia-UI/SkiaViewer-style `specs/**` into `work/**`.
- Keep the SpecKit adapter as compatibility/import tooling.
- Move active generated products to `.fsgg` and FS.GG commands.
- Retire `.specify` and FS-Skia-UI build/package dependencies from the main path.

## SkiaViewer migration assessment

The local sibling checkout is named `SkiaViewer`, not `FS-Skia-UI`. It is a generated-product-style input, not the full upstream repository.

Current local facts:

- 9 feature directories have `tasks.md`.
- 21 `.fsi` files exist under `src/`.
- 3 surface-area test/baseline files exist.
- 14 F# scripts/prelude/example files exist.
- 0 `tasks.deps.yml` files exist.
- 0 generated `readiness/` reports exist.
- No `.governance-phase` marker exists.

Under the replacement model:

| Existing SkiaViewer artifact | Migration target |
|---|---|
| `.specify/memory/constitution.md` | `.fsgg/project.yml`, `.fsgg/policy.yml`, optional `charter.md` |
| `CLAUDE.md` generated from feature plans | Generated agent context from `.fsgg`, active work, route, pending evidence, rule contract |
| `specs/<id>/spec.md` | `work/<id>/intent.md` |
| `specs/<id>/plan.md` | `work/<id>/design.md` |
| `specs/<id>/contracts/` | `work/<id>/contracts/` or links to canonical `.fsi` |
| `specs/<id>/tasks.md` | `work/<id>/graph.yml` plus rendered `work.md` |
| Markdown checkbox states | `work/<id>/evidence.yml` |
| Surface-area tests | Package/API adapter facts and `surfaceBaselineCurrent` checks |
| Scripts/examples | Docs/examples and scriptability rule facts |

## External policy comparison

Cedar and OPA are useful reminders that policy languages should be explainable, testable, and embeddable, but they do not replace the FS.GG kernel. FS.GG's domain is not only authorization. It needs typed F# facts, artifact reads, evidence propagation, route selection, generated views, and human/agent review tiers.

SLSA and in-toto are better fits for the readiness/provenance layer. FS.GG should not claim SLSA compliance casually, but readiness outputs should be shaped so they can carry the same kind of artifact, digest, builder, source, and command facts. That would improve on FS-Skia-UI's presence-oriented readiness files and make stale evidence easier to detect.

GitHub status checks are the practical enforcement edge. A required check must pass before protected-branch merge, so FS.GG's blocking interface should be one stable CI job running `fsgg ship --mode gate --json`, with product test jobs feeding evidence rather than existing as undocumented parallel policy.

NuGet trusted publishing matters only if FS.GG replaces FS-Skia-UI's distribution capability. If it does, release governance must verify package metadata, version pins, template pins, and publish plan before GitHub OIDC exchanges for a short-lived NuGet credential.

## Risks and mitigations

| Risk | Why it matters | Mitigation |
|---|---|---|
| Replacing Spec Kit but not FS-Skia-UI capability | Leaves generated-product, skill, package, surface, docs, and release checks behind | Treat FS-Skia-UI's full capability inventory as parity scope. |
| Recreating oppressive governance | Developers will stop running checks or route around the system | Make cost proportional to risk, add scoped authoring, freshness caching, rule budgets, and advisory-first promotion. |
| Copying FS-Skia-UI's FAKE coupling | Recreates the same concurrency and build-runner constraints | Make CLI/library contracts primary; FAKE only calls them. |
| Recreating Spec Kit with different names | Adds churn without stronger guarantees | Make structured state, typed facts, evidence graph, and generated readiness authoritative. |
| Losing readable authoring flow | Spec Kit's Markdown was approachable | Keep Markdown authoring/rendered views, but pair them with source-of-truth YAML/JSON. |
| Route output becomes another opaque oracle | Agents need to know why gates were selected | Emit JSON traces and proof/explain trees. |
| Generated view drift | Hand-synced docs/contracts become stale | Add source/view/currency manifest and `fsgg refresh`. |
| Stale evidence passes by presence | FS-Skia-UI documents this weakness in `Route --enforce` | Include source hash, command, base/head, generated-at, and artifact digest in readiness. |
| Migration ambiguity | Old `tasks.md` prose may not encode dependencies/evidence | Import with warnings; require humans/agents to complete `graph.yml` and `evidence.yml`. |
| Product adapters leak into kernel | Kernel loses reuse value | Keep product facts in adapters and capability catalogs. |
| Release governance overclaims provenance | SLSA/in-toto have precise meanings | Emit compatible metadata first; claim formal compliance only after explicit verification. |

## Bottom line

The corrected direction is replacement of FS-Skia-UI's useful capabilities, not merely replacement of Spec Kit:

1. FS.GG.Governance owns the generic rule system, adapters, route/contract/explain/evidence outputs, readiness/audit, and CI gate.
2. FS.GG.Rendering owns the UI/runtime packages, templates, samples, product skills, and design/rendering artifacts.
3. `.fsgg` and `work/<id>` replace `.specify` and `specs/<id>` as the authoritative lifecycle model.
4. Capability catalogs connect package surfaces, tests, skills, docs, generated products, design captures, and release artifacts to governance facts.
5. Cost controls make the local loop fast: scoped routing, precise impact facts, evidence caching, rule budgets, and advisory-first rules.
6. Generated readiness is structured, fresh, and auditable; Markdown is a view.
7. `fsgg ship --mode gate --json` becomes the protected-branch merge gate.
8. SpecKit and FS-Skia-UI artifacts remain import sources until migrated, then leave the main path.

That gives FS.GG a real replacement for FS-Skia-UI's governance/build/generated-product capability instead of a stricter wrapper around someone else's workflow.
