// Curated public signature contract for the EDGE loader of the `.fsgg` schemas (F014).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Loader.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Loader.fs body exists (Principle I). This is the I/O EDGE of the Constitution's MVU
// boundary (Principle IV, research D3). The only I/O in the feature — reading the four
// `.fsgg/*.yml` files and distinguishing ABSENT from PRESENT — lives here, isolated behind an
// injected `FileReader` port. `Schema.validate` (the pure core) is then applied to the
// assembled `RawSource`; all decision logic stays pure. A full Elmish `Program` is
// deliberately NOT used (research D3): reading a fixed set of four files has no multi-step
// state, so a local effect/port algebra is the idiomatic, lighter boundary (Principle III).

namespace FS.GG.Governance.Config

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Schema

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loader =

    /// The injected I/O port: read one file relative to the `.fsgg` parent directory. `Ok
    /// (Some content)` = present; `Ok None` = absent (NOT an error — an absent optional file
    /// is normal, FR-015); `Error reason` = a genuine read failure (permissions, etc.),
    /// distinct from absence (Principle VI). Modelling I/O as this supplied function is what
    /// keeps the workflow's decision logic pure and testable with an in-memory reader.
    type FileReader = (string -> Result<string option, string>)

    /// The real-filesystem `FileReader` for a given `.fsgg` parent directory: maps a relative
    /// file name to a read against disk, returning `Ok None` for a missing file. This is the
    /// interpreter that binds the port to `System.IO`; it is the ONLY place the feature touches
    /// the filesystem.
    val fileSystemReader: fsggParentDir: string -> FileReader

    /// Assemble a `RawSource` by reading the four `.fsgg` files through the injected `reader`,
    /// anchored at the normalized `root`. Pure with respect to its argument: it performs I/O
    /// ONLY through `reader`, so an in-memory reader makes this fully deterministic in tests.
    /// A `reader` `Error` for a REQUIRED file surfaces as that slot being treated as unreadable
    /// and is reported by `validate` as the file failing (Principle VI); an `Error` for an
    /// optional file is likewise surfaced, never silently swallowed.
    val readSource: root: GovernedPath -> reader: FileReader -> RawSource

    /// The end-to-end edge convenience: build the real-filesystem reader for `fsggParentDir`,
    /// read the four files, and run the pure `Schema.validate`. Returns the same `Validation`
    /// the pure core produces. This is the function a host/CLI calls; it is the single
    /// composition of edge I/O + pure validation.
    val loadAndValidate: fsggParentDir: string -> Validation
