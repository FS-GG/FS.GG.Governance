// Curated public signature contract for the pure glob-matching + precedence primitive of
// path-to-capability routing (F015).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Glob.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Glob.fs body exists (Principle I). Every binding here is PURE and TOTAL (FR-011): no I/O,
// no clock, never throws. The supported syntax is the CLOSED MVP set (FR-002): literal
// segments, `?` (one char within a segment), `*` (zero+ chars, not crossing `/`), and `**`
// (zero+ whole segments). Matching runs over the F014-normalized `GovernedPath` form, so both
// sides already use `/` separators with `.`/`..` resolved (FR-003, research D8) — this module
// does NO normalization. See contracts/glob-precedence.md for the full syntax + precedence
// contract this signature realizes.

namespace FS.GG.Governance.Routing

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Glob =

    // ── Syntax check (FR-002, FR-010) ──

    /// `Ok ()` when the glob uses only the supported MVP constructs; `Error ()` when it contains
    /// a reserved-but-unimplemented character (`[ ] { } ! ( )` — richer dialects the MVP does not
    /// implement). The caller (`Routing.route`) turns an `Error` into an `UnsupportedGlobSyntax`
    /// diagnostic rather than letting the glob silently never match (FR-010, research D6). PURE.
    val checkSyntax: glob: GovernedPath -> Result<unit, unit>

    // ── Matching (FR-002) ──

    /// Does `glob` match `path`? Both are F014-normalized governed paths (split on `/` into
    /// segments). `**` matches zero or more whole segments; `*` matches zero or more characters
    /// within one segment; `?` matches exactly one character; everything else is literal. PURE
    /// and TOTAL — a glob with unsupported syntax (see `checkSyntax`) is matched with those
    /// characters treated literally, so callers SHOULD `checkSyntax` first. Comparison is
    /// case-sensitive/ordinal (F014 settled case; routing does not re-decide it, FR-003).
    val matches: glob: GovernedPath -> path: GovernedPath -> bool

    // ── Specificity / precedence (FR-005, FR-006, research D3) ──

    /// An opaque, totally-ordered specificity key computed from the glob ALONE (never from the
    /// path it matched), so a glob's rank is constant across every path it matches. The key is the
    /// 3-rung tuple of FR-005 rungs 1–3 — wildcard-free flag, then literal-segment count, then
    /// `**` count. The FINAL ordinal-glob-string tiebreak (FR-005 rung 4) is applied by
    /// `compare`/`isAmbiguousPair` below, NOT folded into this key — so two globs whose keys are
    /// EQUAL are exactly the genuinely co-specific (ambiguous) pair (FR-006). Literal-character
    /// length is deliberately NOT a rung (research D3). Smaller key = higher precedence.
    [<Sealed>]
    type Specificity =
        interface System.IComparable
        override Equals: (obj | null) -> bool
        override GetHashCode: unit -> int

    /// The specificity key of a glob (FR-005). PURE; depends only on the glob string.
    val specificity: glob: GovernedPath -> Specificity

    /// Total precedence comparison between two matching globs: orders by `specificity` first,
    /// then breaks any remaining tie by the ordinal glob string (FR-005 rung 4). Returns <0 when
    /// `a` has higher precedence (wins). Total and deterministic — never returns 0 for distinct
    /// glob strings, so a path matching ≥1 glob always has a unique winner (FR-005).
    val compare: a: GovernedPath -> b: GovernedPath -> int

    /// `true` when two matching globs are co-specific — equal under `specificity`, i.e. separated
    /// ONLY by the ordinal tiebreak of `compare`. The winner of such a pair is still total (the
    /// ordinal-first glob), but `Routing.route` additionally emits an `AmbiguousRoute` diagnostic
    /// (FR-006, research D3). PURE.
    val isAmbiguousPair: a: GovernedPath -> b: GovernedPath -> bool
