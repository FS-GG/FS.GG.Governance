module FS.GG.Governance.EvidenceReuse.Tests.Main

open Expecto

// Entry point for the Expecto runner. `dotnet test` discovers the [<Tests>]-attributed lists via the
// YoloDev VSTest adapter; this entry point serves `dotnet run`.
[<EntryPoint>]
let main argv = runTestsInAssemblyWithCLIArgs [] argv
