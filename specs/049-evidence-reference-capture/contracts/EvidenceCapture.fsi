// Curated public signature contract for the pure evidence-capture bridge (F049).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// EvidenceCapture.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any EvidenceCapture.fs body
// exists (Principle I). Both operations are PURE and TOTAL (FR-007): defined for EVERY input — the empty store,
// an empty stdout/stderr digest, a non-zero exit code, and every captured-output outcome — never throwing,
// reading no clock, filesystem, git, environment, or network, spawning no process, and hashing no bytes;
// byte-for-byte identical for identical input regardless of evaluation time, machine, process, or collection
// order. This row runs NO gate, senses NO fact, resolves NO freshness, persists NOTHING, and adds NO CLI: its
// sole outputs are the derived `EvidenceRef` and the grown `ReuseStore` value.
//
// This is the value-only bridge that turns an ALREADY-EXECUTED gate's reproducible facts into the real,
// reproducible evidence reference the F030 store holds, and folds it in against the gate's resolved freshness
// world — mirroring how F047 delivered the pure write half before F048 wired it. The IMPURE edge (actually
// running gates inside `fsgg route`/`fsgg ship`, sensing each output digest, building the `CommandRecord`, and
// recording during a run) is the FOLLOWING row and is out of scope here; this core consumes an already-built
// F032 `CommandRecord` and an already-resolved F029/F043 `FreshnessInputs`.
//
// It introduces NO new type — it reuses F030 `EvidenceRef`/`ReuseStore`, F032 `CommandRecord`, and F029
// `FreshnessInputs` VERBATIM — and NO new reuse policy, store representation, or evidence representation. The
// reference is EXACTLY the F032 reproducible identity, wrapped: `EvidenceRef (CommandRecord.identityValue
// (CommandRecord.canonicalId record))`; the store fold is EXACTLY `EvidenceReuse.record inputs (referenceOf
// record) store`. It references ONLY F030 `EvidenceReuse` and F032 `CommandRecord`; F029 `FreshnessKey` and
// F014 `Config` arrive transitively. It adds NO new third-party PackageReference (BCL + FSharp.Core only), bumps
// NO schema version, and is referenced by nothing on landing.

namespace FS.GG.Governance.EvidenceCapture

open FS.GG.Governance.CommandRecord.Model // CommandRecord
open FS.GG.Governance.FreshnessKey.Model // FreshnessInputs
open FS.GG.Governance.EvidenceReuse.Model // EvidenceRef, ReuseStore

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceCapture =

    /// Derive the reproducible evidence reference for an ALREADY-EXECUTED gate's `CommandRecord` (F032). PURE
    /// and TOTAL (FR-001, FR-007): defined for every record, never throwing, no I/O, no process, NO hashing —
    /// it reuses F032's already-byte-stable canonical identity rather than computing a new one. The reference
    /// string is EXACTLY `CommandRecord.identityValue (CommandRecord.canonicalId record)`, wrapped as an
    /// `EvidenceRef`. DURATION-INVARIANT (FR-002): `canonicalId` is computed only over `record.Reproducible`
    /// and NEVER reads `record.Duration`, so two records differing ONLY in `SensedDuration` yield the
    /// byte-identical reference. INJECTIVE over the reproducible facts (FR-003): two records differing in ANY
    /// reproducible fact (executable, an argument or its order, working directory, the env delta as a set,
    /// timeout, exit code, either output digest, or the captured-output outcome) yield DIFFERENT references —
    /// inherited verbatim from F032's injective `canonicalId`. DETERMINISTIC and BYTE-STABLE (FR-008):
    /// identical input yields the byte-identical reference on every run and machine, with no
    /// wall-clock/GUID/path/locale/environment leakage.
    val referenceOf: record: CommandRecord -> EvidenceRef

    /// Fold one already-executed gate's evidence into a supplied store, pairing the gate's resolved
    /// `FreshnessInputs` (F029/F043) with the reference derived from its `CommandRecord` (F032). PURE and TOTAL
    /// (FR-004, FR-007): EXACTLY `EvidenceReuse.record inputs (referenceOf record) store` — it reuses the F030
    /// `record` convention VERBATIM (newest-first, store in / store out, immutable) and introduces NO new reuse
    /// policy, store representation, or evidence representation. CLOSE-THE-LOOP (FR-005): after capturing world
    /// `inputs`, `EvidenceReuse.decide inputs (capture inputs record store)` returns `Reuse (referenceOf
    /// record)` — the captured world becomes reusable and serves exactly the derived reference. RECOMPUTE-SAFE
    /// (FR-006): relative to the input store it may make ONLY the just-captured world reusable; for every other
    /// candidate world, `decide` over the result returns EXACTLY what it returned over the input store (capture
    /// never fabricates a match for an unrelated world, never weakens a prior verdict). MECHANICAL, not policy:
    /// it records whatever record it is handed (incl. a non-zero `ExitCode`); gating on success/exit-code is the
    /// out-of-scope host row. DETERMINISTIC and BYTE-STABLE (FR-008): identical input yields the byte-identical
    /// store on every run and machine.
    val capture: inputs: FreshnessInputs -> record: CommandRecord -> store: ReuseStore -> ReuseStore
