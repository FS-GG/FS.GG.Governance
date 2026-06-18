module FS.GG.Governance.Kernel.Tests.Main

open Expecto

// Entry point for `dotnet run` (the Expecto runner). `dotnet test` discovers the
// [<Tests>]-attributed lists via the YoloDev VSTest adapter without using this.
[<EntryPoint>]
let main argv = runTestsInAssemblyWithCLIArgs [] argv
