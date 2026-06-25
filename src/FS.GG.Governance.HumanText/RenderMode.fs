namespace FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderMode =

    type RenderMode =
        | Json
        | Plain
        | Rich

    type ColorCapability =
        { IsTty: bool
          NoColorEnv: bool
          ExplicitPlain: bool
          Width: int option }

    let selectMode (explicitJson: bool) (cap: ColorCapability) : RenderMode =
        // JSON is the only contract and always wins; Width is consumed at render time, never here.
        if explicitJson then Json
        elif cap.IsTty && not cap.NoColorEnv && not cap.ExplicitPlain then Rich
        else Plain
