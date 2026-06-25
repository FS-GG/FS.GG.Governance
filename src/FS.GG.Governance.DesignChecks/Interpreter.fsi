// The EDGE of the design check (F24, P3) — the ONLY place a design catalog is read (FR-007, SC-004).
// Visibility lives here (Constitution Principle II); Interpreter.fs carries NO access modifiers. The real
// port reads the catalogs via `System.IO` / `System.Text.Json` ONLY — NO Skia, NO rendering, NO UI, NO
// registry, NO network. It never throws out of itself — an absent/unreadable catalog becomes an input fact
// in `CatalogUnavailable` (FR-012).

namespace FS.GG.Governance.DesignChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.DesignChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Injected port — the four catalog readers. The ONLY seam through which a design catalog is read.
    /// `System.IO` / `System.Text.Json` only (FR-007).
    type DesignPort =
        { ReadDescriptor: GovernedPath -> Result<string, string>
          ReadTokenCatalog: unit -> Result<Set<string>, string>
          ReadCaptureCatalog: unit -> Result<Set<string>, string>
          ReadControlCatalog: unit -> Result<Set<string>, string>
          ReadContrastCatalog: unit -> Result<Map<string, decimal * decimal>, string> }

    /// Build the REAL port for a repo working dir + a catalog layout `(token, capture, control, contrast)` of
    /// repo-relative JSON catalog paths. Reads via `System.IO` / `System.Text.Json` only (no rendering).
    val realPort: repo: string -> catalogLayout: (string * string * string * string) -> DesignPort

    /// TOTAL and SAFE: reads the design surface descriptor at `request.Path` (its referenced token/capture/
    /// control ids) and the four catalogs, resolves each reference + every contrast pair, and records an
    /// absent/unreadable catalog or descriptor in `CatalogUnavailable` (every exception caught, FR-012).
    val senseDesign:
        port: DesignPort ->
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
            Model.DesignFacts
