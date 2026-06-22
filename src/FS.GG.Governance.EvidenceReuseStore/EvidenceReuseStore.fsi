// Curated public signature contract for the evidence-reuse store WRITE half (F047).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// EvidenceReuseStore.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any EvidenceReuseStore.fs
// body exists (Principle I). All three operations are PURE and TOTAL (FR-009): defined for every input, never
// throwing, reading no clock, filesystem, git, environment, or network, and identical for identical input
// regardless of evaluation time, machine, or process. This row computes NO on-disk persistence (the atomic
// write + store-path discovery + writer port is a LATER host row), produces NO real evidence reference (that
// needs gate execution — a later row), runs NO gate, and bumps NO schema version. It is the deterministic
// inverse of the F046 read-only `FreshnessSensing` deserializer plus a bounded-retention and superseded-world
// pruning policy, all over the existing F030 `ReuseStore` value — reusing F030/F029 `record`/`matches`
// semantics VERBATIM (FR-010). It introduces NO new type, NO new reuse policy, and NO new evidence
// representation. Serialisation uses ONLY the net10.0 shared-framework `System.Text.Json` the F046 reader and
// the F042 projection already use — no new third-party dependency (FR-011).

namespace FS.GG.Governance.EvidenceReuseStore

open FS.GG.Governance.EvidenceReuse.Model // ReuseStore

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EvidenceReuseStore =

    /// The declared schema-version token stamped into every emitted document — the EXACT string the F046
    /// `FreshnessSensing` reader requires (`fsgg.evidence-reuse-store/v1`). A fixed, deterministic literal;
    /// never derived from a clock, environment, or input value, and never bumped by this row (FR-012).
    val schemaVersion: string

    /// The default bounded-retention cap used when a caller has no reason to choose its own. A generous flat
    /// maximum that keeps disk/read cost bounded while covering many gates × a few recent worlds each. The
    /// numeric value is a mechanism detail (research D8), not a contract: callers MAY pass any bound to
    /// `retain`. Deterministic constant.
    val defaultRetentionBound: int

    /// Serialise a `ReuseStore` to the `fsgg.evidence-reuse-store/v1` document text — the deterministic,
    /// byte-stable INVERSE of the F046 read-only deserializer (FR-001). PURE and TOTAL: no I/O, never throws,
    /// defined for every store including empty. The round-trip `serialise` -> `FreshnessSensing.realStoreReader`
    /// is LOSSLESS (FR-002): the re-read store EQUALS the input — every entry's full freshness-input set and
    /// opaque evidence reference preserved, in the same newest-first order, with each entry's covered-artifact
    /// list in its stored order. DETERMINISTIC and BYTE-STABLE (FR-003): the same store value yields
    /// byte-identical output on every run and machine — fixed field/entry order, no wall-clock, path, locale,
    /// or environment leakage. The evidence reference and every freshness-input string is rendered VERBATIM,
    /// never parsed, re-hashed, or interpreted (FR-004); the empty store yields a well-formed document with an
    /// empty entry list, re-readable as the empty store (FR-005).
    val serialise: store: ReuseStore -> string

    /// Bound the store to at most `maxEntries`, retaining the NEWEST entries (the head of the newest-first
    /// store — the worlds a future run is most likely to match). PURE and TOTAL (FR-006): removes only whole
    /// entries (never mutates or fabricates one), so every retained entry is byte-for-byte one of the inputs.
    /// IDEMPOTENT at or under the bound: a store of length <= `maxEntries` is returned UNCHANGED (no reorder,
    /// no rewrite). `maxEntries <= 0` yields the empty store. RECOMPUTE-SAFE (FR-008): relative to the input,
    /// eviction can only ever turn a future `Reuse` into a `Recompute` — never the reverse. Reads no
    /// clock/filesystem/git/environment/network (FR-009).
    val retain: maxEntries: int -> store: ReuseStore -> ReuseStore

    /// Prune entries that no future reuse decision could ever serve: every entry a STRICTLY-NEWER entry already
    /// `FreshnessKey.matches` (the same freshness world is superseded by a newer recorded world for that gate —
    /// F030 `decide` always serves the most-recent full match). PURE and TOTAL (FR-007): survivors are a SUBSET
    /// of the input in newest-first order (the newest entry of each world-class). A store with no dead entries
    /// (e.g. any `record`-built store, already full-match-deduped) is returned UNCHANGED. Reuses the F029
    /// `matches` relation VERBATIM — no new policy (FR-010). RECOMPUTE-SAFE (FR-008): for every candidate the
    /// reuse verdict against the pruned store is identical-or-stricter than against the unpruned store — never
    /// more permissive (in fact verdict-IDENTICAL, since the newest full-match and the cause-locating entry are
    /// both retained). Reads no clock/filesystem/git/environment/network (FR-009).
    val prune: store: ReuseStore -> ReuseStore
