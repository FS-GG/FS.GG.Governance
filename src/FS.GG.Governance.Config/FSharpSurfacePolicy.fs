namespace FS.GG.Governance.Config

open System
open System.IO
open System.Text.Json

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpSurfacePolicy =

    type Exemption =
        { Module: string
          Owner: string
          Rationale: string
          ReviewBy: DateOnly }

    type ProjectPolicy =
        { RequiresBaseline: bool
          BaselineCurrent: bool }

    type Facts =
        { DeclaredGlob: string
          Projects: Map<string, ProjectPolicy>
          Exemptions: Exemption list }

    type LoadResult =
        | Missing of Facts
        | Loaded of Facts
        | Invalid of reason: string

    let defaultFacts =
        { DeclaredGlob = "src/**/*.fsi"
          Projects = Map.empty
          Exemptions = [] }

    let load root =
        let path = Path.Combine(root, ".fsgg", "fsharp-surface.json")
        if not (File.Exists path) then Missing defaultFacts
        else
            try
                use document = JsonDocument.Parse(File.ReadAllText path)
                let top = document.RootElement
                if top.ValueKind <> JsonValueKind.Object then Invalid "policy root must be a JSON object"
                else
                    let tryProperty (name: string) (element: JsonElement) =
                        match element.TryGetProperty name with true, value -> Some value | _ -> None
                    let requiredText (name: string) (element: JsonElement) : Result<string, string> =
                        match tryProperty name element |> Option.bind (fun value -> if value.ValueKind = JsonValueKind.String then value.GetString() |> Option.ofObj else None) with
                        | Some value when not (String.IsNullOrWhiteSpace value) -> Ok value
                        | _ -> Error(sprintf "%s must be a non-empty string" name)
                    let declaredGlob : Result<string, string> =
                        match tryProperty "declaredGlob" top with
                        | None -> Ok defaultFacts.DeclaredGlob
                        | Some value when value.ValueKind = JsonValueKind.String ->
                            match value.GetString() |> Option.ofObj with
                            | Some text when not (String.IsNullOrWhiteSpace text) -> Ok text
                            | _ -> Error "declaredGlob must be a non-empty string"
                        | _ -> Error "declaredGlob must be a non-empty string"
                    let projects : Result<Map<string, ProjectPolicy>, string> =
                        match tryProperty "projects" top with
                        | None -> Ok Map.empty
                        | Some value when value.ValueKind = JsonValueKind.Object ->
                            value.EnumerateObject()
                            |> Seq.map (fun property ->
                                let boolean (name: string) (fallback: bool) : Result<bool, string> =
                                    match tryProperty name property.Value with
                                    | None -> Ok fallback
                                    | Some setting when setting.ValueKind = JsonValueKind.True || setting.ValueKind = JsonValueKind.False -> Ok(setting.GetBoolean())
                                    | _ -> Error(sprintf "project '%s' %s must be boolean" property.Name name)
                                if property.Value.ValueKind <> JsonValueKind.Object then Error(sprintf "project '%s' must be an object" property.Name)
                                else
                                    match boolean "requiresBaseline" false, boolean "baselineCurrent" true with
                                    | Ok required, Ok current -> Ok(property.Name, { RequiresBaseline = required; BaselineCurrent = current })
                                    | Error reason, _ | _, Error reason -> Error reason)
                            |> Seq.fold (fun state item ->
                                match state, item with
                                | Ok values, Ok(name, value) -> Ok(Map.add name value values)
                                | Error reason, _ | _, Error reason -> Error reason) (Ok Map.empty)
                        | _ -> Error "projects must be an object"
                    let exemptions : Result<Exemption list, string> =
                        match tryProperty "exemptions" top with
                        | None -> Ok []
                        | Some value when value.ValueKind = JsonValueKind.Array ->
                            value.EnumerateArray()
                            |> Seq.map (fun entry ->
                                if entry.ValueKind <> JsonValueKind.Object then Error "each exemption must be an object"
                                else
                                    match requiredText "module" entry, requiredText "owner" entry, requiredText "rationale" entry, requiredText "reviewBy" entry with
                                    | Ok moduleName, Ok owner, Ok rationale, Ok reviewBy ->
                                        match DateOnly.TryParse(reviewBy: string) with
                                        | true, date -> Ok { Module = moduleName; Owner = owner; Rationale = rationale; ReviewBy = date }
                                        | _ -> Error(sprintf "exemption '%s' reviewBy must be a date" moduleName)
                                    | Error reason, _, _, _ | _, Error reason, _, _ | _, _, Error reason, _ | _, _, _, Error reason -> Error reason)
                            |> Seq.fold (fun state item ->
                                match state, item with
                                | Ok values, Ok value -> Ok(value :: values)
                                | Error reason, _ | _, Error reason -> Error reason) (Ok [])
                            |> Result.map List.rev
                        | _ -> Error "exemptions must be an array"
                    match declaredGlob, projects, exemptions with
                    | Ok glob, Ok projectFacts, Ok exemptionFacts -> Loaded { DeclaredGlob = glob; Projects = projectFacts; Exemptions = exemptionFacts }
                    | Error reason, _, _ | _, Error reason, _ | _, _, Error reason -> Invalid reason
            with ex -> Invalid ex.Message
