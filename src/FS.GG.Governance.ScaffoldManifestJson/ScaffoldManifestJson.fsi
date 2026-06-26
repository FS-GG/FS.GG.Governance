// Curated public signature contract for the scaffold-manifest projection (071).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching ScaffoldManifestJson.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here. Every JSON writer and closed-token helper lives ONLY
// in the .fs and is absent here, exactly as `FS.GG.Governance.Kernel.Json` keeps its writer plumbing
// off `Json.fsi`.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// ScaffoldManifestJson.fs body exists (Principle I). `ofManifest` is the PURE, TOTAL projection: it
// renders one already-typed `ScaffoldManifest` into the deterministic, versioned `scaffold-manifest`
// document text — the stable, machine-readable provenance record other lifecycle/Governance steps read
// (FR-005, FR-010, FR-012). It performs no I/O, no clock, no git, never throws, and is byte-for-byte
// identical for identical input (SC-004). It carries NO absolute target path, wall-clock, or
// environment value, so the same provider over the same empty target yields an identical manifest on
// any machine (SC-006, research D6). Serialization uses the net10.0 shared-framework `System.Text.Json`
// (`Utf8JsonWriter`) — NO new `PackageReference`.

namespace FS.GG.Governance.ScaffoldManifestJson

open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ScaffoldManifestJson =

    /// The declared schema-version token stamped into every emitted document as its `schemaVersion`
    /// field, so consumers can branch on the contract version. A fixed, deterministic constant — never
    /// derived from a clock, environment, or input value.
    val schemaVersion: string

    /// Project a `ScaffoldManifest` into its deterministic, versioned `scaffold-manifest` document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `outcome`,
    /// `refusal`, `provider`, `generated`, `collisions` (contracts/scaffold-manifest.schema.md):
    ///   • `outcome`    — `"noProvider"` | `"scaffolded"` | `"refused"`, an exhaustive wildcard-free token.
    ///   • `refusal`    — `null` unless `outcome = "refused"`, then the closed `{ reason, … }` object
    ///                    (`contractMismatch`+`declaredVersion`, `providerUnavailable`/`providerErrored`
    ///                    +`detail`, `outOfTarget`/`collision`+`paths`); `reason` is exhaustive.
    ///   • `provider`   — `{ id, contractVersion: "M.m" }`; `null` only when `outcome = "noProvider"`.
    ///   • `generated`  — provider-owned written paths as `{ path, ownership: "providerOwned" }`,
    ///                    ascending by `path`; `[]` unless `outcome = "scaffolded"`.
    ///   • `collisions` — pre-existing/reserved paths that forced a refusal, ascending; `[]` otherwise.
    ///
    /// PURE and TOTAL: no file, process, clock, network, or git access; renders every path verbatim and
    /// re-derives/re-sorts only the documented ascending orders; never throws for any well-typed input
    /// (including the `NoProvider` totality fixture). DETERMINISTIC (SC-004, SC-006): identical inputs
    /// yield byte-for-byte identical text; the document carries NO absolute path, timestamp, or
    /// environment value, so 100% of `generated[]` paths are attributable to the manifest's `provider`
    /// id + contract version alone.
    val ofManifest: manifest: ScaffoldManifest -> string
