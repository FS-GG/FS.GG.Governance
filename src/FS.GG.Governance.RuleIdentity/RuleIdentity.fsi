// Curated public signature contract for the per-finding rule-identity leaf (068).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching RuleIdentity.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI before any RuleIdentity.fs body exists
// (Principle I). Every binding is PURE and TOTAL (FR-002): defined for every input, never throwing,
// reading no clock, filesystem, git, environment, or network, and byte-for-byte identical for identical
// input regardless of evaluation time, machine, process, profile, or run mode. The leaf computes NO
// hash and emits NO verdict — it is pure source-prefixing. The wrapped token names the rule's SOURCE
// class via a disjoint prefix, so a boundary id is always distinguishable from a catalog-gate id
// (FR-008). The id is invariant across profile, run mode, and message text (FR-003, FR-009): it is a
// pure function of the rule's structural identity, never of effective severity, profile, mode, or
// message.

namespace FS.GG.Governance.RuleIdentity

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RuleIdentity =

    /// A stable, deterministic identifier for the rule (or typed gate) that produced a finding. The
    /// wrapped string is the source-prefixed wire token; the leading prefix names the rule's source class
    /// and makes a boundary id distinguishable from a catalog-gate id (FR-008). Profile/mode/message-
    /// independent: it is a pure function of the rule's structural identity (FR-003, FR-009).
    type RuleId = RuleId of string

    /// Catalog typed gate → `gate:<domain>:<check>`. `gateId` is the existing `Gates.gateIdValue` string.
    val gate: gateId: string -> RuleId

    /// Kernel boundary finding → `boundary:<findingToken>`. `findingToken` is `Findings.findingIdToken id`
    /// (e.g. `"boundary:unknownGovernedPath"`).
    val boundary: findingToken: string -> RuleId

    /// Surface-check finding → `surface:<domain>:<code>`. `domain` is `SurfaceChecks.checkDomainToken`,
    /// `code` is the surface finding's `Code`.
    val surface: domain: string -> code: string -> RuleId

    /// Release-rule finding → `release:<kind>`. `kindToken` is the `ReleaseRuleKind` wire token. Authored
    /// + unit-tested for completeness; emitted by no current writer (future-proofing, never dead-by-accident).
    val release: kindToken: string -> RuleId

    /// Disclosed marker for a finding with no rule of record (FR-010) → `unattributed:<reason>`. Never
    /// produced on a normal projection path (asserted by test); never an empty id.
    val unattributed: reason: string -> RuleId

    /// The stable wire token (the source-prefixed string), for JSON emission, messages, and tests. Total.
    val ruleIdToken: id: RuleId -> string
