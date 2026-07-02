module FS.GG.Governance.Config.Tests.DiagnosticTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Config.Tests.Support

// US2 — every malformed-input class named in the spec produces its own distinct, stable,
// located diagnostic and a non-success result with NO typed facts (FR-006, FR-013, SC-003).

let private diagsOf name =
    match validateFixture name with
    | Invalid d -> d
    | Valid _ -> failtestf "expected Invalid for %s, got Valid" name

let private isFileLevel id =
    id = EmptyFile || id = UnreadableFile || id = MissingRequiredFile

// One malformed fixture per DiagnosticId (T009 / fixtures/README.md).
let private cases =
    [ "malformed-duplicate-id", DuplicateId
      "malformed-unknown-field", UnknownField
      "malformed-missing-required-field", MissingRequiredField
      "malformed-malformed-value", MalformedValue
      "malformed-missing-schema-version", MissingSchemaVersion
      "malformed-malformed-schema-version", MalformedSchemaVersion
      "malformed-unsupported-schema-version", UnsupportedSchemaVersion
      "malformed-path-escapes-root", PathEscapesRoot
      "malformed-dangling-domain-ref", DanglingReference
      "malformed-dangling-command-ref", DanglingReference
      "malformed-dangling-default-profile", DanglingReference
      "malformed-empty-file", EmptyFile
      "malformed-missing-required-file", MissingRequiredFile ]

[<Tests>]
let tests =
    testList
        "Diagnostics.US2"
        [ testList
              "one diagnostic id per malformed fixture"
              [ for name, expected in cases ->
                    test name {
                        let diags = diagsOf name
                        Expect.isNonEmpty diags "at least one diagnostic"
                        let hit = diags |> List.tryFind (fun d -> d.Id = expected)
                        Expect.isSome hit (sprintf "%s should carry %s" name (diagnosticIdToken expected))
                        let d = hit.Value
                        Expect.isNotEmpty d.Message "diagnostic carries a human-readable message"
                        let located =
                            d.Locator.Field.IsSome
                            || d.Locator.Id.IsSome
                            || d.Locator.Line.IsSome
                            || isFileLevel d.Id
                        Expect.isTrue located "diagnostic is located (field/id/line, or is file-level)"
                    } ]

          test "no typed facts are emitted for a rejected declaration (FR-006)" {
              // Validation is a closed sum: Invalid carries diagnostics ONLY, never facts.
              for name, _ in cases do
                  match validateFixture name with
                  | Invalid _ -> ()
                  | Valid _ -> failtestf "%s leaked typed facts on failure" name
          }

          test "cross-file dangling command reference is diagnosed, not silently dropped" {
              let diags = diagsOf "malformed-dangling-command-ref"
              Expect.isTrue
                  (diags |> List.exists (fun d -> d.Id = DanglingReference))
                  "a check.command with tooling.yml absent must dangle, not be dropped"
          }

          test "multiple defects → deterministic (file, locator, id) order, byte-stable across runs" {
              let run () = diagsOf "malformed-multi"
              let a = run ()
              let b = run ()
              Expect.equal a b "two runs produce identical diagnostic lists"
              // The two unknown fields were authored zzz-then-aaa; sorted output is aaa-then-zzz.
              let fields = a |> List.choose (fun d -> d.Locator.Field)
              Expect.equal fields (List.sort fields) "diagnostics are emitted in sorted locator order"
          } ]
