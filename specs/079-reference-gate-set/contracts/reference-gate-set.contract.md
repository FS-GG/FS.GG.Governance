# Contract: The Published Reference `.fsgg` Gate Set

**Feature**: `079-reference-gate-set`

The "interface" this feature exposes is a **data artifact** consumed by (a) the existing
governance configuration/routing/enforcement pipeline and (b) the downstream P4 Templates
overlay. This contract pins what that artifact MUST satisfy. It is enforced by the
regression guard (see `regression-guard.contract.md`).

## Location & shape

- **Path**: `samples/sdd-reference-gate-set/.fsgg/` (directory).
- **Files**: `project.yml`, `capabilities.yml`, `policy.yml`, `tooling.yml` (all four
  present).
- **Schema versions**: project 1, capabilities 2, policy 1, tooling 1.
- Concrete field-level content: see `../data-model.md` §A.

## Load contract (FR-007, SC-002)

`Config.Loader.loadAndValidate "samples/sdd-reference-gate-set"` MUST return
`Validation.Valid facts` with an **empty** diagnostics list:
- 0 validation errors (no `MissingRequiredFile`, `EmptyFile`, `MissingRequiredField`,
  `MalformedValue`, `DuplicateId`, schema-version, `PathEscapesRoot`, `DanglingReference`).
- 0 unknown/unrecognized-config findings (no `UnknownField`).

## Registry contract (FR-002, FR-003, FR-004, SC-001)

`Gates.buildRegistry facts` MUST yield exactly **3** gates:

| GateId             | Domain   | Command prerequisite          | Maturity      |
|--------------------|----------|-------------------------------|---------------|
| `build:build`      | build    | `RequiresCommand dotnet-build`| block-on-ship |
| `test:test`        | test     | `RequiresCommand dotnet-test` | block-on-ship |
| `evidence:evidence`| evidence | `RequiresCommand build-evidence`| warn        |

- The check list is non-empty and includes a build check, a test check, and an
  evidence-integrity check.
- Every command prerequisite names a command declared in `tooling.yml` (0 dangling).

## Routing contract (FR-005, FR-008, SC-004)

For each declared check, ≥1 governed candidate path routes to its domain and selects its
gate:

| candidate path              | routed domain | selects gate        |
|-----------------------------|---------------|---------------------|
| `src/App/Program.fs`        | build         | `build:build`       |
| `App.sln`                   | build         | `build:build`       |
| `tests/App.Tests/Tests.fs`  | test          | `test:test`         |
| `build.fsx`                 | evidence      | `evidence:evidence` |

(`src/**` and `*.sln` both route to `build`; including a `.sln` candidate exercises the
`*.sln` glob the `src/**` path alone leaves untested.)

- 0 orphan checks (every check's domain is declared and path-reachable).
- 0 orphan commands (every declared command is referenced by a check — no dead tooling).
- 0 unreachable domains.

## Enforcement contract (FR-006, SC-003, SC-006)

With `BaseSeverity = Blocking` (a change that fails the declared check) at
`RunMode.Verify`:
- **`Profile.Light`** (the declared default): every selected gate ⇒
  `EffectiveSeverity = Advisory`. 0 blocking outcomes.
- **`Profile.Strict`**: ≥1 selected gate ⇒ `EffectiveSeverity = Blocking` on the same
  change.

`policy.defaultProfile` MUST be `light`.

## Downstream-reuse contract (FR-009, SC-005)

The directory MUST be copyable **unedited** into the P4 `fs-gg-governance` overlay and
load + route there with 0 edits required. No absolute paths, no repo-internal references,
no host-specific content in the four YAML files. (The `<App>` placeholder is the only
adopter substitution and does not block load/route.)
