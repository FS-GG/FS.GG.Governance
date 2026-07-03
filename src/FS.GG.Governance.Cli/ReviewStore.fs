namespace FS.GG.Governance.Cli

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewStore =

    let safeFileName (key: string) =
        key
        |> Seq.map (fun ch -> if Char.IsLetterOrDigit ch || ch = '-' || ch = '_' then ch else '_')
        |> Seq.toArray
        |> String

    // A stable, process-independent short digest of the FULL key, appended to the sanitized
    // name. `safeFileName` alone collapses every character outside [A-Za-z0-9_-] to '_', so
    // distinct keys (`rule:a/b` vs `rule:a b`) would otherwise map to one file; the digest keeps
    // them apart. SHA-256 — never String.GetHashCode, which is randomized per process.
    let keyHash (key: string) =
        use sha = SHA256.Create()
        (Encoding.UTF8.GetBytes key |> sha.ComputeHash).[..7]
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    let storeFileName (key: string) = safeFileName key + "-" + keyHash key + ".txt"

    let reviewStoreRoot (request: RunRequest) =
        match request.ReviewStore with
        | Some path -> Path.GetFullPath path
        | None ->
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            Path.Combine(home, ".cache", "fs-gg-governance", "reviews")

    let verdictText (verdict: Verdict) =
        match verdict with
        | Pass -> "Pass"
        | Fail reason -> "Fail:" + reason
        | Uncertain reason -> "Uncertain:" + reason

    let parseVerdict (text: string) =
        if text = "Pass" then
            Pass
        elif text.StartsWith("Fail:", StringComparison.Ordinal) then
            Fail(text.Substring "Fail:".Length)
        elif text.StartsWith("Uncertain:", StringComparison.Ordinal) then
            Uncertain(text.Substring "Uncertain:".Length)
        else
            Uncertain("unrecognized stored verdict")

    let loadReview (request: RunRequest) (key: string) =
        try
            let file = Path.Combine(reviewStoreRoot request, storeFileName key)

            if File.Exists file then
                match File.ReadAllLines file |> Array.toList with
                // Line 0 is the key the entry was stored under; a mismatch (only reachable on a
                // SHA-256 filename collision) is a MISS, never a wrong-verdict hit (the bug this fixes).
                | storedKey :: rule :: verdict :: _ when storedKey = key ->
                    let review: RecordedReview =
                        { Rule = RuleId rule
                          Key = key
                          Verdict = parseVerdict verdict }

                    Ok(Some review)
                | _ :: _ :: _ :: _ -> Ok None
                | _ -> Error("malformed review store entry: " + file)
            else
                Ok None
        with ex ->
            Error ex.Message

    let saveReview (request: RunRequest) (review: RecordedReview) =
        try
            let dir = reviewStoreRoot request
            Directory.CreateDirectory dir |> ignore
            let file = Path.Combine(dir, storeFileName review.Key)
            let (RuleId rule) = review.Rule
            File.WriteAllLines(file, [| review.Key; rule; verdictText review.Verdict |])
            Ok()
        with ex ->
            Error ex.Message
