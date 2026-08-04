// The ONE authoritative source of the org reference profile, and the projection that DERIVES the
// published YAML from it (docs/decisions/0011-generated-reference-gate-set.md).
//
// docs/decisions/0009 §Decision 1 made the reference floor EMBEDDED — an in-code table, so a
// product cannot escape it by deleting a file. It did not say what the published
// `FS.GG.Governance.ReferenceGateSet` YAML is, and the answer that emerged was "a second,
// independently-authored copy kept in step by convention" (see the old
// `samples/sdd-reference-gate-set/.fsgg/capabilities.yml` note, and docs/decisions/0010 §4).
// docs/decisions/0011 ends that: `checksFor` below is authoritative, and the packed YAML's
// gameplay-floor region is GENERATED from it by `capabilitiesRegion`, carrying provenance.
//
// ADR-0009 §1 is PRESERVED, not overturned: nothing here reads an installed package, and
// enforcement still flows `checksFor -> Inheritance.referenceGatesFor -> applyInheritance ->
// Ship.rollup` with no I/O edge. The generated YAML is DISTRIBUTION, never the enforcement input —
// a consumer that deletes or edits its resolved `.fsgg/` copy still inherits the floor.
//
// PURE and TOTAL: no I/O, no git, no clock. Deterministic — byte-identical output for identical
// input; never throws. `replaceRegion`/`extractRegion` return `Result` rather than raising, and
// FAIL CLOSED on an absent, duplicated, or inverted marker pair: a region the tool cannot locate
// is never silently treated as "no drift".

namespace FS.GG.Governance.Inheritance

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReferenceProfile =

    /// Every template-profile this org profile binds, in a deterministic order. `checksFor` returns
    /// a NON-empty list for exactly these and the EMPTY list for anything else, so a caller can
    /// enumerate the whole authoritative profile without knowing the table's shape.
    ///
    /// TWO profiles are bound: `fsharp-constitution` — epic FS-GG/FS.GG.Governance#367's four rule
    /// packs composed into one gate set by #385 (docs/decisions/0012) — and `game`, the gameplay
    /// floor of ADR-0049 / docs/decisions/0010. Adding a profile here is a FAIL-CLOSED act: the
    /// derivation gate's D3 requires every bound profile to have a generated region in the
    /// published `.fsgg/capabilities.yml`, and the marker pair that names the region must be placed
    /// in that file before the bless path can render into it.
    val boundProfiles: TemplateProfile list

    /// THE authoritative org reference profile for one template-profile: the checks every product
    /// carrying that profile inherits as a non-lowerable floor. This is the single source
    /// docs/decisions/0011 names — `Inheritance.referenceGatesFor` projects it into gates for
    /// enforcement, and `capabilitiesRegion` renders it into the published YAML. An unbound or
    /// unknown profile yields the EMPTY list — never an error, never a fabricated check.
    ///
    /// The `fsharp-constitution` case DELEGATES to `SurfaceChecks.Profile.checks` rather than
    /// restating it. That is deliberate and is the whole point of #385: the four packs read their
    /// declared maturity from the same table, so the composed profile has one authority in exactly
    /// the sense docs/decisions/0011 established for the `game` floor.
    val checksFor: profile: TemplateProfile -> Check list

    /// The exact line that opens the generated region for a profile in a `.fsgg/capabilities.yml`
    /// (e.g. `  # BEGIN GENERATED: fsgg-reference-profile:game`). Indented to the `checks:` list
    /// level so the region is valid YAML in place.
    val beginMarker: profile: TemplateProfile -> string

    /// The exact line that closes the generated region for a profile.
    val endMarker: profile: TemplateProfile -> string

    /// The complete generated region for a profile — the begin marker, the provenance comment, one
    /// YAML entry per authoritative check (field order: `id`, `domain`, `command` when bound,
    /// `owner`, `cost`, `environment`, `maturity`, `tier` when declared), and the end marker.
    /// Newline-joined with NO trailing newline. Deterministic: the same profile always renders the
    /// same bytes.
    val capabilitiesRegion: profile: TemplateProfile -> string

    /// The region currently present in `text` between this profile's markers, verbatim and
    /// including both marker lines. FAILS CLOSED with a message naming the cause when the begin or
    /// end marker is missing, appears more than once, or the end precedes the begin — an
    /// unlocatable region is an `Error`, never an implied match.
    val extractRegion: profile: TemplateProfile -> text: string -> Result<string, string>

    /// `text` with this profile's generated region replaced by `capabilitiesRegion profile`. The
    /// line ending style of `text` is preserved. Fails closed on exactly the `extractRegion`
    /// causes. Pure: the caller owns the write.
    val replaceRegion: profile: TemplateProfile -> text: string -> Result<string, string>
