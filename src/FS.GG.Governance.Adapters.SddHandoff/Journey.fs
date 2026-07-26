// Production-journey readiness → gate projection (Governance#324). Visibility lives in Journey.fsi.
// Pure and total: the producer validates receipts; Governance enforces and explains the typed fact.

namespace FS.GG.Governance.Adapters.SddHandoff

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Journey =

    let idOf (source: string) =
        let segments = source.Replace("\\", "/").Split('/') |> Array.toList

        let rec afterReadiness =
            function
            | "readiness" :: id :: _ -> Some id
            | _ :: rest -> afterReadiness rest
            | [] -> None

        afterReadiness segments
        |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        |> Option.defaultValue source

    let toGate (source: string) (readiness: JourneyReadiness) : Gate =
        let id = idOf source
        let maturity = if readiness.ObligationsUnmet = 0 then Warn else BlockOnShip
        let domain = DomainId "gameplay"
        let checkId = CheckId(sprintf "production-journey:%s" id)

        let description =
            sprintf
                "SDD production-journey readiness for '%s': obligationsUnmet=%d, provenanceDisposition=%A, blockingDiagnosticIds=[%s], relatedIds=[%s]"
                id
                readiness.ObligationsUnmet
                readiness.Disposition
                (String.concat ", " readiness.BlockingDiagnosticIds)
                (String.concat ", " readiness.RelatedIds)

        { Id = GateId(sprintf "gameplay:production-journey:%s" id)
          Domain = domain
          Description = description
          Prerequisites = []
          Cost = High
          Timeout = Gates.defaultTimeout
          Owner = Owner "platform"
          Maturity = maturity
          ProductCheck = false
          FreshnessKey =
            { Check = checkId
              Domain = domain
              Cost = High
              Environment = Ci
              Command = None } }
