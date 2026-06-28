# Phase 0 Research: Re-type Config onto FS.GG.Contracts

All Technical-Context unknowns are resolved. Each decision is stated as Decision / Rationale /
Alternatives considered.

## D1 — What is genuinely duplicated, and therefore single-sourced?

**Decision.** Single-source **only the four per-file supported-version constants**
(`Fsgg.Schemas.governanceVersion`, `policyVersion`, `capabilitiesVersion`, `toolingVersion` =
1/1/2/1). Keep **every** Governance `Model`/`Schema`/`Loader` type local. No Contracts record
shape is adopted, because none is duplicated.

**Rationale.** Surface comparison of the restored package
(`~/.nuget/packages/fs.gg.contracts/1.0.1/lib/net10.0/FS.GG.Contracts.dll`) against
`Model.fsi`:

- `Fsgg.Schemas` exposes version constants (`governanceVersion=1`, `policyVersion=1`,
  `capabilitiesVersion=2`, `toolingVersion=1`, plus project/agents/sdd/providers/handoff/
  scaffold versions Governance does not consume) and **raw on-disk artifact record shapes**
  (`ProjectSchema`, `ProvidersSchema`, `AgentsSchema`, `SddSchema`, `GovernanceHandoffSchema`,
  `ScaffoldProvenanceSchema`, and supporting records). `GovernanceSchema`/`PolicySchema`/
  `CapabilitiesSchema`/`ToolingSchema` are fieldless version-marker types.
- Governance `Config.Model` exposes **derived, post-validation typed facts** — `ProjectFacts`,
  `PolicyFacts` (`BranchPolicyDecl`, `ReviewBudgetDecl`), `CapabilityFacts` (`PathMapEntry`,
  `Surface`, `Check`), `ToolingFacts` (`CommandSpec`, `ExternalToolReq`), aggregated as
  `TypedFacts` — plus identity newtypes (`SchemaVersion`, `GovernedPath`, `ProjectId`,
  `DomainId`, `ProfileId`, `SurfaceId`, `CheckId`, `CommandId`, `Owner`, `TimeoutLimit`,
  `EvidenceTag`, `TemplateProfile`, `Baseline`), closed enums (`Cost`, `EnvironmentClass`,
  `Maturity`, `SurfaceClass`, `GeneratedProductTier`), and the diagnostic model (`Locator`,
  `DiagnosticId`, `Diagnostic`, `Validation`).

The two sets do **not** overlap as types: Contracts' `ProjectSchema` is the raw `project.yml`
artifact (fields like `SddConfigPath`, `AgentsConfigPath`), whereas Governance's `ProjectFacts`
is the validated governance fact (`GovernedRoot`, `PackageSurfaces`, classified surfaces).
The only literal Governance held that the package also owns is the per-file *version number*.
So FR-002 is satisfied by the constant swap; FR-003 finds no record to single-source and is
vacuously satisfied; FR-004's local types all stay (they have no Contracts equivalent).

**Alternatives considered.**
- *Adopt Contracts' `*Schema` records as Governance's parse target.* Rejected: they model a
  different layer (raw artifacts vs. derived facts), so adopting them would force a behavior-
  changing re-modelling of validation — the opposite of this feature's zero-behavior-change
  gate, and beyond the item's scope (FR-004, spec Assumptions "single-source scope is shapes +
  version constants, not Governance's whole fact model").
- *Also consume `projectVersion` distinctly from `governanceVersion`.* Rejected: Governance's
  `FsggFile.Project` case **is** the `governance.yml` file (renamed from `project.yml`), so its
  supported version is `Schemas.governanceVersion`. Mapping `Project → governanceVersion` is the
  correct single-source; `projectVersion` is a different (legacy `project.yml`) constant.

## D2 — Mapping each `FsggFile` case to its package constant

**Decision.** `Project → Schemas.governanceVersion` (1), `Policy → Schemas.policyVersion` (1),
`Capabilities → Schemas.capabilitiesVersion` (2), `Tooling → Schemas.toolingVersion` (1). Each
wrapped in Governance's own `SchemaVersion` newtype: `SchemaVersion Schemas.capabilitiesVersion`.

**Rationale.** Reproduces today's exact values (caps=2, others=1) while sourcing each from the
package, so the resolved supported version is unchanged (SC-004) and the unsupported-version
diagnostic path is untouched (FR-005). The newtype wrap keeps `supportedVersionFor`'s return
type — and thus the public surface — identical (FR-009).

**Alternatives considered.** Re-exporting `Schemas.SchemaVersion`-style types: N/A — Contracts
exposes plain `int` constants, not a `SchemaVersion` type; Governance's newtype is its own.

## D3 — Public surface treatment (`.fsi`)

**Decision.** **No `.fsi` change and no re-export.** Surface baseline
`surface/FS.GG.Governance.Config.surface.txt` stays byte-identical.

**Rationale.** The constants are consumed inside the `Schema.fs` body; no public signature
references a Contracts type. `supportedVersionFor: FsggFile -> SchemaVersion` is unchanged.
FR-009's *preferred* outcome ("byte-identical name-level surface via same-named re-exports") is
achieved more strongly — with no re-export at all — so the 50+ downstream consumers compile
unchanged (SC-007). The surface-drift test passing **is** the FR-009 evidence.

**Alternatives considered.** Re-exporting Contracts version constants from the Config surface.
Rejected: nothing downstream consumes a Governance-published version constant, so a re-export
would be surface growth with no consumer — additive churn the constitution's simplicity bias and
the spec's "surface stability preferred over surface change" both discourage.

## D4 — Dependency wiring and pin strategy

**Decision.** Central `PackageVersion Include="FS.GG.Contracts" Version="1.0.1"` in
`Directory.Packages.local.props`; bare `PackageReference Include="FS.GG.Contracts"` (no version)
in `FS.GG.Governance.Config.fsproj` per the repo's central-package-management convention.
Restore from the org GitHub Packages feed (`nuget.config`) at the registry-pinned `1.0.1`.

**Rationale.** Matches the existing CPM setup (`YamlDotNet` is wired the same way) and the
registry pin `fsgg-contracts@1.0.1` (`FS-GG/.github` `registry/dependencies.yml`, owner sdd).
The pin is the single knob a future version bump turns (US3 / FR-001).

**Alternatives considered.** A floating range (`[1.0.0,2.0.0)`): rejected — the org operates
fixed registry pins for cross-repo contracts so version intake is an explicit, reviewed pin bump,
not a silent restore-time float.

## D5 — Lockfile regeneration under locked restore

**Decision.** Regenerate `packages.lock.json` for `FS.GG.Governance.Config` **and every project
in its dependency closure** (the broad lockfile churn is expected), adding `FS.GG.Contracts`
(Direct, resolved 1.0.1) and `FSharp.Core` (CentralTransitive) entries.

**Rationale.** The repo restores in locked mode (feature 085 / `.github#19`); adding a
dependency edge changes the graph, so an un-regenerated lockfile fails CI restore (spec edge
case "new dependency under locked restore"). Regenerating is mechanical (`dotnet restore`
without `--locked-mode`, or `--force-evaluate`) and is part of the feature (FR-010, SC-005).

**Alternatives considered.** Disabling locked mode for Config: rejected — defeats the org's
reproducible-restore posture; the fix is to commit a correct lockfile, not to weaken restore.

## D6 — Restore-feed / credential failure handling

**Decision.** Treat a missing feed/credential or pin mismatch as a hard build/restore failure;
do not provide any fallback to the old local literals.

**Rationale.** Constitution VI (safe failure, no silent fallback) and spec edge case "package
not on the restore feed / wrong version." A loud restore failure is the correct signal; a silent
fallback would re-introduce the drift this feature closes.

**Alternatives considered.** Keeping the literals as a `#if` fallback: rejected — that is
literally the second source of truth being deleted.

---

**Outcome:** No `NEEDS CLARIFICATION` remain. Scope is "swap four literals for four package
constants + dependency/lockfile plumbing," with zero public-surface and zero behavior change.
