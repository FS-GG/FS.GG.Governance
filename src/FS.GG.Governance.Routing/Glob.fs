// Pure, total glob matcher + precedence primitive for path-to-capability routing (F015).
// The public surface is fixed by Glob.fsi (Principle II); no top-level binding carries an
// access modifier. Every binding is PURE and TOTAL (FR-011): no I/O, no clock, never throws.
// The supported syntax is the closed MVP set (literal, `?`, `*`, `**`) over already-
// F014-normalized `GovernedPath` values — this module does NO normalization (FR-003).
// See contracts/glob-precedence.md for the full syntax + precedence contract.

namespace FS.GG.Governance.Routing

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Glob =

    // ── Syntax check (FR-002, FR-010) ──

    // The reserved-but-unimplemented characters of richer glob dialects (character classes,
    // brace expansion, negation, groups). A glob containing any is UnsupportedGlobSyntax.
    let private reservedChars = set [ '['; ']'; '{'; '}'; '!'; '('; ')' ]

    let checkSyntax (glob: GovernedPath) : Result<unit, unit> =
        let (GovernedPath g) = glob
        if g |> Seq.exists reservedChars.Contains then Error() else Ok()

    // ── Matching (FR-002) ──

    /// Match one segment with `*` (zero+ chars) and `?` (one char) resolved within the
    /// segment; every other character is literal. `let rec` backtracking walk — recursion for
    /// the genuine branching of `*` (Principle III blesses the tree-walk shape), not state
    /// hiding. Ordinal/case-sensitive.
    let rec private matchSegment (pat: string) (pi: int) (text: string) (ti: int) : bool =
        if pi = pat.Length then
            ti = text.Length
        else
            match pat.[pi] with
            | '*' ->
                // zero-or-more chars within the segment: consume zero, else one more char.
                matchSegment pat (pi + 1) text ti
                || (ti < text.Length && matchSegment pat pi text (ti + 1))
            | '?' -> ti < text.Length && matchSegment pat (pi + 1) text (ti + 1)
            | c -> ti < text.Length && text.[ti] = c && matchSegment pat (pi + 1) text (ti + 1)

    /// Match a `/`-split glob against a `/`-split path. A `**` segment is a zero-or-more
    /// WHOLE-segment wildcard resolved by backtracking; every other segment matches position-
    /// for-position via `matchSegment`. `let rec` for the `**` branching (disclosed here).
    let rec private matchSegments (pat: string[]) (pi: int) (text: string[]) (ti: int) : bool =
        if pi = pat.Length then
            ti = text.Length
        elif pat.[pi] = "**" then
            if pi + 1 = pat.Length then
                // TRAILING `**` consumes ALL remaining segments but requires at least one: the
                // `/` before `**` is mandatory, so `src/**` matches `src/a` and `src/a/b` but
                // NOT the bare `src` (glob-precedence §1, "the src/ prefix is required").
                ti < text.Length
            else
                // NON-trailing `**`: zero-or-more whole segments via backtracking — consume
                // zero segments, else one more segment (e.g. `a/**/b` matches `a/b`).
                matchSegments pat (pi + 1) text ti
                || (ti < text.Length && matchSegments pat pi text (ti + 1))
        else
            ti < text.Length
            && matchSegment pat.[pi] 0 text.[ti] 0
            && matchSegments pat (pi + 1) text (ti + 1)

    let matches (glob: GovernedPath) (path: GovernedPath) : bool =
        let (GovernedPath g) = glob
        let (GovernedPath p) = path
        matchSegments (g.Split('/')) 0 (p.Split('/')) 0

    // ── Specificity / precedence (FR-005, FR-006, research D3) ──

    /// The 3-rung specificity key of FR-005 rungs 1–3, computed from the glob ALONE. Smaller
    /// ranks higher. `CustomEquality`/`CustomComparison` so two globs whose keys are EQUAL are
    /// exactly the co-specific (ambiguous) pair — the final ordinal tiebreak (rung 4) is NOT
    /// folded in here (it lives in `compare`). Ordering: wildcard-free flag (0 < 1), then
    /// negated literal-segment count (more literal segments ⇒ smaller), then `**` count
    /// (fewer ⇒ smaller).
    [<CustomEquality; CustomComparison>]
    type Specificity =
        { WildcardFree: int
          NegLiteralSegmentCount: int
          StarStarCount: int }

        member private this.Key = (this.WildcardFree, this.NegLiteralSegmentCount, this.StarStarCount)

        override this.Equals(o: obj | null) =
            match o with
            | :? Specificity as other -> this.Key = other.Key
            | _ -> false

        override this.GetHashCode() = hash this.Key

        interface System.IComparable with
            member this.CompareTo(o: obj) =
                match o with
                | :? Specificity as other -> Operators.compare this.Key other.Key
                | _ -> invalidArg "o" "cannot compare Specificity with a value of another type"

    let specificity (glob: GovernedPath) : Specificity =
        let (GovernedPath g) = glob
        let segs = g.Split('/')
        let hasWild (s: string) = s.Contains '*' || s.Contains '?'
        let literalSegments = segs |> Array.filter (hasWild >> not) |> Array.length
        let starStar = segs |> Array.filter (fun s -> s = "**") |> Array.length
        { WildcardFree = (if segs |> Array.exists hasWild then 1 else 0)
          NegLiteralSegmentCount = -literalSegments
          StarStarCount = starStar }

    let compare (a: GovernedPath) (b: GovernedPath) : int =
        let c = Operators.compare (specificity a) (specificity b)
        if c <> 0 then
            c
        else
            // rung 4: ordinal glob-string tiebreak — never 0 for distinct glob strings.
            let (GovernedPath ga) = a
            let (GovernedPath gb) = b
            System.String.CompareOrdinal(ga, gb)

    let isAmbiguousPair (a: GovernedPath) (b: GovernedPath) : bool =
        specificity a = specificity b
