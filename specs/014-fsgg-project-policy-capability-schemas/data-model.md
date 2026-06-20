# Phase 1 Data Model: `.fsgg` Project, Policy, Capability, and Tooling Schemas

This is the typed model the feature produces. It is the machine contract (FR-010): the
YAML files are the *authoring* format; these typed values are the *truth* handed to later
Governance features. Nothing here carries raw YAML text or product-specific vocabulary
(SC-005). All collection fields are emitted in deterministic, id/path-sorted order (D8).

The concrete F# signatures live in [`contracts/Model.fsi`](./contracts/Model.fsi); this
document is the conceptual map.

## Input-side values (edge)

### RawSource
The unparsed-but-located input to the pure core: the four file slots, each either *absent*
or *present with content*. Produced by the `Loader` edge; consumed by `Schema.validate`.

| Field | Type | Notes |
|---|---|---|
| `Root` | normalized dir ref | the `.fsgg` parent (the governed-root anchor) |
| `Project` | `FileSlot` | required |
| `Policy` | `FileSlot` | optional |
| `Capabilities` | `FileSlot` | required |
| `Tooling` | `FileSlot` | optional |

`FileSlot` = `Absent` | `Present of content: string`. The required/optional distinction
(D4) is applied by `validate`, not by the slot type — so an absent *optional* file is fine
and an absent *required* file yields `missingRequiredFile`, while an empty *present* file
yields `emptyFile` (spec edge cases).

## Output-side values (typed facts)

### Validation (top-level result)
The single result of `validate`. Either every required file parsed and validated, yielding
typed facts, or it did not, yielding only ordered diagnostics. Never both partial facts and
silent success (FR-006).

| Case | Carries |
|---|---|
| `Valid of TypedFacts` | the typed facts below |
| `Invalid of Diagnostic list` | one or more diagnostics, deterministically ordered, no facts |

> A `Valid` result MAY still describe optional files that were absent (their facts are
> `None`); it never describes a *present-but-invalid* file — any invalid present file makes
> the whole result `Invalid` (FR-006/FR-015).

### TypedFacts
The product-neutral aggregate handed downstream.

| Field | Type | Source file | Notes |
|---|---|---|---|
| `Project` | `ProjectFacts` | project.yml | required |
| `Policy` | `PolicyFacts option` | policy.yml | `None` when the optional file is absent |
| `Capabilities` | `CapabilityFacts` | capabilities.yml | required |
| `Tooling` | `ToolingFacts option` | tooling.yml | `None` when the optional file is absent |

### SchemaVersion
The explicit, validated version stamp present on every file (FR-001, FR-007). A single
supported version (`1`) for this MVP; `validate` rejects missing, malformed, and any integer
other than `1` — too-new ("upgrade the tool") and too-old alike map to
`unsupportedSchemaVersion`, since silently accepting an unknown version would be partial
acceptance (FR-006). (D7: `missing/malformed/unsupportedSchemaVersion`.)

### ProjectFacts (from `project.yml`)
| Field | Type | Validation |
|---|---|---|
| `SchemaVersion` | `SchemaVersion` | FR-007 |
| `Id` | `ProjectId` (non-empty string) | required, FR-002 |
| `Domains` | `DomainId list` | sorted; ids unique within file |
| `GovernedRoot` | `GovernedPath` | normalized, the path-escape anchor (D5) |
| `PackageSurfaces` | `GovernedPath list` | normalized, sorted |
| `PolicyRef` / `CapabilitiesRef` | `GovernedPath option` | pointers to the policy/capability declarations |

### PolicyFacts (from `policy.yml`, optional)
| Field | Type | Validation |
|---|---|---|
| `SchemaVersion` | `SchemaVersion` | FR-007 |
| `Profiles` | `ProfileId list` | sorted; unique; FR-003 |
| `DefaultProfile` | `ProfileId` | MUST be a member of `Profiles` (cross-ref, FR-009) |
| `BranchPolicy` | `BranchPolicyDecl option` | declared-but-not-enforced placeholder (FR-003) |
| `ReviewBudget` | `ReviewBudgetDecl option` | declared-but-not-enforced placeholder (FR-003) |

> Profiles are parsed and validated but **not** turned into effective enforcement — that is
> Phase 5 (spec "Profiles are declared, not enforced").

### CapabilityFacts (from `capabilities.yml`)
| Field | Type | Validation |
|---|---|---|
| `SchemaVersion` | `SchemaVersion` | FR-007 |
| `Domains` | `DomainId list` | sorted; unique; the reference target set for checks |
| `PathMap` | `PathMapEntry list` | sorted by normalized glob; each `capability` references a declared domain (FR-009) |
| `Surfaces` | `Surface list` | sorted by id; ids unique |
| `Checks` | `Check list` | sorted by id; ids unique; each references declared domain/command (FR-009) |

#### Capability identity & per-entry metadata
In this MVP a *capability* is identified by its **domain id** — there is no separate
`CapabilityId` type. The spec's "two capabilities sharing an id" (US2 AS1) is two
`capabilities.yml` `domains` entries with the same id, which yields `duplicateId`. The
per-entry `owner`/`cost`/`environment`/`maturity` that FR-004 attaches to "capabilities" are
carried by the `Check` (and `owner`/`maturity` by the `Surface`) that the domain governs,
not by a standalone capability record:

| Attribute | Type | Domain |
|---|---|---|
| `Owner` | `Owner` (string) | who owns a failure |
| `Cost` | `Cost` = `Cheap` \| `Medium` \| `High` \| `Exhaustive` | closed DU |
| `Environment` | `EnvironmentClass` = `Local` \| `Ci` \| `LocalOrCi` \| `Release` | closed DU |
| `Maturity` | `Maturity` = `Observe` \| `Warn` \| `BlockOnPr` \| `BlockOnShip` \| `BlockOnRelease` | closed DU |

#### PathMapEntry
| Field | Type | Notes |
|---|---|---|
| `Glob` | `GovernedPath` (glob, normalized) | normalized separators; must stay within governed root (D5) |
| `Capability` | `DomainId` | dangling-checked against `Domains` (FR-009) |

#### Surface (classified — FR-011, D6)
| Field | Type | Notes |
|---|---|---|
| `Id` | `SurfaceId` | unique |
| `Class` | `SurfaceClass` | the typed classification (below) |
| `Paths` | `GovernedPath list` | normalized, sorted, within governed root |
| `Owner` / `Maturity` | as above | preserved per FR-011 / US3 scenario 2 |

`SurfaceClass` = `Routine` | `GovernedRoot` | `ProtectedSurface` | `GeneratedView` |
`ReleaseSurface`. `Routine` is never *emitted* for undeclared files (they produce no surface
fact at all, US3 scenario 3); it exists so an explicitly-declared routine/unmanaged region
can be named.

#### Check
| Field | Type | Notes |
|---|---|---|
| `Id` | `CheckId` | unique |
| `Domain` | `DomainId` | dangling-checked against `Domains` (FR-009, US2 scenario 5) |
| `Command` | `CommandId option` | when present, dangling-checked against `ToolingFacts.Commands` (FR-009 cross-file) |
| `Owner` / `Cost` / `Environment` / `Maturity` | as above | FR-004 |

### ToolingFacts (from `tooling.yml`, optional)
| Field | Type | Validation |
|---|---|---|
| `SchemaVersion` | `SchemaVersion` | FR-007 |
| `Commands` | `CommandSpec list` | sorted by id; ids unique; the allow-list (FR-005) |
| `EnvironmentClasses` | `EnvironmentClass list` | sorted; the declared classes |
| `ExternalTools` | `ExternalToolReq list` | sorted by name; tool/version expectations (FR-005) |

#### CommandSpec
| Field | Type | Notes |
|---|---|---|
| `Id` | `CommandId` | unique; the cross-file reference target for checks |
| `Command` | string | the allowed command line |
| `Timeout` | `TimeoutLimit` | per-command timeout limit (FR-005) |
| `Environment` | `EnvironmentClass` | the class the command runs in |

### Diagnostic (FR-013, D7)
| Field | Type | Notes |
|---|---|---|
| `Id` | `DiagnosticId` | closed DU, the stable id (D7 taxonomy) |
| `File` | `FsggFile` | which of the four files (`Project`/`Policy`/`Capabilities`/`Tooling`) |
| `Locator` | `Locator` | field path / id / line, where available |
| `Message` | string | human-readable explanation + fix hint |

`DiagnosticId` = `UnknownField` | `MissingRequiredField` | `MalformedValue` | `DuplicateId` |
`MissingSchemaVersion` | `MalformedSchemaVersion` | `UnsupportedSchemaVersion` |
`PathEscapesRoot` | `DanglingReference` | `EmptyFile` | `MissingRequiredFile`.

## Cross-reference rules (FR-009)
Validated after each file parses, before facts are emitted:

| Reference | Target | Diagnostic on miss |
|---|---|---|
| `PathMapEntry.Capability` | `CapabilityFacts.Domains` | `DanglingReference` |
| `Check.Domain` | `CapabilityFacts.Domains` | `DanglingReference` |
| `Check.Command` | `ToolingFacts.Commands` (cross-file) | `DanglingReference` |
| `PolicyFacts.DefaultProfile` | `PolicyFacts.Profiles` | `DanglingReference` |

When the referenced file is an absent *optional* file (e.g. a check names a command but
`tooling.yml` is absent), the reference is dangling and diagnosed — it is not silently
dropped (spec "Cross-file references" edge case).

## Determinism invariants (FR-012, SC-002)
- Every list field above is sorted by its stable key (id or normalized path, ordinal).
- No wall-clock, environment, random, or absolute-host-path value appears in any fact.
- `validate` is pure and total: identical `RawSource` → identical `Validation`.
