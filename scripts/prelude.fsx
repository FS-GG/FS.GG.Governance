// FSI entry point for the Principle I design pass (T010) and ad-hoc exploration.
//
//   dotnet build src/FS.GG.Governance.Kernel
//   dotnet fsi scripts/prelude.fsx
//
// It references the BUILT kernel assembly and opens the public namespace so the
// curated contract is exercised exactly as a downstream consumer would (SC-004).

#r "../src/FS.GG.Governance.Kernel/bin/Debug/net10.0/FS.GG.Governance.Kernel.dll"

open FS.GG.Governance.Kernel

// A toy 'fact for the sketch: a plain string. `identify` is the sole identity
// authority (D3) — here, the string itself names the fact.
let identify (s: string) = FactId s

let supplied: FactSet<string> =
    [ { Id = FactId "A"; Value = "A"; Provenance = [] } ]

// Two chained monotonic rules: A ⇒ B, B ⇒ C.
let ruleAB: Rule<string> =
    { Id = RuleId "A=>B"
      Description = "A implies B"
      Apply =
        fun facts ->
            if facts |> List.exists (fun f -> f.Value = "A") then
                [ { Id = FactId "B"; Value = "B"; Provenance = [ { Rule = RuleId "A=>B"; Inputs = [ FactId "A" ]; Note = "A implies B" } ] } ]
            else [] }

let ruleBC: Rule<string> =
    { Id = RuleId "B=>C"
      Description = "B implies C"
      Apply =
        fun facts ->
            if facts |> List.exists (fun f -> f.Value = "B") then
                [ { Id = FactId "C"; Value = "C"; Provenance = [ { Rule = RuleId "B=>C"; Inputs = [ FactId "B" ]; Note = "B implies C" } ] } ]
            else [] }

let result = FixedPoint.evaluate identify [ ruleAB; ruleBC ] supplied

printfn "Facts:  %A" (result.Facts |> List.map (fun f -> f.Id))
printfn "Rounds: %d" result.Rounds
for f in result.Facts do
    printfn "  %A  provenance=%A" f.Id f.Provenance
