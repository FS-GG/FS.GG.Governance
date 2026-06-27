// Readiness → gate projection (F081, US3). Visibility lives in Readiness.fsi (Principle II).
// PURE and TOTAL. Projects the handoff's `readiness.*` block into a first-class typed `Gate` so SDD
// merge-boundary readiness participates in selection/severity/roll-up like any other gate (research
// D3, FR-009/FR-015 — resolving ADR-0002 queue item #4).

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Readiness =

    // The `<id>` segment of `readiness/<id>/governance-handoff.json`; falls back to the whole source.
    let internalIdOf (source: string) : string =
        let segs = source.Replace("\\", "/").Split('/') |> Array.toList

        let rec after =
            function
            | "readiness" :: id :: _ -> Some id
            | _ :: rest -> after rest
            | [] -> None

        match after segs with
        | Some id when id <> "" -> id
        | _ -> source

    // The handoff-gate domain (shared by evidence / readiness / integrity gates).
    let domain = DomainId "sdd-handoff"

    // A handoff `Gate` carrying the declared maturity verbatim — no command, default timeout, advisory
    // owner. The `FreshnessKey` is the always-available declared identity (carried, never evaluated).
    let buildGate (checkId: string) (maturity: Maturity) (description: string) : Gate =
        { Id = GateId(sprintf "sdd-handoff:%s" checkId)
          Domain = domain
          Description = description
          Prerequisites = []
          Cost = Cheap
          Timeout = Gates.defaultTimeout
          Owner = Owner "sdd-handoff"
          Maturity = maturity
          ProductCheck = false
          FreshnessKey =
            { Check = CheckId checkId
              Domain = domain
              Cost = Cheap
              Environment = LocalOrCi
              Command = None } }

    // A disposition is shippable only when it is one of the recognized clean tokens; anything else
    // (including an empty/unknown disposition) is treated as non-shippable ⇒ blocking-capable.
    let private shippableTokens = set [ "shippable"; "ready"; "clean"; "ship" ]

    let private isNonShippable (disposition: string) : bool =
        shippableTokens.Contains(disposition.Trim().ToLowerInvariant()) |> not

    let toGate (source: string) (block: ReadinessBlock) : Gate =
        let id = internalIdOf source
        let blocking = isNonShippable block.ShipDisposition || not (List.isEmpty block.BlockingDiagnosticIds)
        let maturity = if blocking then BlockOnShip else Warn

        let countsText =
            block.Counts
            |> List.map (fun (k, v) -> sprintf "%s=%d" k v)
            |> String.concat ", "

        let perViewText =
            block.PerViewState
            |> List.map (fun (k, v) -> sprintf "%s=%s" k v)
            |> String.concat ", "

        let description =
            sprintf
                "SDD merge-boundary readiness for '%s': shipDisposition=%s, verificationReadiness=%s, blockingDiagnosticIds=[%s], counts=[%s], perViewState=[%s]"
                id
                block.ShipDisposition
                block.VerificationReadiness
                (String.concat ", " block.BlockingDiagnosticIds)
                countsText
                perViewText

        buildGate (sprintf "readiness:%s" id) maturity description
