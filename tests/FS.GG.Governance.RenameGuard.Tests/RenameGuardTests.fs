module FS.GG.Governance.RenameGuard.Tests.RenameGuardTests

// 083 — the governance-side fs-gg-ui rename guard (ADR-0003, cross-repo P5).
//
// This module is the WHOLE deliverable: a self-contained Expecto regression guard (Tier 2, no public
// F# surface, no .fsi, no surface-area baseline). It scans the git-tracked tree for the legacy
// version-machinery token set (fs-skia-ui-version / fs-skia-ui-bom / FsSkiaUiVersion /
// fs-skia-ui/v<n>), proves zero remain on `main` today (R1), fails loudly with an actionable
// diagnostic if any is reintroduced (R3-R7), and spares the legitimate historical-repository
// provenance prose naming the predecessor `EHotwagner/FS-Skia-UI` repository (R2).
//
// Real evidence (Principle V): R1/R2 scan the REAL tracked tree (no mocks); the red-path tests
// (R3-R7) pass literal input strings to the pure matcher `scanText` — its real domain input, not a
// faked dependency, so no `Synthetic` token applies. "No committed tripwire" is the only sense of
// synthetic here: the red-path literals live in this scan-excluded directory, never as a tracked
// fixture the production scan would read.
//
// All bindings are private to this module (no `.fsi`, no public surface — FR-007).

open System
open System.IO
open System.Diagnostics
open System.Text.RegularExpressions
open Expecto

// ---------------------------------------------------------------------------------------------------
// data-model §ScanResult — the per-match value the matcher emits.
// ---------------------------------------------------------------------------------------------------

type Violation =
    { File: string
      Line: int
      Class: string
      Matched: string
      Replacement: string }

// ---------------------------------------------------------------------------------------------------
// data-model §ForbiddenToken — the legacy version-machinery set.
//
// R6 mechanism (1): the patterns are ASSEMBLED FROM FRAGMENTS, never written as one literal
// suffix-bearing legacy token, so this guard source itself carries no token its own scan would flag.
// The load-bearing scoping insight (research D3): every machinery token carries a
// `version`/`bom`/`/v<n>` SUFFIX; the bare predecessor repo name never does — so suffix-anchoring,
// not the allowlist, is what spares lineage prose by construction.
// ---------------------------------------------------------------------------------------------------

// Word fragments (assembled, so the source holds no contiguous legacy token — R6 mechanism 1).
let private fs = "fs"
let private skia = "sk" + "ia"
let private ui = "u" + "i"
let private versionWord = "ver" + "sion"
let private bomWord = "b" + "om"

let private sep = @"[ _.\-]" // a REQUIRED separator: space, underscore, dot, or hyphen.
let private sepOpt = @"[ _.\-]?" // an OPTIONAL separator (the PascalCase CPM property carries none).

let private rx (pattern: string) = Regex(pattern, RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

type private ForbiddenToken =
    { Class: string
      Pattern: Regex
      Replacement: string }

// Order matters for the per-line span dedup in `scanText`: the kebab/dot/underscore contract-id forms
// are declared BEFORE the PascalCase CPM property, so a separated form like `fs-skia-ui-version`
// (which both the required-separator contract pattern and the optional-separator CPM pattern match on
// the same span) is attributed to its kebab replacement `fs-gg-ui-version`. The no-separator form
// `FsSkiaUiVersion` is matched ONLY by the CPM pattern (the contract patterns require a separator), so
// it correctly keeps the `FsGgUiVersion` replacement (data-model §ForbiddenToken; FR-004).
let private forbiddenTokens : ForbiddenToken list =
    [ { Class = "contract id (version)"
        Pattern = rx (fs + sep + skia + sep + ui + sep + versionWord)
        Replacement = "fs-gg-ui-version" }
      { Class = "contract id (bom)"
        Pattern = rx (fs + sep + skia + sep + ui + sep + bomWord)
        Replacement = "fs-gg-ui-bom" }
      { Class = "snapshot-tag namespace"
        Pattern = rx (fs + sep + skia + sep + ui + "/v([0-9]|\\*)")
        Replacement = "fs-gg-ui/v*" }
      { Class = "CPM property"
        Pattern = rx (fs + sepOpt + skia + sepOpt + ui + sepOpt + versionWord)
        Replacement = "FsGgUiVersion" } ]

// ---------------------------------------------------------------------------------------------------
// data-model §ProvenanceAllowlist — the four documentary files whose lineage prose legitimately names
// the predecessor repository and must stay byte-identical (FR-006). The allowlist narrows WHERE prose
// is tolerated; it never disables a pattern.
// ---------------------------------------------------------------------------------------------------

let private provenanceAllowlist : string list =
    [ ".specify/memory/constitution.md"
      "docs/governance-design/index.md"
      "docs/initial-design.md"
      "docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md" ]

// ---------------------------------------------------------------------------------------------------
// data-model §GuardSelfExclusion — the guard's own scaffolding, excluded from the PRODUCTION scan
// because it necessarily quotes the legacy tokens verbatim (as the strings the red-path tests assert
// `scanText` matches, and as the worked examples in this feature's spec docs). This is R6 mechanism
// (2): the red-path input literals cannot be fragment-assembled away without making the tests
// illegible, so the directories that carry them are skipped.
//
// RECORDED DEVIATION (flagged in the PR): the spec/data-model scoped this to a single prefix, the
// guard's test directory. But this feature's OWN spec directory (`specs/083-fs-gg-ui-rename-guard/`)
// also quotes the full suffix-bearing legacy tokens as worked examples — and those spec files become
// tracked on commit, which would self-trip R1. The spec dir is the exact same class of artifact as
// the test dir (the guard's own scaffolding documenting/testing the ban), so it is excluded on the
// identical rationale. Verified minimal: no OTHER tracked spec (older features) names the machinery
// tokens, so coverage elsewhere is unaffected. Neither exclusion disables a pattern.
let private scanExclusions : string list =
    [ "tests/FS.GG.Governance.RenameGuard.Tests/" // the guard's own test source (red-path literals).
      "specs/083-fs-gg-ui-rename-guard/" ] // the guard's own spec scaffolding (worked-example literals).

// ---------------------------------------------------------------------------------------------------
// The PURE matcher (no I/O) — data-model §ScanResult.
// ---------------------------------------------------------------------------------------------------

/// Apply every forbidden-token pattern line by line, emitting one `Violation` per match with its
/// `Class`/`Matched`/`Replacement` and a 1-based `Line`. When two patterns claim the exact same span
/// on a line (the contract-id and CPM forms of a separated `version` token), the earlier-declared
/// pattern wins, so the diagnostic carries a single canonical replacement.
let scanText (path: string) (contents: string) : Violation list =
    let lines = contents.Split('\n')

    [ for i in 0 .. lines.Length - 1 do
          let line = lines.[i]
          let mutable claimed = Set.empty

          for token in forbiddenTokens do
              for m in token.Pattern.Matches line do
                  let span = (m.Index, m.Length)

                  if not (Set.contains span claimed) then
                      claimed <- Set.add span claimed

                      yield
                          { File = path
                            Line = i + 1
                            Class = token.Class
                            Matched = m.Value
                            Replacement = token.Replacement } ]

// ---------------------------------------------------------------------------------------------------
// The failure-message renderer — data-model §Failure-message shape (FR-005/SC-006).
// ---------------------------------------------------------------------------------------------------

/// `"<File>:<Line> contains legacy <Class> '<Matched>' — rename to the fs-gg-ui root (<Replacement>)."`
let render (v: Violation) : string =
    sprintf
        "%s:%d contains legacy %s '%s' — rename to the fs-gg-ui root (%s)."
        v.File
        v.Line
        v.Class
        v.Matched
        v.Replacement

// ---------------------------------------------------------------------------------------------------
// The I/O reader — the lone, auditable I/O edge (Principle IV spirit: I/O isolated, pure core).
// ---------------------------------------------------------------------------------------------------

let private repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// Read a file as UTF-8; tolerate a binary/non-UTF-8/locked file as no-content (never throw on a
/// single file — FR-008, Principle VI). A `git` failure, by contrast, is fatal (see `scanTrackedTree`).
let private tryReadAllText (full: string) : string option =
    try
        Some(File.ReadAllText full)
    with _ ->
        None

let private isExcluded (relForwardSlash: string) : bool =
    (provenanceAllowlist |> List.contains relForwardSlash)
    || (scanExclusions
        |> List.exists (fun prefix -> relForwardSlash.StartsWith(prefix, StringComparison.Ordinal)))

/// Shell `git ls-files` from `repoRoot`, read each tracked file that is neither allowlisted nor under
/// a scan exclusion, fold `scanText`. Deterministic (tracked tree only — excludes bin/obj/untracked).
/// Fails LOUDLY if `git` itself cannot be started or exits non-zero (Principle VI, research D2).
let scanTrackedTree () : Violation list =
    let psi = ProcessStartInfo("git", "ls-files")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.WorkingDirectory <- repoRoot

    use proc =
        match Process.Start psi with
        | null -> failwith "rename-guard: could not start `git ls-files` to enumerate the tracked tree."
        | p -> p

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwithf "rename-guard: `git ls-files` failed (exit %d): %s" proc.ExitCode stderr

    let files =
        stdout.Split('\n')
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s <> "")

    [ for rel in files do
          let relForwardSlash = rel.Replace('\\', '/')

          if not (isExcluded relForwardSlash) then
              match tryReadAllText (Path.Combine(repoRoot, rel)) with
              | Some text -> yield! scanText relForwardSlash text
              | None -> () ]

// ---------------------------------------------------------------------------------------------------
// The contract suite (R1-R7) — contracts/rename-guard.contract.md. Each rule is one named test.
// ---------------------------------------------------------------------------------------------------

[<Tests>]
let renameGuard =
    testList
        "fs-gg-ui rename guard"
        [
          // R1 — Clean tree passes (FR-001, FR-008, SC-001).
          test "Production scan of the tracked tree finds zero legacy version-machinery identifiers" {
              let violations = scanTrackedTree ()

              let offenders =
                  violations |> List.map render |> String.concat Environment.NewLine

              Expect.isEmpty
                  violations
                  (sprintf "the tracked tree must carry no legacy version-machinery identifier, found:%s%s" Environment.NewLine offenders)
          }

          // R2 — Provenance passes untouched (FR-003, FR-006, SC-003).
          test "The four provenance files are present, allowlisted, and not flagged" {
              for rel in provenanceAllowlist do
                  let full = Path.Combine(repoRoot, rel)
                  Expect.isTrue (File.Exists full) (sprintf "provenance file should exist: %s" rel)
                  Expect.isTrue (isExcluded rel) (sprintf "provenance file should be allowlisted: %s" rel)

              // With the four provenance files present, the production scan is still empty (their bare
              // `FS-Skia-UI` prose matches no suffix-anchored pattern, and they are allowlisted regardless).
              Expect.isEmpty (scanTrackedTree ()) "provenance files must not cause any violation"
          }

          // R3 — A legacy CPM property is caught with the canonical PascalCase replacement
          // (FR-002, FR-005, SC-002).
          test "A FsSkiaUiVersion reference is caught with the FsGgUiVersion replacement" {
              let violations =
                  scanText "fake/Directory.Packages.props" "<FsSkiaUiVersion>1.0.0</FsSkiaUiVersion>"

              Expect.isNonEmpty violations "a legacy CPM property must be caught"

              let cpm =
                  violations |> List.tryFind (fun v -> v.Class = "CPM property")

              match cpm with
              | None -> failtest "expected a CPM-property violation"
              | Some v ->
                  Expect.stringContains v.Matched "FsSkiaUiVersion" "the matched text names the legacy property"
                  Expect.equal v.Replacement "FsGgUiVersion" "the canonical PascalCase replacement is surfaced"
          }

          // R4 — Legacy contract ids and the tag namespace are caught (FR-002, FR-004).
          test "Legacy contract ids and the fs-skia-ui tag namespace are caught" {
              let expectReplacement (input: string) (replacement: string) =
                  let violations = scanText "fake/contract.md" input

                  Expect.isTrue
                      (violations |> List.exists (fun v -> v.Replacement = replacement))
                      (sprintf "input %A must yield a violation naming replacement %s" input replacement)

              expectReplacement "id: fs-skia-ui-version" "fs-gg-ui-version"
              expectReplacement "id: fs-skia-ui-bom" "fs-gg-ui-bom"
              expectReplacement "tag: fs-skia-ui/v1" "fs-gg-ui/v*"
          }

          // R5 — Separator/case variants caught; the bare repo name not (Edge: variants; FR-003).
          test "Case and separator variants match; the bare FS-Skia-UI repo name does not" {
              let matches (input: string) =
                  scanText "fake/x.txt" input |> List.isEmpty |> not

              // Version-pinning variants (underscore / dot / all-caps tag) all match.
              Expect.isTrue (matches "Fs_Skia_Ui_Version") "underscore PascalCase variant matches"
              Expect.isTrue (matches "fs.skia.ui.bom") "dotted contract-id variant matches"
              Expect.isTrue (matches "FS-SKIA-UI/V2") "all-caps tag variant matches"

              // The bare predecessor repo name (no version/bom/v suffix) matches nothing (research D3).
              Expect.isFalse (matches "source-analysis of FS-Skia-UI") "the bare repo name must not match"

              Expect.isFalse
                  (matches "https://github.com/EHotwagner/FS-Skia-UI/blob/main/x.md")
                  "a repo URL (suffix /blob, not /v<n>) must not match"
          }

          // R6 — Canonical fs-gg-ui passes; the guard does not self-match (Edge: canonical / own fixtures).
          test "Canonical fs-gg-ui identifiers pass and the guard source does not self-trip" {
              let canonical =
                  "<FsGgUiVersion>1.0.0</FsGgUiVersion> id: fs-gg-ui-version id: fs-gg-ui-bom tag: fs-gg-ui/v1"

              Expect.isEmpty
                  (scanText "fake/canonical.txt" canonical)
                  "the canonical fs-gg-ui root is permitted (the guard forbids only the legacy root)"

              // Mechanism (1) fragment-assembled patterns + (2) the scan exclusion together keep the
              // production scan empty even though the red-path literals live in the tracked test source.
              Expect.isEmpty (scanTrackedTree ()) "the guard's own red-path literals must not self-trip R1"
          }

          // R7 — Diagnostic is actionable and self-describing (FR-005, SC-006).
          test "A violation message names the file, identifier, and fs-gg-ui replacement" {
              let violations =
                  scanText "src/Directory.Packages.props" "<FsSkiaUiVersion>1.0.0</FsSkiaUiVersion>"

              let v = List.head violations
              let message = render v

              Expect.stringContains message "src/Directory.Packages.props" "names the file"
              Expect.stringContains message (string v.Line) "names the line"
              Expect.stringContains message "FsSkiaUiVersion" "names the offending identifier"
              Expect.stringContains message "FsGgUiVersion" "names the canonical replacement"
          } ]
