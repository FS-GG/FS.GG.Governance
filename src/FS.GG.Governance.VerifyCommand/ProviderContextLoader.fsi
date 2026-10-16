namespace FS.GG.Governance.VerifyCommand

/// Package-backed declaration admission. The original capture owner remains with the caller.
module internal ProviderContextLoader =
    /// Strict byte decoding and independent policy binding; no recorded resolved set is trusted.
    val decode:
        ProviderContext.Selection -> byte array -> byte array ->
            Result<FS.GG.Governance.Config.CapabilityBindings.ResolutionRequest, Fsgg.ProviderCatalog.Diagnostic list>

    /// Read immutable observations from the original ready session; never acquire or release it.
    /// This establishes declaration identity only, not current source or execution acceptance.
    val load:
        ProviderContext.Selection -> ProviderContextCapture.CaptureSession ->
            Result<FS.GG.Governance.Config.CapabilityBindings.ResolutionRequest, Fsgg.ProviderCatalog.Diagnostic list>
