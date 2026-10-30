module FS.GG.Governance.VerifyCommand.Tests.Main

open Expecto

[<EntryPoint>]
let main argv =
    // GOV-423 physical fixture roles re-enter this assembly (see PhysicalFixtureRuntime); never run tests there.
    match PhysicalFixtureRuntime.dispatch argv with
    | Some code -> code
    | None -> runTestsInAssemblyWithCLIArgs [] argv
