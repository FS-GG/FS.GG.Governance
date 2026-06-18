// Implementation of the adapter SPI & lift combinators (F09). Visibility lives in
// Adapter.fsi (Principle II) — NO `private`/`internal`/`public` on top-level bindings
// here; the signature file hides every binding it does not declare (so the `projectFacts`
// helper below is internal automatically). Pure values and total folds: no I/O, no
// Model/Msg/Effect, no interpreter (Principle IV N/A). Reuses F04 `CheckRule`/`Bridge`,
// F03 `Check`/`Probe`, F07 `Fence`, F01 `Rule`/`FactSet`; zero new dependencies.

namespace FS.GG.Governance.Adapters.Spi

open FS.GG.Governance.Kernel

type Adapter<'fact, 'artifact, 'change> =
    { Identify: 'fact -> FactId
      ToRef: 'artifact -> ArtifactRef
      Probes: Probe<'fact> list
      Rules: CheckRule<'fact> list
      Fences: Fence<'change> list
      Bridge: Bridge<'fact> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Adapter =

    let toRules (adapter: Adapter<'fact, 'artifact, 'change>) : Rule<'fact> list =
        adapter.Rules |> List.map (CheckRule.toRule adapter.Bridge)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Lift =

    /// Project a `'big` fact set onto `'small`, KEEPING each surviving assertion's `Id` and
    /// `Provenance` and re-mapping only its `Value`; drop the assertions `project` rejects.
    /// This is the single point the lift touches the fact channel (research D3/D4). Not in
    /// Adapter.fsi, so the signature file keeps it internal.
    let projectFacts (project: 'big -> 'small option) (facts: FactSet<'big>) : FactSet<'small> =
        facts
        |> List.choose (fun fa ->
            project fa.Value
            |> Option.map (fun v -> { Id = fa.Id; Value = v; Provenance = fa.Provenance }))

    let check (project: 'big -> 'small option) (check: Check<'small>) : Check<'big> =
        let pf bigFacts = projectFacts project bigFacts
        // Re-target ONLY each probe's run-only `Eval` channel; keep the declared shape
        // (Name/Reads/Args) and the combinator structure untouched, so render/hash/reads/
        // isReified are byte-for-byte invariant — the F04 cache key does not move (law L1).
        let rec go c =
            match c with
            | Atom probe ->
                Atom
                    { Name = probe.Name
                      Reads = probe.Reads
                      Args = probe.Args
                      Eval = fun bigFacts -> probe.Eval (pf bigFacts) }
            | All checks -> All(checks |> List.map go)
            | Any checks -> Any(checks |> List.map go)
            | Not c -> Not(go c)
            | Implies (a, b) -> Implies(go a, go b)
            // Opaque stays opaque (name preserved, no inspectable structure), so `isReified`
            // stays false and a lifted Opaque rule still routes to review (law L1, US2-3).
            | Opaque (name, eval) -> Opaque(name, fun bigFacts -> eval (pf bigFacts))

        go check

    let checkRule (project: 'big -> 'small option) (rule: CheckRule<'small>) : CheckRule<'big> =
        { Id = rule.Id
          Tier = rule.Tier
          Spec = rule.Spec
          Severity = rule.Severity
          Check = check project rule.Check
          Question = rule.Question }

    let rule (inject: 'small -> 'big) (project: 'big -> 'small option) (rule: Rule<'small>) : Rule<'big> =
        { Id = rule.Id
          Description = rule.Description
          Apply =
            fun bigFacts ->
                projectFacts project bigFacts
                |> rule.Apply
                |> List.map (fun fa -> { Id = fa.Id; Value = inject fa.Value; Provenance = fa.Provenance }) }

    let fence (narrow: 'big -> 'small) (fence: Fence<'small>) : Fence<'big> =
        { Name = fence.Name
          Trips = fence.Trips << narrow }
