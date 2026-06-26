// The PURE MVU core of the template-provider seam (071) — visibility lives in Loop.fsi (Principle II),
// so this file carries NO access modifiers on top-level bindings. `init`/`update` perform NO I/O, NO
// clock, NO git: the whole version-check → invoke → boundary-check → probe → write → record composition
// is a pure transition over `Model` + `Msg` emitting `Effect` data the edge `Interpreter` executes.
// Every match is exhaustive and wildcard-free so a new case is a compile error (data-model §7). TOTAL —
// never throws.

namespace FS.GG.Governance.Scaffold

open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    type RunRequest =
        { Request: ScaffoldRequest
          Provider: TemplateProvider option }

    type Effect =
        | InvokeProvider of provider: TemplateProvider * request: ScaffoldRequest
        | ProbeCollisions of paths: string list
        | WriteAll of files: (string * string) list

    type Msg =
        | ProviderEmitted of Result<ProviderEmission, ProviderError>
        | CollisionsProbed of Result<string list, string>
        | FilesWritten of Result<unit, string>

    type Phase =
        | Invoking
        | Probing
        | Writing
        | Done

    type Model =
        { Request: ScaffoldRequest
          Provider: TemplateProvider option
          Phase: Phase
          Emission: ProviderEmission option
          Manifest: ScaffoldManifest option }

    // ── pure decision helpers (hidden — absent from Loop.fsi) ──

    /// The supported contract range for the current major (contract C2): compatible iff Major = 1 and
    /// Minor ≤ 0. Pure — no clock, no env.
    let compatible (declared: ProviderContractVersion) : bool =
        declared.Major = 1 && declared.Minor <= 0

    /// The pure path-boundary predicate (research D5): a target-relative path is in-bounds iff it is
    /// non-empty, NOT rooted (no leading `/`/`\`, no `X:` drive root), and has NO segment that escapes
    /// the target (`..`). String inspection only — no `System.IO`, no filesystem, deterministic across
    /// platforms.
    let inBounds (rel: string) : bool =
        let rooted =
            rel.StartsWith "/"
            || rel.StartsWith "\\"
            || (rel.Length >= 2 && System.Char.IsLetter rel.[0] && rel.[1] = ':')

        let segments = rel.Split([| '/'; '\\' |])

        rel <> ""
        && not rooted
        && not (segments |> Array.exists (fun s -> s = ".."))

    let providerTuple (p: TemplateProvider) : ProviderId * ProviderContractVersion = p.Id, p.ContractVersion

    let init (request: ScaffoldRequest) (provider: TemplateProvider option) : Model * Effect list =
        match provider with
        | None ->
            { Request = request
              Provider = None
              Phase = Done
              Emission = None
              Manifest =
                Some
                    { Provider = None
                      Outcome = NoProvider
                      Generated = []
                      Collisions = [] } },
            []
        | Some p ->
            if compatible p.ContractVersion then
                { Request = request
                  Provider = Some p
                  Phase = Invoking
                  Emission = None
                  Manifest = None },
                [ InvokeProvider(p, request) ]
            else
                { Request = request
                  Provider = Some p
                  Phase = Done
                  Emission = None
                  Manifest =
                    Some
                        { Provider = Some(providerTuple p)
                          Outcome = Refused(ContractMismatch p.ContractVersion)
                          Generated = []
                          Collisions = [] } },
                []

    let update (msg: Msg) (model: Model) : Model * Effect list =
        // The provider tuple is known on every post-invoke path (update is only reached for a selected,
        // compatible provider). `None` is defensive and unreachable given `init`'s transition graph.
        let tuple = model.Provider |> Option.map providerTuple

        let terminateWith (refusal: Refusal) (collisions: string list) : Model * Effect list =
            { model with
                Phase = Done
                Manifest =
                    Some
                        { Provider = tuple
                          Outcome = Refused refusal
                          Generated = []
                          Collisions = collisions } },
            []

        let terminate (refusal: Refusal) : Model * Effect list = terminateWith refusal []

        match msg with
        | ProviderEmitted(Error(Unresolvable d)) -> terminate (ProviderUnavailable d)
        | ProviderEmitted(Error(EmitFailed d)) -> terminate (ProviderErrored d)
        | ProviderEmitted(Ok emission) ->
            let outOfBounds =
                emission.Files
                |> List.map (fun f -> f.RelativePath)
                |> List.filter (inBounds >> not)
                |> List.distinct
                |> List.sort

            if not (List.isEmpty outOfBounds) then
                terminateWith (OutOfTarget outOfBounds) []
            else
                let relPaths = emission.Files |> List.map (fun f -> f.RelativePath)
                let probeSet = (relPaths @ model.Request.ReservedPaths) |> List.distinct

                { model with
                    Phase = Probing
                    Emission = Some emission },
                [ ProbeCollisions probeSet ]
        | CollisionsProbed(Error e) -> terminate (ProviderErrored e)
        | CollisionsProbed(Ok existing) ->
            if not (List.isEmpty existing) then
                let sorted = existing |> List.distinct |> List.sort
                terminateWith (Collision sorted) sorted
            else
                match model.Emission with
                | Some emission ->
                    let files = emission.Files |> List.map (fun f -> f.RelativePath, f.Contents)
                    { model with Phase = Writing }, [ WriteAll files ]
                | None ->
                    // Defensive (unreachable): a probe result without a recorded emission.
                    terminate (ProviderErrored "no recorded emission to write")
        | FilesWritten(Error e) -> terminate (ProviderErrored e)
        | FilesWritten(Ok()) ->
            match model.Emission with
            | Some emission ->
                let generated =
                    emission.Files
                    |> List.map (fun f ->
                        { RelativePath = f.RelativePath
                          Ownership = ProviderOwned })
                    |> List.sortBy (fun g -> g.RelativePath)

                { model with
                    Phase = Done
                    Manifest =
                        Some
                            { Provider = tuple
                              Outcome = Scaffolded
                              Generated = generated
                              Collisions = [] } },
                []
            | None ->
                // Defensive (unreachable): a write ack without a recorded emission.
                terminate (ProviderErrored "no recorded emission after write")
