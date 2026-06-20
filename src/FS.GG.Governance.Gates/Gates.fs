// The `buildRegistry` entry point for the typed gate registry (F018). The public surface is fixed
// by Gates.fsi (Principle II) — only `defaultTimeout` and `buildRegistry` are exported; every
// helper below is hidden by the signature (no access modifiers needed). `buildRegistry` is PURE
// and TOTAL (FR-013, FR-007): no I/O, no git, no clock, never throws, byte-for-byte identical for
// identical input (FR-011, SC-003). It consumes the already-typed, already-validated F014 facts;
// it re-parses no `.fsgg` YAML, re-validates no catalog, and senses no git (FR-013, FR-016).
//
// There is NO diagnostic channel and NO failure mode: F014's `Schema.validate` has already proven
// the facts free of duplicate check ids and dangling cross-references, so the registry PRESERVES
// those guarantees by construction (an injective `GateId` per check) rather than re-checking them
// (research D4). A re-validation layer would be a dead branch no `Valid TypedFacts` can reach.

namespace FS.GG.Governance.Gates

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Gates =

    // ── The documented default timeout (FR-010, SC-005) ──

    /// Five minutes — the bounded fallback when a check references no declared command (or the
    /// command is absent because `tooling.yml` is absent). A gate ALWAYS carries a bounded timeout;
    /// this value is only CARRIED, never enforced or measured.
    let defaultTimeout = TimeoutLimit 300

    // ── Projection primitives (T011) ──

    /// The command-timeout index, built once from the optional tooling facts. `TypedFacts.Tooling`
    /// is a `ToolingFacts option`: an absent `tooling.yml` (`None`) yields an empty index, so every
    /// command lookup falls back to `defaultTimeout`. Maps each `CommandSpec.Id → CommandSpec.Timeout`
    /// so per-check timeout resolution is a single O(1) lookup (O(commands) to build the index).
    let private timeoutIndex (facts: TypedFacts) : Map<CommandId, TimeoutLimit> =
        facts.Tooling
        |> Option.map (fun t -> t.Commands)
        |> Option.defaultValue []
        |> List.map (fun c -> c.Id, c.Timeout)
        |> Map.ofList

    /// The stable, INJECTIVE gate id of a check: `GateId "<domain>:<checkId>"`. Injective over
    /// distinct checks because F014 guarantees check ids are unique catalog-wide and the domain
    /// qualifies them (FR-003/FR-005). Deterministic — never positional, time-derived, or random.
    let private gateIdOf (check: Check) : GateId =
        let (DomainId d) = check.Domain
        let (CheckId c) = check.Id
        GateId(sprintf "%s:%s" d c)

    /// A human-readable purpose composed from the declared ids ONLY — no raw YAML, host paths,
    /// timestamps, or product vocabulary beyond the declared check/domain ids (FR-004, SC-004).
    /// Deterministic for identical input.
    let private describe (check: Check) : string =
        let (DomainId d) = check.Domain
        let (CheckId c) = check.Id
        sprintf "Capability check '%s' in domain '%s'" c d

    /// The bounded timeout of a check: the referenced command's declared timeout when that command
    /// is in the index, else `defaultTimeout` — including when the check is command-less or
    /// `Tooling = None` (empty index) (FR-010, research D9). Always bounded; never enforced.
    let private timeoutOf (index: Map<CommandId, TimeoutLimit>) (check: Check) : TimeoutLimit =
        match check.Command with
        | Some c ->
            match Map.tryFind c index with
            | Some t -> t
            | None -> defaultTimeout
        | None -> defaultTimeout

    // ── Per-check projection (T014/T015/T021/T022/T023) ──

    /// Project one declared `Check` into one `Gate`, filling the full *Gate identities* field set.
    /// Pure and total: every field is a verbatim carry or a deterministic composition of declared
    /// ids. Maturity is carried VERBATIM (no blocking/advisory translation — that is Phase 5);
    /// `ProductCheck` is the MVP environment heuristic; `FreshnessKey` carries the declared identity
    /// inputs a later freshness/cache step will hash, evaluated by nothing here.
    let private projectCheck (index: Map<CommandId, TimeoutLimit>) (check: Check) : Gate =
        { Id = gateIdOf check
          Domain = check.Domain
          Description = describe check
          Prerequisites =
            match check.Command with
            | Some c -> [ RequiresCommand c ]
            | None -> []
          Cost = check.Cost
          Timeout = timeoutOf index check
          Owner = check.Owner
          Maturity = check.Maturity
          // MVP heuristic: the only declared product signal is the release environment (research D6).
          ProductCheck = (check.Environment = Release)
          FreshnessKey =
            { Check = check.Id
              Domain = check.Domain
              Cost = check.Cost
              Environment = check.Environment
              Command = check.Command } }

    // ── The entry point (T012, FR-001/FR-007/FR-011/FR-014) ──

    let buildRegistry (facts: TypedFacts) : GateRegistry =
        let index = timeoutIndex facts

        let gates =
            facts.Capabilities.Checks
            |> List.map (projectCheck index)
            // The single deterministic order: ordinal by the stable gate id. Re-ordering the
            // declared checks or commands cannot change it (FR-011/FR-012, SC-003/SC-006). The gate
            // dependency graph is trivially acyclic in this MVP (no gate-to-gate edges), so this is
            // the whole order and no topological pass is needed (research D5/D7).
            |> List.sortWith (fun a b -> String.CompareOrdinal(gateIdValue a.Id, gateIdValue b.Id))

        { Gates = gates }
