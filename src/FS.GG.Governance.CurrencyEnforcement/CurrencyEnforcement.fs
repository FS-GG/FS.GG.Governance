// The F070 stale-generated-view finding vocabulary + the bridge into the F023 enforcement truth table.
// Visibility lives in CurrencyEnforcement.fsi (Constitution Principle II); this file carries NO top-level
// access modifiers. PURE total functions over closed DUs with exhaustive, wildcard-free matches: no I/O, no
// clock, no environment. `decideCurrency` re-expresses the F057 per-view currency decision over the F029
// `FreshnessKey.diff` comparator verbatim; `enforcementInputOf`/`decisionOf` only assemble/fold the existing
// F023 input — they add NO truth-table logic (FR-003).

namespace FS.GG.Governance.CurrencyEnforcement

open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CurrencyEnforcement =

    type StaleCause =
        | SourceDrift of drifted: InputCategory list
        | Undeterminable of reason: string

    type CurrencyFinding =
        { ViewId: string
          Kind: ViewKind
          Cause: StaleCause
          BaseSeverity: Severity
          Maturity: Maturity }

    // Hidden (absent from the .fsi): a `FreshnessInputs` whose 8 non-source fields are fixed filler so that
    // `FreshnessKey.diff` of two comparands differs ONLY in the source-digest set + generator version — the
    // two currency-relevant categories for a generated view (research D1/D4: revisions held equal). Both
    // comparands share identical filler, so diff can only report CoveredArtifactsCat / GeneratorVersionCat.
    let currencyComparand (artifacts: ArtifactHash list) (version: GeneratorVersion) : FreshnessInputs =
        { Check = CheckId "view-currency"
          Domain = DomainId "view-currency"
          Command = None
          Environment = LocalOrCi
          RuleHash = RuleHash ""
          CoveredArtifacts = artifacts
          CommandVersion = None
          GeneratorVersion = version
          Base = Revision ""
          Head = Revision "" }

    let decideCurrency
        (entry: GenerationEntry)
        (recorded: (ArtifactHash list * GeneratorVersion) option)
        (sensed: Result<ArtifactHash list * GeneratorVersion, string>)
        : ViewDecision =
        let decide status drifted =
            { Entry = entry
              Status = status
              Drifted = drifted }

        match recorded, sensed with
        // Never fabricate currency (FR-008): a sense failure is StaleUnresolved, never Current.
        | _, Error reason -> decide (StaleUnresolved reason) []
        // A view with no recorded provenance lock is undeterminable — never silently passed.
        | None, Ok _ -> decide (StaleUnresolved "no recorded provenance for the generated view") []
        | Some(recordedArtifacts, recordedVersion), Ok(sensedArtifacts, sensedVersion) ->
            let drifted =
                FreshnessKey.diff
                    (currencyComparand recordedArtifacts recordedVersion)
                    (currencyComparand sensedArtifacts sensedVersion)

            if List.isEmpty drifted then
                decide Current []
            else
                decide (WouldRegenerate drifted) drifted

    let findingsOf (maturity: Maturity option) (views: ViewDecision list) : CurrencyFinding list =
        match maturity with
        | None -> []
        | Some configured ->
            views
            |> List.choose (fun view ->
                let finding cause =
                    Some
                        { ViewId = view.Entry.ViewId
                          Kind = view.Entry.Kind
                          Cause = cause
                          BaseSeverity = Blocking
                          Maturity = configured }

                match view.Status with
                | Current
                | NotEvaluated -> None
                | Regenerated drifted
                | WouldRegenerate drifted -> finding (SourceDrift drifted)
                | StaleUnresolved reason -> finding (Undeterminable reason))

    // A refresh.yml that is PRESENT but cannot be read or parsed yields no dial (Maturity) — yet it must
    // NEVER silently pass (FR-008): dropping it lets a configured currency-enforcement dial vanish. This is
    // the fail-closed finding the sensing edge emits for that case. `Undeterminable`/`Blocking` mirror the
    // per-view unresolved path; the Maturity is the STRICTEST dial (`BlockOnPr`) because the configured value
    // is unknowable when the file can't be read — a corrupt manifest must block from PR onward until fixed,
    // rather than be trusted at an also-unknowable severity. Not gated by `findingsOf` (which needs a dial).
    let manifestUnreadableFinding (reason: string) : CurrencyFinding =
        { ViewId = ".fsgg/refresh.yml"
          Kind = Other "refresh-manifest"
          Cause = Undeterminable reason
          BaseSeverity = Blocking
          Maturity = BlockOnPr }

    let enforcementInputOf (finding: CurrencyFinding) (mode: RunMode) (profile: Profile) : EnforcementInput =
        { BaseSeverity = finding.BaseSeverity
          Maturity = finding.Maturity
          Mode = mode
          Profile = profile }

    let decisionOf (finding: CurrencyFinding) (mode: RunMode) (profile: Profile) : EnforcementDecision =
        deriveEffectiveSeverity (enforcementInputOf finding mode profile)

    let staleCauseToken (cause: StaleCause) : string =
        match cause with
        | SourceDrift _ -> "source-drift"
        | Undeterminable _ -> "undeterminable"
