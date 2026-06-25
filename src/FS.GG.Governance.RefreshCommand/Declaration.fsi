// Curated public signature contract for the row-local `.fsgg/refresh.yml` generation-manifest adapter (F057).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Declaration.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// YamlDotNet node helpers and the kind recognizer live ONLY in the .fs and are absent here.
//
// This is a PURE leaf (no MVU ceremony — Principle IV): `parse` is total over the raw file lines and never
// touches the filesystem (the interpreter reads the bytes through the F014 `Loader.FileReader` port and
// hands the content here). It is a ROW-LOCAL adapter: it parses the new generation-manifest surface into the
// shared `RefreshModel` `GenerationManifest`/`DeclError` WITHOUT editing F014 `Config`'s frozen four-file
// schema. It reuses YamlDotNet in parse-to-node mode only (the F014 `Schema.fs` / F055 `release.yml`
// precedent) — NO new dependency.
//
// PRODUCT-NEUTRAL (FR-011): every value — view id, kind, output path, source paths, generator argv, and
// generator-version basis — comes from the file; the adapter hardcodes none. FAIL-SAFE (FR-010): an absent
// OR malformed declaration is an `Error DeclError` (input-unavailable, never partial facts). An EMPTY
// `views` list is VALID (`Ok { Entries = [] }` — "nothing to refresh", FR-012). DETERMINISTIC: entries are
// carried in declared order.

namespace FS.GG.Governance.RefreshCommand

open FS.GG.Governance.RefreshJson.RefreshModel

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    /// Parse the raw lines of a `.fsgg/refresh.yml` into a `GenerationManifest`. PURE and TOTAL — a
    /// malformed document (non-mapping root, a `views` entry that is not a mapping, a missing required
    /// field, a duplicate `id`) is an `Error DeclError`, never an exception and never partial facts. The
    /// `Entries` list is in declared order; an empty/absent `views` sequence yields `Ok { Entries = [] }`
    /// (FR-012). Every value is read from the file (product-neutral, FR-011). Reads NO filesystem — the
    /// content arrives from the F014 `Loader.FileReader` port at the interpreter edge.
    val parse: lines: string list -> Result<GenerationManifest, DeclError>
