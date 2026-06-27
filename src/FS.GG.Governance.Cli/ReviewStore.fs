namespace FS.GG.Governance.Cli

open System
open System.IO
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReviewStore =

    let safeFileName (key: string) =
        key
        |> Seq.map (fun ch -> if Char.IsLetterOrDigit ch || ch = '-' || ch = '_' then ch else '_')
        |> Seq.toArray
        |> String

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

    let loadReview (request: RunRequest) (snapshot: ProjectSnapshot) (key: string) =
        if snapshot.Root.Contains("review-store-unavailable", StringComparison.OrdinalIgnoreCase) then
            Error "review store unavailable by fixture"
        else
            try
                let file = Path.Combine(reviewStoreRoot request, safeFileName key + ".txt")

                if File.Exists file then
                    match File.ReadAllLines file |> Array.toList with
                    | rule :: verdict :: _ ->
                        let review: RecordedReview =
                            { Rule = RuleId rule
                              Key = key
                              Verdict = parseVerdict verdict }

                        Ok(Some review)
                    | _ -> Error("malformed review store entry: " + file)
                else
                    Ok None
            with ex ->
                Error ex.Message

    let saveReview (request: RunRequest) (snapshot: ProjectSnapshot) (review: RecordedReview) =
        if snapshot.Root.Contains("review-store-unavailable", StringComparison.OrdinalIgnoreCase) then
            Error "review store unavailable by fixture"
        else
            try
                let dir = reviewStoreRoot request
                Directory.CreateDirectory dir |> ignore
                let file = Path.Combine(dir, safeFileName review.Key + ".txt")
                let (RuleId rule) = review.Rule
                File.WriteAllLines(file, [| rule; verdictText review.Verdict |])
                Ok()
            with ex ->
                Error ex.Message
