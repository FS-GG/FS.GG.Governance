// Curated public signature contract for the SHARED freshness-sensing edge (F046).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the matching
// FreshnessSensing.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This is the IMPURE sensing edge extracted from F044's interpreter (research D1) so the `fsgg route` (F022)
// and `fsgg ship` (F026) host commands can run the cache-eligibility pipeline without triplicating ~80 lines
// of real SHA-256 / store-deserializer code. It REUSES nothing rendering-specific: rule hash over the repo's
// `.fsgg/*.yml`, covered artifacts over `src/**` (the F044 MVP coverage, carried verbatim — a documented
// later refinement), generator from the assembly version, a coarse command-version digest. It NEVER
// fabricates an unsensed fact (a `None` accessor result is unsensed, not an empty value). It reaches NO
// network. The degrade-on-error POLICY is NOT here — `senseFreshness`/`loadStore` return faithful `Result`s
// and the calling command's pure `update` decides how to degrade (research D2). F044 is left UNTOUCHED this
// row (FR-014); converging it onto this library is a bounded deferred follow-up.

namespace FS.GG.Governance.FreshnessSensing

open FS.GG.Governance.Config.Model              // CommandId
open FS.GG.Governance.Gates.Model               // Gate
open FS.GG.Governance.FreshnessKey.Model         // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion, Revision
open FS.GG.Governance.FreshnessResolution.Model  // SensedFacts
open FS.GG.Governance.EvidenceReuse.Model        // ReuseStore

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessSensing =

    /// The injected FRESHNESS-sensing port. Each accessor returns `option`, carrying the "sensed-empty vs
    /// unsensed" distinction the F043 join depends on: `SenseCoveredArtifacts g = Some []` is a SENSED-EMPTY
    /// covered set (resolves); `= None` is UNSENSED (the gate resolves unresolved on covered artifacts).
    /// `SenseCommandVersion c = None` is unsensed ⇒ the gate resolves unresolved on command version (no-hide)
    /// — never fabricated. The real port computes BCL-crypto digests over real on-disk bytes; tests back it
    /// with fixed literal values (Synthetic).
    type FreshnessSensor =
        { SenseRuleHash: unit -> RuleHash option
          SenseGeneratorVersion: unit -> GeneratorVersion option
          SenseCoveredArtifacts: Gate -> ArtifactHash list option
          SenseCommandVersion: CommandId -> CommandVersion option }

    /// The injected READ-ONLY evidence-reuse store port. `Ok None` = the file is ABSENT; `Ok (Some store)` =
    /// present + well-formed; `Error reason` = present but MALFORMED. Writing/evicting evidence is OUT OF
    /// SCOPE — there is no writer here.
    type StoreReader = string -> Result<ReuseStore option, string>

    /// Build the REAL freshness sensor for a repository working directory: a real SHA-256 `RuleHash` over the
    /// `.fsgg/*.yml` catalog bytes (sorted), real `ArtifactHash`es over `src/**` (the MVP repo-wide coverage),
    /// the executing assembly version as `GeneratorVersion`, and a coarse `CommandVersion` digest stamped
    /// against the rule pack. Coarse MVP sensing; finer PER-GATE scoping is a documented later refinement.
    /// `None` from any accessor when its input is absent (unsensed, no-hide). Reaches NO network.
    val realSensor: repo: string -> FreshnessSensor

    /// The REAL read-only store reader: deserializes `fsgg.evidence-reuse-store/v1` via `System.Text.Json`,
    /// taking the opaque newtype strings VERBATIM (computes NO hash/key/digest). `Ok None` for an absent
    /// file, `Error` for a malformed one.
    val realStoreReader: StoreReader

    /// Assemble a complete `SensedFacts` bundle from a sensor + the selected gates + the change's base/head
    /// (passed through from `RepoSnapshot.Range`, never re-sensed). A PRESENT Map key = SENSED (even when the
    /// value is empty); an ABSENT key = NOT SENSED — never fabricated. TOTAL/guarded: catches any thrown
    /// exception and returns `Error`. The caller's pure `update` decides the degrade policy on `Error`.
    val senseFreshness:
        sensor: FreshnessSensor ->
        gates: Gate list ->
        baseHead: (Revision option * Revision option) ->
            Result<SensedFacts, string>

    /// Load the read-only reuse store, mapping ABSENT ⇒ `EvidenceReuse.empty` and present-well-formed ⇒ the
    /// store; a present-but-MALFORMED store surfaces as `Error reason` (the caller's pure `update` decides
    /// whether to degrade-to-empty or fail). TOTAL/guarded — never throws.
    val loadStore: reader: StoreReader -> path: string -> Result<ReuseStore, string>
