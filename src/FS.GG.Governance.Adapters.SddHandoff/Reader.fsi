// Curated public signature contract for the pure handoff parse + version-check (F081, US2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Reader.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// Design-first artifact: drafted in FSI before any Reader.fs body exists (Principle I). `parse` is PURE
// and TOTAL — it NEVER throws (Constitution VI): a malformed document, a missing required field, an
// unrecognized `contractVersion` major, or a node declaring `autoSynthetic` each yields a distinct,
// descriptive `Diagnostic` (research D2/D5). The impure act of LOCATING and READING the file is the
// host's port (research D6); this layer sees only the already-read `(path, json)` text.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Reader =

    /// One located document: its source path and raw JSON text. The impure read is the host's
    /// `Interpreter.Ports.Handoffs` port; this record is its pure result.
    type HandoffRead =
        { Source: string
          Json: string }

    /// Pure: parse + validate one located document. `Ok handoff` for a well-formed `v1.x` document;
    /// otherwise `Error` with a distinct, descriptive `Diagnostic`:
    ///   • malformed JSON / missing required contract field ⇒ `Malformed`;
    ///   • unrecognized `contractVersion` major (≠ `supportedContractMajor`) ⇒ `VersionMismatch`;
    ///   • a node declaring `state: "autoSynthetic"` ⇒ `AutoSyntheticDeclared` (the authoritative
    ///     rejection — NOT generic `Malformed`, research D4).
    /// Unknown additive (minor) fields are ignored (ADR-0002 versioning posture). NEVER throws.
    val parse: read: HandoffRead -> Result<Handoff, Diagnostic>
