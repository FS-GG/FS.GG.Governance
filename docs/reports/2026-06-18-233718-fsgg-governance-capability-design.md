# FS.GG governance capability design

**Timestamp:** 2026-06-18T23:37:18+02:00
**Revision:** 2026-06-19T10:42:00+02:00
**Author:** Codex
**Status:** Consolidated design update, no implementation changes
**Scope:** Define the FS.GG governance capability envelope for generated products, package surfaces, workflow evidence, design artifacts, documentation, and release gates.

## Executive summary

FS.GG needs a product-neutral governance platform, not a workflow wrapper. The
core system should provide a reusable rule algebra, typed facts, route
selection, evidence propagation, generated readiness, and CI enforcement. Product
domains should plug into that core through adapters and capability catalogs.

The design has three owners:

| Owner | Responsibility |
|---|---|
| `FS.GG.Governance` | Generic rule system, route/evidence/audit outputs, adapters, CLI, profiles, and CI gate contracts. |
| `FS.GG.Rendering` | Rendering packages, templates, samples, product skills, design artifacts, and generated-product assets. |
| Generated products | Project policy, work items, evidence declarations, product-specific capability catalog entries, and readiness artifacts. |

This document is informed by source analysis of FS-Skia-UI, the current
FS.GG.Governance repository, FS.GG.Rendering tooling, and a local generated
product checkout. The design below is the forward path for FS.GG; the source
systems are only inputs.

The primary outcome is:

```text
fsgg ship --mode gate --profile standard --json
```

That command becomes the protected-branch gate. It recomputes applicable rules
from the base/head change, reports deterministic JSON, blocks only on
profile-adjusted blocking findings, and emits enough evidence to explain every
selected gate.

## Design goals

FS.GG governance is successful when it can:

| Goal | Design consequence |
|---|---|
| Govern generated products without coupling to one renderer or template | Put product vocabulary in adapters and `.fsgg/capabilities.yml`, not in the kernel. |
| Keep small safe changes cheap | Route from precise impact facts, support scoped authoring, cache fresh evidence, and budget rule cost. |
| Make gates explainable | Emit route traces, rule contracts, proof trees, base severity, effective severity, and profile reasons. |
| Treat generated files as views | Declare source/view/generator relationships and block stale views at the appropriate boundary. |
| Separate truth from enforcement | Rules always report the same verdict; mode and profile only adjust whether a finding blocks. |
| Keep automation stable | Use deterministic JSON for CI, agents, branch protection, and readiness artifacts. |
| Keep humans oriented | Render the same reports through plain text and optional Spectre.Console views. |
| Move durable behavior into typed code | Compile stable governance commands and leave shell or `.fsx` for bootstrap, migration, and experiments. |

## Non-goals

FS.GG.Governance does not implement renderers, controls, window hosts, product
templates, or sample applications. It governs those surfaces by sensing facts,
checking contracts, and recording evidence.

FS.GG.Governance is also not a planner or optimizer. Planners, agents, and
generators may propose work. Governance checks the resulting artifacts and
evidence at defined boundaries.

## Current foundation

The repository already has the reusable core:

| Capability | Current position |
|---|---|
| Rule algebra | Reified `Check` values evaluate, render, hash, explain, and report reads. |
| Rule bridge | `CheckRule` separates deterministic, agent-reviewed, and human-only checks. |
| Evidence model | Authored and effective evidence states support synthetic and auto-synthetic propagation. |
| Adapter SPI | Domain adapters can keep their own fact vocabulary while reusing kernel behavior. |
| Existing adapters | Spec Kit and design-system adapters prove the model is not tied to one domain. |
| Host and CLI | Route, contract, explain, and evidence commands already exist with text and JSON output. |

The next work is not another kernel rewrite. It is product capability work:
workflow facts, git/CI facts, product gate identities, package/API checks,
generated-product checks, skill checks, docs checks, release checks, cost control,
freshness caching, and profile-aware enforcement.

## Architecture

The kernel remains small and dependency-light. Product knowledge lives outside
it.

```text
FS.GG.Governance.Kernel
  facts, rules, verdicts, checks, explanation, evidence, routes

FS.GG.Governance.Adapters.Spi
  adapter contracts, fact lifting, rule-pack composition

FS.GG.Governance.Adapters.*
  workflow, git/CI, package/API, generated product, skills,
  docs/examples, design/rendering, distribution, provenance

FS.GG.Governance.Host
  read-only sensing, effect boundaries, command execution records,
  report assembly

FS.GG.Governance.Cli
  fsgg route, check, refresh, verify, ship, release, tui, watch

FS.GG.Rendering and generated products
  templates, runtime packages, samples, captures, skills, product policy,
  capability catalogs, source artifacts, and generated readiness
```

Dependency rules:

| Layer | Allowed dependencies |
|---|---|
| Kernel | BCL and FSharp.Core only. |
| SPI | Kernel and light adapter contracts only. |
| Adapters | Domain-specific parsers and inspectors where they own that domain. |
| Host/tooling | File system, process runner, git, MSBuild, NuGet, hashing, generated reports. |
| CLI | Command parsing, JSON, plain text, Spectre.Console presentation. |
| Product runtime packages | No governance dependency unless the package explicitly owns governance behavior. |

## Lifecycle model

The future source model is `.fsgg` plus `work/<id>`. Markdown remains useful for
authoring, but structured files are authoritative for gates.

| Stage | Purpose | Primary command | Gate posture |
|---|---|---|---|
| `Charter` | Establish project identity, package surfaces, domains, branch policy, and default profile. | `fsgg init` or `fsgg charter` | Advisory until branch protection is configured. |
| `Intent` | Capture user value, scope, non-goals, and acceptance criteria. | `fsgg work intent <id>` | Advisory. |
| `Design` | Record architecture, public contracts, dependencies, migration notes, and evidence plan. | `fsgg work design <id>` | Advisory with route preview. |
| `WorkGraph` | Define typed work items, dependencies, owners, skills, and required evidence. | `fsgg work graph <id>` | Advisory or early fence. |
| `Implement` | Complete work and declare evidence. | `fsgg work update <id>` | Local checks, usually light or standard profile. |
| `Verify` | Run selected tests, surface checks, docs checks, generated-view checks, and evidence checks. | `fsgg verify <id>` | Blocking in selected CI contexts. |
| `Ship` | Recompute from base/head and enforce merge policy. | `fsgg ship <id> --mode gate --json` | Blocking. |
| `Release` | Pack, publish, and attest artifacts. | `fsgg release <id>` | Blocking for publication. |

Spec Kit artifacts are migration inputs. They can be imported, rendered, or
supported during transition, but they are not the long-term governing schema.

## Source artifacts

The project-level source files define policy and capability scope:

| Artifact | Purpose |
|---|---|
| `.fsgg/project.yml` | Project id, domain list, package surfaces, capability catalog pointer, default work root. |
| `.fsgg/policy.yml` | Governance profiles, default profile, enforcement mapping, branch policy, review budgets. |
| `.fsgg/capabilities.yml` | Packages, projects, `.fsi` contracts, tests, skills, docs, samples, baselines, template profiles, evidence tags. |
| `.fsgg/tooling.yml` | Command allow-list, timeouts, environment classes, external tool policy, tool version expectations. |
| `work/<id>/intent.md` | Human-authored intent, scope, non-goals, and acceptance criteria. |
| `work/<id>/design.md` | Architecture decisions, public contract impact, dependencies, migration notes. |
| `work/<id>/contracts/` | Source contracts or links to canonical contracts such as `.fsi`, OpenAPI, or gRPC definitions. |
| `work/<id>/graph.yml` | Typed work items, dependencies, owners, expected skills, and required evidence. |
| `work/<id>/evidence.yml` | Authored evidence declarations and artifact URIs. |

Generated views are outputs and must be currency-checked:

| Artifact | Purpose |
|---|---|
| `.fsgg/gates.json` | Generated gate registry with ids, metadata, prerequisites, cost, timeout, and owner. |
| `.fsgg/rules.md` | Rendered rule catalog from reified checks. |
| `readiness/<id>/route.json` | Matched rules, changed paths, selected gates, default-deny findings, cost, cache eligibility, and profile-adjusted enforcement. |
| `readiness/<id>/contract.json` | Rule contracts, required inputs, and source reads. |
| `readiness/<id>/explain.json` | Proof trees and explanation traces for applicable rules. |
| `readiness/<id>/evidence.json` | Effective evidence states, taint propagation, freshness, and graph failures. |
| `readiness/<id>/audit.json` | Ship verdict, blockers, warnings, provenance references, and exit-code basis. |
| `readiness/<id>/summary.md` | Human PR summary rendered from JSON. |
| `readiness/<id>/attestations/` | Optional SLSA/in-toto-shaped provenance summaries. |

## Rule packs and adapters

The kernel stays generic; adapters own domain facts and rule packs.

| Adapter | Facts it owns | Example rules |
|---|---|---|
| Workflow | Stage, work artifacts, graph nodes, dependencies, evidence declarations, work policy. | `workGraphWellFormed`, `designSatisfiesIntent`, `evidenceNotSynthetic`, `shipFence`. |
| Git/CI | Base/head, branch, changed paths, dirty paths, untracked paths, PR labels, status checks. | `changedPathsRouted`, `unknownPathDefaultsSafe`, `requiredStatusPresent`. |
| Policy/profile | Default profile, command override, maturity, base severity, effective severity, enforcement reason. | `profileKnown`, `profileAllowedForMode`, `effectiveSeverityExplained`. |
| Cost/cache | Rule cost, historical runtime, freshness keys, artifact hashes, command versions, cache entries. | `evidenceFresh`, `ruleWithinBudget`, `expensiveGateJustified`. |
| Tooling/process | Command specs, environment class, exit code, timeout, output digests, captured output URI. | `commandAllowed`, `commandCompleted`, `outputDigestRecorded`. |
| Package/API | Package projects, public `.fsi`, baselines, compatibility notes, FSI transcripts. | `publicSurfaceHasSignature`, `surfaceBaselineCurrent`, `breakingChangeHasMigration`. |
| Generated product | Template profile, generated root, package pins, product tests, generated guidance. | `templateInstantiates`, `generatedProductBuilds`, `generatedGuidanceCurrent`. |
| Skills | Skill ids, paths, references, capability mappings, product skill lists, optional mirrors. | `skillExists`, `skillContractPathValid`, `declaredSkillLoadedBeforeWork`. |
| Docs/examples | FsDocs pages, examples, reference docs, API docs, links, literate scripts. | `examplesRun`, `publicApiDocumented`, `generatedDocsCurrent`. |
| Design/rendering | Token sources, generated tokens, captures, contrast facts, control catalog, interaction states. | `tokensCurrent`, `contrastPasses`, `interactionStatesCovered`. |
| Build/package | Build and test commands, pack outputs, versions, package metadata, local or staging feeds. | `testsPassed`, `packableProjectsPacked`, `versionBumpedWhenPacked`. |
| Provenance | Source commit, base/head, builder identity, command records, artifact digests, generator versions. | `readinessHasProvenance`, `artifactDigestMatches`, `attestationCurrent`. |

## Gate identities

Product gates need stable identities. A typed `GateId` or generated registry
entry should include:

| Field | Meaning |
|---|---|
| `id` | Stable machine id used in route, evidence, and audit JSON. |
| `domain` | Owning adapter or capability domain. |
| `description` | Human-readable purpose. |
| `prerequisites` | Gates or facts required before this gate runs. |
| `cost` | Cheap, medium, high, or exhaustive. |
| `timeout` | Expected timeout class or explicit duration. |
| `owner` | Failure owner or responsible domain. |
| `maturity` | Observe, warn, block-on-pr, block-on-ship, or block-on-release. |
| `productCheck` | Whether the gate validates generated consumers. |
| `freshnessKey` | Inputs used to decide whether prior evidence can be reused. |

Routes should explain every selected gate in terms of changed path, affected
capability, matching rule, expected evidence, cost, and cheaper local
alternative when one exists.

## Modes, profiles, and maturity

Run mode answers where the command is running and what boundary is being
protected. Profile answers how strict the project wants to be at that boundary.
Rule maturity answers whether a rule is trusted enough to block.

| Lever | Examples | Changes truth? |
|---|---|---|
| Run mode | `sandbox`, `inner`, `focused`, `verify`, `gate`, `release` | No. |
| Governance profile | `light`, `standard`, `strict`, `release` | No. |
| Rule maturity | `observe`, `warn`, `block-on-pr`, `block-on-ship`, `block-on-release` | No. |

Every finding reports both base and effective severity:

```text
rule: generated-token-current
verdict: fail
baseSeverity: blocking
mode: inner
profile: light
maturity: block-on-ship
effectiveSeverity: advisory
reason: light profile does not block generated-view drift outside the ship gate
```

Profiles belong in `.fsgg/policy.yml`:

```yaml
governance:
  defaultProfile: standard

profiles:
  light:
    unknownPaths: warn
    staleEvidence: warn
    syntheticEvidence: warn
    uncertainVerdict: warn
    generatedViewDrift: warn
    requireProvenance: false
    maxCost: cheap

  standard:
    unknownPaths: block
    staleEvidence: blockAtGate
    syntheticEvidence: blockAtGate
    uncertainVerdict: warn
    generatedViewDrift: blockAtGate
    requireProvenance: false
    maxCost: medium

  strict:
    unknownPaths: block
    staleEvidence: block
    syntheticEvidence: block
    uncertainVerdict: blockAtGate
    generatedViewDrift: block
    requireProvenance: true
    maxCost: high

  release:
    unknownPaths: block
    staleEvidence: block
    syntheticEvidence: block
    uncertainVerdict: block
    generatedViewDrift: block
    requireProvenance: true
    requirePackEvidence: true
    requirePublishPlan: true
    maxCost: exhaustive
```

The profile can change effective enforcement. It must never hide the underlying
verdict, alter rule hashes, or remove findings from JSON.

## Cost model

Cost control is part of correctness. If a one-line low-risk change selects a
high-cost route, the explanation must make the risk obvious. If it cannot, the
route rule is too broad or the gate belongs in advisory or scheduled validation.

| Requirement | Mechanism |
|---|---|
| Keep authoring fast | `fsgg route --paths ...`, `fsgg check --paths ...`, and `fsgg check --since <rev>`. |
| Route from impact facts | Map paths to capabilities, packages, public surfaces, generated views, docs pages, controls, tests, and evidence. |
| Avoid needless reruns | Cache evidence by rule hash, artifact hash, command version, generator version, base/head, environment class, and output digest. |
| Split large gates | Separate structural scans, restore/build, focused product tests, visual captures, full generated-product verify, and release checks. |
| Explain broad routes | Include matched rule, changed path, affected capability, selected gate, cost, and cheaper local alternative. |
| Promote carefully | Start new or heuristic rules as observe/warn before they block PR, ship, or release. |
| Run exhaustive checks at the right boundary | Use ship, release, nightly, or explicit verify for broad matrices. |

Examples:

| Change | Expected route |
|---|---|
| Prose-only docs edit | Docs links, generated-docs currency, and affected examples. |
| Private implementation edit | Affected project build/tests and relevant local rules. |
| Public `.fsi` edit | Surface baseline, FSI transcript, compatibility note, and focused semantic tests. |
| Token-source edit | Token generation, contrast checks for affected roles, and selected visual captures. |
| Template or capability catalog edit | Broad generated-product and package-pin checks, because generated consumers are affected. |

## Command surface

The CLI should expose one data model through three renderers:

| Surface | Purpose | Contract |
|---|---|---|
| JSON | CI, agents, scripts, cached evidence, readiness, branch protection. | Stable schema, deterministic order, no ANSI, no terminal wrapping, no implicit clock. |
| Plain text | Simple logs and redirected output. | Human-readable, not the automation contract. |
| Spectre.Console | Interactive route, evidence, verify, ship, and watch views. | Presentation only over the same report objects. |

Initial commands:

| Command | Purpose |
|---|---|
| `fsgg route` | Show selected gates and route trace for paths, since-rev, or base/head. |
| `fsgg check` | Run selected cheap/focused checks for local authoring. |
| `fsgg refresh` | Regenerate declared views and baselines. |
| `fsgg evidence` | Compute effective evidence state and freshness. |
| `fsgg verify` | Run profile-appropriate verification before PR or explicit local validation. |
| `fsgg ship` | Recompute merge policy from base/head and emit blocking audit. |
| `fsgg release` | Validate pack, publish, version, metadata, and provenance requirements. |
| `fsgg tui` | Optional interactive command center. |
| `fsgg watch` | Optional local watch projection over route/evidence/check reports. |

`fsgg ship --mode gate --profile standard --json` is the stable CI entry point.
Other jobs may produce evidence, but this command owns the protected-branch
verdict.

## Readiness and provenance

Generated readiness is structured first and Markdown second. Each command record
should capture enough information to make builds, tests, packs, template
instantiation, git diffs, package inspection, and visual capture auditable.

```text
CommandRun =
  executable
  arguments
  workingDirectory
  environmentDelta
  timeout
  exitCode
  stdoutDigest
  stderrDigest
  capturedOutputPath
  duration
```

Wall-clock timestamps and durations are useful evidence, but deterministic JSON
should only include them when supplied as sensed input or explicitly marked as
non-deterministic metadata.

Provenance records should include:

| Field | Purpose |
|---|---|
| Source commit and base/head | Tie readiness to the diff that was checked. |
| Rule hash and generator version | Detect stale checks after rule or generator changes. |
| Artifact digests | Detect drift in generated files, packages, baselines, captures, and docs. |
| Command records | Audit external process results without embedding shell policy. |
| Environment class | Separate local, CI, release, and generated-product environments. |
| Builder identity | Support future SLSA/in-toto-shaped attestations without overclaiming compliance. |

## Tooling strategy

Durable governance behavior belongs in compiled F# libraries and CLI commands.
Shell and `.fsx` are allowed, but they should not own policy or stable gate
truth.

| Form | Use |
|---|---|
| Compiled F# libraries | Route sensors, git snapshots, process runner facade, package inspection, generation manifests, freshness keys. |
| Compiled CLI commands | Stable user/CI contract, exit codes, JSON schemas, Spectre projections. |
| `.fsx` scripts | FSI sketches, migrations, exploratory reports, literate docs, compatibility wrappers. |
| Shell scripts | Bootstrap, tool install, or temporary wrapper around `dotnet fsgg ...`. |

Graduation rule: if a script needs tests, stable JSON, stable exit codes,
readiness artifacts, generated-view currency, CI usage, or required user
documentation, it graduates into compiled F#.

Recommended dependencies stay at the edge:

| Need | Tooling |
|---|---|
| File IO, hashing, JSON | BCL and `System.Text.Json`. |
| External processes | In-house process-runner facade over `System.Diagnostics.Process`, with `CliWrap` considered only if needed. |
| CLI parsing | `System.CommandLine` when command complexity exceeds the current parser. |
| Human terminal UI | `Spectre.Console` in the CLI only. |
| Globs | `Microsoft.Extensions.FileSystemGlobbing`. |
| YAML | `YamlDotNet` with strict FS.GG-owned schemas. |
| MSBuild inspection | MSBuild API and `Microsoft.Build.Locator`; actual builds still use `dotnet build/test/pack`. |
| NuGet inspection | `NuGet.Protocol` and `NuGet.Packaging`; actual publishing still uses official tooling. |
| Git facts | Start with the git CLI through the process runner; consider `LibGit2Sharp` only for read-only cases where the native dependency is acceptable. |

## Migration model

Existing generated products can migrate incrementally:

| Existing artifact | Target |
|---|---|
| `.specify/memory/constitution.md` | `.fsgg/project.yml`, `.fsgg/policy.yml`, and optional `charter.md`. |
| `specs/<id>/spec.md` | `work/<id>/intent.md`. |
| `specs/<id>/plan.md` | `work/<id>/design.md`. |
| `specs/<id>/contracts/` | `work/<id>/contracts/` or links to canonical package contracts. |
| `specs/<id>/tasks.md` | `work/<id>/graph.yml` plus a rendered human view. |
| Markdown checkbox states | `work/<id>/evidence.yml`. |
| Surface-area tests and baselines | Package/API adapter facts and `surfaceBaselineCurrent` checks. |
| Local build/pack scripts | Compiled `fsgg` commands with optional compatibility wrappers. |

The importer should preserve uncertainty. If a legacy task file cannot express a
dependency, owner, or evidence requirement unambiguously, it should create a
warning and require the work graph to be completed before ship.

## Implementation roadmap

### Phase 1: route parity

- Add git/CI snapshot facts for base ref, head ref, changed paths, dirty paths,
  untracked paths, and CI context.
- Add typed gate metadata and route traces.
- Add profile parsing, maturity, and base/effective severity computation.
- Add default-deny for unknown paths and scoped `--paths` authoring.
- Emit route JSON with selected gates, matched rules, unmatched paths,
  expected artifacts, cost, cache eligibility, and explanation.

### Phase 2: workflow and evidence

- Add `.fsgg/project.yml`, `.fsgg/policy.yml`, `work/<id>/graph.yml`, and
  `work/<id>/evidence.yml` schemas.
- Add import from legacy spec/task artifacts with uncertainty warnings.
- Implement work DAG validation, synthetic taint propagation, accepted
  deferrals, stale evidence, and ship audit blockers.
- Produce `evidence.json`, `audit.json`, and `summary.md`.

### Phase 3: generated views

- Add `fsgg refresh` as the single regeneration entry point.
- Define a generation manifest for source, generated view, renderer, and
  currency gate.
- Generate gate metadata, rule catalogs, capability docs, skill references,
  API-surface docs, and baselines.
- Block stale generated views at the configured boundary.

### Phase 4: product and capability catalog

- Define `.fsgg/capabilities.yml`.
- Add generated-product checks in cost tiers: structural scan, restore/build,
  focused tests, full verify, and release validation.
- Ensure generated products can run governance locally without monorepo access.
- Replace durable product shell behavior with compiled commands.

### Phase 5: package, design, docs, and skills

- Add `.fsi` surface baseline generation and drift checks.
- Add FSI transcript checks for public examples and package contracts.
- Connect design-system facts to real token, capture, contrast, and control
  catalog sources.
- Add docs/examples checks for FsDocs, literate scripts, public API docs, and
  link/reference currency.
- Add skill-quality checks for product skills, task skill lists, path contracts,
  and optional mirrors.

### Phase 6: ship, release, and provenance

- Define `fsgg verify` and `fsgg ship --mode gate --json` schemas and exit codes.
- Add Spectre.Console projections backed by the same report objects.
- Add command-run records for builds, tests, packs, git facts, and package
  inspections.
- Publish GitHub Actions guidance for required branch protection.
- Add scheduled exhaustive validation for broad matrices.
- Add release rules for version bumps, package metadata, template pins,
  publish plans, trusted publishing, and provenance.

### Phase 7: retirement

- Move active products to `.fsgg` and `work/<id>`.
- Keep legacy adapters only as import or compatibility tooling.
- Remove old build/package dependencies from the main path after migration.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Rebuilding a workflow wrapper instead of a governance platform | Keep `.fsgg` schemas, adapters, capability catalogs, and CI audits as the primary scope. |
| Making local development oppressive | Enforce cost budgets, scoped routing, freshness caching, maturity levels, and advisory-first promotion. |
| Allowing profiles to hide failures | Always emit underlying verdict, base severity, effective severity, mode, profile, maturity, and reason. |
| Letting terminal UI diverge from automation | Render Spectre.Console views from the same immutable report objects used for JSON. |
| Moving shell policy into untested `.fsx` | Graduate required behavior into compiled libraries and commands. |
| Leaking product dependencies into the kernel | Keep product facts in adapters and capability catalogs. |
| Letting stale readiness pass by presence | Key evidence by source hash, rule hash, command version, artifact digest, base/head, and environment class. |
| Overclaiming provenance | Emit compatible metadata first; claim formal compliance only after explicit verification. |
| Ambiguous legacy migration | Import with warnings and require explicit work graph/evidence completion before ship. |

## Acceptance bar

The design is implemented when a generated product can:

1. Declare project policy, capabilities, work, and evidence in `.fsgg` and
   `work/<id>`.
2. Route a local scoped change cheaply and explain selected gates.
3. Refresh generated views from declared sources and detect drift.
4. Validate public package surfaces, docs, examples, skills, design artifacts,
   generated consumers, and release metadata through adapters.
5. Cache fresh expensive evidence and rerun only when relevant inputs change.
6. Emit deterministic route, contract, explain, evidence, and audit JSON.
7. Render useful human CLI output without changing automation truth.
8. Block merge through `fsgg ship --mode gate --profile standard --json`.
9. Support release checks with package, publish, and provenance evidence.
10. Migrate legacy spec/task artifacts without treating them as the future
    source of truth.

The central constraint remains simple: strict at protected boundaries, cheap in
the authoring loop, and explainable everywhere.
