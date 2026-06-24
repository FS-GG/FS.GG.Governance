// The EDGE of release-facts sensing (F054) — the ONLY impure code in the feature (research D2).
// Visibility lives in Interpreter.fsi (Principle II); no `private`/`internal`/`public` modifiers here. The
// per-source file readers/parsers and the exception-reifying gather helper are unexported by ABSENCE from the
// .fsi. `realPort` reads only LOCAL files via BCL `System.IO`; it starts NO process, opens NO socket, and
// references NO registry/publishing-provider SDK (FR-007, SC-004). It NEVER throws out of itself: a missing/
// unreadable/unparseable source or a thrown read exception becomes the matching `Error` and then an
// `Unrecoverable` family with a `SensingDiagnostic` (FR-004). The on-disk FORMAT knowledge lives here in the
// swappable port — the pure core invents no product manifest schema (research D3/D5).

namespace FS.GG.Governance.ReleaseFactsSensing

open FS.GG.Governance.ReleaseFactsSensing.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type RepositoryPort =
        { ReadVersion: unit -> Result<VersionEvidence, string>
          ReadMetadata: unit -> Result<MetadataEvidence, string>
          ReadPins: unit -> Result<PinsEvidence, string>
          ReadPublishPlan: unit -> Result<PostureEvidence, string>
          ReadTrustedPublishing: unit -> Result<PostureEvidence, string>
          ReadProvenance: unit -> Result<PostureEvidence, string> }

    // ── Local-file readers + neutral-format parsers (the ONLY filesystem touch) — research D3/D4 ──

    // Read a layout-relative file's full text, mapping an absent or unreadable file to `Error` (FR-004).
    let readAllText (repoDir: string) (relPath: string) : Result<string, string> =
        let full = System.IO.Path.Combine(repoDir, relPath)

        if not (System.IO.File.Exists full) then
            Error(sprintf "source file not found: %s" relPath)
        else
            try
                Ok(System.IO.File.ReadAllText full)
            with ex ->
                Error(sprintf "source file unreadable: %s: %s" relPath ex.Message)

    // Split a source into trimmed, non-empty tokens on newlines/commas (the neutral token-list format the
    // metadata + posture/config/provenance families use). Deterministic; the pure core sorts on top (D7).
    let splitTokens (text: string) : string list =
        text.Split([| '\n'; '\r'; ',' |])
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s <> "")
        |> List.ofArray

    let readVersion (repoDir: string) (layout: SourceLayout) () : Result<VersionEvidence, string> =
        match readAllText repoDir layout.VersionPath with
        | Error e -> Error e
        | Ok text ->
            let declared = text.Trim()

            if declared = "" then
                Error(sprintf "version source is empty: %s" layout.VersionPath)
            else
                Ok { Declared = declared }

    let readMetadata (repoDir: string) (layout: SourceLayout) () : Result<MetadataEvidence, string> =
        readAllText repoDir layout.MetadataPath
        |> Result.map (fun text -> { PresentFields = splitTokens text })

    // Pins use a neutral `name=version` line format; a non-empty line without `=` is unparseable ⇒ `Error`.
    let readPins (repoDir: string) (layout: SourceLayout) () : Result<PinsEvidence, string> =
        match readAllText repoDir layout.PinsPath with
        | Error e -> Error e
        | Ok text ->
            let lines =
                text.Split([| '\n'; '\r' |])
                |> Array.map (fun s -> s.Trim())
                |> Array.filter (fun s -> s <> "")

            let parsed =
                lines
                |> Array.map (fun line ->
                    let i = line.IndexOf '='

                    if i > 0 then
                        Ok(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim())
                    else
                        Error line)

            match parsed |> Array.tryPick (function Error l -> Some l | Ok _ -> None) with
            | Some bad -> Error(sprintf "pins source has an unparseable line (expected name=version): %s" bad)
            | None ->
                let map = parsed |> Array.choose (function Ok kv -> Some kv | Error _ -> None) |> Map.ofArray
                Ok { Resolved = map }

    let readPosture (repoDir: string) (path: string) () : Result<PostureEvidence, string> =
        readAllText repoDir path |> Result.map (fun text -> { Observed = splitTokens text })

    let realPort (repoDir: string) (layout: SourceLayout) : RepositoryPort =
        { ReadVersion = readVersion repoDir layout
          ReadMetadata = readMetadata repoDir layout
          ReadPins = readPins repoDir layout
          ReadPublishPlan = readPosture repoDir layout.PublishPlanPath
          ReadTrustedPublishing = readPosture repoDir layout.TrustedPublishingPath
          ReadProvenance = readPosture repoDir layout.ProvenancePath }

    let gather (port: RepositoryPort) : RecoveredEvidence =
        // Reify ANY thrown exception as `Error` so a port that throws still yields a well-formed bundle
        // (that family becomes `Unrecoverable`, never a crash) — FR-004.
        let safe (read: unit -> Result<'a, string>) : Result<'a, string> =
            try
                read ()
            with ex ->
                Error(sprintf "read threw: %s" ex.Message)

        { Version = safe port.ReadVersion
          Metadata = safe port.ReadMetadata
          Pins = safe port.ReadPins
          PublishPlan = safe port.ReadPublishPlan
          TrustedPublishing = safe port.ReadTrustedPublishing
          Provenance = safe port.ReadProvenance }

    let senseRelease (port: RepositoryPort) (expectations: ReleaseExpectations) : SensedRelease =
        gather port |> Sensing.deriveFacts expectations
