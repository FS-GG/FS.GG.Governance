namespace FS.GG.Governance.VerifyCommand

/// Explicit input selection only; parsing grants no filesystem, policy or execution authority.
module ProviderContext =
    /// Raw invocation locators and byte digests; the independent policy is never workspace-discovered.
    type Selection =
        { ProvenancePath: string
          ProvenanceSha256: string
          PolicyPath: string
          PolicySha256: string
          Platform: string }

    /// Located provider-selector refusals, distinct from unchanged legacy usage diagnostics.
    type SelectionError =
        | MissingValue of flag: string
        | DuplicateOption of flag: string
        | IncompleteSelection of missingFlags: string list
        | EmptyValue of flag: string
        | InvalidSha256 of flag: string

    /// Validate all five unique options together without rewriting argv or reading selected files.
    /// No provider option returns None. Digests must be exactly 64 ASCII hexadecimal characters;
    /// accepted raw spelling is retained. Locator/platform whitespace-only values refuse.
    val select: argv: string list -> Result<Selection option, SelectionError>
