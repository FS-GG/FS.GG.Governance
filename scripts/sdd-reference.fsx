// FSI entry point for the Principle I design pass for 072 (T005).
//
//   dotnet build -c Release src/FS.GG.Governance.ScaffoldManifestJson
//   dotnet fsi scripts/sdd-reference.fsx
//
// It #r's the BUILT 071 seam assemblies and sketches the reference `provider`
// record + its pure `Emit`, exercising the contract against a literal
// `ScaffoldRequest` exactly as a downstream host would — BEFORE the .fs body
// exists (Constitution Principle I; plan Constitution Check I).

#r "../src/FS.GG.Governance.Scaffold/bin/Release/net10.0/FS.GG.Governance.Scaffold.dll"
#r "../src/FS.GG.Governance.ScaffoldManifestJson/bin/Release/net10.0/FS.GG.Governance.ScaffoldManifestJson.dll"

open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.ScaffoldManifestJson

// The stable id the seam records but never interprets (071 FR-003).
let providerId = ProviderId "fsgg.sample.sdd-reference"

// `<App>` is the leaf name of the target directory — the ONLY request-derived
// variation in the emitted skeleton (data-model §2).
let appName (target: string) : string =
    let trimmed = target.TrimEnd('/', '\\')
    let leaf = trimmed.Substring(trimmed.LastIndexOfAny([| '/'; '\\' |]) + 1)
    if leaf = "" then "App" else leaf

// Pure Emit: derive `<App>`, return the fixed buildable skeleton (FSharp.Core
// only). No clock/guid/env; never throws; deterministic order.
let emit (request: ScaffoldRequest) : Result<ProviderEmission, ProviderError> =
    let app = appName request.Target
    let f path contents = { RelativePath = path; Contents = contents }
    Ok
        { Files =
            [ f (app + ".sln") (sprintf "// solution: %s\n" app)
              f (sprintf "src/%s/%s.fsproj" app app) "<Project />\n"
              f (sprintf "src/%s/Program.fs" app) "[<EntryPoint>]\nlet main _ = 0\n"
              f (sprintf "tests/%s.Tests/%s.Tests.fsproj" app app) "<Project />\n"
              f (sprintf "tests/%s.Tests/Tests.fs" app) "module Tests\n"
              f "README.md" "# generated\n" ] }

let provider: TemplateProvider =
    { Id = providerId
      ContractVersion = { Major = 1; Minor = 0 }
      Emit = emit }

// Exercise against a literal request and drive the seam through realPorts over a
// temp dir, then project the manifest — the contract a host consumes.
let target = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdd-ref-fsi-" + System.Guid.NewGuid().ToString("N"))
System.IO.Directory.CreateDirectory target |> ignore

let model =
    Interpreter.run
        (Interpreter.realPorts target)
        { Request = { Target = target; ReservedPaths = [ ".fsgg/policy.fsgg" ] }
          Provider = Some provider }

printfn "Phase   = %A" model.Phase
model.Manifest |> Option.iter (fun m -> printfn "Outcome = %A" m.Outcome)
model.Manifest |> Option.iter (fun m -> printfn "Manifest JSON:\n%s" (ScaffoldManifestJson.ofManifest m))
