/// Producer-record integrity for the spec-kit integrations under `.specify/integrations/`
/// (FS.GG.Governance#328).
///
/// WHY THESE LIVE HERE, AND WHY THEY READ THE REAL TREE
///
/// `.specify/integrations/*.manifest.json` are content-addressed records of what the spec-kit installer
/// laid down under `.claude/skills/`. They are the artifact any materializer consults to decide whether a
/// tree is current, so a record that misdescribes the tree is not bookkeeping: it makes a correct tree
/// look drifted, and it makes a real edit look like the expected difference.
///
/// That is exactly what had happened. Three of the nine rows in `claude.manifest.json` recorded
/// PRE-OVERLAY digests — the base integration's bytes, before the `fsharp-opinionated` preset replaced
/// those three commands — and the manifest never said so. It had never matched those three files at any
/// commit in this repo's history, including the bootstrap commit. The knowledge that those rows were
/// stale by design lived only in a comment inside `scripts/materialize-skill-roots.sh`, so every other
/// reader of the manifest got three false reds on a correct tree.
///
/// The fix does not rewrite the digests (see that script's header for why: they are a RECEIPT, and
/// `specify integration status` reads them as the tamper baseline for those files). It publishes
/// `overlay.json`, a derived record naming which rows the manifest does not speak for and what does. So
/// the property to defend is not "the digests match" — it is that EVERY ROW IS EXPLAINED: matched by its
/// digest, or accounted for by the overlay record and verified against its real authority.
///
/// These read the committed tree rather than a fixture on purpose (Principle V). A fixture would assert
/// that the checking logic works; only the real tree asserts that THIS repo's records are honest, which
/// is the thing that was wrong. They are pure reads — no file is written.
module FS.GG.Governance.SkillChecks.Tests.IntegrationRecordTests

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Text.RegularExpressions
open Expecto
open FS.GG.Governance.SkillChecks.Tests.Support

let private integrationsDir = Path.Combine(repoRoot, ".specify", "integrations")
let private overlayPath = Path.Combine(integrationsDir, "overlay.json")
let private presetDir = Path.Combine(repoRoot, ".specify", "presets", "fsharp-opinionated")

/// This repo compiles with nullness checking on and warnings as errors, and the BCL/JSON reads below are
/// all annotated nullable. Collapsing a null to "" is safe here because every use is a path or a digest
/// that is then existence-checked or compared — an empty string fails those loudly rather than silently.
let inline private nn x = defaultArg (Option.ofObj x) ""

let private fileNameOf (p: string) = nn (Path.GetFileName p)

let private repoFile (rel: string) =
    Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar))

let private sha256Of (path: string) =
    use sha = SHA256.Create()
    sha.ComputeHash(File.ReadAllBytes path) |> Convert.ToHexString |> _.ToLowerInvariant()

/// The body a producer command and its generated SKILL.md share: frontmatter dropped (each integration
/// writes its own) and the leading run of blank/H1 lines dropped (each integration rewrites the title).
/// Deliberately the same rule `materialize-skill-roots.sh`'s `producer_core` applies — if the two ever
/// disagree, one of them is wrong about what "derived" means, and these tests are where that surfaces.
let private producerCore (text: string) =
    let body =
        if text.StartsWith("---\n", StringComparison.Ordinal) then
            match text.IndexOf("\n---", 3, StringComparison.Ordinal) with
            | -1 -> text
            | i -> text.Substring(i + 4)
        else
            text

    body.Replace("\r\n", "\n").Split('\n')
    |> Array.skipWhile (fun l -> String.IsNullOrWhiteSpace l || Regex.IsMatch(l, @"^#\s"))
    |> String.concat "\n"
    |> fun s -> s.TrimEnd('\n') + "\n"

let private manifestPaths =
    if Directory.Exists integrationsDir then
        Directory.GetFiles(integrationsDir, "*.manifest.json") |> Array.sort
    else
        [||]

/// (manifest file name, declared path, declared digest) for every row of every integration manifest.
let private manifestRows =
    manifestPaths
    |> Array.collect (fun m ->
        use doc = JsonDocument.Parse(File.ReadAllText m)

        match doc.RootElement.TryGetProperty "files" with
        | true, files ->
            files.EnumerateObject()
            |> Seq.map (fun p -> fileNameOf m, p.Name, nn (p.Value.GetString()))
            |> Seq.toArray
        | _ -> [||])

/// The overlay record's two sections, as path -> (authority-relative-path, authority_kind). Absent file ⇒
/// empty, which is the state these tests were written against: nothing explained the three stale rows.
let private overlaySection (section: string) =
    if not (File.Exists overlayPath) then
        Map.empty
    else
        use doc = JsonDocument.Parse(File.ReadAllText overlayPath)

        match doc.RootElement.TryGetProperty section with
        | true, s ->
            s.EnumerateObject()
            |> Seq.map (fun p ->
                let authority = nn (p.Value.GetProperty("authority").GetString())

                let kind =
                    match p.Value.TryGetProperty "authority_kind" with
                    | true, k -> nn (k.GetString())
                    | _ -> ""

                p.Name, (authority, kind))
            |> Map.ofSeq
        | _ -> Map.empty

let private superseded = overlaySection "superseded"
let private notInManifest = overlaySection "not_in_manifest"

/// The preset's override set, read from the producer rather than from the record under test: the command
/// files it ships, cross-checked against what `preset.yml` declares it `replaces`. Quote-tolerant —
/// preset.yml writes `file: "commands/x.md"`, and a pattern anchored on a bare `commands/` silently
/// matches nothing.
let private presetShipped =
    let dir = Path.Combine(presetDir, "commands")

    if Directory.Exists dir then
        Directory.GetFiles(dir, "speckit.*.md")
        |> Array.map fileNameOf
        |> Array.choose (fun f ->
            let m = Regex.Match(f, @"^speckit\.([A-Za-z0-9._-]+)\.md$")
            if m.Success then Some("speckit-" + m.Groups[1].Value.Replace(".", "-")) else None)
        |> Set.ofArray
    else
        Set.empty

let private presetDeclared =
    let yml = Path.Combine(presetDir, "preset.yml")

    if File.Exists yml then
        Regex.Matches(File.ReadAllText yml, "^\\s*file:\\s*[\"']?(commands/[^\"'\\s]+)[\"']?\\s*$", RegexOptions.Multiline)
        |> Seq.choose (fun m ->
            let b = fileNameOf m.Groups[1].Value
            let n = Regex.Match(b, @"^speckit\.([A-Za-z0-9._-]+)\.md$")
            if n.Success then Some("speckit-" + n.Groups[1].Value.Replace(".", "-")) else None)
        |> Set.ofSeq
    else
        Set.empty

let private skillIdOf (path: string) =
    let parts = path.Split('/')
    if parts.Length = 4 && parts[1] = "skills" then Some parts[2] else None

[<Tests>]
let tests =
    testList
        "IntegrationRecord"
        [
          // THE REGRESSION. Before `overlay.json` existed this failed with the three preset-overridden
          // rows unexplained, which is precisely the false red a materializer driven off the manifest
          // would have reported on a correct tree.
          test "every integration-manifest row is explained: digest matches, or the overlay record accounts for it" {
              Expect.isNonEmpty manifestRows "no manifest rows were read — the test found no records to check"

              let unexplained =
                  manifestRows
                  |> Array.choose (fun (manifest, rel, declared) ->
                      let abs = repoFile rel

                      if not (File.Exists abs) then
                          Some $"{manifest}: {rel} — declared but missing from the tree"
                      elif sha256Of abs = declared then
                          None
                      elif superseded.ContainsKey rel then
                          None // accounted for; the derivation itself is asserted by the next test
                      else
                          Some(
                              $"{manifest}: {rel} — declared {declared.Substring(0, 12)} "
                              + $"actual {(sha256Of abs).Substring(0, 12)}, "
                              + "and overlay.json does not record it as superseded"
                          ))

              Expect.isEmpty
                  unexplained
                  ($"integration records disagree with the committed bytes and nothing explains it:\n  "
                   + String.concat "\n  " unexplained
                   + "\nA row is honest when its digest matches OR overlay.json names the authority that "
                   + "supersedes it. Regenerate with scripts/materialize-skill-roots.sh.")
          }

          // A row may only be EXCUSED from its digest by naming an authority it genuinely derives from.
          // Without this, `overlay.json` would be a way to silence any mismatch.
          test "every superseded row derives from the authority the overlay record names" {
              let failures =
                  superseded
                  |> Map.toList
                  |> List.choose (fun (rel, (authority, _)) ->
                      let skill = repoFile rel
                      let src = repoFile authority

                      if not (File.Exists skill) then Some $"{rel} — superseded row has no file in the tree"
                      elif not (File.Exists src) then Some $"{rel} — declared authority {authority} does not exist"
                      elif producerCore (File.ReadAllText skill) = producerCore (File.ReadAllText src) then None
                      else Some $"{rel} — body does not derive from its declared authority {authority}")

              Expect.isEmpty
                  failures
                  ("overlay.json excuses rows from their digest without a producer that backs them:\n  "
                   + String.concat "\n  " failures)
          }

          // Staleness guard. The record is DERIVED from the preset, so a preset that gains or loses an
          // override must move it. A change to an override's CONTENT must not: the record names authorities,
          // not digests, which is what keeps the next preset change from re-creating the original defect.
          test "the overlay record's superseded set is exactly the preset's declared override set" {
              Expect.equal
                  presetDeclared
                  presetShipped
                  "preset.yml and the preset's commands/ directory disagree about which commands the preset provides"

              // Counted across BOTH sections, not just `superseded`. An override lands in `superseded`
              // only when the base integration also installed that skill, so a preset that overrode a
              // command the manifest does not list would be recorded — correctly — under
              // `not_in_manifest`. Reading only one section would fail that legitimate tree, and a test
              // that reds on a correct tree is the defect this item exists to remove.
              let recordedPreset =
                  Map.toSeq superseded
                  |> Seq.append (Map.toSeq notInManifest)
                  |> Seq.filter (fun (_, (_, kind)) -> kind = "preset-command")
                  |> Seq.choose (fst >> skillIdOf)
                  |> Set.ofSeq

              Expect.equal
                  recordedPreset
                  presetShipped
                  ("overlay.json's recorded preset-override set is stale with respect to the preset. "
                   + "Re-run scripts/materialize-skill-roots.sh and commit overlay.json.")
          }

          // The manifest is not a complete inventory of its own output root: the extension-provided skill
          // is produced into `.claude/skills` but was never a manifest row. Recorded, so a consumer meets
          // it as a documented fact rather than as an unattributed directory.
          test "producer-declared skills that the manifest omits are recorded in not_in_manifest" {
              let manifestIds = manifestRows |> Array.choose (fun (_, rel, _) -> skillIdOf rel) |> Set.ofArray

              let recordedAbsent = notInManifest |> Map.toSeq |> Seq.choose (fst >> skillIdOf) |> Set.ofSeq

              Expect.isFalse
                  (recordedAbsent |> Set.exists manifestIds.Contains)
                  "not_in_manifest lists a skill the manifest does in fact record"

              for rel, (authority, _) in Map.toList notInManifest do
                  let skill = repoFile rel
                  let src = repoFile authority
                  Expect.isTrue (File.Exists skill) $"{rel} — recorded in not_in_manifest but absent from the tree"
                  Expect.isTrue (File.Exists src) $"{rel} — declared authority {authority} does not exist"

                  Expect.equal
                      (producerCore (File.ReadAllText skill))
                      (producerCore (File.ReadAllText src))
                      $"{rel} — body does not derive from its declared authority {authority}"
          }
        ]
