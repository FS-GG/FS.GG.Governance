# Phase 1 Data Model: shared-vs-local type boundary

This feature adds **no new types and changes no existing type**. The "data model" here is the
*boundary decision*: which declarations are single-sourced from `FS.GG.Contracts` and which stay
Governance-owned. It records FR-002/003/004 as a concrete inventory so review can confirm nothing
crossed the line.

## A. Single-sourced from `FS.GG.Contracts` (`Fsgg.Schemas`) — FR-002

Consumed as compile-time `int` constants inside `Schema.supportedVersionFor`. Not re-exported.

| Governance use site (`FsggFile` case) | `.fsgg` file | Package constant | Value |
|---|---|---|---|
| `Project` | `governance.yml` | `Fsgg.Schemas.governanceVersion` | 1 |
| `Policy` | `policy.yml` | `Fsgg.Schemas.policyVersion` | 1 |
| `Capabilities` | `capabilities.yml` | `Fsgg.Schemas.capabilitiesVersion` | 2 |
| `Tooling` | `tooling.yml` | `Fsgg.Schemas.toolingVersion` | 1 |

**Validation rule (unchanged):** a present file whose declared `schemaVersion` ≠ the resolved
supported version yields `UnsupportedSchemaVersion`; for `capabilities.yml` the diagnostic also
points at the v1→v2 migration guidance. The *resolved value* is now sourced from the package; the
*rule over it* is byte-identical.

**Guard (SC-004):** no Governance-local integer literal may remain as the supported version for
these four files. After the change, the only place `2` / `1` appear as a supported version is via
`Schemas.*`.

## B. Stays Governance-owned — FR-004 (no Contracts equivalent)

These are derived typed-fact, identity, classification, and diagnostic types produced *by*
validation. `Fsgg.Schemas` has no equivalent (its `*Schema` records model raw on-disk artifacts
authored/emitted elsewhere — a different layer; see [research.md](./research.md) D1). All remain
declared in `Config/Model.fsi` + `Model.fs`, unchanged.

- **Identity newtypes:** `SchemaVersion`, `GovernedPath`, `ProjectId`, `DomainId`, `ProfileId`,
  `SurfaceId`, `CheckId`, `CommandId`, `Owner`, `TimeoutLimit`, `EvidenceTag`, `TemplateProfile`,
  `Baseline`.
- **Closed enums:** `Cost`, `EnvironmentClass`, `Maturity`, `SurfaceClass`,
  `GeneratedProductTier`, `FsggFile`.
- **Typed facts:** `ProjectFacts`, `PolicyFacts` (+`BranchPolicyDecl`, `ReviewBudgetDecl`),
  `CapabilityFacts` (+`PathMapEntry`, `Surface`, `Check`), `ToolingFacts` (+`CommandSpec`,
  `ExternalToolReq`), `TypedFacts`.
- **Diagnostic model:** `Locator`, `DiagnosticId`, `Diagnostic`, `Validation`.
- **Schema/Loader edge types (FR-007):** `FileSlot`, `RawSource` (`Schema.fsi`); `FileReader`
  and `fileSystemReader`/`readSource`/`loadAndValidate` (`Loader.fsi`) — signatures preserved.

**Why local:** these encode Governance's validation/classification policy and its diagnostic
contract, not the cross-repo schema shape. Surrendering them would change the loaded-facts shape
or the public surface, both forbidden (FR-004, FR-008, FR-009).

## C. Not consumed (available in the package, irrelevant here)

`Fsgg.Schemas` also declares `projectVersion`, `agentsVersion`, `sddVersion`,
`providers/handoff/scaffold` versions and their record shapes. Governance Config governs only the
four `.fsgg` files above, so it consumes only the four matching version constants and ignores the
rest. (Note `FsggFile.Project` maps to `governanceVersion`, *not* `projectVersion` — see
research D1/D2.)

## State / transitions

None. The change is a pure constant-resolution swap; no entity has lifecycle or state. Validation
flow (`Loader.loadAndValidate → Schema.validate → Validation`) is unchanged.

## Surface impact

`surface/FS.GG.Governance.Config.surface.txt` — **byte-identical** before and after. No type in
sections A/B/C appears, moves, or changes in the public baseline (the constants are internal to
`Schema.fs`). This is the FR-009 / SC-007 evidence and is asserted by the Config surface-drift
test.
