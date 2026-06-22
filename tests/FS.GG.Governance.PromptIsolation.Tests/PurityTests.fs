module FS.GG.Governance.PromptIsolation.Tests.PurityTests

open System.IO
open Expecto
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation.Tests.Support

// Cross-cutting (SC-005, FR-006): `excerpt`, `assemble`, and `render` are identical across changed working
// directory, elapsed wall-clock time, and unrelated filesystem state — no clock/cwd/filesystem/git/
// environment/network read, no model invoked, no bytes hashed, nothing persisted.

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "assemble+render and excerpt are identical across changed cwd and unrelated filesystem state" {
              let request = requestOf baseInstructions [ excerptPayload 12 instructionImitatingText; digestPayload "sha256:abc" ]
              let beforeRender = PromptIsolation.render request |> PromptIsolation.renderedValue
              let beforeExcerpt = excerpt (SizeBound 5) "abcdefgh"

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore

              try
                  Directory.SetCurrentDirectory tempDir
                  let tempFile = Path.Combine(tempDir, "unrelated.tmp")
                  File.WriteAllText(tempFile, "noise")
                  let duringRender = PromptIsolation.render request |> PromptIsolation.renderedValue
                  let duringExcerpt = excerpt (SizeBound 5) "abcdefgh"
                  File.Delete tempFile
                  let afterRender = PromptIsolation.render request |> PromptIsolation.renderedValue

                  Expect.equal duringRender beforeRender "changed cwd + new temp file does not change the rendering"
                  Expect.equal afterRender beforeRender "deleting the temp file does not change the rendering"
                  Expect.equal (excerptContent duringExcerpt) (excerptContent beforeExcerpt) "excerpt capture is cwd-independent"
                  Expect.equal (excerptTruncation duringExcerpt) (excerptTruncation beforeExcerpt) "excerpt truncation is cwd-independent"
              finally
                  Directory.SetCurrentDirectory originalCwd

                  try
                      Directory.Delete(tempDir, true)
                  with _ ->
                      ()
          }

          test "repeated renders at different wall-clock moments are byte-identical" {
              let request = requestOf baseInstructions [ digestPayload "h1" ]
              let a = PromptIsolation.render request |> PromptIsolation.renderedValue
              System.Threading.Thread.Sleep 5
              let b = PromptIsolation.render request |> PromptIsolation.renderedValue
              Expect.equal a b "no clock influence on the rendering"
          } ]
