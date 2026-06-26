// Curated public signature contract for the canonical deterministic-emit leaf (feature 073).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching JsonText.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted .fsi-first (Principle I). The single member is PURE and TOTAL: it
// reads no clock, filesystem, git, environment, or network, and is byte-for-byte deterministic — a
// given emit callback always yields the same string. It uses the net10.0 shared-framework
// System.Text.Json (`Utf8JsonWriter`) only, so it adds NO `PackageReference` and stays
// System.*/FSharp.Core-only — the same dependency posture every `*Json` projection already keeps.
//
// NOTE: this leaf is the SHARED home of the helper the projections used to hand-copy. It is NOT the
// kernel: the pure projections must not reference the kernel/host capability (their scope-guard tests
// forbid it), so the shared copy lives here, below everything, reachable by all. The kernel keeps its
// own irreducible internal copy (the BCL-only core may reference no leaf).

namespace FS.GG.Governance.JsonText

open System.Text.Json

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonText =

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string.
    /// Default Utf8JsonWriter options ⇒ no indentation ⇒ deterministic, compact output. This is
    /// the canonical deterministic-emit helper every projection shares; the ~14 hand-copied
    /// definitions across `src` are deleted in favour of this one (feature 073).
    val writeToString: emit: (Utf8JsonWriter -> unit) -> string
