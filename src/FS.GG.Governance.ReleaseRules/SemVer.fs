namespace FS.GG.Governance.ReleaseRules

open System

// The single semantic-version comparator (review M-ADPT-1). Extracted verbatim from PackEvidence's comparator
// — the correct one: numeric core segments compared numerically, missing trailing segments treated as 0,
// build metadata stripped, and a pre-release ranked lower than its release. PURE and TOTAL: pure string work,
// never a clock/filesystem/process, never throws. The surface is SemVer.fsi (Principle II) — no access
// modifiers here; the sub-comparators are hidden by absence from the .fsi.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SemVer =

    /// Strip build metadata (`+…`) and split the optional pre-release (`-…`) off the numeric core.
    let splitMeta (v: string) : string * string option =
        let noBuild =
            match v.IndexOf('+') with
            | -1 -> v
            | i -> v.Substring(0, i)

        match noBuild.IndexOf('-') with
        | -1 -> noBuild, None
        | i -> noBuild.Substring(0, i), Some(noBuild.Substring(i + 1))

    /// Compare two dot-separated core versions: each segment numerically when both parse as integers, else
    /// ordinally. Missing trailing segments are treated as 0 (`1.2` = `1.2.0`).
    let compareCore (a: string) (b: string) : int =
        let pa = a.Split('.')
        let pb = b.Split('.')
        let n = max pa.Length pb.Length
        let mutable res = 0
        let mutable i = 0

        while res = 0 && i < n do
            let segA = if i < pa.Length then pa.[i] else "0"
            let segB = if i < pb.Length then pb.[i] else "0"

            res <-
                match Int64.TryParse segA, Int64.TryParse segB with
                | (true, na), (true, nb) -> compare na nb
                | _ -> String.CompareOrdinal(segA, segB)

            i <- i + 1

        res

    /// Compare two pre-release identifiers per semantic-version precedence (numeric < alphanumeric).
    let compareIdent (a: string) (b: string) : int =
        match Int64.TryParse a, Int64.TryParse b with
        | (true, na), (true, nb) -> compare na nb
        | (true, _), (false, _) -> -1
        | (false, _), (true, _) -> 1
        | _ -> String.CompareOrdinal(a, b)

    /// A version with NO pre-release outranks one WITH; both present ⇒ compare identifiers, shorter < longer.
    let comparePre (a: string option) (b: string option) : int =
        match a, b with
        | None, None -> 0
        | None, Some _ -> 1
        | Some _, None -> -1
        | Some pa, Some pb ->
            let xa = pa.Split('.')
            let xb = pb.Split('.')
            let n = max xa.Length xb.Length
            let mutable res = 0
            let mutable i = 0

            while res = 0 && i < n do
                res <-
                    if i >= xa.Length then -1
                    elif i >= xb.Length then 1
                    else compareIdent xa.[i] xb.[i]

                i <- i + 1

            res

    let compareVersions (a: string) (b: string) : int =
        let ca, pa = splitMeta a
        let cb, pb = splitMeta b
        let c = compareCore ca cb
        if c <> 0 then c else comparePre pa pb
