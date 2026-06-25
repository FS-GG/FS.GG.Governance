module FS.GG.Governance.Config.Tests.ProductSchemaV2Tests

open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Tests.Support

// F23 — capabilities.yml schemaVersion 2: the expanded product-surface vocabulary parses to typed facts;
// strict validation (closed diagnostic set) extends to every new field; per-file versioning rejects a
// non-2 capabilities.yml with a migration pointer. Exercised through the real Loader edge over real YAML.

let private factsOf name =
    match validateFixture name with
    | Valid f -> f
    | Invalid d -> failtestf "expected Valid for %s, got %A" name d

let private diagsOf name =
    match validateFixture name with
    | Invalid d -> d
    | Valid _ -> failtestf "expected Invalid for %s, got Valid" name

let private surfaceById (f: TypedFacts) id =
    f.Capabilities.Surfaces |> List.find (fun s -> s.Id = SurfaceId id)

let private checkById (f: TypedFacts) id =
    f.Capabilities.Checks |> List.find (fun c -> c.Id = CheckId id)

[<Tests>]
let tests =
    testList
        "Config.ProductSchemaV2.F23"
        [ testList
              "US1 — new kinds + optional attributes parse (T014)"
              [ test "supportedVersionFor is per-file: Capabilities → 2, the rest → 1 (D1)" {
                    Expect.equal (Schema.supportedVersionFor Capabilities) (SchemaVersion 2) "capabilities → 2"
                    Expect.equal (Schema.supportedVersionFor Project) (SchemaVersion 1) "project → 1"
                    Expect.equal (Schema.supportedVersionFor Policy) (SchemaVersion 1) "policy → 1"
                    Expect.equal (Schema.supportedVersionFor Tooling) (SchemaVersion 1) "tooling → 1"
                }

                test "each new kind token parses to its SurfaceClass" {
                    let f = factsOf "product-surface-all-kinds"
                    Expect.equal (surfaceById f "public-api").Class PackageSurface "package"
                    Expect.equal (surfaceById f "product-root").Class GeneratedProductRoot "generatedProduct"
                    Expect.equal (surfaceById f "guide-docs").Class DocsSurface "docs"
                    Expect.equal (surfaceById f "ship-skill").Class SkillSurface "skill"
                    Expect.equal (surfaceById f "tokens").Class DesignSurface "design"
                    Expect.equal (surfaceById f "sample").Class SampleAppSurface "sampleApp"
                }

                test "optional evidenceTag/templateProfile/baseline + check tier parse onto their records" {
                    let f = factsOf "product-surface-all-kinds"
                    Expect.equal (surfaceById f "public-api").Baseline (Some(Baseline "src/public-api.baseline.txt")) "baseline"
                    Expect.equal (surfaceById f "public-api").EvidenceTag (Some(EvidenceTag "api-surface")) "evidenceTag"
                    Expect.equal (surfaceById f "product-root").TemplateProfile (Some(TemplateProfile "fsharp-lib")) "templateProfile"
                    Expect.equal (checkById f "scan").Tier (Some StructuralScan) "tier structuralScan"
                    Expect.equal (checkById f "relgate").Tier (Some ReleaseValidation) "tier releaseValidation"
                }

                test "a subset declaration (no optional attrs / no tier) is Valid, not an error (FR-004)" {
                    let f = factsOf "product-surface-all-kinds"
                    let bare = surfaceById f "ship-skill"
                    Expect.equal bare.EvidenceTag None "no evidenceTag"
                    Expect.equal bare.TemplateProfile None "no templateProfile"
                    Expect.equal bare.Baseline None "no baseline"
                }

                test "a malformed kind scalar ⇒ MalformedValue naming the field" {
                    let d = diagsOf "malformed-v2-bad-kind"
                    let hit = d |> List.tryFind (fun x -> x.Id = MalformedValue && x.Locator.Field = Some "kind")
                    Expect.isSome hit "MalformedValue on 'kind'"
                }

                test "a malformed tier scalar ⇒ MalformedValue naming the field" {
                    let d = diagsOf "malformed-v2-bad-tier"
                    let hit = d |> List.tryFind (fun x -> x.Id = MalformedValue && x.Locator.Field = Some "tier")
                    Expect.isSome hit "MalformedValue on 'tier'"
                } ]

          testList
              "US4 — versioned schema with a safe migration (T035/T036)"
              [ test "a v1 capabilities.yml ⇒ UnsupportedSchemaVersion naming version 1 + the migration pointer (SC-006)" {
                    let d = diagsOf "migration-v1-capabilities"
                    let hit = d |> List.tryFind (fun x -> x.Id = UnsupportedSchemaVersion && x.File = Capabilities)
                    Expect.isSome hit "UnsupportedSchemaVersion on capabilities.yml"
                    Expect.stringContains hit.Value.Message "1" "message names the actual version"
                    Expect.stringContains hit.Value.Message "migration.md" "message points at the migration guidance"
                }

                test "a v3 capabilities.yml ⇒ UnsupportedSchemaVersion naming version 3" {
                    let d = diagsOf "malformed-v2-version-three"
                    let hit = d |> List.tryFind (fun x -> x.Id = UnsupportedSchemaVersion && x.File = Capabilities)
                    Expect.isSome hit "UnsupportedSchemaVersion"
                    Expect.stringContains hit.Value.Message "3" "names version 3"
                }

                test "project/policy/tooling at version 1 remain valid under per-file versioning (D1)" {
                    // valid-complete keeps project/policy/tooling at 1 and capabilities at 2 → Valid.
                    let f = factsOf "valid-complete"
                    Expect.equal f.Capabilities.SchemaVersion (SchemaVersion 2) "capabilities migrated to 2"
                    Expect.equal f.Project.SchemaVersion (SchemaVersion 1) "project stays at 1"
                }

                test "duplicate ids among the new kinds ⇒ DuplicateId (both locations)" {
                    let d = diagsOf "malformed-v2-duplicate-id"
                    let dups = d |> List.filter (fun x -> x.Id = DuplicateId)
                    Expect.isNonEmpty dups "DuplicateId emitted"
                    Expect.isTrue (dups |> List.exists (fun x -> x.Locator.Id = Some "dup-surface")) "duplicate surface id"
                    Expect.isTrue (dups |> List.exists (fun x -> x.Locator.Id = Some "dup-check")) "duplicate check id"
                }

                test "an unknown field on a new-kind surface ⇒ UnknownField naming the field" {
                    let d = diagsOf "malformed-v2-unknown-field"
                    let hit = d |> List.tryFind (fun x -> x.Id = UnknownField && x.Locator.Field = Some "templateProfle")
                    Expect.isSome hit "UnknownField names the misspelled field"
                } ]

          testList
              "US3/US4 — path escape + determinism (T032/T037)"
              [ test "a monorepo-only (`..`) surface path ⇒ PathEscapesRoot naming the field (FR-010)" {
                    let d = diagsOf "malformed-product-path-escapes-root"
                    let hit = d |> List.tryFind (fun x -> x.Id = PathEscapesRoot)
                    Expect.isSome hit "PathEscapesRoot, never a fabricated success"
                }

                test "two loads of product-surface-all-kinds are byte-identical including the new fields (SC-005)" {
                    let a = factsOf "product-surface-all-kinds"
                    let b = factsOf "product-surface-all-kinds"
                    Expect.equal a b "deterministic typed facts incl. new attributes/tier"
                } ] ]
