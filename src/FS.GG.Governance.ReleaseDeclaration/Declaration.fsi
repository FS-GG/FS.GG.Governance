// Curated public signature contract for the SHARED `.fsgg/release.yml` declaration adapter (065 — F26
// host wiring). This leaf is the F055 `ReleaseCommand.Declaration` adapter LIFTED OUT of the release exe
// so BOTH hosts — `fsgg release` (which packs) and `fsgg verify` (which previews) — parse the identical
// file through ONE module (research D6, contracts/shared-declaration.md GD-1/GD-2). It is an ADAPTER
// RELOCATION, not a new pure evaluation core.
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Declaration.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// YamlDotNet node helpers, the per-family rule/expectation/layout readers, and the token recognizers live
// ONLY in the .fs and are absent here.
//
// This is a PURE leaf (no MVU ceremony — Principle IV): `parse` is total over the raw file lines and never
// touches the filesystem (the interpreter reads the bytes through the F014 `Loader.FileReader` port at each
// host edge and hands the content here). PRODUCT-NEUTRAL (FR-014): every value comes from the file; the
// adapter hardcodes none. FAIL-SAFE (FR-014): an absent OR malformed declaration is an `Error DeclError`
// (input-unavailable, never partial facts, never a fabricated `Met`). It reuses the already-pinned
// YamlDotNet in parse-to-node mode only (the F014 `Schema.fs` precedent) — NO new dependency.

namespace FS.GG.Governance.ReleaseDeclaration

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.ValidationMatrix.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    /// One declared packable project: the governed surface id, the pack `GateCommand` the release host runs
    /// through the F051 execution port, and the released-version baseline the packed version is compared
    /// against. `Baseline = None ⇒ first release` (F26 `versionPolicy ⇒ NoBaseline`, not a downgrade).
    type PackableProject =
        { Surface: SurfaceId
          PackCommand: GateCommand
          Baseline: string option }

    /// The typed result of parsing `.fsgg/release.yml` — the F055 trio (rules/expectations/layout, the
    /// EXACT inputs the F053/F054 cores need) PLUS the two additive sections this row introduces:
    /// `PackableProjects` (the per-project pack boundary — `[]` ⇒ `NoPackableProjects`, vacuously
    /// satisfied) and `Matrix` (the optional declared exhaustive validation matrix — `None` ⇒
    /// `NotDeclared`, never invented). No raw YAML, host path, or timestamp is carried.
    type ReleaseDeclaration =
        { Rules: ReleaseRule list
          Expectations: ReleaseExpectations
          Layout: SourceLayout
          PackableProjects: PackableProject list
          Matrix: ExhaustiveMatrix option }

    /// A closed, explained reason a `release.yml` was rejected (the F014 `Diagnostic` spirit): actionable,
    /// product-neutral text identifying the missing/invalid declaration. Distinct from a sensing
    /// `Unrecoverable` family — this is the whole declaration being unavailable (⇒ exit 3).
    type DeclError = { Reason: string }

    /// Parse the raw lines of a `.fsgg/release.yml` into a `ReleaseDeclaration`. PURE and TOTAL — a
    /// malformed document (non-mapping root, unknown rule kind token, unrecognized severity/maturity token,
    /// missing required section, absent expectation/layout value, a malformed packable-project entry, or a
    /// malformed matrix declaration) is an `Error DeclError`, never an exception and never partial facts.
    /// A `release.yml` with NO `packableProjects`/`matrix` sections parses with `PackableProjects = []` and
    /// `Matrix = None` (GD-3 backward-compat). The `Rules` list is normalized to the F053 stable composite
    /// key order; every value is read from the file (product-neutral, FR-014). Reads NO filesystem — the
    /// content arrives from the F014 `Loader.FileReader` port at the interpreter edge.
    val parse: lines: string list -> Result<ReleaseDeclaration, DeclError>
