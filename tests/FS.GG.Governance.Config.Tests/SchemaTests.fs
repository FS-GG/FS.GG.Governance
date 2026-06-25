module FS.GG.Governance.Config.Tests.SchemaTests

open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Tests.Support

// US1 — a valid product validates to deterministic, YAML-free typed facts. Tests exercise
// the PUBLIC surface (Loader edge over real fixture dirs) with REAL YAML (Principle V).

let private factsOf name =
    match validateFixture name with
    | Valid f -> f
    | Invalid d -> failtestf "expected Valid for %s, got %A" name d

[<Tests>]
let tests =
    testList
        "Schema.US1"
        [ test "valid-complete → ProjectFacts" {
              let f = factsOf "valid-complete"
              Expect.equal f.Project.Id (ProjectId "my-product") "id"
              Expect.equal f.Project.Domains [ DomainId "package-api"; DomainId "workflow" ] "domains sorted"
              Expect.equal f.Project.GovernedRoot (GovernedPath ".") "governed root normalized"
              Expect.equal f.Project.PackageSurfaces [ GovernedPath "src" ] "package surfaces"
              Expect.equal f.Project.PolicyRef (Some(GovernedPath ".fsgg/policy.yml")) "policyRef"
              Expect.equal f.Project.CapabilitiesRef (Some(GovernedPath ".fsgg/capabilities.yml")) "capabilitiesRef"
          }

          test "valid-complete → PolicyFacts" {
              let f = factsOf "valid-complete"
              match f.Policy with
              | Some p ->
                  Expect.equal p.Profiles [ ProfileId "light"; ProfileId "standard"; ProfileId "strict" ] "profiles sorted"
                  Expect.equal p.DefaultProfile (ProfileId "standard") "default profile"
                  Expect.equal p.BranchPolicy (Some { Pattern = "main"; RequirePr = true }) "branch policy placeholder"
                  Expect.equal p.ReviewBudget (Some { MaxReviews = 3 }) "review budget placeholder"
              | None -> failtest "expected policy facts"
          }

          test "valid-complete → CapabilityFacts" {
              let f = factsOf "valid-complete"
              let c = f.Capabilities
              Expect.equal c.Domains [ DomainId "package-api"; DomainId "workflow" ] "domains sorted"
              Expect.equal
                  c.PathMap
                  [ { Glob = GovernedPath "src/**"; Capability = DomainId "package-api" }
                    { Glob = GovernedPath "work/**"; Capability = DomainId "workflow" } ]
                  "path map sorted by glob"
              Expect.equal
                  c.Surfaces
                  [ { Id = SurfaceId "public-api"
                      Class = ProtectedSurface
                      Paths = [ GovernedPath "src/**/*.fsi" ]
                      Owner = Owner "platform"
                      Maturity = BlockOnShip
                      EvidenceTag = None
                      TemplateProfile = None
                      Baseline = None } ]
                  "surfaces classified"
              Expect.equal
                  c.Checks
                  [ { Id = CheckId "build"
                      Domain = DomainId "package-api"
                      Command = Some(CommandId "dotnet-build")
                      Owner = Owner "platform"
                      Cost = Medium
                      Environment = LocalOrCi
                      Maturity = BlockOnShip
                      Tier = None } ]
                  "checks with per-entry metadata"
          }

          test "valid-complete → ToolingFacts" {
              let f = factsOf "valid-complete"
              match f.Tooling with
              | Some t ->
                  Expect.equal
                      t.Commands
                      [ { Id = CommandId "dotnet-build"
                          Command = "dotnet build"
                          Timeout = TimeoutLimit 600
                          Environment = LocalOrCi } ]
                      "commands with timeout + environment"
                  Expect.equal t.EnvironmentClasses [ Ci; Local ] "environment classes sorted"
                  Expect.equal t.ExternalTools [ { Tool = "dotnet"; MinVersion = "10.0.0" } ] "external tools"
              | None -> failtest "expected tooling facts"
          }

          test "no raw-YAML enum spellings leak into typed facts (FR-010, SC-005)" {
              let rendered = sprintf "%A" (factsOf "valid-complete")
              // The hyphenated YAML spellings must have been converted to DU cases, not kept
              // as raw text — proving the facts are product-neutral, not raw YAML.
              for raw in [ "block-on-ship"; "local-or-ci" ] do
                  Expect.isFalse (rendered.Contains raw) (sprintf "raw YAML token '%s' leaked into typed facts" raw)
          }

          test "valid-no-policy → Valid with Policy = None (FR-015)" {
              let f = factsOf "valid-no-policy"
              Expect.isNone f.Policy "absent optional policy.yml → None"
              Expect.isSome f.Tooling "tooling present"
          }

          test "valid-no-tooling → Valid with Tooling = None (FR-015)" {
              let f = factsOf "valid-no-tooling"
              Expect.isNone f.Tooling "absent optional tooling.yml → None"
              Expect.isSome f.Policy "policy present"
          } ]
