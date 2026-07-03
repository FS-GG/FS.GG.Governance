module FS.GG.Governance.ReleaseRules.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules.Model

// Shared REAL-input builders + FsCheck generators for the F053 tests (Principle V — every value below is
// a real, literally-constructible typed value, never a mock). The evaluation/rollup input domain is the
// finite cross-product of rule kind × fact state × base severity × maturity, all literally constructible,
// so the coverage sweeps enumerate it and the FsCheck arbitraries draw from the same finite enumerations.
// No network, no governed repository, no process (SC-004).

// ── The closed enumerations (mirrors the source DUs) ──

/// The seven closed release rule kinds, in declaration order — for one-fixture-per-kind coverage.
/// 088: ApiCompatibility is the additive seventh case.
let allKinds: ReleaseRuleKind list =
    [ VersionBump
      PackageMetadata
      TemplatePins
      PublishPlan
      TrustedPublishing
      Provenance
      ApiCompatibility ]

/// Both base severities (the F023 lever a rule declares directly).
let allSeverities: Severity list = [ Advisory; Blocking ]

/// All five F014 maturities.
let allMaturities: Maturity list =
    [ Observe; Warn; BlockOnPr; BlockOnShip; BlockOnRelease ]

/// The three fact states (the supplied tri-state per kind).
let allFactStates: FactState list = [ Met; Unmet; Unrecoverable ]

// ── Real rule / facts builders ──

/// A blocking-at-release rule: base `Blocking` + `BlockOnRelease` maturity — a violation BLOCKS.
let blocking (kind: ReleaseRuleKind) (surface: string) : ReleaseRule =
    { Kind = kind
      Surface = SurfaceId surface
      BaseSeverity = Blocking
      Maturity = BlockOnRelease }

/// An advisory rule: base `Advisory` + `Warn` maturity — a violation WARNS/PASSES, never blocks.
let advisory (kind: ReleaseRuleKind) (surface: string) : ReleaseRule =
    { Kind = kind
      Surface = SurfaceId surface
      BaseSeverity = Advisory
      Maturity = Warn }

/// A blocking rule whose maturity is relaxed to `Warn` — base `Blocking` derives effective `Advisory`,
/// so a violation becomes a visible Warning (the FR-010 / SC-006 paired-relax fixture).
let relaxed (kind: ReleaseRuleKind) (surface: string) : ReleaseRule =
    { (blocking kind surface) with Maturity = Warn }

/// Build `ReleaseFacts` from an explicit `(kind, state)` list (absent kinds resolve to `Unrecoverable`).
let factsOf (pairs: (ReleaseRuleKind * FactState) list) : ReleaseFacts =
    { States = Map.ofList pairs }

/// Facts marking every given rule's kind `Met`.
let allMet (rules: ReleaseRule list) : ReleaseFacts =
    factsOf (rules |> List.map (fun r -> r.Kind, Met))

// ── FsCheck generators over the finite enumerations (real rules / facts) ──

let private elements xs = Gen.elements xs

/// A real declared rule: random kind / surface / base severity / maturity, all from the finite domains.
let private genRule (i: int) : Gen<ReleaseRule> =
    gen {
        let! kind = elements allKinds
        let! sev = elements allSeverities
        let! mat = elements allMaturities
        return
            { Kind = kind
              Surface = SurfaceId(sprintf "surface-%d" i)
              BaseSeverity = sev
              Maturity = mat }
    }

/// A real `ReleaseRule list` of up to a few rules.
let genRules: Gen<ReleaseRule list> =
    gen {
        let! n = Gen.choose (0, 6)
        return! Gen.collectToList genRule [ 1..n ]
    }

/// A real `ReleaseFacts`: a random subset of kinds each mapped to a random `FactState` (absent kinds
/// exercise the fail-safe). Built from a per-kind optional state so the map is honestly partial.
let genFacts: Gen<ReleaseFacts> =
    gen {
        let! pairs =
            allKinds
            |> List.map (fun k ->
                gen {
                    let! include' = Gen.elements [ true; false ]
                    let! state = elements allFactStates
                    return if include' then Some(k, state) else None
                })
            |> Gen.sequenceToList

        return { States = pairs |> List.choose id |> Map.ofList }
    }

let genRulesAndFacts: Gen<ReleaseRule list * ReleaseFacts> =
    gen {
        let! rules = genRules
        let! facts = genFacts
        return rules, facts
    }

type ReleaseArbs =
    static member ReleaseRuleKind() = Arb.fromGen (elements allKinds)
    static member Severity() = Arb.fromGen (elements allSeverities)
    static member Maturity() = Arb.fromGen (elements allMaturities)
    static member ReleaseRules() = Arb.fromGen genRules
    static member ReleaseFacts() = Arb.fromGen genFacts
    static member RulesAndFacts() = Arb.fromGen genRulesAndFacts

/// FsCheck config wiring the release arbitraries (used by the property tests).
let fsCheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<ReleaseArbs> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
