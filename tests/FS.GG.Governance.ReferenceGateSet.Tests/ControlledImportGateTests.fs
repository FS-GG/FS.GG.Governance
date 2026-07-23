module FS.GG.Governance.ReferenceGateSet.Tests.ControlledImportGateTests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open Expecto
open FS.GG.Governance.Tests.Common

let private repoRoot = RepositoryHelpers.repoRoot

let private gateScript =
    Path.Combine(repoRoot, "samples", "sdd-reference-gate-set", ".fsgg", "controlled-imports.fsx")

let private appendUInt64BigEndian (hash: IncrementalHash) (value: uint64) =
    let bytes = BitConverter.GetBytes value

    if BitConverter.IsLittleEndian then
        Array.Reverse bytes

    hash.AppendData bytes

// Independent contract oracle for the v1 directory digest. It deliberately does not load or call
// the production script: the test computes the documented bytes and hands only the resulting pin
// to the real CLI.
let private treeDigest directory =
    let files =
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        |> Array.map (fun path -> Path.GetRelativePath(directory, path).Replace('\\', '/'), path)
        |> Array.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))

    use hash = IncrementalHash.CreateHash HashAlgorithmName.SHA256

    for relative, path in files do
        let pathBytes = Encoding.UTF8.GetBytes relative
        let content = File.ReadAllBytes path
        appendUInt64BigEndian hash (uint64 pathBytes.LongLength)
        hash.AppendData pathBytes
        appendUInt64BigEndian hash (uint64 content.LongLength)
        hash.AppendData content

    hash.GetHashAndReset()
    |> Convert.ToHexString
    |> fun text -> text.ToLowerInvariant()

let private fileDigest path =
    File.ReadAllBytes path
    |> SHA256.HashData
    |> Convert.ToHexString
    |> fun text -> text.ToLowerInvariant()

let private tempRoot () =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-controlled-import-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
    root

let private writeManifest root entries =
    let body = String.concat ",\n" entries

    File.WriteAllText(
        Path.Combine(root, ".fsgg", "controlled-imports.json"),
        $"{{\n  \"schemaVersion\": 1,\n  \"imports\": [\n{body}\n  ]\n}}\n"
    )

let private directoryEntry destination digest =
    $"""    {{
      "kind": "directory",
      "destinationPath": "{destination}",
      "upstreamRepository": "https://example.invalid/upstream",
      "upstreamRevision": "0123456789abcdef0123456789abcdef01234567",
      "upstreamPath": "content",
      "license": "MIT",
      "importMethod": "archive-copy",
      "sha256": "{digest}"
    }}"""

let private fileEntry destination digest =
    $"""    {{
      "kind": "file",
      "destinationPath": "{destination}",
      "upstreamRepository": "https://example.invalid/upstream",
      "upstreamRevision": "0123456789abcdef0123456789abcdef01234567",
      "upstreamPath": "NOTICE",
      "license": "MIT",
      "importMethod": "archive-copy",
      "sha256": "{digest}"
    }}"""

let private runGate root extraArgs =
    let psi = ProcessStartInfo "dotnet"
    psi.ArgumentList.Add "fsi"
    psi.ArgumentList.Add gateScript
    psi.ArgumentList.Add "--"
    psi.ArgumentList.Add "--root"
    psi.ArgumentList.Add root
    psi.ArgumentList.Add "--manifest"
    psi.ArgumentList.Add ".fsgg/controlled-imports.json"
    extraArgs |> List.iter psi.ArgumentList.Add
    psi.WorkingDirectory <- root
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    match Process.Start psi with
    | null -> failwith "dotnet fsi did not start"
    | child ->
        use child = child
        let output = child.StandardOutput.ReadToEnd()
        let error = child.StandardError.ReadToEnd()
        child.WaitForExit()
        child.ExitCode, output, error

let private withTemp testBody =
    let root = tempRoot ()

    try
        testBody root
    finally
        try
            Directory.Delete(root, true)
        with _ ->
            ()

[<Tests>]
let controlledImportGate =
    testSequenced
    <| testList
        "ControlledImportGate"
        [
          test "directory tree verifies before a descendant receives the exemption" {
              withTemp (fun root ->
                  let tree = Path.Combine(root, "vendor", "content")
                  Directory.CreateDirectory(Path.Combine(tree, "nested")) |> ignore
                  File.WriteAllBytes(Path.Combine(tree, "alpha.bin"), [| 0uy; 10uy; 255uy |])
                  File.WriteAllText(Path.Combine(tree, "nested", "beta.txt"), "beta\n")
                  writeManifest root [ directoryEntry "vendor/content" (treeDigest tree) ]
                  File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/content/** -text\n")

                  let code, output, error =
                      runGate root [ "--check-exemption"; "vendor/content/nested/beta.txt" ]

                  Expect.equal code 0 error
                  Expect.stringContains output "GOV-IMPORT-VERIFIED\tdirectory\tvendor/content" "the tree is verified"
                  Expect.stringContains output "GOV-IMPORT-EXEMPT\tvendor/content/nested/beta.txt" "only then is its descendant exempt")
          }

          test "mutating one descendant fails the named digest rule and grants no exemption" {
              withTemp (fun root ->
                  let tree = Path.Combine(root, "vendor", "content")
                  Directory.CreateDirectory tree |> ignore
                  let changed = Path.Combine(tree, "changed.txt")
                  File.WriteAllText(changed, "before\n")
                  writeManifest root [ directoryEntry "vendor/content" (treeDigest tree) ]
                  File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/content/** -text\n")
                  File.AppendAllText(changed, "mutation\n")

                  let code, output, error =
                      runGate root [ "--check-exemption"; "vendor/content/changed.txt" ]

                  Expect.notEqual code 0 "mutated content fails closed"
                  Expect.stringContains error "GOV-IMPORT-DIGEST\tvendor/content" "the named digest rule identifies the imported tree"
                  Expect.isFalse (output.Contains "GOV-IMPORT-EXEMPT") "a failed tree never grants a descendant exemption")
          }

          test "regular file is a distinct typed import kind" {
              withTemp (fun root ->
                  let target = Path.Combine(root, "vendor", "NOTICE")
                  Directory.CreateDirectory(Path.Combine(root, "vendor")) |> ignore
                  File.WriteAllText(target, "upstream notice\n")
                  writeManifest root [ fileEntry "vendor/NOTICE" (fileDigest target) ]
                  File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/NOTICE -text\n")

                  let code, output, error = runGate root [ "--check-exemption"; "vendor/NOTICE" ]

                  Expect.equal code 0 error
                  Expect.stringContains output "GOV-IMPORT-VERIFIED\tfile\tvendor/NOTICE" "file kind verifies by raw-file SHA-256"
                  Expect.stringContains output "GOV-IMPORT-EXEMPT\tvendor/NOTICE" "the exact verified file is exempt")
          }

          test "missing import and absent byte-identity attribute both fail closed" {
              withTemp (fun root ->
                  writeManifest root [ directoryEntry "vendor/missing" (String.replicate 64 "0") ]

                  let code, _, error = runGate root []

                  Expect.notEqual code 0 "missing input is not verification"
                  Expect.stringContains error "GOV-IMPORT-ATTRIBUTES\t.gitattributes" "checkout byte policy is mandatory"
                  Expect.stringContains error "GOV-IMPORT-DIGEST\tvendor/missing" "missing tree is named")
          }

          test "symlink inside a controlled tree is rejected before digest comparison" {
              if OperatingSystem.IsWindows() then
                  Tests.skiptest "Windows symlink creation requires host privileges; the Linux CI leg exercises this contract"
              else
                  withTemp (fun root ->
                      let tree = Path.Combine(root, "vendor", "content")
                      Directory.CreateDirectory tree |> ignore
                      let outside = Path.Combine(root, "outside.txt")
                      File.WriteAllText(outside, "outside\n")
                      let link = Path.Combine(tree, "escape.txt")
                      File.CreateSymbolicLink(link, outside) |> ignore
                      writeManifest root [ directoryEntry "vendor/content" (String.replicate 64 "0") ]
                      File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/content/** -text\n")

                      let code, _, error = runGate root []

                      Expect.notEqual code 0 "symlink escape fails closed"
                      Expect.stringContains error "GOV-IMPORT-SYMLINK\tvendor/content/escape.txt" "the offending link is named")
          }

          test "symlink in the destination ancestry cannot escape the repository" {
              if OperatingSystem.IsWindows() then
                  Tests.skiptest "Windows symlink creation requires host privileges; the Linux CI leg exercises this contract"
              else
                  withTemp (fun root ->
                      let outside = Path.Combine(Path.GetTempPath(), "fsgg-controlled-outside-" + Guid.NewGuid().ToString("N"))
                      Directory.CreateDirectory(Path.Combine(outside, "content")) |> ignore
                      File.WriteAllText(Path.Combine(outside, "content", "outside.txt"), "outside\n")
                      let vendor = Path.Combine(root, "vendor")
                      Directory.CreateDirectory vendor |> ignore
                      let link = Path.Combine(vendor, "escaped")
                      Directory.CreateSymbolicLink(link, outside) |> ignore
                      writeManifest root [ directoryEntry "vendor/escaped/content" (String.replicate 64 "0") ]
                      File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/escaped/content/** -text\n")

                      try
                          let code, _, error = runGate root []
                          Expect.notEqual code 0 "ancestor symlink escape fails closed"
                          Expect.stringContains error "GOV-IMPORT-SYMLINK\tvendor/escaped" "the escaping ancestor is named"
                      finally
                          try Directory.Delete(outside, true) with _ -> ())
          }

          test "unreadable descendant is a named read failure" {
              if OperatingSystem.IsWindows() then
                  Tests.skiptest "Unix mode bits are exercised on the Linux CI leg"
              else
                  withTemp (fun root ->
                      let tree = Path.Combine(root, "vendor", "content")
                      Directory.CreateDirectory tree |> ignore
                      let unreadable = Path.Combine(tree, "secret.bin")
                      File.WriteAllText(unreadable, "secret")
                      let pin = treeDigest tree
                      writeManifest root [ directoryEntry "vendor/content" pin ]
                      File.WriteAllText(Path.Combine(root, ".gitattributes"), "vendor/content/** -text\n")
                      File.SetUnixFileMode(unreadable, UnixFileMode.None)

                      try
                          let code, _, error = runGate root []
                          Expect.notEqual code 0 "unreadable bytes fail closed"
                          Expect.stringContains error "GOV-IMPORT-READ\tvendor/content" "the controlled tree is named"
                      finally
                          File.SetUnixFileMode(
                              unreadable,
                              UnixFileMode.UserRead ||| UnixFileMode.UserWrite
                          ))
          }

          test "repo-relative destination cannot escape through parent traversal" {
              withTemp (fun root ->
                  writeManifest root [ fileEntry "../outside.bin" (String.replicate 64 "0") ]
                  File.WriteAllText(Path.Combine(root, ".gitattributes"), "../outside.bin -text\n")

                  let code, _, error = runGate root []

                  Expect.notEqual code 0 "parent traversal fails closed"
                  Expect.stringContains error "GOV-IMPORT-PATH" "the path rule reports the escape")
          }
        ]
