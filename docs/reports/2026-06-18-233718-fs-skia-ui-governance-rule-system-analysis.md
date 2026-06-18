# FS-Skia-UI governance as a replacement for Spec Kit

**Timestamp:** 2026-06-18T23:37:18+02:00
**Revision:** 2026-06-18T23:44:06+02:00
**Author:** Codex
**Status:** Analysis, no implementation changes
**Scope:** Research FS-Skia-UI / local SkiaViewer governance and analyze how this repository's rule system should become a custom framework that replaces Spec Kit rather than layering on top of it.

## Executive summary

The target should be a full FS.GG governance framework, not a Spec Kit adapter. Spec Kit is useful as historical input because it gave the repository a constitution, feature artifacts, plans, tasks, and lifecycle vocabulary. It should not remain the governing source of truth.

The right replacement architecture is:

- A custom FS.GG lifecycle with its own stages, commands, artifact names, and storage layout.
- A pure rule system as the authority for checks, routing, explanation, evidence, and gates.
- Domain adapters for workflow, design/rendering, package/API surface, docs, and product-specific invariants.
- A generated readiness layer for machine-readable reports, not hand-maintained status prose.
- CI and local commands that run the same rule catalog in different modes.

FS-Skia-UI's governance prior art is valuable because it already solved practical problems that Spec Kit leaves mostly as prompt discipline: route selection from changed paths, evidence taint, generated-view currency, and merge-time enforcement. This repo already implements the deeper general machinery: reified checks, `CheckTier`, deterministic routing, evidence propagation, a host effects loop, review caching, contracts, explanations, and a CLI. What remains is to replace the SpecKit-specific adapter/terminology with a first-class custom workflow adapter and artifact model.

The local sibling checkout is named `SkiaViewer`, not `FS-Skia-UI`. It has many Spec Kit-style artifacts today: 9 `tasks.md` files, 21 `.fsi` files, 3 surface-area test/baseline files, 14 F# scripts, and a constitution. Those should be treated as migration material. They are not the desired future contract. The desired future contract is an FS.GG-owned feature/work/evidence model with machine-readable topology, phase/stage facts, and gate reports.

## Sources and method

Offline sources inspected:

- This repository: `src/FS.GG.Governance.Kernel`, `src/FS.GG.Governance.Host`, `src/FS.GG.Governance.Adapters.SpecKit`, `src/FS.GG.Governance.Adapters.DesignSystem`, `src/FS.GG.Governance.Cli`, `docs/governance-design`, `specs/010-adapter-speckit`, and CLI tests.
- Local sibling: `/home/developer/projects/SkiaViewer`, especially `.specify/memory/constitution.md`, `CLAUDE.md`, `README.md`, `specs/**`, `src/**/*.fsi`, `tests/**/*Surface*`, and `scripts/**/*.fsx`.
- Empirical CLI runs, cache-only and read-only:
  - `dotnet run --project src/FS.GG.Governance.Cli -- route --root /home/developer/projects/SkiaViewer --mode inner --json --review-budget 0 --domain speckit`
  - `dotnet run --project src/FS.GG.Governance.Cli -- evidence --root /home/developer/projects/SkiaViewer --mode inner --json --review-budget 0 --domain speckit`
  - `dotnet run --project src/FS.GG.Governance.Cli -- route --root /home/developer/projects/SkiaViewer --mode gate --json --review-budget 0 --domain speckit`

Online sources inspected:

- FS-Skia-UI governance index: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/index.md>
- FS-Skia-UI routing and gates: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/routing-and-gates.md>
- FS-Skia-UI evidence and audit: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/evidence-and-audit.md>
- FS-Skia-UI single-source generation: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/single-source-generation.md>
- FS-Skia-UI Spec Kit placement: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/governance/speckit-placement.md>
- FS-Skia-UI design-system governance design: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/reports/2026-06-16-0958-design-system-governance-domain-detailed-design.md>
- FS-Skia-UI kernel split design: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/reports/2026-06-07-0838-governance-kernel-split-detailed-design.md>
- FS-Skia-UI kernel extraction plan: <https://raw.githubusercontent.com/EHotwagner/FS-Skia-UI/main/docs/reports/2026-06-06-1055-governance-kernel-extraction-implementation-plan.md>
- GitHub Spec Kit quickstart and repository, used only as replacement baseline: <https://github.github.com/spec-kit/quickstart.html>, <https://github.com/github/spec-kit>
- Microsoft F# signature files: <https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files>
- Cedar documentation: <https://docs.cedarpolicy.com/>
- Open Policy Agent documentation: <https://openpolicyagent.org/docs>
- SLSA provenance requirements: <https://slsa.dev/spec/v1.0/requirements>
- in-toto attestation framework: <https://github.com/in-toto/attestation>
- GitHub status checks / branch protection: <https://docs.github.com/articles/about-status-checks>

## What should be replaced

Spec Kit provides five things today:

| Spec Kit role | Why replace it | FS.GG replacement |
|---|---|---|
| Lifecycle commands (`specify`, `clarify`, `plan`, `tasks`, `analyze`, `implement`) | The lifecycle is prompt/tool convention, not a typed product contract | FS.GG commands backed by typed workflow state and rule catalogs |
| Artifact layout (`.specify`, `specs/<feature>/*`) | Names and structure are external and too prose-heavy for gates | `.fsgg/` project policy plus `work/<id>/` feature artifacts and `readiness/<id>/` generated evidence |
| Constitution as prompt context | Good as prose, weak as executable configuration | Policy/dial as data, rendered to human docs and enforced by rules |
| Task statuses as Markdown checkboxes | Human-readable, weak for dependency/evidence semantics | Machine-readable work graph plus rendered Markdown view |
| Analyze as an agent pass | Produces reports but does not define durable semantics | Deterministic `contract`, `route`, `explain`, and `evidence` outputs from the same rule values |

The replacement should not be "Spec Kit with renamed files." It should make the rule system the product and leave Markdown as a view.

## What to keep from FS-Skia-UI

FS-Skia-UI governance is the best source material because it already moved beyond pure Spec Kit. Keep these concepts:

| Concept | Keep? | Reason |
|---|---:|---|
| Typed routing targets | Yes | A closed F# union catches invalid gate names at compile time. |
| Path-diff route selection | Yes | Real governance starts from what changed, not from which command the user remembered to run. |
| Evidence states `[ ]`, `[X]`, `[S]`, `[F]`, `[-]`, computed `[S*]` | Yes | This is stronger than task checkboxes and maps directly to this repo's `EvidenceState`. |
| Evidence audit as merge gate | Yes | Synthetic or tainted evidence should not reach trunk silently. |
| Single-source generation | Yes | Generated views must be rendered from canonical sources and checked for currency. |
| Design-system rule catalog and `CheckTier` | Yes | Machine/agent/human judgement split is the right abstraction. |
| FAKE target coupling | No | The framework should expose CLI/library contracts; FAKE can be one consumer, not the architecture. |
| Spec Kit placement docs | No as runtime contract | Useful migration notes, but future placement should be FS.GG-owned stages. |

## Proposed FS.GG framework lifecycle

Replace Spec Kit phases with FS.GG stages. These are facts in the workflow adapter, not hard-coded command branches.

| Stage | Purpose | Primary command | Gate posture |
|---|---|---|---|
| `Charter` | Establish project policy, packages, surfaces, domains, enforcement dial | `fsgg work charter` | Advisory until first protected branch setup |
| `Intent` | Capture user value, scope, non-goals, acceptance criteria | `fsgg work intent <id>` | Advisory |
| `Design` | Decide architecture, public contracts, dependencies, evidence plan | `fsgg work design <id>` | Advisory, with high-stakes preview |
| `WorkGraph` | Produce typed tasks, dependencies, owners, required evidence | `fsgg work graph <id>` | Advisory or optional early fence |
| `Implement` | Execute tasks and update evidence declarations | `fsgg work update <id>` | Advisory |
| `Verify` | Run product tests, surface checks, docs checks, generated-view currency checks | `fsgg verify <id>` | Advisory locally, blocking in CI for selected surfaces |
| `Ship` | Recompute from base/head, enforce blocking rules, publish readiness | `fsgg ship <id>` | Blocking under `Gate` mode |

This keeps the useful Spec Kit separation of intent/design/work, but the semantics are FS.GG-owned and typed.

## Proposed artifacts

Use Markdown for authoring where it is pleasant, but make YAML/JSON/F# generated contracts authoritative for gates.

| Artifact | Source/generated | Owner | Purpose |
|---|---|---|---|
| `.fsgg/project.yml` | Source | Maintainers | Project id, package surfaces, domains, default run modes, branch policy |
| `.fsgg/policy.yml` | Source | Maintainers | Enforcement dial: blocking rules, early fences, review budgets, generated-view policy |
| `.fsgg/rules/README.md` | Generated view | Governance CLI | Human-readable rendered rule catalog |
| `work/<id>/intent.md` | Source | Product author/agent | User value, scope, non-goals, acceptance criteria |
| `work/<id>/design.md` | Source | Engineer/agent | Architecture, API impact, dependency decisions, migration notes |
| `work/<id>/contracts/` | Source | Engineer/agent | `.fsi`, OpenAPI, gRPC, package-surface contracts or links to canonical files |
| `work/<id>/graph.yml` | Source | Agent/engine | Typed work items, dependencies, skills/tools, required evidence |
| `work/<id>/evidence.yml` | Source | Implementer/agent | Declared evidence state per work item and evidence URI |
| `work/<id>/notes.md` | Source | Maintainers/agent | Human context that is not used as a gate input |
| `readiness/<id>/route.json` | Generated | Governance CLI | Fences tripped, advisory requirements, blocking gates |
| `readiness/<id>/contract.json` | Generated | Governance CLI | Rendered rule contract from `Check.render` |
| `readiness/<id>/explain.json` | Generated | Governance CLI | Proof trees for applicable rules |
| `readiness/<id>/evidence.json` | Generated | Governance CLI | Effective evidence states, taint propagation, failures |
| `readiness/<id>/audit.json` | Generated | CI / `fsgg ship` | Merge verdict and reasons |
| `readiness/<id>/summary.md` | Generated view | Governance CLI | Human PR summary rendered from JSON |
| Review cache records | Generated | Review edge / maintainer | Frozen agent verdicts keyed by judge, prompt, check hash, and artifact hashes |

Migration rule: existing `.specify/memory/constitution.md` maps once into `.fsgg/project.yml` and `.fsgg/policy.yml`; existing `specs/<feature>/spec.md`, `plan.md`, and `tasks.md` map once into `work/<id>/intent.md`, `design.md`, `graph.yml`, and `evidence.yml`.

## Rule-system mapping

The existing repo pieces map well, but the names should move away from SpecKit.

| Current piece | Replacement direction |
|---|---|
| `FS.GG.Governance.Adapters.SpecKit` | Replace with `FS.GG.Governance.Adapters.Workflow` or `...Work` |
| `Phase` | Rename/redefine as `Stage` (`Charter`, `Intent`, `Design`, `WorkGraph`, `Implement`, `Verify`, `Ship`) |
| `SpecKitArtifact` | Replace with `WorkArtifact` / `GovernanceArtifact` |
| `SpecKitFact` | Replace with `WorkflowFact` carrying stage, artifact, work item, dependency, policy, surface, evidence facts |
| `ConstitutionDial` | Replace with `PolicyDial` sourced from `.fsgg/policy.yml` |
| `whenPhase` | Generalize to `whenStage` |
| `tasksGraphWellFormed` | Rename to `workGraphWellFormed` and evaluate `work/<id>/graph.yml` |
| `planSatisfiesSpec` | Rename to `designSatisfiesIntent` |
| `tasksCompleteOrdered` | Rename to `workGraphCompleteOrdered` |
| `featureInScope` | Keep as `HumanOnly`, maybe `workInScope` |
| `evidenceNotSynthetic` | Keep semantics, source facts from `work/<id>/evidence.yml` |
| `contractsCurrent` | Keep semantics, source from `.fsgg/project.yml` surfaces and generated-view manifests |
| `mergeFence` | Rename to `shipFence` |

The kernel should remain unchanged. The replacement is mostly an adapter, CLI, and artifact-model change. The old SpecKit adapter can remain temporarily as a migration importer, but it should not be the main product surface.

## Who runs what

| Stage | Actor | Command | What is produced |
|---|---|---|---|
| Project setup | Maintainer | `fsgg init` | `.fsgg/project.yml`, `.fsgg/policy.yml`, initial rendered rule catalog |
| Intent | Product author or agent | `fsgg work intent <id>` | `work/<id>/intent.md` and initial stage fact |
| Design | Engineer/agent | `fsgg work design <id>` | `work/<id>/design.md`, `contracts/`, route preview |
| Work graph | Agent | `fsgg work graph <id>` | `work/<id>/graph.yml`, rendered `work.md` if desired |
| Implementation | Developer/agent | `fsgg work update <id>` | Updated `work/<id>/evidence.yml`, task evidence URIs |
| Local check | Developer/agent | `fsgg check <id> --mode inner` | `readiness/<id>/route.json`, `contract.json`, `explain.json`, `evidence.json`; advisory exit |
| Verification | Developer/CI | `fsgg verify <id>` plus product test commands | Product test evidence and generated-view currency evidence |
| Agent review | Review edge | `fsgg review <id> --budget N` | Recorded review verdicts or pending review failures |
| Ship gate | CI/maintainer | `fsgg ship <id> --mode gate` | `readiness/<id>/audit.json`, process exit `0` or blocking failure |
| Release/pack | CI/maintainer | Product-specific package command after `fsgg ship` | Packages only after governance and tests pass |

This makes "who runs what" independent of Spec Kit. A GitHub required status check should run `fsgg ship` for protected branches. Local agent loops should run `fsgg check` often because it is advisory and cheap.

## SkiaViewer migration assessment

Current local facts:

- 9 feature directories have `tasks.md`.
- 21 `.fsi` files exist under `src/`.
- 3 surface-area test/baseline files exist.
- 14 F# scripts/prelude/example files exist.
- 0 `tasks.deps.yml` files exist.
- 0 `readiness/` generated reports exist.
- No `.governance-phase` marker exists.

Under the replacement model, these are migration inputs:

| Existing SkiaViewer artifact | Migration target |
|---|---|
| `.specify/memory/constitution.md` | `.fsgg/project.yml` + `.fsgg/policy.yml` + optional human `charter.md` |
| `CLAUDE.md` generated from feature plans | Generated agent context from `.fsgg/project.yml` + latest `work/<id>` |
| `specs/<id>/spec.md` | `work/<id>/intent.md` |
| `specs/<id>/plan.md` | `work/<id>/design.md` |
| `specs/<id>/contracts/` | `work/<id>/contracts/` or direct links to canonical `.fsi` files |
| `specs/<id>/tasks.md` | `work/<id>/graph.yml` plus rendered `work.md` |
| Markdown checkbox states | `work/<id>/evidence.yml` |
| Surface-area tests | Package/API adapter facts and `contractsCurrent` checks |
| Scripts/examples | Scriptability/domain rules and generated-view currency checks |

Empirical dry run of the current CLI over SkiaViewer is still useful as a baseline. It showed the current SpecKit adapter can read the sibling, but it also showed exactly why replacement work is needed: the existing data is not sufficiently machine-readable for reliable graph/evidence enforcement.

## Custom framework adapters needed

| Adapter | Facts it owns | Example rules |
|---|---|---|
| Workflow adapter | Stage, work artifacts, graph nodes, dependencies, evidence declarations, policy dial | `workGraphWellFormed`, `designSatisfiesIntent`, `evidenceNotSynthetic`, `shipFence` |
| Surface/API adapter | Public `.fsi`, package projects, surface baselines, compatibility notes | `publicSurfaceHasSignature`, `surfaceBaselineCurrent`, `breakingChangeHasMigration` |
| Docs/scripts adapter | FsDocs pages, FSI preludes, example scripts, generated docs | `examplesRun`, `publicApiDocumented`, `generatedDocsCurrent` |
| Design/rendering adapter | Token policy, rendered captures, accessibility measures, page/control states | `tokensCurrent`, `contrastPasses`, `interactionStatesCovered`, `captureMatchesPolicy` |
| Build/package adapter | Test commands, pack commands, NuGet metadata, versioning | `testsPassed`, `packableProjectsPacked`, `versionBumpedWhenPacked` |
| Git/CI adapter | Diff paths, base/head, branch, PR labels, status checks | `shipFenceTrips`, `requiredStatusesPresent`, `unknownPathDefaultsSafe` |

This is the point where FS-Skia-UI's old routing implementation matters: changed-path facts are essential. The current CLI's `Scope` field is not enough. The custom framework needs a snapshot layer that senses git diff, package metadata, generated view manifests, and feature/work ids.

## Implementation roadmap

1. Define the replacement vocabulary.
   Add a new workflow spec for FS.GG-owned `Stage`, `WorkArtifact`, `WorkflowFact`, `WorkChange`, and `PolicyDial`. Treat `SpecKit` names as deprecated in new design docs.

2. Add `.fsgg` and `work/` contracts.
   Specify schemas for `.fsgg/project.yml`, `.fsgg/policy.yml`, `work/<id>/graph.yml`, and `work/<id>/evidence.yml`. Keep Markdown files as source text or generated views, but not as the only machine input.

3. Build `Adapters.Workflow`.
   Port the existing SpecKit adapter semantics to the new vocabulary: `whenStage`, `workGraphWellFormed`, `designSatisfiesIntent`, `workGraphCompleteOrdered`, `evidenceNotSynthetic`, `contractsCurrent`, `workInScope`, and `shipFence`.

4. Build a migration/import command.
   Read `.specify` and `specs/**` once and emit `.fsgg` / `work/**`. The importer should mark uncertain mappings explicitly rather than hiding them.

5. Extend snapshot sensing.
   Sense changed paths from git, work id/stage from `.fsgg`, surface baselines, package projects, docs/scripts, design JSON facts, review cache, and generated-readiness outputs.

6. Redesign CLI commands.
   Keep low-level `route`, `contract`, `explain`, and `evidence`, but add product commands: `init`, `work intent`, `work design`, `work graph`, `work update`, `check`, `verify`, `review`, and `ship`.

7. Replace agent context generation.
   Stop generating agent guidance from Spec Kit plans. Generate it from `.fsgg/project.yml`, active `work/<id>`, current route, pending evidence, and rule contract.

8. Enforce through CI.
   Make `fsgg ship --mode gate` the required protected-branch status. Product tests and package commands should be evidence-producing steps consumed by the gate, not separate undocumented tribal checks.

9. Retire Spec Kit adapter from the main path.
   Keep it only as compatibility/import tooling until active repositories no longer use `.specify` / `specs/**` as primary state.

## Risks and mitigations

| Risk | Why it matters | Mitigation |
|---|---|---|
| Recreating Spec Kit with different names | Adds churn without stronger guarantees | Make typed facts, schemas, evidence graph, and generated readiness authoritative |
| Losing readable authoring flow | Spec Kit's appeal is simple Markdown workflow | Keep Markdown authoring views, but pair them with machine-readable state |
| Migration ambiguity | Old `tasks.md` prose may not encode dependencies or evidence cleanly | Import with explicit warnings and require humans/agents to fill `graph.yml` and `evidence.yml` |
| Over-coupling to FAKE | FS-Skia-UI had useful targets but also build-system coupling | Keep FAKE as optional command runner; framework contract is CLI/library/schema |
| Agent judgement becoming a hidden gate | Agent review can be noisy and hard to reproduce | Keep `AgentReviewed` cached, budgeted, and advisory unless explicitly promoted by policy |
| CI gate lacks diff facts | Without base/head path facts, route precision is weak | Add Git/CI adapter before making route parity claims |
| Product adapters leak into kernel | Custom framework could become less reusable | Keep the kernel generic; put workflow/product facts in adapters |

## Bottom line

The corrected direction is replacement:

- Do not make this repo "Spec Kit, but stricter."
- Do not keep `.specify` and `specs/**` as the authoritative future artifact model.
- Do not treat the SpecKit adapter as the end-state adoption path.

Use FS-Skia-UI and SkiaViewer to mine lessons and migrate existing work. Then make FS.GG.Governance own the lifecycle:

1. `.fsgg` project/policy files define the governance dial.
2. `work/<id>` owns intent, design, graph, and evidence.
3. Domain adapters turn repository state into typed facts.
4. The kernel evaluates reified rules and produces route/contract/explain/evidence outputs.
5. `fsgg ship` is the merge gate.

That gives FS.GG an actual custom governance framework instead of a stricter wrapper around someone else's workflow.
