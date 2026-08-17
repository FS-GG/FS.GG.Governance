open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

type ProductFact =
    | PolicyPresent of bool
    | Governance of RuleOutcome

type ProductArtifact = PolicyFile
type ProductChange = { Paths: Set<string> }

let identify fact =
    match fact with
    | PolicyPresent value -> FactId(sprintf "policy:%b" value)
    | Governance(Decided(RuleId rule, _)) -> FactId("governance:decided:" + rule)
    | Governance(NeedsReview request) -> FactId("governance:needs-review:" + request.Key)
    | Governance(Reviewed review) -> FactId("governance:reviewed:" + review.Key)
    | Governance(Escalated(RuleId rule)) -> FactId("governance:escalated:" + rule)

let policyRef = { Kind = "rules"; Key = "policy" }

let policyCheck: Check<ProductFact> =
    Check.probe "policy-present" [ policyRef ] [] (fun facts ->
        if facts |> List.exists (fun fact -> fact.Value = PolicyPresent true) then Met
        else Unmet "policy missing")

let policyRule =
    CheckRule.rule
        (RuleId "product-policy")
        Deterministic
        { Document = "product-rules"; Section = "policy" }
        policyCheck
    |> Result.map CheckRule.blocking
    |> function
        | Ok rule -> rule
        | Error error -> failwithf "rule construction failed: %A" error

let adapter: Adapter<ProductFact, ProductArtifact, ProductChange> =
    { Identify = identify
      ToRef = fun PolicyFile -> policyRef
      Probes =
        [ match policyCheck with
          | Atom probe -> probe
          | _ -> failwith "expected atomic policy check" ]
      Rules = [ policyRule ]
      Fences = [ { Name = "rules-corpus"; Trips = fun change -> change.Paths.Contains "rules.fs" } ]
      Bridge =
        { Judge = { ModelId = "package-consumer"; Version = "1" }
          ArtifactHash = fun _ _ -> ""
          Embed = Governance
          Project = function Governance outcome -> Some outcome | PolicyPresent _ -> None } }

let supplied: FactSet<ProductFact> =
    [ { Id = FactId "policy:true"; Value = PolicyPresent true; Provenance = [] } ]

let evaluation = FixedPoint.evaluate adapter.Identify (Adapter.toRules adapter) supplied
let verdict = Check.eval supplied policyCheck
let explanation = Check.explain supplied policyCheck
let route = Route.route adapter.Fences adapter.Rules Gate { Paths = set [ "rules.fs" ] }

printfn "render=%s" (Check.render policyCheck)
printfn "hash=%s" (Check.hash policyCheck)
printfn "verdict=%A" verdict
printfn "explanation=%A" (Explanation.verdict explanation)
printfn "evaluated-facts=%d" evaluation.Facts.Length
printfn "route-stakes=%A" route.Stakes
printfn "route-blocking=%d" route.Blocking.Length
