module FS.GG.Governance.Cli.Tests.VerifyVerbTests

// #470: the published `fsgg-governance` tool ships the Verify host. These tests spawn the built CLI host binary (the
// same one the tool package bundles) and prove that `fsgg-governance verify <args>` reaches the Verify host with its
// own argument grammar, exit codes and provider-mode admission, and that the dispatcher's one-shot parser is not
// what answers it. Behaviour of the host itself stays owned by FS.GG.Governance.VerifyCommand.Tests.

open System.IO
open Expecto
open FS.GG.Governance.Cli.Tests.ParserTests.Support

let private sha (c: char) = System.String(c, 64)

/// Provider-mode selection (all five selectors) plus an explicit path scope, aimed at a throwaway repo directory.
let private providerArgs (dir: string) =
    [
        "verify"
        "--repo"
        dir
        "--provider-provenance"
        Path.Combine(dir, "p.json")
        "--provider-provenance-sha256"
        sha 'a'
        "--provider-policy"
        Path.Combine(dir, "pol.json")
        "--provider-policy-sha256"
        sha 'b'
        "--provider-platform"
        "linux"
        "--paths"
        "src/a.fs"
    ]

/// Run the CLI host with the outer-runtime ends set or removed explicitly, so ambient variables cannot decide a test.
let private runWithEnv (work: string option) (cleanup: string option) (args: string list) =
    let set name value =
        match value with
        | Some v -> System.Environment.SetEnvironmentVariable(name, v)
        | None -> System.Environment.SetEnvironmentVariable(name, null)

    let oldWork = System.Environment.GetEnvironmentVariable "FSGG_VERIFY_WORK_SECONDS"

    let oldCleanup =
        System.Environment.GetEnvironmentVariable "FSGG_VERIFY_CLEANUP_SECONDS"

    try
        set "FSGG_VERIFY_WORK_SECONDS" work
        set "FSGG_VERIFY_CLEANUP_SECONDS" cleanup
        runCli args
    finally
        set "FSGG_VERIFY_WORK_SECONDS" (Option.ofObj oldWork)
        set "FSGG_VERIFY_CLEANUP_SECONDS" (Option.ofObj oldCleanup)

let private withTempDir f =
    let dir =
        Path.Combine(Path.GetTempPath(), "fsgg-verify-verb-" + System.Guid.NewGuid().ToString "N")

    Directory.CreateDirectory dir |> ignore

    try
        f dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

[<Tests>]
let tests =
    // Sequenced: the two provider-mode tests set process-wide environment variables for the child they spawn.
    testSequenced
    <| testList
        "Verify verb (#470)"
        [
            test "verify forwards its own usage grammar: an unknown Verify flag exits 2 with the Verify message" {
                let r = runCli [ "verify"; "--bogus" ]
                Expect.equal r.ExitCode 2 (r.Stdout + r.Stderr)
                Expect.stringContains r.Stderr "fsgg verify: unknown flag: --bogus" "Verify host message"
            }

            test "causal control: the same unknown flag on route is answered by the dispatcher parser (64), not Verify" {
                let r = runCli [ "route"; "--bogus" ]
                Expect.equal r.ExitCode 64 (r.Stdout + r.Stderr)
                Expect.isFalse (r.Stderr.Contains "fsgg verify:") "dispatcher output is not the Verify host's"
            }

            test "verify runs the legacy Verify host: a directory that is not a git repository is input-unavailable (3)" {
                withTempDir (fun dir ->
                    let r = runCli [ "verify"; "--repo"; dir ]
                    Expect.equal r.ExitCode 3 (r.Stdout + r.Stderr)
                    Expect.stringContains r.Stderr "notARepository" "legacy Verify diagnostic")
            }

            test "provider mode is hosted: without the outer runtime's ends nothing is admitted or started (3)" {
                withTempDir (fun dir ->
                    let r = runWithEnv None None (providerArgs dir)
                    Expect.equal r.ExitCode 3 (r.Stdout + r.Stderr)
                    Expect.stringContains r.Stderr "FSGG_VERIFY_WORK_SECONDS is not set" "names the missing work end"
                    Expect.isFalse (r.Stderr.Contains "capture owner unavailable") "not the retired standalone refusal")
            }

            test "provider mode is hosted: inconsistent ends are refused before any capture (3)" {
                withTempDir (fun dir ->
                    let r = runWithEnv (Some "90") (Some "60") (providerArgs dir)
                    Expect.equal r.ExitCode 3 (r.Stdout + r.Stderr)
                    Expect.stringContains r.Stderr "precedes its work end" "cleanup before work refused")
            }

            test "provider mode is hosted: admitted ends reach the capture layer, which refuses this request itself (3)" {
                withTempDir (fun dir ->
                    let r = runWithEnv (Some "30") (Some "60") (providerArgs dir)
                    Expect.equal r.ExitCode 3 (r.Stdout + r.Stderr)

                    Expect.stringContains
                        r.Stderr
                        "provider capture request refused"
                        "host formed and submitted the capture request")
            }

            test "an invalid provider selection exits 2 through the Verify usage path" {
                let r = runCli [ "verify"; "--provider-platform"; "linux" ]
                Expect.equal r.ExitCode 2 (r.Stdout + r.Stderr)
                Expect.stringContains r.Stderr "provider selection rejected" "provider usage message"
            }
        ]
