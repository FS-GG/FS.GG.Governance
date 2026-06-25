// The EDGE of the skill check (F24, P2) — the ONLY impure code in the domain (FR-007). Visibility lives in
// Interpreter.fsi (Constitution Principle II); no top-level access modifiers here. `realPort` reads only
// LOCAL files via BCL `System.IO`; it never throws out of itself — an unreadable manifest/mirror becomes an
// input fact in `Unreadable` (FR-012). The neutral manifest FORMAT knowledge lives here in the swappable
// port (`path:` / `task:` / `mirror:` lines); the pure pack invents no schema.

namespace FS.GG.Governance.SkillChecks

open System.IO
open FS.GG.Governance.Config.Model
open FS.GG.Governance.SkillChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type SkillPort =
        { ReadManifest: GovernedPath -> Result<string, string>
          ResolvePath: string -> Result<bool, string>
          ReadMirror: string -> Result<string option, string> }

    // BCL Path.GetDirectoryName is nullable; coalesce to "" (Nullable=enable). Hidden by ABSENCE from .fsi.
    let dirOrEmpty (p: string) : string =
        match Path.GetDirectoryName p with
        | null -> ""
        | d -> d

    let readManifest (repo: string) (path: GovernedPath) : Result<string, string> =
        let (GovernedPath rel) = path
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "skill manifest not found: %s" rel)
        else
            try
                Ok(File.ReadAllText full)
            with ex ->
                Error(sprintf "skill manifest unreadable: %s: %s" rel ex.Message)

    let resolvePath (repo: string) (rel: string) : Result<bool, string> =
        try
            let full = Path.Combine(repo, rel)
            Ok(File.Exists full || Directory.Exists full)
        with ex ->
            Error(sprintf "path resolve threw: %s" ex.Message)

    let readMirror (repo: string) (rel: string) : Result<string option, string> =
        try
            let full = Path.Combine(repo, rel)

            if File.Exists full then
                Ok(Some(File.ReadAllText full))
            else
                Ok None
        with ex ->
            Error(sprintf "mirror unreadable: %s" ex.Message)

    let realPort (repo: string) : SkillPort =
        { ReadManifest = readManifest repo
          ResolvePath = resolvePath repo
          ReadMirror = readMirror repo }

    // Parse the neutral manifest: collect `path:` / `task:` / `mirror:` values (trimmed, in order).
    let parseManifest (text: string) : string list * string list * string option =
        let lines =
            text.Split([| '\n'; '\r' |])
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (fun s -> s <> "")

        let valuesFor (prefix: string) =
            lines
            |> Array.filter (fun l -> l.StartsWith prefix)
            |> Array.map (fun l -> l.Substring(prefix.Length).Trim())
            |> List.ofArray

        let paths = valuesFor "path:"
        let tasks = valuesFor "task:"

        let mirror =
            match valuesFor "mirror:" with
            | m :: _ -> Some m
            | [] -> None

        paths, tasks, mirror

    let senseSkill (port: SkillPort) (request: SC.SurfaceCheckRequest) : SkillFacts =
        let (SurfaceId skillId) = request.Surface
        let (GovernedPath manifestRel) = request.Path
        let skillDir = dirOrEmpty manifestRel

        let safe (read: unit -> Result<'a, string>) : Result<'a, string> =
            try
                read ()
            with ex ->
                Error(sprintf "read threw: %s" ex.Message)

        match safe (fun () -> port.ReadManifest request.Path) with
        | Error e ->
            { SkillId = skillId
              PathContract = []
              TaskList = TaskListConsistent
              Mirror = NoMirrorDeclared
              Unreadable = [ e ] }
        | Ok text ->
            let paths, tasks, mirror = parseManifest text
            let mutable unreadable = []

            // Path contract: bounds are pure string math; existence is the port (FR-007).
            let pathFacts =
                paths
                |> List.map (fun claimed ->
                    if claimed.Contains ".." || Path.IsPathRooted claimed then
                        { Claimed = claimed
                          Outcome = PathEscapesBounds claimed }
                    else
                        let combined = Path.Combine(skillDir, claimed)

                        match safe (fun () -> port.ResolvePath combined) with
                        | Ok true ->
                            { Claimed = claimed
                              Outcome = PathHolds }
                        | Ok false ->
                            { Claimed = claimed
                              Outcome = PathUnresolved claimed }
                        | Error e ->
                            unreadable <- e :: unreadable

                            { Claimed = claimed
                              Outcome = PathUnresolved claimed })

            // Task-list consistency: a duplicate task id is the canonical inconsistency.
            let taskList =
                let dup = tasks |> List.countBy id |> List.tryFind (fun (_, n) -> n > 1)

                match dup with
                | Some(t, _) -> TaskListInconsistent(sprintf "duplicate task '%s'" t)
                | None -> TaskListConsistent

            // Mirror: declared-absent ⇒ MirrorMissing; present ⇒ in-sync iff content matches the manifest.
            let mirrorOutcome =
                match mirror with
                | None -> NoMirrorDeclared
                | Some m ->
                    let mirrorRel = Path.Combine(skillDir, m)

                    match safe (fun () -> port.ReadMirror mirrorRel) with
                    | Ok None -> MirrorMissing m
                    | Ok(Some content) ->
                        if content.Trim() = text.Trim() then
                            MirrorInSync
                        else
                            MirrorDrifted(m, "mirror content differs from the manifest")
                    | Error e ->
                        unreadable <- e :: unreadable
                        NoMirrorDeclared

            { SkillId = skillId
              PathContract = pathFacts
              TaskList = taskList
              Mirror = mirrorOutcome
              Unreadable = List.rev unreadable }
