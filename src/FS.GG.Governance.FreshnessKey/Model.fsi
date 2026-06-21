// Curated public signature contract for the freshness-input vocabulary of the freshness-key core (F029).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, comparable values the `FreshnessKey.compute`
// projection fingerprints: the closed set of inputs that, if changed, invalidate prior evidence. They
// REUSE the F014 typed-fact newtypes (`CheckId`, `DomainId`, `CommandId`, `EnvironmentClass`) — the very
// newtypes the F018 gate `FreshnessKey` is built from — rather than redefining them or referencing the
// Gates record wrapper (research D1). No field carries raw bytes, host paths, clock readings, or product
// vocabulary; the Phase-11 hashes/versions/revisions are opaque single-case strings supplied by the edge
// (FR-008). Base/head use a LOCAL `Revision` newtype, NOT Snapshot's `CommitId`, so the pure core never
// references the git-sensing Snapshot assembly (research D3).

namespace FS.GG.Governance.FreshnessKey

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── New opaque newtypes (the Phase-11 additions, research D2/D3) ──

    /// A supplied digest of the rule that produced the evidence. Opaque and comparable; the actual digest
    /// is computed at the edge and passed in as data (FR-008). No validation, no parsing — an empty string
    /// is a literal value (FR-011).
    type RuleHash = RuleHash of string

    /// A supplied digest of ONE artifact the evidence covers. The key fingerprints the SET of these
    /// (order and duplication never matter — FR-004). Opaque, comparable, edge-supplied.
    type ArtifactHash = ArtifactHash of string

    /// A supplied version stamp of the gate's command. Optional in `FreshnessInputs` (a command-less gate
    /// has no command version). Opaque, comparable, edge-supplied.
    type CommandVersion = CommandVersion of string

    /// A supplied version stamp of the generator/tool that produced the evidence. Opaque, comparable,
    /// edge-supplied.
    type GeneratorVersion = GeneratorVersion of string

    /// A resolved revision identity (a base or head revision). LOCAL to this core so the pure key never
    /// references the git-sensing Snapshot assembly; the later edge maps `Snapshot.Model.CommitId` ->
    /// `Revision` (research D3). Opaque, comparable, edge-supplied.
    type Revision = Revision of string

    // ── Key entity: the closed freshness-input set (FR-001) ──

    /// The closed, typed set of inputs that determine whether prior evidence may be reused (FR-001). Every
    /// category is named and type-checked. `CoveredArtifacts` is a list compared as a SET (order and
    /// duplication ignored — FR-004). `Command`/`CommandVersion` are `None` together for a command-less
    /// gate (the contracts treat them as two independently-flippable categories). `Cost` is DELIBERATELY
    /// ABSENT (research D5): cost does not affect reuse validity. The carried-identity fields reuse the
    /// F014 newtypes verbatim (FR-009).
    type FreshnessInputs =
        { // ── carried gate identity (F014 newtypes, research D1/D5) ──
          Check: CheckId
          Domain: DomainId
          Command: CommandId option
          Environment: EnvironmentClass
          // ── Phase-11 additions ──
          RuleHash: RuleHash
          CoveredArtifacts: ArtifactHash list
          CommandVersion: CommandVersion option
          GeneratorVersion: GeneratorVersion
          Base: Revision
          Head: Revision }

    // ── Key entity: the computed fingerprint ──

    /// The deterministic, byte-stable, comparable fingerprint produced from a `FreshnessInputs` value by
    /// `FreshnessKey.compute`. Equal `Key`s mean "same world, reuse permitted"; different `Key`s mean
    /// "something that matters changed, reuse forbidden". The wrapped string is the canonical tagged,
    /// length-prefixed rendering (contracts/freshness-key-format.md), so equality is exact byte equality
    /// and the value is portable across runs and machines.
    ///
    /// NAMING NOTE (avoid confusion): this computed-fingerprint type is `Key` — NOT `FreshnessKey`. The
    /// name `FreshnessKey` is taken twice elsewhere: it is THIS project's operations module
    /// (`FS.GG.Governance.FreshnessKey`) and it is also F018's CARRIED MVP identity record
    /// (`Gates.Model.FreshnessKey`: the check/domain/cost/environment/command a gate carries). Both are
    /// different concepts from this computed `Key`.
    type Key = Key of string

    // ── Key entity: the comparable categories (the no-hide explainer's vocabulary) ──

    /// The closed enumeration of comparable input categories, returned by `FreshnessKey.diff` to name
    /// exactly what changed between two inputs (FR-007, the no-hide requirement). One case per comparable
    /// field, in the fixed key-encoding order.
    type InputCategory =
        | CheckIdentity
        | DomainIdentity
        | CommandIdentity
        | EnvironmentClassCat
        | RuleHashCat
        | CoveredArtifactsCat
        | CommandVersionCat
        | GeneratorVersionCat
        | BaseRevisionCat
        | HeadRevisionCat

    /// Stable, human-readable wire token for an `InputCategory` (for `diff` output and messages).
    /// Deterministic, total, and INJECTIVE over the 10 cases. This readable vocabulary
    /// (`ruleHash`/`coveredArtifacts`/…) is DELIBERATELY DISTINCT from the terse encoding tags inside the
    /// key string (`rule`/`art`/…, contracts/freshness-key-format.md) — see the table in
    /// contracts/freshness-key-api.md.
    val categoryToken: category: InputCategory -> string
