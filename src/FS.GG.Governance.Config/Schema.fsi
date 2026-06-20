// Curated public signature contract for the PURE validation core of the `.fsgg` schemas
// (F014).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Schema.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Schema.fs body exists (Principle I). This is the PURE side of the Constitution's MVU/I-O
// boundary (Principle IV, research D3): `validate` takes an already-read `RawSource` and
// returns a `Validation` — it performs NO I/O (no filesystem, process, network, clock) and
// NEVER throws. Reading the files from disk is the `Loader` edge's job (Loader.fsi).
//
// YAML is read into a node tree with YamlDotNet (parse-to-node only, research D2); EVERY
// strictness rule below is this module's own code over that tree, not YamlDotNet binding:
// unknown fields, missing required fields, malformed values, duplicate ids, schemaVersion
// range, path normalization/escape, and cross-reference resolution all become located
// `Diagnostic`s (FR-006, FR-013). For identical input the output is byte-for-byte identical
// (FR-012, SC-002).

namespace FS.GG.Governance.Config

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Schema =

    // ── Input value (produced by the Loader edge) ──

    /// One of the four file slots: ABSENT (the file was not on disk) or PRESENT with its raw
    /// textual content. The required/optional decision (research D4) is applied by `validate`,
    /// NOT by this type — so an absent OPTIONAL file is fine, an absent REQUIRED file yields
    /// `MissingRequiredFile`, and a PRESENT empty/whitespace file yields `EmptyFile`
    /// (FR-015, spec edge cases). `Present` keeps the raw text only as transient input; it
    /// never reaches the typed facts (SC-005).
    type FileSlot =
        | Absent
        | Present of content: string

    /// The unparsed-but-located input to the pure core: the governed-root anchor plus the four
    /// file slots. Built by `Loader.load`; consumed by `validate`.
    type RawSource =
        { /// The `.fsgg` parent directory as a normalized ref — the anchor every declared path
          /// is normalized against and bounds-checked within (FR-008, D5). Used ONLY as the
          /// in-memory normalization anchor; it is never emitted into `TypedFacts` (the emitted
          /// `ProjectFacts.GovernedRoot` comes from `project.yml`), so no absolute host path
          /// leaks (SC-002/SC-005).
          Root: GovernedPath
          Project: FileSlot
          Policy: FileSlot
          Capabilities: FileSlot
          Tooling: FileSlot }

    // ── Supported versions (FR-007) ──

    /// The schema version this build understands. Any `schemaVersion` other than this fails with
    /// `UnsupportedSchemaVersion`: greater is "upgrade the tool" (spec edge case); lesser is
    /// likewise rejected — no historical versions exist for the MVP, and silently accepting one
    /// would be partial acceptance (FR-006).
    val supportedSchemaVersion: SchemaVersion

    // ── The pure validation entry point ──

    /// Parse, validate strictly, normalize, classify, and convert a `RawSource` into a
    /// `Validation` (FR-006, FR-010). PURE and TOTAL (Principle IV, FR-012): no I/O, never
    /// throws, and byte-for-byte identical for identical input (SC-002).
    ///
    /// Required vs optional (research D4): `Project` and `Capabilities` are REQUIRED; an
    /// `Absent` required file yields `MissingRequiredFile`. `Policy` and `Tooling` are
    /// OPTIONAL; `Absent` is fine (their facts are `None`), but a `Present` file is fully
    /// validated. Any `Present` file that is empty/whitespace yields `EmptyFile`.
    ///
    /// Strictness (FR-006): unknown fields → `UnknownField`; missing required fields →
    /// `MissingRequiredField`; malformed scalar/enum values → `MalformedValue`; repeated ids
    /// where uniqueness is required → `DuplicateId`; a missing/malformed/too-new
    /// `schemaVersion` → the matching schema-version diagnostic. NO partial acceptance and NO
    /// silent correction: any failure makes the whole result `Invalid` with NO typed facts.
    ///
    /// Paths (FR-008, D5): every declared path is normalized (separators unified, `.`/`..`
    /// resolved, kept relative to `Root`); a path that escapes `Root` yields `PathEscapesRoot`.
    ///
    /// Cross-references (FR-009): `PathMapEntry.Capability` and `Check.Domain` must name a
    /// declared `CapabilityFacts.Domains`; `Check.Command`, when present, must name a declared
    /// `ToolingFacts.Commands` (cross-file — dangling even when `tooling.yml` is absent);
    /// `PolicyFacts.DefaultProfile` must name a declared profile. A miss yields
    /// `DanglingReference`.
    ///
    /// Determinism (FR-012, SC-002): every emitted list (domains, path-map, surfaces, checks,
    /// commands, environment classes, diagnostics) is sorted by its stable id/normalized-path
    /// key, so re-ordering equivalent authored entries does not change the result. Diagnostics
    /// are ordered by (file, locator, id).
    val validate: source: RawSource -> Validation
