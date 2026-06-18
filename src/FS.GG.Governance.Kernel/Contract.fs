namespace FS.GG.Governance.Kernel

// The drift-proof published contract (F06 · US2). Folds an F04 CheckRule<'fact> catalog
// into one ContractEntry per rule whose Statement IS Check.render of the rule's check —
// the single source, so the contract cannot drift (FR-006). Non-generic (drops 'fact) for
// domain-neutrality (FR-012). No visibility modifiers — the surface is Contract.fsi
// (Principle II).

type ContractEntry =
    { Id: RuleId
      Severity: Severity
      Spec: SpecSource
      Statement: string }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Contract =

    let ofRules (rules: CheckRule<'fact> list) : ContractEntry list =
        // One entry per rule, in catalog order. Statement IS Check.render of the rule's
        // check — never a separately authored string — so the contract cannot drift from
        // what is enforced (FR-005/006). Total over the empty catalog (FR-007). render
        // folds structure only; no probe runs, no I/O (R-C4).
        rules
        |> List.map (fun rule ->
            { Id = rule.Id
              Severity = rule.Severity
              Spec = rule.Spec
              Statement = Check.render rule.Check })

    let render (contract: ContractEntry list) : string =
        // One deterministic stanza per entry: "<id> [<severity>] (<document> §<section>)"
        // then the rendered statement indented. Empty contract ⇒ "" (FR-007).
        contract
        |> List.map (fun e ->
            let (RuleId id) = e.Id

            let severity =
                match e.Severity with
                | Advisory -> "advisory"
                | Blocking -> "blocking"

            sprintf "%s [%s] (%s §%s)\n  %s" id severity e.Spec.Document e.Spec.Section e.Statement)
        |> String.concat "\n\n"
