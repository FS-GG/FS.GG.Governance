module FS.GG.Governance.Config.Tests.DeterminismTests

open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Schema
open FS.GG.Governance.Config.Tests.Support

// FR-012 / SC-002 — validation is byte-stable for identical input, and re-ordering
// equivalent authored entries does not change the typed facts.

// All permutations of a distinct list (used as the order-independence generator).
let rec private permutations =
    function
    | [] -> [ [] ]
    | xs -> xs |> List.collect (fun x -> permutations (List.filter ((<>) x) xs) |> List.map (fun p -> x :: p))

let private domains = [ "alpha"; "beta"; "gamma"; "delta" ]

let private projectYaml (ds: string list) =
    "schemaVersion: 1\nid: p\ngovernedRoot: .\ndomains:\n"
    + (ds |> List.map (sprintf "  - %s") |> String.concat "\n")

let private capsYaml (ds: string list) =
    // capabilities.yml is schemaVersion 2 as of F23 (per-file versioning, D1).
    "schemaVersion: 2\ndomains:\n" + (ds |> List.map (sprintf "  - %s") |> String.concat "\n")

let private sourceFor (ds: string list) : RawSource =
    { Root = GovernedPath "."
      Project = Present(projectYaml ds)
      Policy = Absent
      Capabilities = Present(capsYaml ds)
      Tooling = Absent }

// Single-case wrapper so FsCheck draws a domain PERMUTATION, not an arbitrary string list.
type Perm = Perm of string list

type PermArb =
    static member Perm() = Arb.fromGen (Gen.elements (permutations domains) |> Gen.map Perm)

let private cfg =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<PermArb> ]
        maxTest = 200 }

let private canonical = domains |> List.sort |> List.map DomainId

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "validate is byte-stable: valid-complete twice → structurally identical (SC-002)" {
              Expect.equal (validateFixture "valid-complete") (validateFixture "valid-complete") "two validations identical"
          }

          test "re-ordered authored input yields identical typed facts (FR-012)" {
              Expect.equal (validateFixture "valid-reordered") (validateFixture "valid-complete") "reordered ≡ original"
          }

          testPropertyWithConfig cfg "permuting authored domains yields identical typed facts" (fun (Perm ds) ->
              match validate (sourceFor ds) with
              | Valid f -> f.Project.Domains = canonical && f.Capabilities.Domains = canonical
              | Invalid _ -> false) ]
