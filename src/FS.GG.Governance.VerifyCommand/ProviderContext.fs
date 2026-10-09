namespace FS.GG.Governance.VerifyCommand
module ProviderContext =
    type Selection =
        { ProvenancePath: string; ProvenanceSha256: string; PolicyPath: string; PolicySha256: string; Platform: string }
    type SelectionError =
        | MissingValue of flag: string
        | DuplicateOption of flag: string
        | IncompleteSelection of missingFlags: string list
        | EmptyValue of flag: string
        | InvalidSha256 of flag: string
    let flags = [ "--provider-provenance"; "--provider-provenance-sha256"; "--provider-policy"; "--provider-policy-sha256"; "--provider-platform" ]
    let select (argv: string list) : Result<Selection option, SelectionError> =
        // mutable: one bounded-by-input argv scan preserving original tokens for the legacy parser.
        let mutable remaining = argv
        let mutable values = Map.empty<string, string>
        let mutable failure: SelectionError option = None
        while not (List.isEmpty remaining) && Option.isNone failure do
            match remaining with
            | flag :: rest when List.contains flag flags ->
                if Map.containsKey flag values then failure <- Some(DuplicateOption flag)
                else
                    match rest with
                    | value :: tail when not (value.StartsWith "--") ->
                        values <- Map.add flag value values
                        remaining <- tail
                    | _ -> failure <- Some(MissingValue flag)
            | _ :: rest -> remaining <- rest
            | [] -> ()
        match failure with
        | Some error -> Error error
        | None when Map.isEmpty values -> Ok None
        | None ->
            let missing = flags |> List.filter (fun flag -> not (Map.containsKey flag values))
            if not (List.isEmpty missing) then Error(IncompleteSelection missing)
            else
                let empty = flags |> List.tryFind (fun flag -> System.String.IsNullOrWhiteSpace values.[flag])
                let digests = [ "--provider-provenance-sha256"; "--provider-policy-sha256" ]
                let isHex c = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')
                let invalid = digests |> List.tryFind (fun flag -> let value = values.[flag] in value.Length <> 64 || not (Seq.forall isHex value))
                match empty, invalid with
                | Some flag, _ -> Error(EmptyValue flag)
                | None, Some flag -> Error(InvalidSha256 flag)
                | None, None -> Ok(Some { ProvenancePath = values.[flags.[0]]; ProvenanceSha256 = values.[flags.[1]]; PolicyPath = values.[flags.[2]]; PolicySha256 = values.[flags.[3]]; Platform = values.[flags.[4]] })
