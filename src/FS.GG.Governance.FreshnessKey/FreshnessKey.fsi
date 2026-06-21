// Curated public signature contract for the freshness-key operations (F029).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching FreshnessKey.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any FreshnessKey.fs
// body exists (Principle I). All three operations are PURE and TOTAL (FR-002, FR-008): defined for every
// `FreshnessInputs` value, never throwing, reading no clock, filesystem, git, environment, or network, and
// byte-for-byte identical for identical input regardless of evaluation time, machine, process, or
// collection order (FR-003). This row computes NO cache lookup/storage/eviction, NO ship verdict, persists
// NO artifact, and adds NO CLI (FR-010): its sole outputs are the key value and the match decision.

namespace FS.GG.Governance.FreshnessKey

open FS.GG.Governance.FreshnessKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FreshnessKey =

    /// Render the freshness inputs to their canonical, deterministic, byte-stable `Key`
    /// (contracts/freshness-key-format.md). PURE and TOTAL: defined for every `FreshnessInputs` value;
    /// reads no clock, filesystem, git, environment, or network (FR-002, FR-008). Covered artifacts are
    /// compared as a SET — deduplicated and ordinally sorted — so order and duplication never affect the
    /// result (FR-004). The encoding is INJECTIVE across categories: the same opaque string placed in two
    /// different categories yields different keys (FR-006). BCL string building only; no hashing.
    val compute: inputs: FreshnessInputs -> Key

    /// The reuse predicate: true IFF the two inputs agree on EVERY input category — i.e. their keys are
    /// equal. TOTAL. Defined as `compute a = compute b`, so the predicate and the key can never disagree
    /// (FR-005). The literal foundation of the later "reuse evidence only when all freshness inputs match"
    /// row.
    val matches: a: FreshnessInputs -> b: FreshnessInputs -> bool

    /// The no-hide explainer: the categories whose values differ between two inputs, in the fixed
    /// key-encoding order (FR-007). Empty IFF `matches a b`. Covered artifacts are compared as a SET
    /// (reordered/duplicated artifacts are not reported). TOTAL.
    val diff: a: FreshnessInputs -> b: FreshnessInputs -> InputCategory list

    /// Unwrap a `Key` to its canonical string (for storage, messages, tests). TOTAL.
    val value: key: Key -> string
