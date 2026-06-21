// The pure, total ship-verdict rollup (F024). Visibility lives in Ship.fsi (Constitution Principle
// II); this file carries NO top-level access modifiers. The gate/finding -> `EnforcementInput`
// mappings, the item-identity builder, and the sort-key helper live ONLY here and are absent from
// Ship.fsi (the Enforcement.fs / GatesJson.fs hidden-helper precedent).
//
// REUSES F023 `deriveEffectiveSeverity` for every per-item decision (FR-003) and the F019
// `RouteResult` / F018 `Gate` / F017 finding values verbatim. PURE and TOTAL (FR-008): no I/O, no
// clock, never throws; byte-identical for identical input (FR-009). Computes no audit.json/exit
// code/cache/freshness/policy dial (FR-012); the carried `route.Cost` is never read.

namespace FS.GG.Governance.Ship

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ship =

    // ── Hidden gate/finding -> EnforcementInput mappings (research D3/D4; absent from Ship.fsi) ──

    /// Map one selected gate to its F023 `EnforcementInput`: base `Blocking` iff the gate's maturity
    /// is a `block-on-*`, else base `Advisory`; maturity passed VERBATIM (research D3). Exhaustive
    /// `match` over the closed `Maturity` DU — a future maturity is a compile error, never a wildcard.
    let gateToInput (mode: RunMode) (profile: Profile) (gate: Gate) : EnforcementInput =
        let baseSeverity =
            match gate.Maturity with
            | Observe
            | Warn -> Advisory
            | BlockOnPr
            | BlockOnShip
            | BlockOnRelease -> Blocking

        { BaseSeverity = baseSeverity
          Maturity = gate.Maturity
          Mode = mode
          Profile = profile }

    /// Map one finding to its F023 `EnforcementInput` (research D4): a `GovernedRootUnknown` is base
    /// `Advisory` with maturity-equivalent `Warn` (always passing); a `ProtectedBoundaryUnknown` is
    /// base `Blocking` with `BlockOnShip` (floor = `gate`), so it blocks at `--mode gate` even when
    /// the change selected no gate. Exhaustive `match` over the closed `FindingZone` DU — no wildcard.
    let findingToInput (mode: RunMode) (profile: Profile) (finding: UnknownGovernedPathFinding) : EnforcementInput =
        let baseSeverity, maturity =
            match finding.Zone with
            | GovernedRootUnknown -> Advisory, Warn
            | ProtectedBoundaryUnknown _ -> Blocking, BlockOnShip

        { BaseSeverity = baseSeverity
          Maturity = maturity
          Mode = mode
          Profile = profile }

    // ── Hidden item identity + stable sort key (research D6; absent from Ship.fsi) ──

    /// The stable composite sort key for an enforced item: `"gate:" + gateIdValue id` for a gate,
    /// `"finding:" + <normalized path> + ":" + findingIdToken id` for a finding. Ordinal string
    /// comparison orders gates before findings (`"finding:"` > `"gate:"`), gates by `GateId`, and
    /// findings by `(Path, finding-id token)` — reusing F018 `gateIdValue` and F017 `findingIdToken`,
    /// rendering no new id. Total over both kinds; exhaustive `match`, no wildcard.
    let itemSortKey (item: EnforcedItem) : string =
        match item.Id with
        | GateItem id -> "gate:" + gateIdValue id
        | FindingItem(id, GovernedPath path) -> "finding:" + path + ":" + findingIdToken id

    // ── The rollup (FR-002/FR-004/FR-007; the sole public entry point) ──

    let rollup (route: RouteResult) (mode: RunMode) (profile: Profile) : ShipDecision =
        // One enforced item per selected gate and per finding — mapped 1:1, none dropped (FR-010).
        let gateItems =
            route.SelectedGates
            |> List.map (fun selected ->
                { Id = GateItem selected.Gate.Id
                  Decision = deriveEffectiveSeverity (gateToInput mode profile selected.Gate) })

        let findingItems =
            route.Findings.Findings
            |> List.map (fun finding ->
                { Id = FindingItem(finding.Id, finding.Path)
                  Decision = deriveEffectiveSeverity (findingToInput mode profile finding) })

        let items = gateItems @ findingItems

        // Three-way partition on the F023 decision — disjoint and exhaustive over every item.
        // `Blockers` = effective Blocking; `Warnings` = base Blocking relaxed to effective Advisory;
        // `Passing` = base Advisory (never escalated — FR-011).
        let classify (item: EnforcedItem) =
            match item.Decision.EffectiveSeverity, item.Decision.BaseSeverity with
            | Blocking, _ -> 0 // blocker
            | Advisory, Blocking -> 1 // relaxed blocker -> warning
            | Advisory, Advisory -> 2 // passing

        let sorted = List.sortBy itemSortKey
        let blockers = items |> List.filter (fun i -> classify i = 0) |> sorted
        let warnings = items |> List.filter (fun i -> classify i = 1) |> sorted
        let passing = items |> List.filter (fun i -> classify i = 2) |> sorted

        let verdict = if List.isEmpty blockers then Pass else Fail

        let exitCodeBasis =
            match verdict with
            | Pass -> Clean
            | Fail -> Blocked

        { Verdict = verdict
          Blockers = blockers
          Warnings = warnings
          Passing = passing
          ExitCodeBasis = exitCodeBasis }
