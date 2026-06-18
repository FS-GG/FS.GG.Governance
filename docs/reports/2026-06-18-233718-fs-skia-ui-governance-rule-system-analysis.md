# FS-Skia-UI governance through the FS.GG.Governance rule system

**Timestamp:** 2026-06-18T23:37:18+02:00
**Author:** Codex
**Status:** Analysis, no implementation changes
**Scope:** FS-Skia-UI governance prior art, the local sibling `SkiaViewer`, and how this repository's rule system can reproduce or improve that governance model.

## Executive summary

The FS-Skia-UI governance system is already close to the model this repository implements. Its core ideas are: compiled F# rules instead of hand-edited policy prose, a route selector that maps changed paths to required gates, an evidence graph/audit over Spec Kit task states, single-source generated views with currency gates, and explicit placement of each touchpoint in the Spec Kit lifecycle. The online FS-Skia-UI docs describe this as a governance system that turns an open-ended change into a deterministic, provable one by deciding which checks must run and which evidence makes the change safe to merge.

This repository can achieve that model, but not by cloning FS-Skia-UI's FAKE target selector one-to-one. The stronger translation is:

- FS-Skia-UI `Route` -> this repo's `Route` plus domain fences and rule catalogs.
- `EvidenceGraph` / `EvidenceAudit` -> this repo's `Evidence.build` / `Evidence.effective` plus the SpecKit `evidenceNotSynthetic` rule.
- `validation.contract.yml` and other generated views -> deterministic `contractsCurrent` / currency rules, backed by rendered contracts from `Check.render`.
- design-system checks -> this repo's DesignSystem adapter or a Skia-specific adapter that senses tokens, captures, style policy, and public surfaces.
- Spec Kit phase touchpoints -> this repo's SpecKit adapter, with `PhaseReached`, `whenPhase`, `ConstitutionDial`, and `mergeFence`.

The local sibling checkout is named `SkiaViewer`, not `FS-Skia-UI`. It has the Spec Kit constitution and the expected feature artifacts (`spec.md`, `plan.md`, `tasks.md`, `.fsi` contracts, tests, FSI scripts), but it does not currently have the machine-readable governance artifacts the new rule system expects for full enforcement: no `tasks.deps.yml`, no readiness outputs, and no `.governance-phase` marker. A cache-only dry run of this repo's CLI over `SkiaViewer` therefore succeeds in advisory mode while reporting that `speckit:task-deps` is missing and that two agent-reviewed rules are pending.

The practical recommendation is to adopt this repo as an observer first: run `fsgg-governance route`, `contract`, `explain`, and `evidence` against SkiaViewer during authoring, then add a merge-time CI check once task dependency data and phase sensing exist. Exact FS-Skia-UI parity requires one additional adapter/sensing layer for path-diff routing and Skia/design artifacts.

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
- GitHub Spec Kit quickstart: <https://github.github.com/spec-kit/quickstart.html>
- GitHub Spec Kit repository: <https://github.com/github/spec-kit>
- Microsoft F# signature files: <https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/signature-files>
- Cedar documentation: <https://docs.cedarpolicy.com/>
- Open Policy Agent documentation: <https://openpolicyagent.org/docs>
- SLSA provenance requirements: <https://slsa.dev/spec/v1.0/requirements>
- in-toto attestation framework: <https://github.com/in-toto/attestation>
- GitHub status checks / branch protection: <https://docs.github.com/articles/about-status-checks>

## What FS-Skia-UI governance is

FS-Skia-UI governance has four implemented subsystems and one forward-looking domain design.

| Subsystem | What it does | Main artifacts | Runner / owner |
|---|---|---|---|
| Routing and gates | Reads the working-tree diff, matches typed path-glob rules, chooses a tier, and prints the minimal ordered gate list. It fails safe through default-deny for unknown non-source paths. | `build/Governance/Routing.fs`, `build/Governance/Targets.fsi`, `validation.contract.yml`, route text output | Developer or agent runs `./fake.sh build -t Route`; CI/merge uses the printed gates or broad verification path |
| Evidence graph | Parses task states and dependency topology, validates graph integrity, computes propagated synthetic taint (`[S*]`), and writes task graph views. | `specs/**/tasks.md`, `specs/**/tasks.deps.yml`, `readiness/task-graph.json`, `readiness/task-graph.md` | Developer/agent runs `EvidenceGraph` after task generation and after status changes |
| Evidence audit | Merge gate. Recomputes the graph, blocks remaining `[S]` / `[S*]`, and scans the diff for blocking patterns. Disclosure flags do not change the verdict. | `readiness/diff-scan-hits.json`, synthetic evidence inventory, audit status block, diff scan pattern config | CI or maintainer/agent runs `EvidenceAudit` before merge |
| Single-source generation | Keeps generated views in sync with canonical sources by regenerating and comparing committed output. | `validation.contract.yml`, `.claude/skills/**`, docs, design tokens, catalogs, surface docs | Implementer runs `RefreshSurfaceBaselines`; currency gates block stale views |
| Spec Kit placement | Locates each governance touchpoint in the feature lifecycle from constitution through merge. | `.specify/memory/constitution.md`, specs, plans, tasks, readiness artifacts | Speckit skills author artifacts; governance gates observe and enforce |

The important design properties are:

- Gate identity is typed: FS-Skia-UI uses a closed `Targets.Target` union, so a misspelled gate is a compile error rather than a bad string.
- Route selection is deterministic over the actual diff and de-duplicates gates in registry order.
- Evidence is explicit: `[X]` means real evidence, `[S]` means synthetic evidence, and `[S*]` is computed propagated taint, never authored by hand.
- Generated files are not policy. They are rendered views of canonical sources, and currency gates re-render and compare.
- Spec Kit is not forked. Governance is layered as extensions, presets, skills, and gates over standard phases.

The design-system governance report adds one more idea that this repository now implements more cleanly: each design rule declares `CheckTier` (`Deterministic`, `AgentReviewed`, `HumanOnly`). Deterministic rules run in the pure engine; agent rules emit review requests; recorded, hashed verdicts become evidence. The kernel stays pure and reproducible.

## Local sibling state: SkiaViewer

The local sibling checkout has much of the human-authored governance surface:

- 9 feature directories with `tasks.md`.
- 21 `.fsi` signature files under `src/`.
- 3 surface-area test files/baselines.
- 14 F# script/prelude/example files.
- A constitution at `.specify/memory/constitution.md` requiring spec-first delivery, `.fsi` structural contracts, surface baselines, test evidence, safe failure handling, FSI scripting, and packable projects.

It is missing the machine-readable pieces that make FS-Skia-UI-style evidence enforcement precise:

- 0 `tasks.deps.yml` files under `specs/`.
- 0 `readiness/` outputs.
- No `.governance-phase` marker.
- No generated validation contract or route contract equivalent.

That matters because this repo's current CLI snapshot layer can sense a Spec Kit feature, parse `tasks.md` statuses, read `tasks.deps.yml` if present, and read selected artifacts. Without dependency topology, it cannot prove graph integrity or synthetic propagation. Without phase sensing, a `gate` mode run over SkiaViewer still sees a routine Implement-phase change rather than a Merge fence.

Observed CLI result against SkiaViewer:

- `route --mode inner --domain speckit` exited `0`.
- `evidence --mode inner --domain speckit` exited `0`.
- `route --mode gate --domain speckit` exited `0` because no merge fence tripped.
- All runs reported `speckit:task-deps unavailable` for `specs/009-layout-graph-viz/tasks.deps.yml`.
- Two agent-review cache keys were requested, missed, and stayed pending because review budget was `0`.

This is a useful adoption signal: the observer can run today, but enforcement would be premature until the missing artifacts are introduced.

## Why this repo's rule system fits

This repo is the extracted, generalized version of the FS-Skia-UI direction. The fit is strong for five reasons.

1. Both systems use typed F# as the rule language.
   FS-Skia-UI's prior art deliberately avoids loose YAML or an external policy DSL for core semantics. This repo keeps that decision: facts are closed unions, rules are values, and public surfaces are curated by `.fsi`.

2. Both systems separate pure decision logic from effects.
   FS-Skia-UI's docs repeatedly distinguish compiled pure logic from the FAKE/build edge. This repo makes that boundary explicit: `Kernel` and adapters are pure; `Host.Loop` emits effects as data; `Cli.Program` reads files, dispatches review-cache operations, and writes output.

3. Both systems treat evidence as data.
   FS-Skia-UI's `[S]` / `[S*]` model becomes this repo's `EvidenceState` and `Evidence.effective`. The current SpecKit catalog already contains `evidenceNotSynthetic`, which is always blocking once a merge fence is active.

4. Both systems require drift-proof explanations.
   FS-Skia-UI generated contract views from typed sources. This repo's `Check` algebra gives one value that can be evaluated, rendered, hashed, read for artifact dependencies, and explained. A rendered contract and an enforced check cannot silently diverge.

5. Both systems need phased enforcement.
   FS-Skia-UI uses Spec Kit placement and route tiers. This repo uses `RunMode` (`Sandbox`, `Inner`, `Gate`), phase facts, `whenPhase`, `Severity`, and fences. The newer model is lighter by default and reserves hard blocking for merge or explicitly configured early fences.

The online policy-as-code landscape supports the architectural shape. Cedar and OPA both decouple policy from application logic and evaluate policy over supplied data. SLSA and in-toto make a similar point for supply-chain trust: provenance and attestations should say what was produced, by whom or what process, and from which inputs. This repo does not need to become Cedar, OPA, SLSA, or in-toto; it needs to keep the same separation of authored rule, supplied artifact, deterministic evaluation, and recorded evidence.

## Translation map

| FS-Skia-UI concept | This repo concept | Achievable now? | Notes |
|---|---|---:|---|
| `Targets.Target` closed union | `RuleId`, `CheckRule`, `ContractEntry`, CLI command envelope | Partial | This repo governs requirements, not FAKE target identity. Exact target registry parity would need a BuildTargets adapter or SkiaViewer-specific command mapping. |
| Path-glob route selector | `Fence<'change>`, `ProjectChange.Scope`, adapter-specific change facts | Partial | Current CLI does not compute git diff routing. Add a SkiaViewer/FS-Skia adapter or extend `ProjectSnapshot` to include changed paths and public-surface roles. |
| Tier ranks (`inner-loop`, `focused-authority`, etc.) | `RunMode` + `Severity` + `Stakes` | Yes, with semantic change | This repo intentionally moves from tiered target lists to advisory-vs-blocking requirements and merge fences. |
| `EvidenceGraph` | `Evidence.build` / `Evidence.effective` and `tasksGraphWellFormed` | Yes | Needs `tasks.deps.yml` or an extractor that derives dependencies from tasks. |
| `EvidenceAudit` | `evidenceNotSynthetic` plus optional diff-scan rules | Partial | Synthetic taint exists. Diff scan would be a new deterministic rule/probe or adapter fact. |
| `--accept-synthetic` disclosure | `Disclosure` in `Host.Loop` | Yes in kernel/host | Current CLI reports disclosures but does not expose a user-facing flag equivalent. |
| `validation.contract.yml` generated from routing rules | `Contract.ofRules`, `Json.ofContract`, `contract` command | Yes | The output schema differs. If a YAML contract is still needed for consumers, render it from `ContractEntry`. |
| `RefreshSurfaceBaselines` / currency gates | `contractsCurrent`, `fencedSurfacesVerified`, DesignSystem rules | Partial | Need concrete sensors for SkiaViewer surface baselines and generated views. |
| Design token drift / control rendering checks | DesignSystem adapter or future Skia/Rendering adapter | Partial | Existing DesignSystem adapter expects JSON policy/artifact facts. SkiaViewer needs producers for those facts. |
| Agent-reviewed design judgement | `AgentReviewed`, `ReviewRequest`, review cache key, `Host.Loop` dispatch | Yes in architecture | Fresh dispatch is not configured in current CLI; cache-only and recorded verdicts are available. |
| Human-only judgement | `HumanOnly` / `Escalated` | Yes | Requires maintainer process for recording final human decisions. |

## Artifact model for adoption

The adoption should have a small set of canonical artifacts. Anything generated should be clearly marked as generated and checked for currency.

| Artifact | Source or generated? | Produced by | Consumed by |
|---|---|---|---|
| `.specify/memory/constitution.md` | Source | Maintainers, `/speckit-constitution` | SpecKit adapter, human review, plan checks |
| `specs/<feature>/spec.md` | Source | `/speckit-specify`, `/speckit-clarify` | Agent-reviewed plan coverage, human review |
| `specs/<feature>/plan.md` | Source | `/speckit-plan` | Constitution check, contracts/currency rules |
| `specs/<feature>/research.md`, `data-model.md`, `contracts/`, `quickstart.md` | Source | `/speckit-plan` | Contract/currency rules, agent-reviewed coverage |
| `specs/<feature>/tasks.md` | Source | `/speckit-tasks`, implementer status updates | Evidence parser, evidence report |
| `specs/<feature>/tasks.deps.yml` | Source or generated from tasks | `/speckit-tasks` or a converter | `tasksGraphWellFormed`, `Evidence.effective` |
| `readiness/task-graph.json` / `.md` | Generated | Governance CLI or future bridge | Maintainers, PR evidence, CI artifacts |
| `readiness/evidence-audit.json` | Generated | Gate-mode governance run | CI status, PR evidence |
| `surface/**` or project surface baselines | Source baseline / generated refresh | Surface tool or tests | `contractsCurrent`, `fencedSurfacesVerified` |
| Design-system JSON facts (`policy.json`, token surfaces, rendered captures) | Source/generated bridge artifacts | Design/Rendering adapter sensor | DesignSystem adapter |
| Review cache records | Generated evidence | Agent-review edge or maintainer | `AgentReviewed` cache hits |
| CLI JSON reports (`route`, `contract`, `explain`, `evidence`) | Generated | `fsgg-governance` | CI, PR summaries, humans |

## Who runs what

| Phase | Actor | Command / action | Expected result |
|---|---|---|---|
| Constitution setup | Maintainer | Author or amend `.specify/memory/constitution.md` | Governance dial and principles exist; no placeholder sections |
| Specify / clarify | Product author or agent | Speckit specify/clarify skills | `spec.md` with testable requirements |
| Plan | Agent, reviewed by maintainer | Speckit plan; then `fsgg-governance route --mode inner` and `contract` | Advisory view of requirements, surfaces, future gates |
| Tasks | Agent | Speckit tasks; produce `tasks.md` and `tasks.deps.yml` | Story-grouped tasks with machine-readable topology |
| Analyze | Agent | Speckit analyze plus `fsgg-governance explain --mode inner` | Cross-artifact gaps are reported, not merged as hidden debt |
| Implement | Developer/agent | Update task states; run `evidence --mode inner`; run product tests | `[X]`, `[S]`, `[F]`, `[-]` are honest; synthetic debt visible |
| Pre-merge | CI or maintainer | `fsgg-governance route --mode gate`, `evidence --mode gate`, product test suite | Blocking requirements bite only when a fence trips |
| Agent review | Review-agent edge, budgeted | Load recorded verdict or dispatch when budget allows | Deterministic cache hits; pending misses do not become silent passes |
| Human judgement | Maintainer | Resolve `HumanOnly` escalations | Human decision is explicit and recorded outside the pure kernel |
| Generated-view refresh | Implementer | Regenerate baselines/views, then rerun contract/currency checks | Source and generated views are committed together |

## Proposed rollout

1. Adopt advisory observer mode first.
   Run this repo's CLI over SkiaViewer in `inner` mode from local scripts or CI non-blocking jobs. Capture `route`, `contract`, `explain`, and `evidence` JSON as build artifacts.

2. Add missing Spec Kit topology.
   Teach SkiaViewer's task generation to produce `tasks.deps.yml`, or add a converter that extracts dependency facts from existing task sections. This is the highest-leverage prerequisite because it unlocks `tasksGraphWellFormed` and synthetic-taint propagation.

3. Add phase sensing.
   Use `.governance-phase`, `.specify/feature.json`, branch naming, or CI event context to set `PhaseReached Merge` at merge. Without this, gate mode remains advisory because no merge fence trips.

4. Wire a merge CI job.
   GitHub required status checks can block merges until configured checks pass. A protected-branch workflow should run `fsgg-governance` in `Gate` mode against the PR merge base/current head and publish the JSON report.

5. Add a SkiaViewer path/surface adapter if FS-Skia-UI route parity is required.
   The current SpecKit adapter governs lifecycle artifacts. It does not know that `src/**/*.fsi` is a public package surface or that `src/SkiaViewer.Layout/**` should trigger layout/rendering checks. Add a domain adapter whose change shape includes changed paths and whose facts classify public API, tests, scripts, docs, and packaging surfaces.

6. Add generated-view currency rules.
   Translate surface baselines, docs views, catalogs, and examples into deterministic checks. The rule should compare a committed generated view against a fresh render from the canonical source and fail with the exact regeneration command.

7. Add design/rendering facts only after deterministic adoption is stable.
   For visual/UI governance, first generate structured evidence from SkiaViewer or FS.GG.Rendering: token policy, rendered captures, interaction state coverage, and page pattern specs. Feed those into the existing DesignSystem adapter or a Skia-specific adapter. Agent review should be opt-in and cached.

## Risks and mitigations

| Risk | Why it matters | Mitigation |
|---|---|---|
| Missing `tasks.deps.yml` | Evidence taint cannot be trusted without dependency topology | Generate deps during tasks or derive them from task sections before enabling gate mode |
| Wrong feature selected | A governance run can validate the wrong feature directory | Pass an explicit `--root` plus phase/feature inputs in CI; avoid implicit "latest specs dir" for merge checks |
| Advisory signals ignored | Inner-loop runs can produce warnings nobody reads | Publish JSON reports in PR checks and promote selected rules to blocking only at merge |
| Path-diff semantics absent | FS-Skia-UI route parity depends on changed paths | Add a SkiaViewer route adapter or extend CLI snapshot with git diff facts |
| Agent-review cache misses | Pending agent checks can look like noise | Keep cache-only default for CI; require recorded verdicts or explicit budgeted review only for rules that truly need judgement |
| Generated view drift | Humans may edit generated files directly | Make generated files single-source outputs and enforce currency checks |
| Over-porting FAKE behavior | Recreating FS-Skia-UI targets would import old coupling | Preserve the pure kernel/effects edge split; model gates as rules and reports, not as target strings |

## Bottom line

This repo can achieve the governance intent of FS-Skia-UI, and the current code already implements the hardest general pieces: reified checks, tiered rule arbitration, deterministic routing, evidence taint propagation, SpecKit phase rules, host effects, review caching, and a CLI surface.

The remaining work is not a new rule engine. It is artifact and adapter work:

1. Make SkiaViewer emit the machine-readable Spec Kit topology and phase facts.
2. Add or extend a domain adapter for SkiaViewer-specific changed paths, public surfaces, generated views, and rendering/design artifacts.
3. Run the existing CLI in advisory mode first, then make gate-mode CI required once the inputs are complete.

That path keeps the useful FS-Skia-UI guarantees while avoiding the old problem where a large FAKE-centered governance package becomes the thing every project must understand.
