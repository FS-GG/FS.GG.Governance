namespace FS.GG.Governance.Enforcement

open FS.GG.Governance.Config.Model

// F023: the first Phase-5 pure core — the typed enforcement vocabulary and the single pure, total
// `deriveEffectiveSeverity` that maps (base severity, maturity, run mode, profile) -> (effective
// severity, reason). The public surface is Enforcement.fsi (Principle II): this .fs carries NO
// `private`/`internal`/`public` modifiers — the `maturityFloor`/`profileTighten` maps, the token
// helpers, and the four reason builders are hidden by their ABSENCE from the .fsi (the
// `Kernel/Json.fs` + `GatesJson.fs` precedent). PURE and TOTAL (FR-005): no I/O, no git, no clock;
// deterministic (FR-006) — byte-identical for identical input; never throws. REUSES F014
// `Maturity`/`ProfileId` verbatim (FR-003).

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Enforcement =

    // ── Closed levers & severity ──

    type RunMode =
        | Sandbox
        | Inner
        | Focused
        | Verify
        | Gate
        | Release

    type Profile =
        | Light
        | Standard
        | Strict
        | Release

    type Severity =
        | Advisory
        | Blocking

    type Recognized<'T> =
        | Recognized of 'T
        | Unrecognized of string

    // ── Input / result ──

    type EnforcementInput =
        { BaseSeverity: Severity
          Maturity: Maturity
          Mode: RunMode
          Profile: Profile }

    type EnforcementDecision =
        { BaseSeverity: Severity
          Maturity: Maturity
          Mode: RunMode
          Profile: Profile
          EffectiveSeverity: Severity
          Reason: string }

    // ── Ordering (exposed: the order IS the enforcement semantics, research D3) ──

    let runModeOrdinal (mode: RunMode) : int =
        match mode with
        | Sandbox -> 0
        | Inner -> 1
        | Focused -> 2
        | Verify -> 3
        | Gate -> 4
        | RunMode.Release -> 5

    // Inverse of `runModeOrdinal` over the clamped 0..5 ordinal range, used to name the governing
    // boundary mode in reason text. Hidden (absent from the .fsi). The clamp in `deriveEffectiveSeverity`
    // guarantees the argument is 0..5; the `_` arm covers the saturated `release` ordinal.
    let modeOfOrdinal (n: int) : RunMode =
        match n with
        | 0 -> Sandbox
        | 1 -> Inner
        | 2 -> Focused
        | 3 -> Verify
        | 4 -> Gate
        | _ -> RunMode.Release

    // ── Canonical tokens (hidden helpers; the `.fsi` exposes only the typed values) ──

    let maturityToken (m: Maturity) : string =
        match m with
        | Observe -> "observe"
        | Warn -> "warn"
        | BlockOnPr -> "block-on-pr"
        | BlockOnShip -> "block-on-ship"
        | BlockOnRelease -> "block-on-release"

    let runModeToken (mode: RunMode) : string =
        match mode with
        | Sandbox -> "sandbox"
        | Inner -> "inner"
        | Focused -> "focused"
        | Verify -> "verify"
        | Gate -> "gate"
        | RunMode.Release -> "release"

    let profileToken (p: Profile) : string =
        match p with
        | Light -> "light"
        | Standard -> "standard"
        | Strict -> "strict"
        | Profile.Release -> "release"

    // ── Profile <-> F014 ProfileId mapping (FR-003) ──

    let profileToProfileId (profile: Profile) : ProfileId = ProfileId(profileToken profile)

    let profileOfProfileId (id: ProfileId) : Recognized<Profile> =
        let (ProfileId raw) = id

        match raw with
        | "light" -> Recognized Light
        | "standard" -> Recognized Standard
        | "strict" -> Recognized Strict
        | "release" -> Recognized Profile.Release
        | other -> Unrecognized other

    // ── Total string recognition (FR-011, US2) — exact-token; no trim/case-fold/default ──

    let recognizeMode (raw: string) : Recognized<RunMode> =
        match raw with
        | "sandbox" -> Recognized Sandbox
        | "inner" -> Recognized Inner
        | "focused" -> Recognized Focused
        | "verify" -> Recognized Verify
        | "gate" -> Recognized Gate
        | "release" -> Recognized RunMode.Release
        | other -> Unrecognized other

    let recognizeProfile (raw: string) : Recognized<Profile> =
        match raw with
        | "light" -> Recognized Light
        | "standard" -> Recognized Standard
        | "strict" -> Recognized Strict
        | "release" -> Recognized Profile.Release
        | other -> Unrecognized other

    // ── Hidden enforcement maps the derivation reduces against (research D3/D4) ──

    let maturityFloor (m: Maturity) : int option =
        match m with
        | Observe -> None
        | Warn -> None
        | BlockOnPr -> Some 4
        | BlockOnShip -> Some 4
        | BlockOnRelease -> Some 5

    let profileTighten (p: Profile) : int =
        match p with
        | Light -> 0
        | Standard -> 0
        | Strict -> 1
        | Profile.Release -> 2

    let clamp (lo: int) (hi: int) (v: int) : int = max lo (min hi v)

    // ── Hidden reason builders (research D6) — one fixed sentence per branch, interpolating only the
    //    lower-case canonical tokens of the typed inputs. No clock, host path, or environment value. ──

    let withholdReason (m: Maturity) : string =
        sprintf "maturity '%s' withholds blocking; no run mode or profile can make it block" (maturityToken m)

    let baseAdvisoryReason (p: Profile) : string =
        sprintf
            "base severity is advisory; '%s' profile does not escalate it (per-class strictness dials deferred)"
            (profileToken p)

    let blockingReason (mode: RunMode) (floorMode: RunMode) (m: Maturity) (p: Profile) : string =
        sprintf
            "run mode '%s' reaches the '%s' blocking boundary for maturity '%s' under '%s' profile"
            (runModeToken mode)
            (runModeToken floorMode)
            (maturityToken m)
            (profileToken p)

    let relaxedReason (p: Profile) (m: Maturity) (floorMode: RunMode) (mode: RunMode) : string =
        sprintf
            "'%s' profile does not block this '%s' finding outside the '%s' boundary (run mode '%s')"
            (profileToken p)
            (maturityToken m)
            (runModeToken floorMode)
            (runModeToken mode)

    // ── The derivation (FR-005 total, FR-006 deterministic) ──

    let deriveEffectiveSeverity (input: EnforcementInput) : EnforcementDecision =
        let effective, reason =
            match input.Maturity with
            // (1) Withhold — observe/warn never block, overriding mode and profile (FR-007).
            | Observe
            | Warn -> Advisory, withholdReason input.Maturity
            | _ ->
                match input.BaseSeverity with
                // (2) Base-advisory — this core never escalates (research D4).
                | Advisory -> Advisory, baseAdvisoryReason input.Profile
                // (3) Blocking-eligible — block iff the run mode reaches the profile-adjusted floor.
                | Blocking ->
                    match maturityFloor input.Maturity with
                    | None ->
                        // Unreachable: observe/warn are handled above; kept total without a partial fn.
                        Advisory, withholdReason input.Maturity
                    | Some floor ->
                        let effectiveFloor = clamp 0 5 (floor - profileTighten input.Profile)
                        let floorMode = modeOfOrdinal effectiveFloor

                        if runModeOrdinal input.Mode >= effectiveFloor then
                            Blocking, blockingReason input.Mode floorMode input.Maturity input.Profile
                        else
                            Advisory, relaxedReason input.Profile input.Maturity floorMode input.Mode

        { BaseSeverity = input.BaseSeverity
          Maturity = input.Maturity
          Mode = input.Mode
          Profile = input.Profile
          EffectiveSeverity = effective
          Reason = reason }
