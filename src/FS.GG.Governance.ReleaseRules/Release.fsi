// CONTRACT DRAFT (Phase 1) for specs/053-release-gate-rules.
// The shipped Release.fsi (in src/FS.GG.Governance.ReleaseRules/) is authored from this draft and is the SOLE
// declaration of the module's public surface (Constitution Principle II). The matching Release.fs carries NO
// `private`/`internal`/`public` modifiers — the per-rule classifier, the `EnforcementInput` builder, and the
// three-way partition helper live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Release.fs body exists
// (Principle I). `evaluate`/`rollup`/`evaluateRelease` are PURE and TOTAL (FR-007): no I/O, no git, no clock,
// no process, no document, never throw, byte-for-byte identical for identical input (SC-003). They REUSE F023
// `deriveEffectiveSeverity` for every per-finding decision (FR-003) and the F024 `Verdict`/`ExitCodeBasis`
// result types VERBATIM; they re-apply the F024 partition rule but do NOT call `Ship.rollup` (release rules
// are not `RouteResult` gates/findings — research D1). They sense NO fact, spawn NO process, and emit NO
// document (FR-008) — facts are supplied as typed input.

namespace FS.GG.Governance.ReleaseRules

open FS.GG.Governance.ReleaseRules.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Release =

    /// The stable wire token for a `ReleaseRuleKind` (e.g. `VersionBump` → `"versionBump"`,
    /// `TrustedPublishing` → `"trustedPublishing"`). Deterministic and total — the same stable-token idiom as
    /// F017 `findingIdToken` / F014 `diagnosticIdToken`, used in finding reasons and any later JSON.
    val releaseRuleKindToken: kind: ReleaseRuleKind -> string

    /// The intrinsic ordinal of a release rule kind (`VersionBump` 0 .. `ApiCompatibility` 6), in the order
    /// the closed `ReleaseRuleKind` is declared. Total; exposed because it is the primary deterministic sort
    /// key for findings (research D4).
    val releaseRuleKindOrdinal: kind: ReleaseRuleKind -> int

    /// The provided `FactState` governing a rule kind: the value mapped to `kind` in `facts.States`, or
    /// `Unrecoverable` when `kind` is ABSENT from the map (fail-safe — an unsupplied fact is treated as
    /// unrecoverable, never satisfied — FR-005, research D3). Total; the single lookup `evaluate` uses, also
    /// exposed so a caller/test can assert the fact a rule will see.
    val factFor: facts: ReleaseFacts -> kind: ReleaseRuleKind -> FactState

    /// Evaluate each declared rule against the provided facts and emit EXACTLY ONE finding per rule (US1,
    /// FR-001). For each rule, `factFor facts rule.Kind` decides the outcome: `Met` ⇒ `Satisfied`; `Unmet`
    /// and `Unrecoverable` ⇒ `Violated` (fail-safe, FR-005) — with a self-explaining, product-neutral
    /// `Reason` naming the kind token, the governed surface, and the outcome basis (research D7). The rule's
    /// declared `BaseSeverity` + `Maturity` are carried onto the finding unchanged (FR-003).
    ///
    /// The result is sorted by a stable composite key — `releaseRuleKindOrdinal` then the `Surface` id string
    /// — so identical inputs yield a byte-identical list and re-ordering the input rules never changes it
    /// (FR-007, SC-003, research D4). The output rule-kind MULTISET equals the declared rule-kind multiset:
    /// nothing dropped, nothing fabricated (FR-006, SC-001) — duplicate rules of the same kind each yield
    /// their own finding, and facts for a kind no rule declares invent nothing (edge cases).
    ///
    /// TOTAL: defined for every rule set (including the EMPTY set ⇒ the empty finding list) and every facts
    /// map. Never throws. PURE: no I/O, clock, process, or document (FR-007, FR-008).
    val evaluate: rules: ReleaseRule list -> facts: ReleaseFacts -> ReleaseFinding list

    /// Roll a finding list up into one whole-release decision (US2, FR-004). For each finding, build the F023
    /// `EnforcementInput` from its declared `BaseSeverity` + `Maturity` under `RunMode.Release` and
    /// `Profile.Release` (FR-003, fixed — research D2) and call `deriveEffectiveSeverity` VERBATIM to get the
    /// `EnforcementDecision`. Then partition by RE-APPLYING the F024 partition rule UNCHANGED (research D1):
    ///   • a `Satisfied` finding ⇒ `Passing` (a satisfied rule is never a concern);
    ///   • a `Violated` finding with effective `Blocking` ⇒ `Blockers`;
    ///   • a `Violated` finding, base `Blocking` relaxed to effective `Advisory` by maturity ⇒ `Warnings`
    ///     (visible, never dropped — FR-006, FR-010, US2.3);
    ///   • a `Violated` finding with base `Advisory` ⇒ `Passing` (F024 never escalates — still visible).
    /// Each partition list preserves the input finding order (stable). `Verdict` and `ExitCodeBasis` REUSE the
    /// F024 `Ship.Model` types VERBATIM: `Verdict = Fail` iff `Blockers` is non-empty, else `Pass`;
    /// `ExitCodeBasis = Blocked` iff `Fail`, else `Clean` (FR-004).
    ///
    /// `|Blockers| + |Warnings| + |Passing| = |findings|` — every finding is placed exactly once, none
    /// dropped (FR-006, SC-001). TOTAL: defined for every finding list (including the EMPTY list ⇒ `Pass`,
    /// empty partition, `Clean` — edge case "empty rule set"). Never throws. PURE (FR-007).
    val rollup: findings: ReleaseFinding list -> ReleaseDecision

    /// The whole release gate in one call: `evaluate rules facts |> rollup`. The single entry the future
    /// `fsgg release` host row will wire (sensing → this → exit code). PURE and TOTAL (FR-007); facts in,
    /// decision out; no I/O, process, or document (FR-008).
    val evaluateRelease: rules: ReleaseRule list -> facts: ReleaseFacts -> ReleaseDecision
