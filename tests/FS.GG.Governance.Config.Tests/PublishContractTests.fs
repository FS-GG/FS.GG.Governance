module FS.GG.Governance.Config.Tests.PublishContractTests

open System.Diagnostics
open System.IO
open Expecto
open FS.GG.Governance.Config.Tests.Support

[<Tests>]
let tests =
    testList "ConfigPublishContract" [
        test "actual publisher and fail-closed mutations pass before costly qualification" {
            let script = Path.Combine(repoRoot, "tests/config-package-smoke/publish_contract.py")
            let workflow = Path.Combine(repoRoot, ".github/workflows/publish.yml")
            let start = ProcessStartInfo("python3")
            start.ArgumentList.Add script
            start.ArgumentList.Add workflow
            start.ArgumentList.Add "--mutations"
            start.UseShellExecute <- false
            use child = new Process(StartInfo = start)
            Expect.isTrue (child.Start()) "Python workflow preflight process started"
            if not (child.WaitForExit 15000) then
                child.Kill(true)
                failtest "Config publication static preflight exceeded 15 seconds"
            Expect.equal child.ExitCode 0 "actual workflow contract and mutation controls"
            let archiveStart = ProcessStartInfo("python3")
            archiveStart.ArgumentList.Add(Path.Combine(repoRoot, "tests/config-package-smoke/archive_controls.py"))
            archiveStart.UseShellExecute <- false
            use archiveChild = new Process(StartInfo = archiveStart)
            Expect.isTrue (archiveChild.Start()) "Python archive control process started"
            if not (archiveChild.WaitForExit 15000) then
                archiveChild.Kill(true)
                failtest "synthetic Config archive controls exceeded 15 seconds"
            Expect.equal archiveChild.ExitCode 0 "synthetic archive refusals; real smoke remains separate"
        }
    ]
