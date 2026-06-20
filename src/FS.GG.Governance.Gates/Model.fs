// Gate-domain types for the typed gate registry (F018). The public surface is fixed by Model.fsi
// (Principle II); no top-level binding here carries an access modifier. These are product-neutral,
// YAML-free values that `Gates.buildRegistry` returns; they reuse the F014 typed-fact newtypes
// (`DomainId`, `Owner`, `Cost`, `Maturity`, `TimeoutLimit`, `CommandId`, `EnvironmentClass`,
// `CheckId`) rather than redefining them (FR-004, FR-013). Every emitted collection is in
// deterministic `GateId`-ordinal order (FR-011, SC-003/SC-006).

namespace FS.GG.Governance.Gates

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type GateId = GateId of string

    type GatePrerequisite = RequiresCommand of command: CommandId

    type FreshnessKey =
        { Check: CheckId
          Domain: DomainId
          Cost: Cost
          Environment: EnvironmentClass
          Command: CommandId option }

    type Gate =
        { Id: GateId
          Domain: DomainId
          Description: string
          Prerequisites: GatePrerequisite list
          Cost: Cost
          Timeout: TimeoutLimit
          Owner: Owner
          Maturity: Maturity
          ProductCheck: bool
          FreshnessKey: FreshnessKey }

    type GateRegistry = { Gates: Gate list }

    // The stable wire string of a `GateId` (e.g. `GateId "build:tests"` → `"build:tests"`).
    // Total: a single projection out of the newtype, never throws.
    let gateIdValue (id: GateId) : string =
        let (GateId s) = id
        s
