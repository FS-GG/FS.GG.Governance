// CONTRACT DRAFT (Phase 1) for specs/053-release-gate-rules.
// The shipped Model.fsi (in src/FS.GG.Governance.ReleaseRules/) is authored from this draft and is the SOLE
// declaration of the module's public surface (Constitution Principle II). The matching Model.fs carries NO
// `private`/`internal`/`public` modifiers on top-level bindings — visibility is presence/absence here.
//
// These are the result-vocabulary values the pure release-gate core evaluates and rolls up: the closed
// release rule-kind set, a declared release rule, the typed release facts, the per-rule finding, and the
// whole-release decision. They REUSE F014 `Maturity`/`SurfaceId`, F023 `Severity`/`EnforcementDecision`, and
// F024 `Verdict`/`ExitCodeBasis` rather than redefining them (research D1/D5/D6). No field carries raw YAML,
// host paths, timestamps, a serialized document, a process exit code, or a sensed fact — facts are SUPPLIED as
// typed input (FR-008); sensing, the `fsgg release` host command, and the `release.json` projection are
// following rows.

namespace FS.GG.Governance.ReleaseRules

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── The closed set of release rule kinds (FR-002) ──

    /// The closed enumeration of release expectation families this core recognizes — the families the
    /// roadmap (Phase 13) names. Closed so tests assert exactly one fixture per kind and a later sensing/
    /// projection row can switch on it exhaustively. Additional kinds, if introduced later, extend this set
    /// ADDITIVELY (a new case + a new `factFor` key) without changing existing behavior (Assumptions).
    type ReleaseRuleKind =
        | VersionBump
        | PackageMetadata
        | TemplatePins
        | PublishPlan
        | TrustedPublishing
        | Provenance

    // ── The provided fact state for one rule kind (FR-005, research D3) ──

    /// The tri-state outcome of the governing fact for a rule kind, SUPPLIED as input (sensing is a later
    /// row). `Unmet` and `Unrecoverable` are kept DISTINCT so the finding reason can distinguish "expected
    /// state was not met" from "no recoverable evidence" — both classify the rule `Violated` (fail-safe,
    /// FR-005), neither is ever treated as satisfied.
    ///   • `Met`           — the governing fact is satisfied.
    ///   • `Unmet`         — the fact was recovered and the expectation is NOT satisfied.
    ///   • `Unrecoverable` — the fact is absent / could not be recovered from the provided facts.
    type FactState =
        | Met
        | Unmet
        | Unrecoverable

    // ── A declared release rule (key entity "Release rule (declared)", FR-002, research D6) ──

    /// One declared release expectation: its `Kind`, the governed identity (`Surface` — an F014 `SurfaceId`,
    /// typically a declared `ReleaseSurface`), and its declared enforcement levers — `BaseSeverity` and
    /// `Maturity`, fed VERBATIM into the F023 `EnforcementInput` (research D6). A blocking-at-release rule
    /// declares `BaseSeverity = Blocking` + `Maturity = BlockOnRelease`; relaxing `Maturity` (e.g. to `Warn`)
    /// makes a violation advisory WITHOUT changing its satisfied/violated truth or visibility (FR-010).
    /// Provided as input.
    type ReleaseRule =
        { Kind: ReleaseRuleKind
          Surface: SurfaceId
          BaseSeverity: Severity
          Maturity: Maturity }

    // ── The provided release facts (key entity "Release facts", FR-008, research D3) ──

    /// The typed description of the current release state, SUPPLIED as input. A `Map` from rule kind to the
    /// provided `FactState`: a rule reads the fact for ITS kind; a kind ABSENT from the map resolves to
    /// `Unrecoverable` (⇒ `Violated`, FR-005). Facts for a kind no rule declares are simply never read and
    /// invent no finding (edge case "extra/unrecognized facts"). Sensing the real facts is a later row.
    type ReleaseFacts = { States: Map<ReleaseRuleKind, FactState> }

    // ── The per-rule outcome classification (FR-001) ──

    /// Whether a rule's governing fact was met (`Satisfied`) or not (`Violated`). `Violated` covers BOTH a
    /// recovered-but-unmet fact and an absent/unrecoverable one (FR-005). Closed.
    type RuleOutcome =
        | Satisfied
        | Violated

    // ── The per-rule finding (key entity "Release finding", FR-001/FR-006) ──

    /// The deterministic outcome for ONE declared rule: the rule's `Kind` and governed `Surface`, the
    /// `Outcome` classification, the rule's declared `BaseSeverity` + `Maturity` carried through (FR-003),
    /// and a self-explaining product-neutral `Reason` naming the kind, the surface, and the outcome basis
    /// (research D7). Exactly one per declared rule — never dropped (FR-006) or fabricated. A pure value: no
    /// raw YAML, host paths, timestamps, or product vocabulary beyond the declared ids.
    type ReleaseFinding =
        { Kind: ReleaseRuleKind
          Surface: SurfaceId
          Outcome: RuleOutcome
          BaseSeverity: Severity
          Maturity: Maturity
          Reason: string }

    // ── A finding after enforcement (mirrors F024 `EnforcedItem`, FR-003/FR-004) ──

    /// One release finding paired with the F023 `EnforcementDecision` returned VERBATIM from
    /// `deriveEffectiveSeverity` under the `Release` mode/profile — carrying all six no-hide fields (base
    /// severity echoed unchanged, maturity, run mode, profile, effective severity, reason). The element type
    /// of the `ReleaseDecision` partition lists, mirroring `Ship.Model.EnforcedItem` (research D1).
    type EnforcedReleaseFinding =
        { Finding: ReleaseFinding
          Decision: EnforcementDecision }

    // ── The whole-release decision (key entity "Release verdict / decision", FR-004, research D1) ──

    /// The whole-release rollup. `Blockers`/`Warnings`/`Passing` are the mutually-exclusive, jointly-
    /// exhaustive partition of every enforced finding (FR-004, FR-006, SC-001). A `Satisfied` finding is
    /// never a concern, so it always lands in `Passing`; a `Violated` finding is partitioned by RE-APPLYING
    /// the F024 (base severity, effective severity) partition rule UNCHANGED — the same observable rule F024
    /// applies to its enforced gates/findings (research D1):
    ///   • `Blockers` — `Violated` with effective severity `Blocking`.
    ///   • `Warnings` — `Violated`, base `Blocking` relaxed to effective `Advisory` by the declared maturity
    ///                  (FR-010) — the "advisory violation" the spec keeps visible (US2.3).
    ///   • `Passing`  — every `Satisfied` finding, plus a `Violated` finding whose declared base severity is
    ///                  `Advisory` (F024 never escalates a base-advisory item — still visible, FR-006).
    /// Each list preserves the deterministic finding order (research D4). `Verdict` and `ExitCodeBasis` REUSE
    /// the F024 `Ship.Model` types VERBATIM (FR-004): `Verdict = Fail` iff `Blockers` is non-empty, else
    /// `Pass`; `ExitCodeBasis = Blocked` iff `Fail`, else `Clean`. No serialized document and no process exit
    /// code — only the typed basis (FR-007, FR-008).
    type ReleaseDecision =
        { Verdict: Verdict
          Blockers: EnforcedReleaseFinding list
          Warnings: EnforcedReleaseFinding list
          Passing: EnforcedReleaseFinding list
          ExitCodeBasis: ExitCodeBasis }
