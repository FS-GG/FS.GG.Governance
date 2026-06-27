# Data Model: Publish a Populated Reference `.fsgg` Gate Set

**Feature**: `079-reference-gate-set` | **Date**: 2026-06-27

This feature authors **data** (the reference `.fsgg`), not new F# types. The entities
below are (a) the concrete YAML the reference declares, and (b) the **existing** typed
facts / gate / route / enforcement values it flows through, reused verbatim.

## A. Authored artifact — the reference `.fsgg`

Published at `samples/sdd-reference-gate-set/.fsgg/`. Field names, enums, and required
vs optional status follow `src/FS.GG.Governance.Config/Model.fsi` and `Schema.fs`.

### `.fsgg/project.yml` (schemaVersion 1, required file)

```yaml
schemaVersion: 1
id: sdd-reference-gate-set
governedRoot: .
domains:
  - build
  - test
  - evidence
packageSurfaces:
  - src
policyRef: .fsgg/policy.yml
capabilitiesRef: .fsgg/capabilities.yml
```

### `.fsgg/capabilities.yml` (schemaVersion 2, required file)

```yaml
schemaVersion: 2
domains:
  - build
  - test
  - evidence
pathMap:
  - glob: "src/**"
    capability: build
  - glob: "*.sln"
    capability: build
  - glob: "tests/**"
    capability: test
  - glob: "build.fsx"
    capability: evidence
surfaces:
  - id: public-api
    kind: package
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship
checks:
  - id: build
    domain: build
    command: dotnet-build
    owner: platform
    cost: medium
    environment: local-or-ci
    maturity: block-on-ship
    tier: restoreBuild
  - id: test
    domain: test
    command: dotnet-test
    owner: platform
    cost: high
    environment: ci
    maturity: block-on-ship
    tier: focusedTests
  - id: evidence
    domain: evidence
    command: build-evidence
    owner: platform
    cost: medium
    environment: local-or-ci
    maturity: warn
```

### `.fsgg/policy.yml` (schemaVersion 1, optional file — present)

```yaml
schemaVersion: 1
defaultProfile: light
profiles:
  - light
  - standard
  - strict
  - release
branchPolicy:
  pattern: "main"
  requirePr: true
reviewBudget:
  maxReviews: 3
```

### `.fsgg/tooling.yml` (schemaVersion 1, optional file — present)

```yaml
schemaVersion: 1
commands:
  - id: dotnet-build
    command: "dotnet build <App>.sln"
    timeout: 600
    environment: local-or-ci
  - id: dotnet-test
    command: "dotnet test <App>.sln"
    timeout: 900
    environment: ci
  - id: build-evidence
    command: "dotnet fsi build.fsx -- evidence"
    timeout: 600
    environment: local-or-ci
environmentClasses:
  - local
  - ci
externalTools:
  - tool: dotnet
    minVersion: "10.0.0"
```

> The `<App>` placeholder mirrors the SDD reference skeleton's app-name substitution; an
> adopter copies the directory and renames. Concrete strings are finalized in `tasks.md`.

## B. Reused typed values (no new types authored)

| Stage | Module / function | Value | Reference invariant |
|-------|-------------------|-------|---------------------|
| Load + validate | `Config.Loader.loadAndValidate` → `Config.Schema.validate` | `Validation = Valid TypedFacts \| Invalid Diagnostic list` | MUST be `Valid` with **empty** `Diagnostic list` (FR-007/SC-002) |
| Registry | `Gates.buildRegistry: TypedFacts -> GateRegistry` | one `Gate` per `Check`, `GateId = "<domain>:<checkId>"` | exactly 3 gates: `build:build`, `test:test`, `evidence:evidence`; each carries `Domain`/`Cost`/`Owner`/`Maturity` + `RequiresCommand` prerequisite (SC-001/SC-004) |
| Route | `Routing.route: TypedFacts -> GovernedPath list -> RouteReport` | per-path `Routed(domain, glob, reason) \| UnmatchedInRoot \| OutOfScope` | candidate paths route to `build`/`test`/`evidence` domains |
| Select | `Route.select: GateRegistry -> RouteReport -> FindingReport -> RouteResult` | gates selected by domain equality, deduped by `GateId` | build/test/evidence each selected by ≥1 candidate path (FR-008/SC-004) |
| Enforce | `Enforcement.deriveEffectiveSeverity: EnforcementInput -> EnforcementDecision` | `EffectiveSeverity = Advisory \| Blocking` | see severity matrix below (SC-003/SC-006) |

### Effective-severity matrix (BaseSeverity = `Blocking`, the failing-change case)

| check (maturity)        | RunMode  | Light                | Strict               |
|-------------------------|----------|----------------------|----------------------|
| build (block-on-ship)   | Verify(3)| Advisory (floor 4)   | **Blocking** (floor 3) |
| test  (block-on-ship)   | Verify(3)| Advisory (floor 4)   | **Blocking** (floor 3) |
| evidence (warn)         | Verify(3)| Advisory (always)    | Advisory (always)    |

Default-profile (`Light`) column ⇒ **0 blocking** (SC-003); `Strict` column ⇒ **≥1
blocking** on the same change (SC-006). See research D5 for the floor/tighten derivation.

## C. Validation / invariant rules (frozen by the FR-010 guard)

1. Loads `Valid` with 0 diagnostics, 0 unknown-field findings (FR-007/SC-002).
2. Check list non-empty and contains `build` + `test` + `evidence` (FR-002/FR-003/SC-001).
3. Every check's `command` is declared in `tooling.yml` — 0 dangling command refs
   (FR-004/SC-001). [The loader already rejects dangling refs as `DanglingReference`;
   the guard asserts the artifact never trips it.]
4. Every check's `domain` is declared and reachable by ≥1 path-map glob — 0 orphan
   checks / unreachable domains (FR-005/SC-004).
5. `policy.defaultProfile == light` (FR-006/SC-007).
6. Under `Light` at `RunMode.Verify`, every selected gate derives `Advisory`
   (FR-006/SC-003); under `Strict`, ≥1 derives `Blocking` (SC-006).
