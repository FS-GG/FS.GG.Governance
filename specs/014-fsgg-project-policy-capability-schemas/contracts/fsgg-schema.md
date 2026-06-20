# `.fsgg` YAML Schema Contract (schemaVersion 1)

This is the **authoring** contract for the four `.fsgg` files. The **machine** contract is
the typed facts in [`Model.fsi`](./Model.fsi); this document defines the YAML a product
author writes, and exactly what the strict validator accepts and rejects.

General rules (apply to every file):

- Every file MUST declare `schemaVersion: 1`. Missing → `missingSchemaVersion`; non-integer
  → `malformedSchemaVersion`; any integer other than `1` (greater *or* lesser) →
  `unsupportedSchemaVersion` (`> 1` is "upgrade the tool"; `< 1` has no historical version and
  is not silently accepted).
- **Unknown fields are rejected** (`unknownField`). The validator does not ignore extras.
- **Empty / whitespace-only present file** → `emptyFile`.
- Required-where-stated fields missing → `missingRequiredField`.
- Ids that must be unique and are repeated → `duplicateId`.
- Closed enums (`cost`, `environment`, `maturity`, surface `kind`) with an out-of-set value
  → `malformedValue`.
- Paths are normalized (separators unified, `./` dropped, `..` resolved) and MUST stay within
  the governed root; escaping it → `pathEscapesRoot`.
- Re-ordering list entries MUST NOT change the typed facts (validator sorts by id/path).

Required files: **`project.yml`**, **`capabilities.yml`**. Optional (fully validated when
present): **`policy.yml`**, **`tooling.yml`**.

---

## `.fsgg/project.yml` (required)

```yaml
schemaVersion: 1
id: my-product                 # required, non-empty  -> ProjectId
governedRoot: .                # required, path        -> GovernedRoot (the escape anchor)
domains:                       # list of domain ids, unique within file
  - workflow
  - package-api
packageSurfaces:               # optional list of paths
  - src
policyRef: .fsgg/policy.yml          # optional pointer
capabilitiesRef: .fsgg/capabilities.yml  # optional pointer
```

| Field | Required | Maps to |
|---|---|---|
| `schemaVersion` | yes | `ProjectFacts.SchemaVersion` |
| `id` | yes | `ProjectFacts.Id` |
| `governedRoot` | yes | `ProjectFacts.GovernedRoot` |
| `domains` | yes (may be empty list) | `ProjectFacts.Domains` |
| `packageSurfaces` | no | `ProjectFacts.PackageSurfaces` |
| `policyRef` / `capabilitiesRef` | no | `ProjectFacts.PolicyRef` / `CapabilitiesRef` |

---

## `.fsgg/policy.yml` (optional)

```yaml
schemaVersion: 1
defaultProfile: standard       # MUST be a member of `profiles` -> else danglingReference
profiles:                      # list of profile ids, unique
  - light
  - standard
  - strict
branchPolicy:                  # optional placeholder, declared not enforced
  pattern: "main"
  requirePr: true
reviewBudget:                  # optional placeholder, declared not enforced
  maxReviews: 3
```

> Profiles are parsed and validated but NOT turned into effective enforcement — that is a
> later phase. `branchPolicy` / `reviewBudget` are accepted placeholders only.

---

## `.fsgg/capabilities.yml` (required)

```yaml
schemaVersion: 1
domains:                       # unique ids; reference target for pathMap & checks.
  - workflow                   # a capability *is* its domain id (no separate capability id);
  - package-api                # a repeated domain id -> duplicateId (spec US2 AS1).
pathMap:                       # glob -> capability domain
  - glob: "src/**"
    capability: package-api    # MUST be a declared domain -> else danglingReference
  - glob: "work/**"
    capability: workflow
surfaces:                      # classified regions of the tree
  - id: public-api
    kind: protected            # one of: routine | governedRoot | protected | generatedView | release
    paths: ["src/**/*.fsi"]
    owner: platform
    maturity: block-on-ship    # observe | warn | block-on-pr | block-on-ship | block-on-release
checks:                        # verifications associated with a capability domain
  - id: build
    domain: package-api        # MUST be a declared domain -> else danglingReference
    command: dotnet-build      # optional; if present MUST name a tooling.yml command (cross-file)
    owner: platform
    cost: medium               # cheap | medium | high | exhaustive
    environment: local-or-ci   # local | ci | local-or-ci | release
    maturity: block-on-ship
```

`kind` → `SurfaceClass`: `routine`→`Routine`, `governedRoot`→`GovernedRoot`,
`protected`→`ProtectedSurface`, `generatedView`→`GeneratedView`, `release`→`ReleaseSurface`.

Per-entry `owner`/`cost`/`environment`/`maturity` on checks (and `owner`/`maturity` on
surfaces) are required where shown (FR-004).

---

## `.fsgg/tooling.yml` (optional)

```yaml
schemaVersion: 1
commands:                      # the allow-list; id is the cross-file reference target
  - id: dotnet-build
    command: "dotnet build"
    timeout: 600               # seconds -> TimeoutLimit
    environment: local-or-ci
environmentClasses:            # declared classes
  - local
  - ci
externalTools:                 # tool/version expectations
  - tool: dotnet
    minVersion: "10.0.0"
```

---

## Diagnostic id reference

| Id | Raised when |
|---|---|
| `missingRequiredFile` | a required file (`project.yml` / `capabilities.yml`) is absent |
| `emptyFile` | a present file is empty / whitespace-only |
| `missingSchemaVersion` | `schemaVersion` absent |
| `malformedSchemaVersion` | `schemaVersion` not an integer |
| `unsupportedSchemaVersion` | `schemaVersion` is an integer other than the supported version (greater or lesser) |
| `unknownField` | a field the schema does not define |
| `missingRequiredField` | a required field absent |
| `malformedValue` | a scalar/enum value outside its allowed set/shape |
| `duplicateId` | a uniqueness-required id repeated (within or, for capability ids, across files) |
| `pathEscapesRoot` | a normalized path resolves outside the governed root |
| `danglingReference` | a reference (capability/domain/command/default-profile) names something not declared |
