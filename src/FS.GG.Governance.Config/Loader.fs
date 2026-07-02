// The I/O EDGE of the `.fsgg` schemas (F014). Visibility lives in Loader.fsi
// (Principle II). This is the ONLY place the feature touches the filesystem; all
// decision logic stays in the pure `Schema.validate` (Principle IV, research D3).

namespace FS.GG.Governance.Config

open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Schema

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loader =

    type FileReader = (string -> Result<string option, string>)

    let fileSystemReader (fsggParentDir: string) : FileReader =
        fun name ->
            let path = Path.Combine(fsggParentDir, ".fsgg", name)
            try
                if File.Exists path then Ok(Some(File.ReadAllText path)) else Ok None
            with ex ->
                Error ex.Message

    let readSource (root: GovernedPath) (reader: FileReader) : RawSource =
        let slot name =
            match reader name with
            | Ok(Some content) -> Present content
            | Ok None -> Absent
            // A genuine read failure is surfaced with its CAUSE, never swallowed (Principle VI): an
            // unreadable slot yields `Unreadable`, which `validate` maps to the distinct `UnreadableFile`
            // diagnostic — so an Error can never pass as `Valid`/`None`, and is no longer mis-reported as an
            // `EmptyFile` with the underlying error discarded.
            | Error e -> Unreadable e
        { Root = root
          Project = slot "governance.yml"
          Policy = slot "policy.yml"
          Capabilities = slot "capabilities.yml"
          Tooling = slot "tooling.yml" }

    let loadAndValidate (fsggParentDir: string) : Validation =
        // Root is the in-memory normalization anchor only; it never enters TypedFacts (the
        // emitted GovernedRoot comes from governance.yml), so no absolute host path leaks.
        readSource (GovernedPath ".") (fileSystemReader fsggParentDir)
        |> Schema.validate
