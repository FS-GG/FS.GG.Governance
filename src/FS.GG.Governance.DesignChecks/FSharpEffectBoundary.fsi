namespace FS.GG.Governance.DesignChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FSharpEffectBoundary =

    /// Classified external capability observed at a declared transition or edge interpreter.
    type EffectCategory = Filesystem | Process | Environment | ClockOrRandomness | Network | UiOrHost | Persistence | MutableGlobalState

    /// The semantic delivery contract of an effect that can be repeated by an interpreter.
    type Delivery =
        { SuccessMessage: string option
          FailureMessage: string option
          RetryPolicy: string option
          Idempotency: string option }

    /// A narrow, time-bounded exception bound to one declared symbol.
    type Exemption = NoExemption | ActiveExemption of owner: string * rationale: string * reviewBy: System.DateOnly | InvalidExemption of reason: string

    /// Facts for one declared F# workflow boundary.  Names are evidence only: applicability is carried by
    /// `IsStatefulWorkflow`, so Model/Msg/Effect/update naming is never required.
    type BoundaryFacts =
        { Project: string
          Symbol: string
          Source: FS.GG.Governance.Config.Model.GovernedPath
          IsStatefulWorkflow: bool
          IsPureParserOrValidator: bool
          IsThinOneShotAdapter: bool
          DirectEffects: EffectCategory list
          CallbackHiddenState: bool
          ExceptionDrivenContinuation: bool
          EdgeInterpreter: string option
          Delivery: Delivery option
          Exemption: Exemption }

    /// Read declared compiled F# sources and conservatively classify effect symbols. Each exact key/value
    /// declaration binds only the immediately following same-named `let`; malformed policy fails closed.
    /// This sensor does not infer a workflow from a module or function name.
    val senseProject: root: string -> project: string -> Result<BoundaryFacts list, string>

    /// PURE/TOTAL policy evaluation.  Findings name both the prohibited category and nearest declared boundary.
    val evaluate:
        request: FS.GG.Governance.SurfaceChecks.Model.SurfaceCheckRequest ->
        boundaries: BoundaryFacts list -> FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list
