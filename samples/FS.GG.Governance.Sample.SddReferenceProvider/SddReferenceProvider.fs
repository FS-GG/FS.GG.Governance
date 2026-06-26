// The SDD reference template provider (072) — a CONCRETE, conforming instance of the unchanged 071
// `Model.TemplateProvider` contract. Visibility lives in SddReferenceProvider.fsi (Principle II), so this
// file carries NO access modifiers on top-level bindings. It is plain data: a record value whose `Emit`
// is PURE and deterministic — it DESCRIBES a minimal but buildable F#/.NET runtime skeleton and never
// touches the filesystem/clock/env and never throws (contract R1, research D2/D6). The emitted skeleton's
// dependency closure is FSharp.Core only (SDK-bundled), so `dotnet build <App>.sln` succeeds
// first-attempt, offline, on every run (SC-002/SC-003). All provider-/package-/layout-specific knowledge
// lives HERE, in the sample — the generic seam core gains none (FR-002, SC-006).

namespace FS.GG.Governance.Sample.SddReferenceProvider

open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SddReferenceProvider =

    let providerId = ProviderId "fsgg.sample.sdd-reference"

    // ── pure emission helpers (hidden — absent from SddReferenceProvider.fsi) ──

    /// `<App>` is the leaf directory name of the request target — the ONLY request-derived variation in
    /// the emitted skeleton (data-model §2). Pure string inspection; falls back to "App" for an empty
    /// leaf so emission is total.
    let appName (target: string) : string =
        let trimmed = target.TrimEnd('/', '\\')
        let cut = trimmed.LastIndexOfAny([| '/'; '\\' |])
        let leaf = trimmed.Substring(cut + 1)
        if leaf = "" then "App" else leaf

    /// Fixed, literal project GUIDs so the emitted `.sln` is byte-deterministic (no clock/guid source).
    /// The F# project-type GUID is the well-known MSBuild constant; the two project GUIDs are distinct
    /// literals (unique within the solution), identical on every run.
    let fsharpProjectTypeGuid = "F2A71F9B-5D33-465A-A702-920D77279786"
    let appProjectGuid = "11111111-1111-1111-1111-111111111111"
    let testProjectGuid = "22222222-2222-2222-2222-222222222222"

    /// The documented build unit: a classic `.sln` wiring both projects with fixed literal GUIDs and full
    /// Debug/Release configuration maps so `dotnet build <App>.sln` builds both. Solution files use TAB
    /// indentation and backslash paths (MSBuild normalizes the separators per platform).
    let solutionFile (app: string) : string =
        let proj name path guid =
            sprintf
                "Project(\"{%s}\") = \"%s\", \"%s\", \"{%s}\"\nEndProject"
                fsharpProjectTypeGuid
                name
                path
                guid

        let cfg guid =
            [ sprintf "\t\t{%s}.Debug|Any CPU.ActiveCfg = Debug|Any CPU" guid
              sprintf "\t\t{%s}.Debug|Any CPU.Build.0 = Debug|Any CPU" guid
              sprintf "\t\t{%s}.Release|Any CPU.ActiveCfg = Release|Any CPU" guid
              sprintf "\t\t{%s}.Release|Any CPU.Build.0 = Release|Any CPU" guid ]
            |> String.concat "\n"

        [ "Microsoft Visual Studio Solution File, Format Version 12.00"
          "# Visual Studio Version 17"
          proj app (sprintf "src\\%s\\%s.fsproj" app app) appProjectGuid
          proj (app + ".Tests") (sprintf "tests\\%s.Tests\\%s.Tests.fsproj" app app) testProjectGuid
          "Global"
          "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
          "\t\tDebug|Any CPU = Debug|Any CPU"
          "\t\tRelease|Any CPU = Release|Any CPU"
          "\tEndGlobalSection"
          "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"
          cfg appProjectGuid
          cfg testProjectGuid
          "\tEndGlobalSection"
          "EndGlobal"
          "" ]
        |> String.concat "\n"

    /// The runtime source project: an `Exe`, `net10.0`, FSharp.Core only (implicitly referenced by the
    /// F# SDK — no `PackageReference`), so the dependency closure is empty beyond the SDK.
    let sourceProject : string =
        [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
          ""
          "  <PropertyGroup>"
          "    <OutputType>Exe</OutputType>"
          "    <TargetFramework>net10.0</TargetFramework>"
          "  </PropertyGroup>"
          ""
          "  <ItemGroup>"
          "    <Compile Include=\"Program.fs\" />"
          "  </ItemGroup>"
          ""
          "</Project>"
          "" ]
        |> String.concat "\n"

    /// The trivial buildable entry point ([<EntryPoint>] returning 0). An implicit module named after the
    /// file carries the attribute.
    let programFile : string = "[<EntryPoint>]\nlet main _ = 0\n"

    /// The test project: a `net10.0` library that references the source project + FSharp.Core only.
    let testProject (app: string) : string =
        [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
          ""
          "  <PropertyGroup>"
          "    <TargetFramework>net10.0</TargetFramework>"
          "  </PropertyGroup>"
          ""
          "  <ItemGroup>"
          "    <Compile Include=\"Tests.fs\" />"
          "  </ItemGroup>"
          ""
          "  <ItemGroup>"
          sprintf "    <ProjectReference Include=\"../../src/%s/%s.fsproj\" />" app app
          "  </ItemGroup>"
          ""
          "</Project>"
          "" ]
        |> String.concat "\n"

    /// The trivial buildable test body.
    let testFile : string = "module Tests\n\nlet answer = 42\n"

    /// Provenance: the generated-path list + the documented build command.
    let readmeFile (app: string) : string =
        [ sprintf "# %s (generated by fsgg.sample.sdd-reference)" app
          ""
          "Generated runtime skeleton:"
          ""
          sprintf "- `%s.sln`" app
          sprintf "- `src/%s/%s.fsproj`" app app
          sprintf "- `src/%s/Program.fs`" app
          sprintf "- `tests/%s.Tests/%s.Tests.fsproj`" app app
          sprintf "- `tests/%s.Tests/Tests.fs`" app
          "- `README.md`"
          ""
          "Build:"
          ""
          "```"
          sprintf "dotnet build %s.sln" app
          "```"
          "" ]
        |> String.concat "\n"

    /// PURE, deterministic emission: derive `<App>`, return the fixed buildable file set in a stable
    /// order. No clock/guid/env; never throws; every path is relative, `..`-free, not rooted (contract R1
    /// obligations 1-5).
    let emit (request: ScaffoldRequest) : Result<ProviderEmission, ProviderError> =
        let app = appName request.Target

        let file path contents = { RelativePath = path; Contents = contents }

        Ok
            { Files =
                [ file (sprintf "%s.sln" app) (solutionFile app)
                  file (sprintf "src/%s/%s.fsproj" app app) sourceProject
                  file (sprintf "src/%s/Program.fs" app) programFile
                  file (sprintf "tests/%s.Tests/%s.Tests.fsproj" app app) (testProject app)
                  file (sprintf "tests/%s.Tests/Tests.fs" app) testFile
                  file "README.md" (readmeFile app) ] }

    let provider =
        { Id = providerId
          ContractVersion = { Major = 1; Minor = 0 }
          Emit = emit }
