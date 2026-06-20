# Quickstart: `.fsgg` Project, Policy, Capability, and Tooling Schemas

This is a runnable validation guide for the F014 schemas. It proves the feature end-to-end:
a valid product validates to typed facts, a malformed product validates to a located
diagnostic, and the result is deterministic. Schema details live in
[`contracts/fsgg-schema.md`](./contracts/fsgg-schema.md); typed shapes in
[`contracts/Model.fsi`](./contracts/Model.fsi).

## Prerequisites

- .NET `net10.0` SDK (per `Directory.Build.props`).
- Built solution: `dotnet build FS.GG.Governance.sln`.

## Build & test

```bash
# Build the new library and run its semantic tests
dotnet build src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj
dotnet test  tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj
```

Expected: all tests pass, including determinism, order-independence, every diagnostic id,
every MVP surface class, the loader edge over real directories, and the surface-drift check.

## Scenario 1 — A valid product validates to typed facts (US1)

A fixture product `tests/.../fixtures/valid-complete/` contains all four `.fsgg` files
(see the schema contract for their content). Through the built library:

```fsharp
open FS.GG.Governance.Config

match Loader.loadAndValidate "tests/.../fixtures/valid-complete" with
| Model.Valid facts ->
    // ProjectId, sorted domains, normalized governed root,
    // classified surfaces, checks with owner/cost/environment/maturity,
    // default profile, command allow-list, environment classes, timeouts.
    printfn "valid: %A" facts.Project.Id
| Model.Invalid diags ->
    failwithf "expected valid, got %A" diags
```

**Pass when**: the result is `Valid`, the typed facts expose project identity, domains, path
map, classified surfaces, checks (with per-entry metadata), default profile, command
allow-list, environment classes, and timeouts — and contain no raw YAML text or undeclared
product vocabulary (FR-010, SC-005).

## Scenario 2 — Determinism (US1 scenario 2, SC-002)

```fsharp
let a = Loader.loadAndValidate "tests/.../fixtures/valid-complete"
let b = Loader.loadAndValidate "tests/.../fixtures/valid-complete"
// a = b  (structural equality of the whole Validation)
```

A companion fixture re-orders the domains/surfaces/checks/commands in the source files; its
typed facts MUST equal the original's (order-independence is also property-tested with
FsCheck permutations).

**Pass when**: repeated validation is structurally identical, and re-ordered authored input
yields identical typed facts.

## Scenario 3 — Malformed product → located diagnostic (US2)

Each malformed fixture carries exactly one defect. Examples:

| Fixture | Expected `DiagnosticId` |
|---|---|
| two capabilities sharing an id | `DuplicateId` |
| a field the schema does not define | `UnknownField` |
| `schemaVersion` missing / non-int / `2` | `MissingSchemaVersion` / `MalformedSchemaVersion` / `UnsupportedSchemaVersion` |
| a surface path containing `..` that escapes the root | `PathEscapesRoot` |
| a check referencing an undeclared domain | `DanglingReference` |
| empty `project.yml` | `EmptyFile` |
| absent `capabilities.yml` | `MissingRequiredFile` |

```fsharp
match Loader.loadAndValidate "tests/.../fixtures/malformed-duplicate-id" with
| Model.Invalid diags ->
    // diags carries DuplicateId with File + Locator + fix-hint Message; NO typed facts.
    diags |> List.map (fun d -> Model.diagnosticIdToken d.Id)
| Model.Valid _ -> failwith "expected invalid"
```

**Pass when**: each malformed fixture returns `Invalid` with its expected diagnostic id, a
file, a locator, and a human-readable message — and emits no typed facts for the rejected
declaration (FR-006, FR-013, SC-003).

## Scenario 4 — MVP surface classes (US3, SC-004)

Fixtures `surface-routine/`, `surface-governed-root/`, `surface-protected/`,
`surface-generated-view/`, `surface-release/` each declare one MVP surface class.

**Pass when**: each is `Valid` and its surface classifies into the matching `SurfaceClass`;
a fixture whose tree has only routine, undeclared files produces no protected-surface or
governed-root fact (US3 scenario 3).

## Scenario 5 — Absent vs invalid (FR-015)

- `valid-no-policy/` omits the optional `policy.yml` → `Valid` with `Policy = None`.
- `malformed-policy-present/` has a present-but-invalid `policy.yml` → `Invalid` (a present
  optional file is fully validated).

**Pass when**: an absent optional file is accepted (its facts `None`); a present-but-invalid
optional file fails — the two are never conflated.

## What this guide intentionally omits

No routing, git/CI facts, gate registry, profile enforcement, or `ship` command — those are
later Phase-2 features that consume these typed facts (FR-016). This guide validates only the
declarations and the typed facts.
