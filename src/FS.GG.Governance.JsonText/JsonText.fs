namespace FS.GG.Governance.JsonText

// The 073 canonical deterministic-emit leaf. One pure plumbing helper that runs a caller's emit
// callback over a default-options Utf8JsonWriter and returns the compact (non-indented), UTF-8 JSON
// text — the byte-identical body the *Json projections, EvidenceReuseStore, and the RefreshCommand
// interpreter each used to hand-copy (the kernel's `Json.fs` writeToString precedent). No clock, host,
// filesystem, git, environment, or network input; byte-for-byte deterministic. No visibility modifiers
// — the surface is JsonText.fsi (Principle II).

open System.IO
open System.Text
open System.Text.Json

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonText =

    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
