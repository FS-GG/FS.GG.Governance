// Curated public signature contract for the drift-proof published contract (F06).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Contract.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Contract.fs body exists (Principle I). The shape
// mirrors docs/governance-design/rule-edsl.md ("the published contract") — the catalog of
// rules folded into the document that answers "what does this enforce?".
//
// The whole point is DRIFT-PROOF: each entry's `Statement` IS `Check.render` of the
// rule's check (F03), NOT a separately authored string — so the contract is the rendered
// selector itself and CANNOT diverge from what is evaluated (FR-006, SC-005). It folds
// the F04 `CheckRule<'fact>` catalog and reuses F03 `Check.render`; it performs NO I/O and
// runs NO probe (rendering never executes `Eval`). The output is DOMAIN-NEUTRAL: it drops
// `'fact`, carrying only the rule's id, severity, spec source, and rendered statement
// (FR-012). Emitting it as JSON is `Json.ofContract`/`Json.toContract` (Json.fsi); this
// module produces the contract value and its human/agent-readable text. Zero new deps.

namespace FS.GG.Governance.Kernel

/// One published entry per rule in the contract: the rule's identity, how badly a failure
/// matters, the requirement it traces to, and a rendered statement of WHAT it checks.
/// Non-generic (it drops `'fact`) so the contract is domain-neutral (FR-012). `Statement`
/// is `Check.render` of the rule's check — the single source, so it cannot drift (FR-006).
type ContractEntry =
    { Id: RuleId
      Severity: Severity
      Spec: SpecSource
      Statement: string }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Contract =

    /// Fold a catalog of reified rules into the published contract — one `ContractEntry`
    /// per rule, in catalog order, each carrying the rule's `Id`, `Severity`, `Spec`, and a
    /// `Statement` equal to `Check.render` of that rule's `Check` (FR-005, FR-006). Because
    /// the statement IS the rendered selector, changing a rule's check changes its entry
    /// accordingly — the contract tracks what is enforced and cannot drift (SC-005), and
    /// each rule's own entry is independent of catalog order (per-rule rendering, SC-005).
    /// DETERMINISTIC and TOTAL over any catalog including the EMPTY one (which yields the
    /// empty contract) (FR-007, SC-006). Runs no probe and performs no I/O (FR-013, SC-004).
    val ofRules: rules: CheckRule<'fact> list -> ContractEntry list

    /// Render the contract as a deterministic, human- and agent-readable text block — one
    /// stanza per entry naming its id, severity, spec source, and rendered statement.
    /// Deterministic (stable line/field order) and total, including the empty contract
    /// (which renders to the empty string) (FR-007). For the JSON form use
    /// `Json.ofContract` (Json.fsi).
    val render: contract: ContractEntry list -> string
