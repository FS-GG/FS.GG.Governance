namespace FS.GG.Governance.ScaffoldManifestJson

open System.IO
open System.Text
open System.Text.Json
open FS.GG.Governance.Scaffold.Model

// The 071 scaffold-manifest projection. Renders one `ScaffoldManifest` into the deterministic,
// versioned `scaffold-manifest` document text via a hand-driven `System.Text.Json` `Utf8JsonWriter`
// walk — the net10.0 shared-framework mechanism the kernel's `Json.fs` uses, so NO new dependency.
// PURE and TOTAL: no I/O, no git, no clock, never throws. Emit-only: re-derives nothing beyond the
// documented ascending orders (the manifest already fixed its collections; the re-sort is defence for
// determinism). No visibility modifiers — the surface is ScaffoldManifestJson.fsi (Principle II); every
// token helper and sub-object writer below is hidden by its absence from the .fsi (the Kernel/Json.fs
// precedent). Carries NO absolute path, clock, or env value (SC-004, SC-006).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ScaffoldManifestJson =

    let schemaVersion = "fsgg.scaffold-manifest/v1"

    // ── internal writer plumbing (hidden — absent from ScaffoldManifestJson.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string. Default
    /// `Utf8JsonWriter` options ⇒ no indentation ⇒ deterministic, compact output (the `Json.fs`
    /// `writeToString` precedent).
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ── closed-token helpers (hidden) — each match is EXHAUSTIVE and wildcard-free, so a future case
    //    is a compile error here, never a silently mis-tokened field. ──

    let outcomeToken (outcome: ScaffoldOutcome) : string =
        match outcome with
        | NoProvider -> "noProvider"
        | Scaffolded -> "scaffolded"
        | Refused _ -> "refused"

    let ownershipToken (ownership: PathOwnership) : string =
        match ownership with
        | ProviderOwned -> "providerOwned"

    /// `contractVersion` renders as `"<Major>.<Minor>"`.
    let versionString (v: ProviderContractVersion) : string = sprintf "%d.%d" v.Major v.Minor

    /// Write a sorted string array property.
    let writeSortedArray (w: Utf8JsonWriter) (name: string) (items: string list) =
        w.WriteStartArray name

        for item in items |> List.sort do
            w.WriteStringValue item

        w.WriteEndArray()

    /// The closed `refusal` object — `reason` is an exhaustive, wildcard-free token.
    let writeRefusal (w: Utf8JsonWriter) (refusal: Refusal) =
        w.WriteStartObject()

        match refusal with
        | ContractMismatch declared ->
            w.WriteString("reason", "contractMismatch")
            w.WriteString("declaredVersion", versionString declared)
        | ProviderUnavailable detail ->
            w.WriteString("reason", "providerUnavailable")
            w.WriteString("detail", detail)
        | ProviderErrored detail ->
            w.WriteString("reason", "providerErrored")
            w.WriteString("detail", detail)
        | OutOfTarget paths ->
            w.WriteString("reason", "outOfTarget")
            w.WriteStartArray "paths"

            for p in paths |> List.sort do
                w.WriteStringValue p

            w.WriteEndArray()
        | Collision paths ->
            w.WriteString("reason", "collision")
            w.WriteStartArray "paths"

            for p in paths |> List.sort do
                w.WriteStringValue p

            w.WriteEndArray()

        w.WriteEndObject()

    let ofManifest (manifest: ScaffoldManifest) : string =
        writeToString (fun w ->
            w.WriteStartObject()

            // Fixed field order: schemaVersion, outcome, refusal, provider, generated, collisions.
            w.WriteString("schemaVersion", schemaVersion)
            w.WriteString("outcome", outcomeToken manifest.Outcome)

            match manifest.Outcome with
            | Refused r ->
                w.WritePropertyName "refusal"
                writeRefusal w r
            | NoProvider
            | Scaffolded -> w.WriteNull "refusal"

            match manifest.Provider with
            | Some(ProviderId id, version) ->
                w.WriteStartObject "provider"
                w.WriteString("id", id)
                w.WriteString("contractVersion", versionString version)
                w.WriteEndObject()
            | None -> w.WriteNull "provider"

            w.WriteStartArray "generated"

            for g in manifest.Generated |> List.sortBy (fun g -> g.RelativePath) do
                w.WriteStartObject()
                w.WriteString("path", g.RelativePath)
                w.WriteString("ownership", ownershipToken g.Ownership)
                w.WriteEndObject()

            w.WriteEndArray()

            writeSortedArray w "collisions" manifest.Collisions

            w.WriteEndObject())
