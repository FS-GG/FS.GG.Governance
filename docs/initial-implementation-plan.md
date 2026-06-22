---
title: Initial implementation plan
category: SDD
categoryindex: 6
index: 12
description: Implementation plan for the FS.GG.SDD consumer product design, with explicit SDD, Governance, Rendering, and generated-product ownership.
---

# Initial implementation plan

This is the implementation plan for the FS.GG.SDD consumer product design in
[initial-design.md](initial-design.md). It is maintained in this repository
because FS.GG.SDD owns the native spec-driven development lifecycle. The plan
also names optional Governance, Rendering, and generated-product work where the
consumer workflow crosses repository boundaries.

The design is implemented by coordinated work across:

| Owner | Implementation responsibility |
|---|---|
| `FS.GG.SDD` | Lifecycle artifact model, normalized work model, generated SDD views, lifecycle authoring commands, agent command/skill generation, and optional Governance-facing contracts. |
| `FS.GG.Governance` | Rule algebra use, route/evidence/audit reports, capability catalogs, profiles, freshness, cost/cache, protected-boundary gates, policy enforcement, and release/provenance gates. |
| `FS.GG.Rendering` | Rendering templates, generated-product assets, design-system facts, samples, captures, and product-specific documentation surfaces. |
| Generated products | Project policy, declared capabilities, work items, evidence declarations, local readiness artifacts, and product-specific configuration. |

FS.GG.SDD remains independently buildable and usable without Governance
installed. Governance integration is optional and versioned.

Implementation is driven by the consumer experience:

| Consumer need | Product response |
|---|---|
| Start a project without hidden FS.GG repository knowledge. | `fsgg-sdd init` creates the SDD skeleton, work root, policy pointers, and agent guidance targets. |
| Turn intent into executable work. | Charter, spec, clarify, checklist, plan, tasks, evidence, verify, and ship share one typed lifecycle model. |
| Know what artifacts must contain. | SDD lifecycle rules define valid spec/plan/task/evidence shape, loaded-skill expectations, and test obligations. |
| Use agents safely. | Claude and Codex guidance is generated from the lifecycle model and cannot become a second source of truth. |
| Keep local authoring cheap. | SDD validates lifecycle artifacts and generated-view currency before optional broad product gates. |
| Add protected-boundary rigor later. | SDD emits versioned readiness JSON that Governance can inspect for routing, freshness, profiles, and enforcement. |

## Development Workflow

Standard Spec Kit remains the workflow used to develop this repository:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence
```

Product code, packages, and tests are added to this repository only through a
Spec Kit feature that defines artifact contracts and verification. The first
FS.GG.SDD feature remains `001-sdd-artifact-model`; it defines the lifecycle
artifact contract before commands or generators are implemented.

For the native SDD product lifecycle, `fsgg-sdd analyze` is the tasks-ready
readiness step that emits `readiness/<id>/analysis.json`; `fsgg-sdd evidence`
records authored evidence declarations and refreshes the SDD work model before
later verify and ship readiness slices.

## Scope Boundary

FS.GG.SDD owns:

- project charter and policy workflow as SDD lifecycle artifacts;
- specify, clarify, checklist, plan, tasks, analyze, implement, verify, and
  ship readiness artifacts;
- schema-versioned structured contracts for SDD-authored sources;
- normalized work model generation;
- generated SDD readiness views;
- agent command and skill generation for Claude and Codex;
- optional contracts consumed by FS.GG.Governance.

FS.GG.SDD does not own:

- rule evaluation;
- evidence freshness policy;
- route/profile selection;
- protected branch gate enforcement;
- package release enforcement for non-SDD products;
- rendering templates, controls, captures, or generated-product runtime
  behavior.

Those concerns are planned here so the design is complete, but their
implementation happens in FS.GG.Governance, FS.GG.Rendering, or generated
products.

## Design Coverage

| Design area | Primary owner | Implementation track |
|---|---|---|
| Consumer SDD experience | `FS.GG.SDD` | `fsgg-sdd init`, lifecycle commands, readable diagnostics, quickstart, migration, and no-Governance workflow. |
| Native SDD lifecycle | `FS.GG.SDD` | Artifact model, work model, lifecycle commands, task/evidence state, agent guidance. |
| Lifecycle rule pack | `FS.GG.SDD` with Governance machinery | Spec/plan/task/evidence contracts, skill requirements, test obligations, and Governance-compatible checks. |
| Normalized work model | `FS.GG.SDD` | `WorkModel` assembly, source digests, conflict diagnostics, deterministic JSON. |
| Capability catalog MVP | `FS.GG.Governance` | `.fsgg/capabilities.yml`, path map, surfaces, checks, governed-root classification. |
| Project policy and tooling | `FS.GG.Governance` | `.fsgg/policy.yml`, `.fsgg/tooling.yml`, profile, command, timeout, and environment schemas. |
| Route and ship walking skeleton | `FS.GG.Governance` | Git/CI facts, route traces, selected gates, audit JSON, protected-branch guidance. |
| Profiles, modes, and maturity | `FS.GG.Governance` | Base/effective severity, truth-table fixtures, enforcement JSON snapshots. |
| Cost and cache | `FS.GG.Governance` | Rule cost, freshness keys, evidence reuse, broad-route explanation. |
| Generated readiness views | Shared | SDD emits lifecycle readiness; Governance emits route/contract/explain/evidence/audit. |
| Agent guidance | `FS.GG.SDD` with Governance contracts | Generated Claude/Codex instructions from the lifecycle model; agent-reviewed rule outputs stay advisory until calibrated. |
| Package, docs, skills, design adapters | `FS.GG.Governance`, `FS.GG.Rendering` | Adapter rule packs, product facts, generated-product checks, docs/examples checks, design facts. |
| Release and provenance | `FS.GG.Governance`; SDD for its own packages | Command records, artifact digests, package/release rules, attestations, compatibility docs. |

## Ground Rules

- Markdown is an authoring surface. Schema-versioned structured artifacts are
  the machine contract.
- If prose and structured data disagree, the feature plan must say which source
  wins, how the conflict is reported, and which generated view records the
  diagnostic.
- Public F# modules get `.fsi` signatures first, semantic tests through the
  public surface, then `.fs` implementation.
- Stateful commands, generators, validators, and agent writers expose or wrap an
  Elmish-style `Model`, `Msg`, `Effect`, `init`, and `update` boundary.
- JSON is the automation contract. Plain text and Spectre.Console output are
  projections over the same report objects.
- Deterministic JSON must not depend on implicit clocks, terminal wrapping, or
  ANSI output.
- Generated views are outputs. Their presence is not proof of currency.
- Agent-reviewed findings are advisory until cache, prompt-isolation,
  confidence, and calibration constraints are implemented.
- SDD must remain useful without the Governance gate runtime installed.

## Command Naming

`initial-design.md` uses `fsgg ...` for the broad FS.GG command family. This
repository's constitution reserves the SDD command family as `fsgg-sdd` unless
a later release decision chooses otherwise.

| Design command | Owner | Planned command |
|---|---|---|
| `fsgg new` / `fsgg init` | SDD plus optional template providers | `fsgg-sdd init` for SDD skeletons; optional FS.GG umbrella command may delegate. |
| `fsgg charter` | SDD | `fsgg-sdd charter` |
| `fsgg work specify` | SDD | `fsgg-sdd specify` |
| `fsgg work clarify` | SDD | `fsgg-sdd clarify` |
| `fsgg work checklist` | SDD | `fsgg-sdd checklist` |
| `fsgg work plan` | SDD | `fsgg-sdd plan` |
| `fsgg work tasks` | SDD | `fsgg-sdd tasks` |
| `fsgg analyze` | SDD | `fsgg-sdd analyze` |
| `fsgg work update` / `fsgg evidence` | SDD for declarations; Governance for freshness | `fsgg-sdd evidence` or `fsgg-sdd update`; Governance computes effective evidence. |
| `fsgg route` | Governance | Governance CLI command |
| `fsgg check` | Governance | Governance CLI command |
| `fsgg refresh` | Shared | SDD refreshes SDD views; Governance refreshes gate/rule/capability views. |
| `fsgg verify` | Shared | `fsgg-sdd verify` emits SDD readiness; Governance owns profile-aware verification gates. |
| `fsgg ship` | Shared contract; Governance enforcement | `fsgg-sdd ship` emits SDD readiness; Governance owns protected-branch verdict. |
| `fsgg release` | Governance | Governance release gate; SDD uses it for its own packages once packaged. |
| `fsgg tui` / `fsgg watch` | Governance | Optional Governance presentation commands. |

Exact subcommand names are locked by the feature specs that introduce each CLI
surface.

## Source Model

The full design source model is `.fsgg` plus `work/<id>`. SDD owns the lifecycle
contract. Governance owns capability, route, profile, freshness, and gate
policy.

Project-level authored sources:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `.fsgg/project.yml` | Shared, with SDD-owned lifecycle fields | Project id, domain list, default work root, package surfaces, and pointers to SDD and Governance policy. |
| `.fsgg/sdd.yml` | SDD | SDD lifecycle policy, artifact layout, generated-view policy, and schema migration posture. |
| `.fsgg/agents.yml` | SDD | Agent command and skill generation targets for Claude, Codex, and future agents. |
| `.fsgg/policy.yml` | Governance | Governance profiles, default profile, enforcement mapping, branch policy, and review budgets. |
| `.fsgg/capabilities.yml` | Governance with generated-product input | Capability domains, path map, protected surfaces, checks, owners, cost, maturity, and release surfaces. |
| `.fsgg/tooling.yml` | Governance | Command allow-list, timeouts, environment classes, external tool policy, and tool version expectations. |

Work-item authored sources:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `work/<id>/charter.md` | SDD | Project or work charter when a lifecycle slice needs local principles and boundaries. |
| `work/<id>/spec.md` | SDD | User value, scope, stories, requirements, non-goals, and acceptance criteria. |
| `work/<id>/clarifications.md` | SDD | Material ambiguity and explicit answers. |
| `work/<id>/checklist.md` | SDD | Requirements-quality checks before planning. |
| `work/<id>/plan.md` | SDD | Technical plan, contracts, risks, verification, and migration posture. |
| `work/<id>/contracts/` | SDD and product owners | Public or tool-facing contracts described by the plan. |
| `work/<id>/tasks.yml` | SDD | Typed implementation task graph. |
| `work/<id>/evidence.yml` | SDD for declarations; Governance for effective state | Declared implementation, verification, synthetic, and deferral evidence. |

Generated views:

| Artifact | Primary owner | Purpose |
|---|---|---|
| `readiness/<id>/work-model.json` | SDD | Deterministic normalized work model with source digests and diagnostics. |
| `readiness/<id>/analysis.json` | SDD | Cross-artifact consistency diagnostics. |
| `readiness/<id>/verify.json` | SDD | SDD-owned verification results and readiness facts. |
| `readiness/<id>/ship.json` | SDD | Merge-boundary SDD readiness for CI and optional Governance consumers. |
| `readiness/<id>/summary.md` | Shared | Human-readable summary rendered from structured readiness data. |
| `readiness/<id>/agent-commands/` | SDD | Generated agent guidance derived from the same lifecycle model. |
| `.fsgg/gates.json` | Governance | Generated gate registry with ids, prerequisites, cost, timeout, owner, maturity, and freshness keys. |
| `.fsgg/rules.md` | Governance | Rendered rule catalog from reified checks. |
| `readiness/<id>/route.json` | Governance | Matched rules, changed paths, selected gates, unknown-path findings, cost, cache eligibility, and profile-adjusted enforcement. |
| `readiness/<id>/contract.json` | Governance | Rule contracts, required inputs, and source reads. |
| `readiness/<id>/explain.json` | Governance | Proof trees and explanation traces for applicable rules. |
| `readiness/<id>/evidence.json` | Governance | Effective evidence states, taint propagation, freshness, and graph failures. |
| `readiness/<id>/audit.json` | Governance | Ship verdict, blockers, warnings, provenance references, and exit-code basis. |
| `readiness/<id>/attestations/` | Governance | Optional SLSA/in-toto-shaped provenance summaries. |

Every generated view must identify sources, source digests, generator version,
and stale-view diagnostics.

## Roadmap

Progress markers (status legend): 🟢 / ✅ complete · 🟡 partial (core landed;
emission/wiring deferred) · 🔴 not started · ⬜ optional/out of scope. As of
2026-06-22 every `FS.GG.SDD`-owned feature is complete (🟢); the remaining 🟡/🔴
rows are Governance- or Rendering-owned follow-ons (host wiring, capability-catalog
expansion, release gates).

- 🟢 [x] Scaffold empty repository with Spec Kit metadata, constitution, docs, and
  Claude/Codex guidance.
- 🟢 [x] Create GitHub repository under `FS-GG`.
- 🟢 [x] Update FS-GG org profile/site to list SDD as a separate product.
- 🟢 [x] Copy development-relevant Governance and org reference docs into
  `docs/reference/`.
- 🟢 [x] Replace copied Governance-only roadmap with an SDD-scoped plan.
- 🟢 [x] Expand this plan to cover the full `initial-design.md` design with
  explicit owner boundaries.
- 🟢 [x] Implement `001-sdd-artifact-model` as the first packable SDD artifact
  model library with fixtures, diagnostics, deterministic JSON, optional
  Governance boundary contracts, and readiness evidence.
- 🟢 [x] Implement `002-normalized-work-model` by extending the artifact model
  library with pure normalized work-model generation, generated-view currency
  checks, schema migration posture, diagnostics, fixtures, deterministic JSON,
  and readiness evidence.
- 🟢 [x] Complete `003-native-sdd-lifecycle-commands`; the command library, CLI
  host, public MVU/report surface, and `fsgg-sdd init` MVP are implemented
  with readiness evidence.
- 🟢 [x] Implement `004-charter-command` by adding `fsgg-sdd charter`, safe
  authored charter create/rerun behavior, generated work-model state reporting
  and refresh where source data is valid, deterministic reports, text
  projection, optional Governance compatibility facts, CLI smoke evidence, FSI
  evidence, and full-suite verification.
- 🟢 [x] Implement `005-specify-command` by adding `fsgg-sdd specify`, typed
  specification ids and parser contracts, safe specification create/rerun and
  refusal behavior, specification summaries in command reports, generated-view
  currency reporting and refresh where source data is valid, deterministic
  JSON, text projection, dry-run behavior, optional Governance compatibility
  facts, CLI smoke evidence, FSI evidence, and full-suite verification.
- 🟢 [x] Implement `006-clarify-command` by adding `fsgg-sdd clarify`, typed
  clarification question ids and parser contracts, safe clarification
  create/rerun behavior, durable decisions and accepted deferrals, missing
  answer and unsafe-change diagnostics, clarification summaries in command
  reports, generated-view currency reporting and refresh where source data is
  valid, deterministic JSON, text projection, dry-run behavior, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence, and
  full-suite verification.
- 🟢 [x] Implement `007-checklist-command` by adding `fsgg-sdd checklist`, typed
  checklist item/result ids and parser contracts, safe checklist create/rerun
  behavior, durable requirements-quality results, failed-quality and stale
  result diagnostics, checklist summaries in command reports, generated-view
  currency reporting and refresh where source data is valid, deterministic
  JSON, text projection, dry-run behavior, optional Governance compatibility
  facts, CLI smoke evidence, FSI evidence, and full-suite verification.
- 🟢 [x] Implement `008-plan-command` by adding `fsgg-sdd plan`, typed plan
  decision/contract/verification/migration/generated-view ids and parser
  contracts, safe plan create/rerun behavior, durable planning decisions,
  accepted deferral visibility, stale decision and unsafe-change diagnostics,
  plan summaries in command reports, generated-view currency reporting and
  refresh where source data is valid, deterministic JSON, text projection,
  dry-run behavior, optional Governance compatibility facts, CLI smoke
  evidence, FSI evidence, performance evidence, and full-suite verification.
- 🟢 [x] Implement `009-tasks-command` by adding `fsgg-sdd tasks`, typed
  `tasks.yml` facts and parser contracts, task source snapshots, task graph
  derivation, safe task create/rerun behavior, stable task ids, stale task
  visibility, dependency/evidence/status diagnostics, task summaries in command
  reports, generated-view currency reporting and refresh where source data is
  valid, deterministic JSON, text projection, dry-run behavior, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence,
  performance evidence, and full-suite verification. Evidence is recorded in
  `specs/009-tasks-command/readiness/`.
- 🟢 [x] Implement `010-analyze-command` by adding `fsgg-sdd analyze`, the
  generated `readiness/<id>/analysis.json` contract, analysis summaries in
  command reports, tasks-ready prerequisite diagnostics, authored-source
  preservation, dry-run reporting, deterministic JSON/text projection, optional
  Governance compatibility facts, CLI smoke evidence, FSI evidence,
  performance evidence, and full-suite verification. Evidence is recorded in
  `specs/010-analyze-command/readiness/`.
- 🟢 [x] Implement `011-evidence-command` by adding `fsgg-sdd evidence`, the
  schema-versioned `work/<id>/evidence.yml` contract, evidence summaries in
  command reports, analysis-ready prerequisite diagnostics, safe authored
  evidence writes, dry-run reporting, deterministic JSON/text projection,
  optional Governance compatibility facts, CLI JSON/dry-run/text smoke
  evidence, FSI evidence, performance evidence, and full-suite verification.
  Evidence is recorded in `specs/011-evidence-command/readiness/`.

### Phase 1: SDD Artifact Model — 🟢 complete

Owner: `FS.GG.SDD`.

Purpose: define the typed lifecycle contract before SDD commands or generators
exist.

- 🟢 [x] Create the first feature spec for `001-sdd-artifact-model`.
- 🟢 [x] Define `WorkId`, `Stage`, `RequirementId`, `DecisionId`, `TaskId`,
  `EvidenceId`, `ArtifactRef`, `SchemaVersion`, source digest, and generator
  version types.
- 🟢 [x] Specify SDD-owned schemas for `.fsgg/project.yml`, `.fsgg/sdd.yml`, and
  `.fsgg/agents.yml`.
- 🟢 [x] Specify schemas for `work/<id>` metadata, structured front matter where
  used, `tasks.yml`, and `evidence.yml`.
- 🟢 [x] Define diagnostic ids for missing artifacts, malformed schema versions,
  duplicate ids, unknown references, stale generated views, and
  prose/structured mismatch.
- 🟢 [x] Define the first SDD lifecycle rule contracts for required spec sections,
  plan obligations, task graph shape, evidence declarations, loaded skills, and
  test obligations.
- 🟢 [x] Express lifecycle rules in a Governance-compatible check model without
  implementing route/profile/freshness/gate semantics in SDD.
- 🟢 [x] Define conflict behavior for requirement ids, task references, decision
  references, status, dependency, owner, and required-evidence disagreement.
- 🟢 [x] Add `.fsi` signatures before implementation.
- 🟢 [x] Add semantic tests for schema validation, id stability, conflict
  diagnostics, stale-view diagnostics, and deterministic ordering.
- 🟢 [x] Record compatibility boundaries for Governance-owned `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` without implementing
  Governance semantics.

Status: complete on 2026-06-19. The implemented library is
`src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`; verification evidence is
recorded in `specs/001-sdd-artifact-model/readiness/`.

Exit criteria:

- The repository has a packable SDD artifact-model library.
- Public signatures define the SDD machine contract.
- Fixtures cover valid, malformed, duplicate-id, unknown-reference,
  prose/structured mismatch, and stale-view cases.
- Lifecycle rule fixtures explain what a consumer must fix in specs, plans,
  tasks, evidence, skills, and test declarations.
- The plan for every lifecycle artifact identifies authored source, structured
  model, generated view, stale behavior, and diagnostics.

### Phase 2: Governance Ship Walking Skeleton And Catalog MVP

Owner: `FS.GG.Governance`; SDD provides optional lifecycle inputs.

Purpose: prove the protected-boundary value early, as required by the design,
without waiting for the full lifecycle command suite.

- 🟢 [x] Define versioned `.fsgg/project.yml`, `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` MVP schemas in Governance.
  **(F014 — `FS.GG.Governance.Config`, done 2026-06-20)**
- 🟢 [x] Include the minimum capability catalog fields: domains, path map,
  surfaces, checks, cost, owner, environment, and maturity.
  **(F014 — typed facts, done 2026-06-20)**
- 🟢 [x] Implement deterministic glob precedence for path-to-capability routing.
  **(F015 — `FS.GG.Governance.Routing`, done 2026-06-20)**
- 🟢 [x] Add git/CI snapshot facts for base ref, head ref, changed paths, dirty
  paths, untracked paths, branch, PR labels, status checks, and CI context.
  **(F016 — `FS.GG.Governance.Snapshot`, done 2026-06-20)**
- 🟢 [x] Add unknown governed path findings only inside governed roots or protected
  boundaries. **(F017 — `FS.GG.Governance.Findings`, done 2026-06-20)** Closes the
  two exit criteria below: routine unclassified files do not trigger global
  default-deny, and unknown paths under declared governed roots produce explicit
  findings.
- 🟢 [x] Define typed `GateId` metadata with prerequisites, cost, timeout, owner,
  maturity, product-check flag, and freshness key. **(F018 —
  `FS.GG.Governance.Gates`, done 2026-06-20)** A single pure, total
  `Gates.buildRegistry : TypedFacts -> GateRegistry` projects each declared
  capability check into one `Gate` with a stable, injective `GateId`
  (`domain:checkId`); it establishes the stable gate identities the remaining
  Phase-2 rows (`fsgg route`/`fsgg ship`, route/audit JSON, `.fsgg/gates.json`)
  and Phase 5/11 consume. Deferred to Phase 10: gate-to-gate prerequisites +
  topological order, and a richer product-check derivation.
- 🟢 [x] Select the gates a specific change reaches from the registry + route +
  findings. **(F019 — `FS.GG.Governance.Route`, done 2026-06-20)** A single pure,
  total `Route.select : GateRegistry -> RouteReport -> FindingReport ->
  RouteResult` joins the typed registry to a routed change, carrying selected
  gates (with their selecting paths), the unknown-path findings, and the per-tier
  cost rollup — no severity, profile, enforcement, or ship verdict.
- 🟢 [x] Emit deterministic **route.json** (per-change view). **(F020 —
  `FS.GG.Governance.RouteJson`, done 2026-06-20)** A pure, total
  `RouteJson.ofRouteResult : RouteResult -> string` renders the `RouteResult` into
  a versioned (`fsgg.route/v1`) document — selected gates, findings, and cost — via
  `System.Text.Json`, byte-identical for identical input, no new dependency.
- 🟢 [x] Emit deterministic **`.fsgg/gates.json`** (whole-catalog view). **(F021 —
  `FS.GG.Governance.GatesJson`, done 2026-06-20)** A pure, total
  `GatesJson.ofGateRegistry : GateRegistry -> string` renders the F018
  `GateRegistry` into a versioned (`fsgg.gates/v1`) document listing every declared
  gate with its carried metadata, prerequisites, and freshness-key inputs — the
  per-gate entry is exactly route.json's `selectedGates[*]` minus `selectingPaths`.
  Byte-identical for identical input; no new dependency.
- 🟢 [x] Add `fsgg route [--repo <dir>] [--paths ...] [--since <rev>] [--json]
  [--gates-out <path>] [--route-out <path>]` (the CLI host edge that persists
  route.json / gates.json to disk). **(F022 — `FS.GG.Governance.RouteCommand`, done
  2026-06-21)** The first composition/edge tier: a packable `fsgg` tool modeled
  through an Elmish/MVU boundary (pure `Loop` + edge `Interpreter` over injected,
  fakeable ports) that composes F014–F021 verbatim over a real repository —
  selects the changed-path scope, loads+validates the catalog, routes, builds the
  registry, computes findings, selects gates, projects `gates.json` (F021) +
  `route.json` (F020) **byte-for-byte unchanged**, writes both via temp+atomic
  rename, and prints a deterministic text/JSON summary. No new third-party
  dependency; computes **no** ship verdict. Every failure (not-a-repo, unresolved
  rev, missing/invalid catalog, unwritable output) → distinct diagnostic +
  category-mapped non-zero exit (2/3/4); the interpreter never throws.
- 🟢 [x] Add `fsgg ship --mode gate --profile standard --json` (the ship/merge verdict
  host edge — `audit.json`, blockers, profile-adjusted enforcement, exit-code basis;
  distinct from the `route` slice above). **(F026 — `FS.GG.Governance.ShipCommand`,
  done 2026-06-21)** The second composition/edge tier: the host sibling of F022,
  modeled through the same Elmish/MVU boundary (pure `Loop` + edge `Interpreter` over
  injected, fakeable ports). It selects the changed-path scope, loads+validates the
  catalog, routes, builds the registry, computes findings, and selects gates (the F022
  composition verbatim), then **rolls the selection up into a `ShipDecision`** via
  `Ship.rollup` (F024) under the chosen `--mode`/`--profile` levers (F023 recognizers,
  default `gate`/`standard`), **projects it to `audit.json`** via
  `AuditJson.ofShipDecision` (F025) byte-for-byte unchanged, writes it via temp+atomic
  rename, prints a deterministic text/JSON summary (the no-hide partition with base +
  effective severity), and **maps the verdict's `ExitCodeBasis` to a blocking process
  exit code** — `Clean → 0`, `Blocked → 1` (the single code reserved for a blocked
  merge). No new third-party dependency; references nine cores (not RouteJson/GatesJson).
  Every failure (not-a-repo, unresolved rev, missing/invalid catalog, unrecognized
  lever, unwritable output) → distinct diagnostic + category-mapped non-zero exit
  (2/3/4), each distinct from the blocked code 1; the interpreter never throws and
  writes no partial artifact.
- 🟡 [ ] Emit deterministic route and **audit** JSON with selected gates, matched
  rules, unmatched governed paths, expected artifacts, cost, cache eligibility,
  profile-adjusted enforcement, and exit-code basis. **(route.json done — F020;
  gates.json done — F021; audit.json done — F025; profile-adjusted enforcement done
  — F023; ship verdict + exit-code basis done — F024/F026; the freshness-key and
  evidence-reuse **cores** done — F029/F030; the per-gate **cache-eligibility roll-up
  core** done — F041 (`FS.GG.Governance.CacheEligibility` — pure, total `evaluate`
  attributing a `Reusable`/`MustRecompute` verdict per selected gate); and its
  deterministic, versioned **`cache-eligibility.json` projection** done — F042
  (`FS.GG.Governance.CacheEligibilityJson` — pure, total `ofReport :
  CacheEligibilityReport -> string` + `schemaVersion` "fsgg.cache-eligibility/v1",
  done 2026-06-22); and the per-gate **freshness-inputs resolution (join) core** done
  — F043 (`FS.GG.Governance.FreshnessResolution` — pure, total `resolve : Gate list ->
  SensedFacts -> FreshnessResolutionReport` joining each selected gate's carried
  five-field `FreshnessKey` identity, dropping `Cost`, with a supplied bundle of
  already-sensed repository facts into a complete F029 `FreshnessInputs` per gate — or
  a no-hide `Unresolved` naming every missing fact, recompute-safe by construction —
  whose `candidate` bridge feeds resolved gates straight into F041 `evaluate` without
  adaptation, done 2026-06-22). The evaluated cache-eligibility verdict now **exists**
  as a deterministic projection and each gate's `FreshnessInputs` can now be **resolved**
  from a supplied facts bundle; the one remaining piece is the **host wiring** — the CLI
  edge that actually *senses* each gate's facts from the real repo (git/filesystem),
  supplies them as `SensedFacts` to F043 `resolve`, runs F041 `evaluate` over the
  resolved candidates, and emits/embeds the verdict into the route/audit artifacts.)**
- 🟢 [x] Publish the first GitHub Actions guidance for branch protection. **(F027 —
  `docs/ci/github-actions-branch-protection.md` + a copyable workflow template wiring
  the F026 `fsgg ship` exit-code taxonomy into a GitHub protected branch, done
  2026-06-21. This closed Phase 2.)**

> **Legend:** 🟢 [x] done · 🟡 [ ] in progress · 🔴 [ ] not started · ⬜ optional. F014 (the `.fsgg`
> schemas → typed facts) is complete; the remaining Phase 2 rows (routing, git/CI
> facts, gate registry, `ship`) consume those facts and are held out of F014 scope
> by FR-016.

Exit criteria:

- A generated product can run a minimal ship gate before the full SDD lifecycle
  exists.
- Routine unclassified files do not trigger global default-deny behavior.
- Unknown paths under declared governed roots produce explicit findings.
- Route and audit JSON explain every selected protected-boundary gate.

### Phase 3: Normalized Work Model — 🟢 complete

Owner: `FS.GG.SDD`.

Purpose: turn authored lifecycle artifacts into the single machine-readable SDD
contract consumed by humans, agents, CI, and optional Governance tooling.

- 🟢 [x] Parse `.fsgg` and `work/<id>` authored sources into a `WorkModel`.
- 🟢 [x] Emit `readiness/<id>/work-model.json` with model version, source paths,
  source digests, schema versions, generator version, and diagnostics.
- 🟢 [x] Guarantee byte-stable JSON for identical source trees.
- 🟢 [x] Prefer structured graph data for execution when Markdown prose disagrees,
  keep prose visible, and emit a consistency diagnostic.
- 🟢 [x] Emit `requirementNotTyped` when a Markdown requirement id is missing from
  the normalized model.
- 🟢 [x] Emit `workModelInconsistent` when structured tasks reference unknown
  requirements or decisions.
- 🟢 [x] Report stale or missing generated work models.
- 🟢 [x] Document schema migration behavior and compatibility rules.

Status: complete on 2026-06-19. The implementation extends
`src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`; verification evidence is
recorded in `specs/002-normalized-work-model/readiness/`.

Exit criteria:

- Given the same source tree, work-model JSON is byte-stable.
- Diagnostics explain malformed or conflicting input without replacing user
  intent.
- Stale generated models are detectable from source digests and generator
  metadata.

### Phase 4: Native SDD Lifecycle Commands — 🟢 complete

Owner: `FS.GG.SDD`.

Purpose: expose the native spec-driven development stages as SDD commands over
the same model.

Status: in progress as of 2026-06-20. The implemented slices add
`src/FS.GG.SDD.Commands`, `src/FS.GG.SDD.Cli`, command tests, lifecycle-command
fixture roots, readiness evidence, deterministic init, charter, specify, and
clarify/checklist/plan/tasks/analyze/evidence command reports, and real
filesystem smoke paths for `fsgg-sdd init`, `fsgg-sdd charter`,
`fsgg-sdd specify`, and
`fsgg-sdd clarify`, `fsgg-sdd checklist`, `fsgg-sdd plan`, and
`fsgg-sdd tasks`, `fsgg-sdd analyze`, and `fsgg-sdd evidence`.

- 🟢 [x] Add `fsgg-sdd init` for SDD skeleton creation.
- 🟢 [x] Add `fsgg-sdd charter`.
- 🟢 [x] Add `fsgg-sdd specify`.
- 🟢 [x] Add `fsgg-sdd clarify`.
- 🟢 [x] Add `fsgg-sdd checklist`.
- 🟢 [x] Add `fsgg-sdd plan`.
- 🟢 [x] Add `fsgg-sdd tasks`.
- 🟢 [x] Add `fsgg-sdd analyze`.
- 🟢 [x] Add `fsgg-sdd evidence`.
- 🟢 [x] Keep stateful or I/O command behavior behind `Model`, `Msg`, `Effect`,
  `init`, and `update` boundaries for the implemented init, charter, specify,
  clarify, checklist, plan, tasks, analyze, and evidence slices.
- 🟢 [x] Ensure command output has deterministic JSON for automation and plain text
  for humans for the implemented init, charter, specify, clarify, checklist,
  plan, tasks, analyze, and evidence slices.
- 🟢 [x] Refresh generated SDD views when possible and report stale-view
  diagnostics when not for the implemented charter, specify, clarify, and
  checklist/plan/tasks/analyze/evidence work-model and analysis views.

Current verification evidence for the implemented slice is recorded in
`specs/003-native-sdd-lifecycle-commands/readiness/`: clean Release build,
focused command workflow/init/report/text/Governance-boundary tests, full suite
with 50 passing tests, FSI public-surface transcript, real init interpreter
transcript, and disposable-directory CLI smoke output. Charter verification
evidence is recorded in `specs/004-charter-command/readiness/`: clean Release
build, focused command workflow/charter/generated-view/report/text/
Governance-boundary tests, full suite with 70 passing tests, FSI
public-surface transcript, disposable-directory CLI smoke output, performance
evidence, SDD/Governance boundary review, and artifact traceability.
Specify verification evidence is recorded in
`specs/005-specify-command/readiness/`: clean Release build, focused specify
create/rerun/diagnostic tests, generated-view tests, deterministic
report/text/Governance-boundary tests, command workflow MVU tests, full suite
with 91 passing tests, FSI public-surface transcript, disposable-directory CLI
smoke output, performance evidence, SDD/Governance boundary review, human
summary review, and artifact traceability.
Clarify verification evidence is recorded in
`specs/006-clarify-command/readiness/`: clean Release build, focused clarify
create/rerun/diagnostic tests, generated-view tests, deterministic
report/text/Governance-boundary tests, command workflow MVU tests, full suite
with 114 passing tests, FSI public-surface transcript, disposable-directory CLI
smoke output, performance evidence, SDD/Governance boundary review, human
summary review, and artifact traceability.
Checklist verification evidence is recorded in
`specs/007-checklist-command/readiness/`: clean Release build, focused
checklist artifact and command create/rerun/diagnostic tests, generated-view
tests, deterministic report/text/Governance-boundary tests, command workflow
MVU tests, full suite with 140 passing tests, FSI public-surface transcript,
disposable-directory CLI smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Plan verification evidence is recorded in
`specs/008-plan-command/readiness/`: clean Release build, focused plan artifact
and command create/rerun/diagnostic tests, generated-view tests,
deterministic report/text/Governance-boundary tests, command workflow MVU
tests, full suite with 168 passing tests, FSI public-surface transcript,
disposable-directory CLI JSON/dry-run/text smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Tasks verification evidence is recorded in
`specs/009-tasks-command/readiness/`: clean Release build, focused task artifact
and command create/rerun/diagnostic tests, generated-view tests,
deterministic report/text/Governance-boundary tests, command workflow MVU
tests, full suite with 189 passing tests, FSI public-surface transcript,
disposable-directory CLI JSON/dry-run/text smoke output, performance evidence,
SDD/Governance boundary review, human summary review, and artifact
traceability.
Analyze verification evidence is recorded in
`specs/010-analyze-command/readiness/`: clean Release build, focused analysis
view and command tests, generated-view tests, deterministic report/text/
Governance-boundary tests, command workflow MVU tests, full suite, FSI
public-surface transcript, disposable-directory CLI JSON/dry-run/text smoke
output, performance evidence, SDD/Governance boundary review, human summary
review, and artifact traceability.
Evidence verification evidence is recorded in
`specs/011-evidence-command/readiness/`: clean Release build, focused evidence
artifact and command tests, output/boundary tests, full suite with 63 artifact
tests and 152 command tests, FSI public-surface transcript, disposable-directory
CLI JSON/dry-run/text smoke output, performance evidence, SDD/Governance
boundary review, human summary review, and artifact traceability.

Exit criteria:

- A user can create and advance a work item from charter through evidence
  without product source code.
- Commands write authored sources and refresh or diagnose generated views.
- JSON output is deterministic and plain text is presentation only.

### Phase 5: Route Parity, Profiles, And Enforcement Fixtures

Owner: `FS.GG.Governance`.

Purpose: make route selection, profile strictness, and blocking behavior
explainable and testable.

- 🟢 [x] Parse run modes: `sandbox`, `inner`, `focused`, `verify`, `gate`, and
  `release`. **(F023 — `Enforcement.recognizeMode`, done 2026-06-21)**
- 🟢 [x] Parse Governance profiles: `light`, `standard`, `strict`, and `release`.
  **(F023 — `Enforcement.recognizeProfile`, done 2026-06-21)**
- 🟢 [x] Parse rule maturity: `observe`, `warn`, `block-on-pr`, `block-on-ship`,
  and `block-on-release`. **(F014 `Config` typed facts; surfaced through F023.)**
- 🟢 [x] Emit every finding with rule id, verdict, base severity, mode, profile,
  maturity, effective severity, and reason. **(F023 `EnforcementDecision`'s six
  no-hide fields → F024 `ShipDecision` verdict → F025 `audit.json`, done 2026-06-21.)**
- 🟢 [x] Ensure profiles never hide underlying verdicts, alter rule hashes, or
  remove findings from JSON. **(F024/F025 no-hide rule: a base-blocking item relaxed
  by mode/profile appears in `warnings` carrying both base and effective severity.)**
- 🟢 [x] Add scoped `--paths` authoring and complete base/head route parity with
  CI. **(F022 `route` + F026 `ship` share the `--paths`/`--since`/default base-head
  scope surface.)**
- 🟢 [x] Generate golden enforcement truth-table fixtures covering routine versus
  fenced routes, base severity, rule tier, all modes, all profiles, all maturity
  levels, and unknown governed paths. **(F028 — golden enforcement truth-table
  fixtures over the enforcement dials, done 2026-06-21; builds on the F023
  (severity × maturity × mode × profile) cross-product. This closed Phase 5.)**
- 🟢 [x] Add representative JSON snapshots for combinations that alter blocking.
  **(F028 — golden `audit.json` snapshots over the blocking/relaxing combinations,
  alongside the F025 snapshot tests, done 2026-06-21.)**

Exit criteria:

- Local route previews and CI route decisions agree for the same inputs.
- Every enforcement dial has fixture coverage.
- Profile-adjusted blocking is explained without changing rule truth.

### Phase 6: Tasks, Evidence, Verify, And Ship Readiness — 🟢 SDD slice complete (Governance evidence inputs pending)

Owner: `FS.GG.SDD` for task/evidence declarations and SDD readiness;
`FS.GG.Governance` for effective evidence freshness and enforcement.

Purpose: make implementation work and merge readiness inspectable without
turning SDD into the Governance rule engine.

- 🟢 [x] Validate task graph structure, dependencies, ids, owners, required skills,
  required evidence, and status transitions.
- 🟢 [x] Check that required Claude/Codex skills or capability tags are available
  before agent-driven task execution.
- 🟢 [x] Derive required test/evidence obligations from lifecycle rules and changed
  artifact impact.
- 🟢 [x] Parse and normalize evidence declarations.
- 🟢 [x] Distinguish real evidence, accepted deferrals, missing evidence, and
  synthetic evidence disclosures.
- 🟢 [x] Add `fsgg-sdd evidence` or equivalent update command for authored
  declarations.
- 🟢 [x] Add `fsgg-sdd verify` to run selected SDD-owned checks and emit
  `readiness/<id>/verify.json`.
- 🟢 [x] Add `fsgg-sdd ship` to produce SDD merge-boundary readiness in
  `readiness/<id>/ship.json`.
- ⬜ [ ] Define Governance effective-evidence inputs for freshness, synthetic taint
  propagation, accepted deferrals, and stale evidence.
- ⬜ [ ] Keep protected-branch enforcement decisions in Governance.

Exit criteria:

- Work items can prove what tasks were completed and what evidence supports
  them.
- Verify and ship outputs are stable enough for CI, agents, and optional
  Governance consumers.
- Missing, stale, synthetic, and deferred evidence produces actionable
  diagnostics.
- Task readiness explains missing skills and missing tests before implementation
  or ship.

### Phase 7: Generated Views And Refresh — 🟢 SDD slice complete (Governance refresh/boundary pending)

Owner: Shared.

Purpose: make generated artifacts explicit, reproducible, and currency-checked.

- 🟢 [x] Define a generation manifest shape for source, generated view, renderer,
  generator version, source digest, output digest, and currency gate.
- 🟢 [x] Add an SDD refresh path for lifecycle views:
  `work-model.json`, `analysis.json`, `verify.json`, `ship.json`,
  `summary.md`, and `agent-commands/`.
- 🟡 [ ] Add Governance `fsgg refresh` for gate metadata, rule catalogs,
  capability docs, skill references, API-surface docs, route projections, and
  baselines.
- 🟢 [x] Emit stale-view diagnostics when generated views are older than their
  declared sources.
- 🟡 [ ] Block stale generated views at the configured Governance boundary.
- 🟢 [x] Add snapshot or golden-fixture coverage once a generated view becomes
  public or tool-facing.

Exit criteria:

- Generated files can be traced back to declared sources and generator versions.
- Stale views are detected by source and generator digests, not by presence.
- Markdown summaries are rendered from structured JSON.

### Phase 8: Agent Guidance Generation — 🟢 complete

Owner: `FS.GG.SDD`; Governance contributes optional rule/evidence contracts.

Purpose: keep human, Claude, Codex, and future-agent workflows on one lifecycle
contract.

- 🟢 [x] Generate Claude command and skill guidance from the normalized lifecycle
  model.
- 🟢 [x] Generate Codex skill guidance from the same lifecycle model.
- 🟢 [x] Mark generated agent files as generated and include source digests.
- 🟢 [x] Report stale generated agent guidance.
- 🟢 [x] Keep Claude and Codex behavior equivalent when workflow behavior changes.
- 🟢 [x] Ensure agent prompts may author Markdown but do not become a second source
  of truth.
- 🟢 [x] If agent guidance writes Markdown, refresh corresponding structured
  models or report stale-view diagnostics.

Exit criteria:

- Agent guidance is generated from structured SDD data.
- Stale guidance is detected when lifecycle contracts change.
- Agent instructions identify the same authored sources and generated views as
  the CLI.

### Phase 9: Bootstrap And Migration Experience — 🟢 SDD slice complete (runtime templates optional/out of scope)

Owner: `FS.GG.SDD`, with optional FS.GG.Rendering template providers and
Governance policy setup.

Purpose: make FS.GG.SDD useful for new products and existing Spec Kit projects.

- ⬜ [ ] Add project templates for a new SDD-governed product skeleton.
- 🟢 [x] Create `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`,
  and initial readiness directories.
- ⬜ [ ] Optionally call a template provider for runtime code while keeping runtime
  ownership outside SDD.
- 🟢 [x] Provide migration guidance from existing Spec Kit projects to native SDD
  artifacts.
- 🟢 [x] Preserve standard Spec Kit as a valid development workflow for the SDD repo
  itself.
- 🟢 [x] Add quickstart docs for `fsgg-sdd init` through `fsgg-sdd ship`.
- 🟢 [x] Add smoke tests that create a temporary SDD project and run the lifecycle
  without the Governance gate runtime installed.
- 🟢 [x] Document how Governance can add `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` after SDD initialization.

Exit criteria:

- A new project can initialize the SDD skeleton and continue through the native
  lifecycle.
- Existing Spec Kit users have a documented migration path.
- Bootstrap does not assume FS.GG.Rendering, Governance, or a monorepo checkout.

### Phase 10: Capability Catalog And Product Adapter Expansion

Owner: `FS.GG.Governance`, with product facts from FS.GG.Rendering and generated
products.

Purpose: expand beyond the MVP catalog into the product surfaces named by the
design.

- 🔴 [ ] Expand `.fsgg/capabilities.yml` for generated products, package surfaces,
  docs, skills, samples, design artifacts, release surfaces, baselines,
  template profiles, and evidence tags.
- 🔴 [ ] Add generated-product checks in cost tiers: structural scan,
  restore/build, focused tests, full verify, and release validation.
- 🔴 [ ] Ensure generated products can run Governance locally without monorepo
  access.
- 🔴 [ ] Add package/API facts for package projects, public `.fsi` contracts,
  baselines, compatibility notes, and FSI transcripts.
- 🔴 [ ] Add docs/examples facts for FsDocs pages, literate scripts, public API
  docs, links, and reference currency.
- 🔴 [ ] Add skill facts for skill ids, paths, references, capability mappings,
  task skill lists, and optional mirrors.
- 🔴 [ ] Add design/rendering facts for token sources, generated tokens, captures,
  contrast facts, control catalog, and interaction states.
- 🔴 [ ] Keep product vocabulary in adapters and capability catalogs, not in the
  Governance kernel or generic SDD code.

Exit criteria:

- Product surfaces can be routed and checked through declared capabilities.
- New package, docs, skills, generated-product, design, or release surfaces
  cannot be hidden under a governed root without classification.
- Generated-product checks scale by cost tier and explain broad routes.

### Phase 11: Cost, Cache, And Provenance

Owner: `FS.GG.Governance`; SDD supplies source and generated-view digests for
its lifecycle artifacts.

Purpose: keep local authoring cheap while making protected-boundary evidence
auditable.

- 🟢 [x] Define freshness keys over rule hash, artifact hash, command version,
  generator version, base/head, environment class, and output digest. **(F029 —
  `FS.GG.Governance.FreshnessKey`, done 2026-06-21)** A pure, total
  `FreshnessKey.compute : FreshnessInputs -> Key` over the closed typed input set,
  byte-stable in a tagged/length-prefixed/injective encoding; covered artifacts a
  set; no new dependency.
- 🟢 [x] Cache reusable evidence only when all freshness inputs match. **(F030 —
  `FS.GG.Governance.EvidenceReuse`, done 2026-06-21)** A pure, total reuse decision
  that reuses prior evidence iff every freshness input matches (the F029 key),
  otherwise reports the changed inputs; deterministic, no new dependency.
- 🟢 [x] Explain high-cost routes with matched rule, changed path, affected
  capability, selected gate, cost, and cheaper local alternative. **(F031 —
  `FS.GG.Governance.RouteExplain`, done 2026-06-21)** A pure, total broad-route
  cost-explanation core; deterministic, no new dependency.
- 🟢 [x] Record command runs with executable, arguments, working directory,
  environment delta, timeout, exit code, stdout digest, stderr digest, captured
  output path, and duration. **(F032 — `FS.GG.Governance.CommandRecord`, done
  2026-06-21)** A pure, total `CommandRecord.build : … -> CommandRecord` over the
  ten already-sensed run facts; the sensed duration held structurally apart from
  the nine reproducible facts; `canonicalId` renders only the reproducible facts to
  a byte-stable identity (arguments order-significant, each env-delta class a set,
  duration never read); no execution/hashing, no new dependency.
- 🟢 [x] Include source commit, base/head, rule hash, generator version, artifact
  digests, command records, environment class, and builder identity in
  provenance. **(F033 — `FS.GG.Governance.Provenance`, done 2026-06-21)** A pure,
  total `Provenance.build : … -> Provenance` assembles the nine already-sensed
  facts into one flat complete value carrying all eight declared facts (base and
  head are the two revisions of one base/head fact); `canonicalId : Provenance ->
  ProvenanceIdentity` renders only the reproducible facts to a byte-stable identity
  in the F029/F032 tagged/length-prefixed/injective discipline (artifact digests a
  set; command records ordered, each folded via F032 `CommandRecord.canonicalId` so
  the sensed durations — held apart inside the embedded records — are never read).
  The **first core to reference three sibling cores** (FreshnessKey + CommandRecord
  + Config — D1), reusing F029 `Revision`/`RuleHash`/`GeneratorVersion`/`ArtifactHash`,
  F032 `CommandRecord`, and F014 `EnvironmentClass` verbatim; no sensing/timing/
  hashing/persistence/JSON/attestation/CLI, no new dependency.
- 🟢 [x] Mark wall-clock timestamps and durations as sensed or non-deterministic
  metadata when included in deterministic reports. **(F034 —
  `FS.GG.Governance.SensedMetadata`, done 2026-06-21)** The structural foundation was
  already there — F032 carries the run duration as a distinct `SensedDuration` held
  apart from the reproducible facts and F033 keeps the embedded durations
  structurally excluded from the provenance identity — and F034 adds the missing
  **presentation half**: pure, total `SensedMetadata.markDuration` / `markTimestamp`
  mark an already-measured duration / timestamp (each with its label) as a
  `SensedMetadatum` whose `Value` is a closed `SensedValue` DU, so the type is the
  flag; `render` surfaces one metadatum behind a reserved `!sensed!` marker (a form
  no reproducible field tag ever produces) and `renderSection` groups a list into one
  separable `!sensed-section!`. Identity-neutral; reuses F032's `SensedDuration`
  verbatim (only new fact `SensedTimestamp`); no new dependency. **This closes
  Phase 11.**

Exit criteria:

- Expensive evidence is reused only when freshness is defensible.
- Route reports explain cost and cheaper local alternatives.
- Audit records are sufficient to explain builds, tests, packs, template
  instantiation, git diffs, package inspection, and visual capture.

### Phase 12: Agent-Reviewed Rule Guardrails — 🟢 complete (cache key, invalidation, prompt isolation, review record, advisory promotion, calibration)

Owner: `FS.GG.Governance`; SDD and generated products may provide artifacts
under review.

Purpose: allow judgement-heavy checks without treating uncalibrated agent output
as deterministic proof.

Status (2026-06-22): **all six rows are 🟢 complete** — six pure, total,
deterministic cores, each the analogue of a Phase-11 core specialised to
agent-reviewed verdicts, reusing the F029/F032/F035 length-prefixed, injective
encoding discipline: F035 `AgentReviewKey` (cache key), F036 `VerdictReuse`
(invalidation decision), F037 `PromptIsolation` (prompt isolation), F038
`ReviewRecord` (auditable review record), F039 `AdvisoryPromotion` (the
advisory-to-blocking promotion gate), and F040 `Calibration` (the judge-vs-human
calibration-evidence gate). **Phase 12 is closed.**

Legend: 🟢 complete · 🟡 partial (core landed; emission/wiring deferred) ·
🔴 not started.

- 🟢 [x] Cache agent-reviewed verdicts by model id, model version, reviewer prompt
  hash, model configuration, check hash, artifact hashes, and question text. **(F035
  — `FS.GG.Governance.AgentReviewKey`, done 2026-06-21)** A pure, total
  `compute`/`matches`/`diff`/`value` over the seven judge / prompt / check / artifact
  inputs (reviewed artifacts a set), byte-stable in the injective encoding, reusing
  F029 `RuleHash`/`ArtifactHash` verbatim; no new dependency.
- 🟢 [x] Invalidate cached verdicts when judge identity or prompt identity changes.
  **(F036 — `FS.GG.Governance.VerdictReuse`, done 2026-06-22)** A pure, total
  `lookup`/`record` over a cached-verdict store (the analogue of F030 `EvidenceReuse`):
  Valid iff an entry F035-`matches` the request on every input, else `Invalidated`
  with a located cause (`NoCachedVerdict` for different work, or `InputsChanged`
  naming the moved inputs); pure de-duplicating most-recent-wins insert; reuses F035
  `AgentReviewInputs`/`matches`/`diff` verbatim; no persistence, no new dependency.
- 🟢 [x] Separate governed artifact content from reviewer instructions and pass it
  as bounded data or digests. **(F037 — `FS.GG.Governance.PromptIsolation`, done
  2026-06-22)** A pure, total `assemble`/`render` (structural sibling of F035): keeps
  trusted reviewer instructions and untrusted governed-artifact content in separate
  typed channels, carries each artifact as a bounded excerpt (abstract `BoundedExcerpt`
  — no over-bound/unbounded form by construction) or a digest, and renders the two
  channels with an injective, unspoofable length-prefixed data fence so artifact
  content can never masquerade as an instruction; reuses F035 `QuestionText` and F029
  `ArtifactHash` verbatim; no model invoked, no bytes hashed, no new dependency.
- 🟢 [x] Record review requests, response digests, model identity, prompt identity,
  artifact digests, and final verdict. **(F038 — `FS.GG.Governance.ReviewRecord`, done
  2026-06-22)** A pure, total `build`/`canonicalId`/`identityValue` (the agent-review
  analogue of F032 `CommandRecord` / F033 `Provenance`): assembles one completed review
  as an immutable `ReviewRecord` carrying the six audit facts (F037 `ReviewRequest`, F035
  `ModelId`/`ModelVersion`/`ReviewerPromptHash`, F029 `ArtifactHash` digests, response
  digest, final verdict) plus F034 `SensedMetadatum` held structurally apart, and derives
  a byte-stable, injective `RecordIdentity` over the reproducible facts only (artifacts
  compared as a set; sensed metadata excluded — the F032/F033 honesty boundary); reuses
  F037/F035/F029/F034 vocabulary verbatim; no model invoked, no bytes hashed, no verdict
  interpreted, no new dependency.
- 🟢 [x] Keep agent-reviewed findings advisory until deterministic backing
  evidence, repeated-review confidence thresholds, or explicit human sign-off
  exists. **(F039 — `FS.GG.Governance.AdvisoryPromotion`, done 2026-06-22)** A pure,
  total, deterministic decision core (the agent-review analogue of F023
  `deriveEffectiveSeverity` / F030 `decide` / F036 `lookup`): `decide : PromotionFacts
  -> PromotionDecision` returns `EligibleToBlock` naming **every** satisfied basis (in
  the fixed order *DeterministicBackingEvidence, RepeatedReviewConfidence,
  HumanSignOff*) **iff** at least one of the only three permitted bases holds —
  deterministic backing evidence, a repeated-review confidence count clearing the
  threshold at the inclusive `c >= t && c >= 2` floor (a lone review never clears it),
  or an explicit human sign-off — and otherwise **defaults to advisory**
  (`StaysAdvisory` with a no-hide reason: `NoPermittedBasis`, or
  `ConfidenceBelowThreshold` carrying the count and threshold). The model's own
  self-confidence is not a basis; `EligibleToBlock` is necessary-not-sufficient (no
  blocking action, no calibration claim) and is non-empty by construction. Reuses F030
  `EvidenceRef` verbatim for the backing-evidence basis; no model invoked, no bytes
  hashed, no verdict produced/interpreted, no new dependency. Evidence:
  `specs/039-advisory-promotion-gate/` (33 green tests: advisory-default, all-named,
  the inclusive no-single-sample comparator, totality, determinism/purity,
  necessary-not-sufficient, non-empty eligibility, surface drift).
- 🟢 [x] Define judge-vs-human calibration evidence before any agent-reviewed rule
  can block protected boundaries. **(F040 — `FS.GG.Governance.Calibration`, done
  2026-06-22)** A pure, total, deterministic decision core (the agent-review analogue
  of F023 `deriveEffectiveSeverity` / F030 `decide` / F036 `lookup` / F039 `decide`):
  `decide : CalibrationThresholds -> CalibrationEvidence -> CalibrationDecision` returns
  `Calibrated` naming the satisfied `CalibrationMetrics` (the no-hide rule) **iff** the
  judge-vs-human comparison-sample count clears the **effective** minimum `max(MinimumSamples, 2)`
  (a lone sample never calibrates) **and** the observed agreement clears the threshold at
  the inclusive `>=` floor — and otherwise **defaults to uncalibrated** (`Uncalibrated`
  with a no-hide reason: `NoCalibrationEvidence`, `TooFewSamples`, or
  `AgreementBelowThreshold`). The model's own self-reported confidence is not an input
  (calibration is human comparison, never self-assessment); a `Calibrated` decision is
  necessary-not-sufficient (no blocking action, no severity, no enforcement verdict — it
  asserts only beyond-advisory maturity). Reuses F035 `ModelId`/`ModelVersion`/`ReviewerPromptHash`
  and F038 `RecordedVerdict` verbatim (the per-judge scope + opaque sample verdicts, never
  interpreted); no model invoked, no human consulted, no review run, no bytes hashed, no
  cache/verdict operation, no new dependency. Evidence: `specs/040-calibration-evidence-gate/`
  (27 green tests: uncalibrated-default, calibrated-with-named-metrics, the inclusive
  two-threshold no-single-sample comparator, totality, determinism/purity,
  necessary-not-sufficient/no-hide, surface drift). **This closes Phase 12.**

Exit criteria:

- Agent-reviewed outputs are auditable and prompt-isolated.
- Missing or stale reviews are visible findings.
- Protected-branch blocking does not depend on uncalibrated agent judgement.

### Phase 13: Release And Distribution Readiness — 🟢 SDD slice complete (Governance release gates pending)

Owner: `FS.GG.Governance` for release gates; `FS.GG.SDD` for SDD package and
CLI distribution once its lifecycle surface is stable.

Purpose: prepare SDD and Governance-managed products for versioned release.

- 🟢 [x] Add package identity and versioning policy for `FS.GG.SDD.*`.
- 🟢 [x] Add SDD release checklist and compatibility matrix for Spec Kit and
  Governance versions.
- 🟢 [x] Add CLI installation docs.
- 🟢 [x] Add generated artifact schema documentation.
- 🟢 [x] Add baseline fixtures for public schemas and command output.
- 🟢 [x] Add migration notes for breaking schema or command changes.
- 🟡 [ ] Define Governance `fsgg verify` and `fsgg release` schemas and exit
  codes.
- 🔴 [ ] Add release rules for version bumps, package metadata, template pins,
  publish plans, trusted publishing, and provenance.
- 🟢 [x] Add Spectre.Console projections backed by the same report objects used for
  JSON.
- 🟢 [x] Add scheduled exhaustive validation for broad matrices.

Exit criteria:

- SDD packages and CLI can be versioned and released with clear compatibility
  guarantees.
- Public schemas, generated views, and command JSON have documented stability
  rules.
- Breaking changes require explicit migration notes.
- Release gates support package, publish, and provenance evidence.

## First Features

The first SDD implementation feature is:

```text
001-sdd-artifact-model
```

It must not add lifecycle commands yet. It defines the artifact model, schema
versioning posture, id types, diagnostics, and deterministic JSON fixtures that
later commands and generators use.

The first Governance implementation slice from the design is:

```text
ship-walking-skeleton-and-catalog-mvp
```

It belongs in FS.GG.Governance and proves
`fsgg ship --mode gate --profile standard --json` with a minimal capability
catalog before the full lifecycle command suite is complete.

## Design Acceptance Trace

| Design acceptance item | Planned coverage |
|---|---|
| Start as a greenfield project through `fsgg-sdd init`. | SDD bootstrap and migration phase; optional template-provider delegation. |
| Spec-drive work through charter, specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship. | SDD artifact model, work model, lifecycle commands, task/evidence, verify, and ship phases. |
| Declare project policy, capabilities, work, and evidence in `.fsgg` and `work/<id>`. | SDD source model plus Governance policy, capability, and tooling schemas. |
| Produce deterministic SDD readiness without the Governance gate runtime installed. | SDD work model, lifecycle commands, verify, ship, generated views, and refresh phases. |
| Route a local scoped change cheaply and explain selected gates. | Optional Governance ship skeleton, route parity, cost/cache, and capability expansion phases. |
| Distinguish routine unclassified files from unknown governed paths. | Optional Governance catalog MVP and route parity phases. |
| Run `fsgg ship --mode gate --profile standard --json` as a protected boundary after SDD readiness exists. | Governance ship walking skeleton phase. |
| Refresh generated views from declared sources and detect drift. | Shared generated views and refresh phase. |
| Validate public package surfaces, docs, examples, skills, design artifacts, generated consumers, and release metadata. | Governance capability and product adapter expansion plus release readiness phases. |
| Cache fresh expensive evidence and rerun only when inputs change. | Governance cost, cache, and provenance phase. |
| Emit deterministic route, contract, explain, evidence, and audit JSON. | Governance ship skeleton, route parity, evidence, generated views, and provenance phases. |
| Render useful human CLI output without changing automation truth. | SDD lifecycle commands and Governance release/readiness presentation work. |
| Cover enforcement dials with truth-table fixtures and golden JSON snapshots. | Governance route parity, profiles, and enforcement fixtures phase. |
| Support release checks with package, publish, and provenance evidence. | Governance release and distribution readiness phase. |

## Risks And Mitigations

| Risk | Mitigation |
|---|---|
| The SDD repo accidentally takes ownership of the Governance rule engine. | Keep route, profile, freshness, and enforcement work explicitly assigned to FS.GG.Governance; SDD emits optional contracts only. |
| SDD becomes document ceremony instead of executable project control. | Make spec, plan, tasks, evidence, generated views, and ship readiness machine-checkable from one normalized work model. |
| Markdown and structured artifacts drift. | Prefer structured graph data for execution, keep prose visible, emit conflict diagnostics, and currency-check generated views. |
| Local development becomes oppressive. | Keep SDD usable without Governance; Governance enforces cost budgets, scoped routing, freshness caching, and advisory-first promotion. |
| Profiles hide failures. | Governance always emits underlying verdict, base severity, effective severity, mode, profile, maturity, and reason. |
| Generated views pass because files exist. | Key generated views by source digest, generator version, and output digest. |
| Agent-reviewed checks become uncalibrated blockers. | Keep them advisory until prompt isolation, cache keys, confidence thresholds, and calibration are implemented. |
| Product vocabulary leaks into generic SDD code. | Keep product facts in Governance adapters, Rendering providers, generated-product capability catalogs, and optional SDD contracts. |
| Release/provenance claims overreach. | Emit compatible metadata first; claim formal compliance only after explicit verification. |

## Acceptance Bar

The SDD consumer product is implemented when a consumer can:

1. Start as a greenfield project through `fsgg-sdd init` or an approved FS.GG
   umbrella command that delegates to it.
2. Spec-drive work through charter, specify, clarify, checklist, plan, tasks,
   analyze, implement, verify, and ship.
3. Declare lifecycle policy, work, and evidence in `.fsgg` and `work/<id>`.
4. Produce a deterministic normalized work model.
5. Generate Claude and Codex guidance from the same contract.
6. Run lifecycle commands without the Governance gate runtime installed.
7. Refresh generated views from declared sources and detect drift.
8. Emit deterministic `analysis.json`, `verify.json`, `ship.json`, and
   `summary.md`.
9. Record task and evidence state in structured artifacts.
10. Evolve schemas with explicit migration notes.

The optional Governance integration is implemented when a generated product can:

1. Add Governance policy, capability, and tooling files after SDD initialization.
2. Route a local scoped change cheaply and explain selected gates.
3. Distinguish routine unclassified files from unknown governed paths and
   explain why either does or does not block.
4. Run `fsgg ship --mode gate --profile standard --json` as a protected
   boundary.
5. Validate public package surfaces, docs, examples, skills, design artifacts,
   generated consumers, and release metadata through adapters.
6. Cache fresh expensive evidence and rerun only when relevant inputs change.
7. Emit deterministic route, contract, explain, evidence, and audit JSON.
8. Render useful human CLI output without changing automation truth.
9. Cover enforcement dials with truth-table fixtures and golden JSON snapshots.
10. Support release checks with package, publish, and provenance evidence.

FS.GG.SDD is complete enough for its own first release when it can:

1. Initialize an SDD skeleton.
2. Author lifecycle artifacts in Markdown and structured files.
3. Produce a deterministic normalized work model.
4. Generate Claude and Codex guidance from the same contract.
5. Run lifecycle commands without the Governance gate runtime installed.
6. Optionally expose readiness artifacts that Governance can inspect.
7. Detect stale generated views.
8. Record task and evidence state in structured artifacts.
9. Produce verify and ship readiness JSON.
10. Evolve schemas with explicit migration notes.

The central constraint is unchanged: useful to consumers before Governance is
installed, strict at protected boundaries when Governance is adopted, cheap in
the authoring loop, and explainable everywhere, while SDD remains the lifecycle
product and Governance remains the rule and gate product.
