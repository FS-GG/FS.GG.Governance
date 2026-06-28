# Data Model: Governance `.fsgg` Slot Rename

**Feature**: 084-governance-yml-rename | **Date**: 2026-06-28

This feature changes **no in-memory data shape**. It changes one *filename* that one of the
existing entities is read from. The entities below are pre-existing; the only delta is the
source-filename of the primary slot. No type, field, or `schemaVersion` changes.

## Entities

### `.fsgg/` directory (shared config directory)

A project's governance configuration directory, **shared between SDD and Governance** under
ADR-0005.

| Slot | Owner | Filename | Required by Governance | Delta this feature |
|------|-------|----------|------------------------|--------------------|
| Primary (Governance) | Governance | `governance.yml` | Yes | **Renamed from `project.yml`** |
| Primary (SDD) | SDD | `project.yml` | No (ignored) | Unchanged — SDD-owned, coexists |
| Policy | Governance | `policy.yml` | No (optional) | Unchanged |
| Capabilities | Governance | `capabilities.yml` | No (optional) | Unchanged |
| Tooling | Governance | `tooling.yml` | No (optional) | Unchanged |

Steady state under ADR-0005: both `governance.yml` and `project.yml` may be present;
Governance reads only `governance.yml`.

### `FileSlot` (loader input slot)

The loader's `Source` record (in `Schema.fsi`) holds one `FileSlot` per file it reads:
`Root`, `Project`, `Policy`, `Capabilities`, `Tooling`. A slot resolves to `Present content`,
`Absent`, or an error-as-`Present ""` (so an `Error` can never masquerade as `Valid`/absent).

- **Delta**: `Loader.fileSystemReader` populates `Project` from `slot "governance.yml"`
  (was `slot "project.yml"`). The **field name `Project` is retained** (FR-009); only the
  filename string it reads changes.

### `ProjectFacts` (typed primary-slot facts)

The in-memory, YAML-free, product-neutral facts produced from the primary slot
(`SchemaVersion`, governed root, domains, default work root, package surfaces, pointers to
optional catalogs).

- **Delta**: **none to the type.** Name and every member retained (FR-009). The source
  section comment in `Model.fs`/`Model.fsi` updates from `// ── project.yml ──` to
  `// ── governance.yml ──` for reader orientation only.

## Validation rules (unchanged behavior, new filename)

- **R1 (FR-001)**: The primary slot is read from `governance.yml`.
- **R2 (FR-002, no-fallback / SC-004)**: If `governance.yml` is absent, the primary slot is
  **absent/missing** — the loader yields the missing-required-primary diagnostic. A present
  `project.yml` does NOT satisfy the primary slot.
- **R3 (FR-003)**: For byte-equivalent content, the loader produces identical `Valid` facts /
  gate set / routing facts / diagnostics under `governance.yml` as it did under `project.yml`.
- **R4**: Optional-slot rules (`policy.yml`/`capabilities.yml`/`tooling.yml` absent ⇒ `None`;
  a `FileReader` `Error` is surfaced, never swallowed) are unchanged.
- **R5 (no schema bump)**: `schemaVersion` validation range is unchanged.

## State transitions

None. The loader is a pure read+validate; there is no stateful workflow, no MVU boundary
introduced or changed by this feature.
