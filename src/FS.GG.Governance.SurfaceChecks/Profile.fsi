// THE composed F# constitution profile: the ONE place the four rule packs of epic
// FS-GG/FS.GG.Governance#367 are named, versioned, and resolved against one another
// (docs/decisions/0012-composed-fsharp-constitution-profile.md).
//
// Epic #367 named an integration criterion no child owned — "#366, #368, #369, and #370 are
// integrated into one reusable F# profile/reference gate consumed by generated products and
// ordinary repositories". All four children shipped, all four declared `reference-gates/` in their
// touch-set, and none landed there: four rule packs shipped SEPARATELY, with two incompatible
// finding shapes and no artifact anywhere that enumerated them together. This module is that
// artifact.
//
// Why HERE. `SurfaceChecks` is already the shared cross-domain finding vocabulary every pack must
// agree on (`Model.SurfaceFinding`, `Model.mkFinding`), and it is the lowest project all four packs
// can reach — `DesignChecks` (#366/#369/#370) already references it, and `Inheritance` reaches it
// with one new edge and no cycle. Composing here therefore adds no new package and no new layer:
// the packs read their declared maturity FROM this table rather than hard-coding it, so there is
// exactly one authority, which is the property docs/decisions/0011 established for the org profile
// and this record extends to the constitution packs.
//
// PURE and TOTAL: no I/O, no clock, no exceptions. Every lookup is defined for every input —
// `ruleOwner` on an unknown id yields `None`, never a fabricated owner.

namespace FS.GG.Governance.SurfaceChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Profile =

    /// One of the four F# constitution rule packs epic #367 named. CLOSED: a fifth pack is a
    /// compile error at every match site, never a silent omission from the composed profile.
    type Pack =
        /// FS-GG/FS.GG.Governance#366 — curated `.fsi` surfaces, explicit visibility, XML docs,
        /// surface baselines. Implemented by `DesignChecks.FSharpSurface`.
        | PublicSurface
        /// FS-GG/FS.GG.Governance#368 — idiomatic simplicity and the evidence-bound exception
        /// contract. Implemented by `CodeChecks`.
        | IdiomaticSimplicity
        /// FS-GG/FS.GG.Governance#369 — functional core / effect edge. Implemented by
        /// `DesignChecks.FSharpEffectBoundary`.
        | EffectBoundary
        /// FS-GG/FS.GG.Governance#370 — behavior evidence, generated-contract goldens, safe
        /// failure. Implemented by `DesignChecks.EvidenceBoundary`.
        | EvidenceBoundary

    /// The composed profile's name — the key `Inheritance.ReferenceProfile.checksFor` binds and the
    /// name the generated region in the published `.fsgg/capabilities.yml` carries.
    val profileKey: TemplateProfile

    /// The single capability domain every composed check declares. One domain, so a consumer's
    /// `pathMap` binds the whole profile with one glob rather than four.
    val domain: DomainId

    /// Every pack in the composed profile, in a deterministic order. The composition is exactly
    /// these — `checks` and `collisions` enumerate from this list, so a pack cannot be in the
    /// profile's rule table and absent from its published gate set.
    val packs: Pack list

    /// Stable wire token for a pack — also the `CheckId` its gate carries
    /// (e.g. `public-surface` ⇒ gate id `fsharp:public-surface`).
    val packToken: pack: Pack -> string

    /// The child item that AUTHORED this pack, as an org-qualified issue reference
    /// (e.g. `FS-GG/FS.GG.Governance#366`). This is AC1's traceability: every rule in the composed
    /// profile resolves, through `ruleOwner`, to the child that wrote it.
    val authoringItem: pack: Pack -> string

    /// The complete, closed rule-identity set this pack contributes, ascending. Identities are the
    /// packs' OWN codes, unchanged — #385 deliberately renames nothing, because AC1 asks for
    /// identities that are STABLE, and a composition that renamed them would break every existing
    /// finding, receipt, and `RuleIdentity.surface` wire token to buy nothing.
    val ruleIds: pack: Pack -> string list

    /// The maturity every rule in this pack is emitted at. This is the SINGLE declaration —
    /// `DesignChecks.FSharpSurface` and `DesignChecks.FSharpEffectBoundary` read it from here
    /// rather than hard-coding their own, so the two can no longer drift apart silently.
    val declaredMaturity: pack: Pack -> Maturity

    /// Why this pack binds at that maturity, in one sentence. Recorded as DATA rather than a
    /// comment because the packs disagree on purpose (#366 at `Warn`, #369 at `BlockOnPr`) and a
    /// reader of the composed profile must be able to see that the difference was decided.
    val maturityRationale: pack: Pack -> string

    /// The pack that owns a rule identity, or `None` when no pack in the profile declares it.
    /// TOTAL — an unknown id is `None`, never a guess. This lookup is what makes "whichever pack
    /// loaded last wins" impossible: ownership is declared, not discovered at runtime.
    val ruleOwner: ruleId: string -> Pack option

    /// The resolved maturity for a rule identity, or `None` when the profile does not declare it.
    val maturityOf: ruleId: string -> Maturity option

    /// The gate one pack contributes to the published profile: `Command = None` (satisfied by the
    /// pack's own sensing and by handoff evidence, never by a local `tooling.yml` command — the
    /// same posture as the `gameplay` floor), `Tier = None`, owner `platform`.
    val checkFor: pack: Pack -> Check

    /// The composed profile's complete gate set — `checkFor` over `packs`, ascending by check id.
    /// This is what `Inheritance.ReferenceProfile.checksFor` returns for `profileKey` and what the
    /// generated region of the published `.fsgg/capabilities.yml` renders.
    val checks: Check list

    // ── Conflict resolution (AC6) ────────────────────────────────────────────────────────────

    /// Two rule identities that collide across packs, with every pack declaring them.
    type Collision =
        { RuleId: string
          Packs: Pack list }

    /// Every `(pack, ruleId)` pair the composed profile declares — `ruleIds` flattened over `packs`,
    /// in `packs` order. This is the input `collisions` and `ruleOwner` are both computed from, so a
    /// caller can inspect exactly what they inspect.
    val declarations: (Pack * string) list

    /// Every rule identity declared by more than one pack in `declarations`, ascending. The composed
    /// profile is well-formed exactly when `collisions declarations` is EMPTY, and
    /// `ReferenceProfileComposition` C3 asserts that it is.
    ///
    /// It takes its input rather than reading the table directly, and #385's repair round 1 is why:
    /// as a nullary `unit -> Collision list` over the fixed table it could only ever return `[]`,
    /// so the test claiming to prove the detector had to re-implement the grouping locally and
    /// assert its own pipeline — breaking the real function outright left that test green. A
    /// detector that cannot be handed a positive case is not a detector anybody has checked.
    val collisions: declarations: (Pack * string) list -> Collision list

    /// Resolve two maturities declared for the SAME rule identity: the HIGHER-ranked wins. This is
    /// ADR-0009 §Decision 3's non-lowerable rule reused verbatim (`Model.maturityRank`), not a
    /// second precedence invented here — a composition that let the lower one win would be exactly
    /// the downgrade the org floor exists to prevent. Commutative and associative, so the result
    /// does not depend on the order the packs are folded in.
    val composeMaturity: left: Maturity -> right: Maturity -> Maturity

    // ── Normalization (the two incompatible finding shapes) ──────────────────────────────────

    /// Build a `Model.SurfaceFinding` for a rule of `pack`, at the pack's declared maturity, in the
    /// `DesignDomain`. This is the ONE builder the composed profile offers, and it exists because
    /// two of the four packs emit shapes that carry no severity axis at all
    /// (`CodeChecks.ArchitectureFinding` and `DesignChecks.EvidenceBoundary.Finding`), so their
    /// findings could not previously reach `Enforcement.deriveEffectiveSeverity` at any maturity.
    /// A `ruleId` this pack does not declare still produces a finding — with `IsInputState = true`
    /// and a message naming the undeclared id, because an unrecognised rule is a malformed input to
    /// the composition, never a silent pass (fail closed, #370's own Principle).
    val findingOf:
        pack: Pack ->
        ruleId: string ->
        request: Model.SurfaceCheckRequest ->
        source: GovernedPath ->
        detail: string ->
        message: string ->
            Model.SurfaceFinding
