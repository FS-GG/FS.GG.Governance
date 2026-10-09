module FS.GG.Governance.VerifyCommand.Tests.ProviderContextTests

open Expecto
open FS.GG.Governance.VerifyCommand

// Synthetic: literal invocation controls exercise pure parsing only, not filesystem or host admission.
let flags = [ "--provider-provenance"; "--provider-provenance-sha256"; "--provider-policy"; "--provider-policy-sha256"; "--provider-platform" ]
let digest = System.String('A', 64)
let values = [ "missing/provenance.json"; digest; "independent/policy.json"; digest; "linux-x64" ]
let pairs = List.zip flags values
let argv = pairs |> List.collect (fun (flag, value) -> [flag; value])
let equal expected actual = if actual <> expected then failwithf "Expected %A, actual %A" expected actual
let selectorError expected input = equal (Error expected) (ProviderContext.select input)
let invocationError expected input = equal (Error expected) (Loop.parseInvocation input)
let replace (flag: string) (value: string) : string list =
    pairs |> List.collect (fun (f,v) ->
        let replacement = if f = flag then value else v
        [f; replacement])
let fixedCases: (string * (unit -> unit)) list =
    [
        ("Synthetic no selection preserves legacy defaults", (fun () ->
            match Loop.parseInvocation [] with
            | Ok invocation -> equal None invocation.Provider; equal (Loop.parse []) (Ok invocation.Request)
            | Error e -> failwithf "%A" e))
        ("Synthetic complete selection retains raw independent locators and digest spelling", (fun () ->
            match Loop.parseInvocation argv with
            | Ok { Provider = Some selection } ->
                equal values.[0] selection.ProvenancePath; equal values.[2] selection.PolicyPath
                equal digest selection.ProvenanceSha256; equal digest selection.PolicySha256
                equal values.[4] selection.Platform
            | other -> failwithf "%A" other))
        ("Synthetic missing legacy repo is not filled across provider boundary", (fun () ->
            invocationError (Loop.LegacyUsage(Loop.MissingValue "--repo")) (["--repo"] @ argv @ ["later"])))
        ("Synthetic paths stops at provider boundary", (fun () ->
            invocationError (Loop.LegacyUsage(Loop.UnexpectedArgument "later")) (["--paths";"first"] @ argv @ ["later"])))
        ("Synthetic empty paths remains empty at provider boundary", (fun () ->
            invocationError (Loop.LegacyUsage Loop.EmptyPaths) (["--paths"] @ argv)))
        ("Synthetic valid paths and provider interleave", (fun () ->
            let legacy = ["verify";"--paths";"a b.fs";"$literal.fs";"--json"]
            match Loop.parseInvocation (["verify";"--paths";"a b.fs";"$literal.fs"] @ argv @ ["--json"]) with
            | Ok invocation -> equal (Loop.parse legacy) (Ok invocation.Request)
            | Error e -> failwithf "%A" e))
        ("Synthetic legacy entrypoint still rejects provider flag", (fun () ->
            equal (Error(Loop.UnknownFlag flags.Head)) (Loop.parse argv)))
        ("Synthetic unknown flag after complete selection remains legacy usage", (fun () ->
            invocationError (Loop.LegacyUsage(Loop.UnknownFlag "--nope")) (argv @ ["--nope"])))
    ]

let incompleteCases =
    [1 .. 30] |> List.map (fun mask ->
        ("Synthetic incomplete subset " + string mask, (fun () ->
            let selected = pairs |> List.mapi (fun i pair -> i,pair) |> List.filter (fun (i,_) -> mask &&& (1 <<< i) <> 0) |> List.map snd
            let input = selected |> List.collect (fun (f,v) -> [f;v])
            let missing = flags |> List.filter (fun f -> not (selected |> List.exists (fun (s,_) -> s=f)))
            selectorError (ProviderContext.IncompleteSelection missing) input)))

let optionCases =
    flags |> List.collect (fun flag ->
        [
            ("Synthetic duplicate " + flag, (fun () -> selectorError (ProviderContext.DuplicateOption flag) (argv @ [flag; "extra"])))
            ("Synthetic missing value " + flag, (fun () -> selectorError (ProviderContext.MissingValue flag) [flag; "--json"]))
            ("Synthetic empty value " + flag, (fun () -> selectorError (ProviderContext.EmptyValue flag) (replace flag " ")))
        ])

let digestCases =
    [flags.[1];flags.[3]] |> List.map (fun flag ->
        ("Synthetic invalid raw digest " + flag, (fun () ->
            selectorError (ProviderContext.InvalidSha256 flag) (replace flag (System.String('g',64))))))

let legacyCases =
    [ ["--mode";"gate"]; ["--repo"]; ["stray"]; ["--paths"]; ["--paths";"a";"--since";"x"]; ["--profile";"bogus"]; ["--nope"] ]
    |> List.map (fun legacy ->
        ("Synthetic unchanged legacy diagnostic " + System.String.Join(" ",legacy), (fun () ->
            match Loop.parse legacy, Loop.parseInvocation legacy with
            | Error expected, Error(Loop.LegacyUsage actual) -> equal expected actual
            | other -> failwithf "%A" other)))

let cases: (string * (unit -> unit)) list = fixedCases @ incompleteCases @ optionCases @ digestCases @ legacyCases

[<Tests>]
let tests = testList "Explicit provider selector" (cases |> List.map (fun (name, run) -> testCase name run))
