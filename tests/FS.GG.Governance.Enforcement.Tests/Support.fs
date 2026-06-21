module FS.GG.Governance.Enforcement.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

// Shared real-input domains for the F023 tests (Principle V — every value below is a real,
// literally-constructible typed lever value, never a mock). The entire input domain is the finite
// cross-product of base severity × maturity × run mode × profile (2 × 5 × 6 × 4 = 240), so the
// totality/determinism/carry sweeps enumerate it exhaustively and the FsCheck arbitraries draw from
// the same finite enumerations — every generated input is a constructible lever value.

// ── The four total lever domains ──

/// All six run modes, least -> most protective (data-model ordinal table).
let allModes: RunMode list =
    [ Sandbox; Inner; Focused; Verify; Gate; RunMode.Release ]

/// All four profiles, least -> most strict.
let allProfiles: Profile list =
    [ Light; Standard; Strict; Profile.Release ]

/// Both severities.
let allSeverities: Severity list = [ Advisory; Blocking ]

/// All five F014 maturities (reused verbatim).
let allMaturities: Maturity list =
    [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]

/// The full cross-product (2 × 5 × 6 × 4 = 240 inputs) driving the enumeration-based
/// totality/determinism/carry tests.
let allInputs: EnforcementInput list =
    [ for s in allSeverities do
          for m in allMaturities do
              for md in allModes do
                  for p in allProfiles do
                      { BaseSeverity = s; Maturity = m; Mode = md; Profile = p } ]

// ── FsCheck arbitraries over the finite enumerations ──

let private elements xs = Gen.elements xs

type EnforcementArbs =
    static member RunMode() = Arb.fromGen (elements allModes)
    static member Profile() = Arb.fromGen (elements allProfiles)
    static member Severity() = Arb.fromGen (elements allSeverities)
    static member Maturity() = Arb.fromGen (elements allMaturities)

    static member EnforcementInput() =
        gen {
            let! s = elements allSeverities
            let! m = elements allMaturities
            let! md = elements allModes
            let! p = elements allProfiles
            return { BaseSeverity = s; Maturity = m; Mode = md; Profile = p }
        }
        |> Arb.fromGen

/// FsCheck config wiring the lever arbitraries (used by the property tests).
let fsCheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<EnforcementArbs> ] }

// ── Canonical / invalid token tables for recognition assertions (enforcement-decision §3) ──

/// The six canonical run-mode tokens paired with their typed value.
let canonicalModeTokens: (string * RunMode) list =
    [ "sandbox", Sandbox
      "inner", Inner
      "focused", Focused
      "verify", Verify
      "gate", Gate
      "release", RunMode.Release ]

/// The four canonical profile tokens paired with their typed value.
let canonicalProfileTokens: (string * Profile) list =
    [ "light", Light
      "standard", Standard
      "strict", Strict
      "release", Profile.Release ]

/// A representative invalid set: a case-variant, a non-token, the empty string, a whitespace-padded
/// token, and two near-misses (enforcement-decision §3). None of these is canonical.
let invalidTokens: string list =
    [ "Gate"; "ship"; ""; "  inner "; "lite"; "normal" ]

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
