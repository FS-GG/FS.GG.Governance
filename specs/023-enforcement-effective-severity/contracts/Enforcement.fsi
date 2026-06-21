// Curated public signature contract for the Phase-5 enforcement core (F023).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Enforcement.fs carries NO `private`/`internal`/`public` modifiers on top-level
// bindings — visibility is presence/absence here. The maturity-floor map, the profile-tighten
// map, and the four reason builders live ONLY in the .fs and are absent here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Enforcement.fs body exists (Principle I). It is the first Phase-5 pure core — the typed
// enforcement vocabulary and the single total, deterministic `deriveEffectiveSeverity` that maps
// (base severity, maturity, run mode, profile) -> (effective severity, reason), the pure decision
// the later `fsgg ship` and `audit.json` rows will reuse. It is PURE and TOTAL (FR-005): no I/O,
// no git, no clock, no `.fsgg/policy.yml` parsing (FR-014), never throws, and is byte-for-byte
// identical for identical input (FR-006, SC-004). It computes NO ship/merge verdict, blockers
// list, exit code, or cross-finding rollup (FR-013) and emits no CLI.
//
// It REUSES F014 `FS.GG.Governance.Config.Model.Maturity` and `...ProfileId` verbatim (FR-003) and
// introduces only the vocabulary F014 did not model: the six-value run `Mode`, the base/effective
// `Severity`, and the four-value `Profile` strictness. These `RunMode`/`Severity` types are NEW
// here and are NOT the kernel's three-value `RunMode` / two-case `Severity` (the Phase-2/5 line
// references the kernel nowhere; FR-001 needs six modes the kernel ladder cannot express —
// research D2). The maturity->floor and profile->tighten mappings are the plan-time
// reconciliations D3/D4 against the design's run-mode ladder.

namespace FS.GG.Governance.Enforcement

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Enforcement =

    // ── Closed levers & severity ──

    /// Where a Governance command is running and which boundary it protects (FR-001). A closed,
    /// ORDERED set of exactly six values, least -> most protective. NEW here — not the kernel's
    /// three-value `RunMode` (research D2).
    type RunMode =
        | Sandbox
        | Inner
        | Focused
        | Verify
        | Gate
        | Release

    /// How strict the project chose to be at the boundary (FR-002). A closed, ORDERED set of
    /// exactly four values, least -> most strict. Maps to/from F014 `ProfileId` (FR-003).
    type Profile =
        | Light
        | Standard
        | Strict
        | Release

    /// The base/effective severity value (FR-004). Base severity is an immutable INPUT; effective
    /// severity is the DERIVED output; both use this same enumeration so they are directly
    /// comparable. NEW here — not the kernel's `Severity` (research D2).
    type Severity =
        | Advisory
        | Blocking

    /// The total outcome of recognizing a caller-supplied string as a canonical lever (FR-011):
    /// the typed value, or the offending string carried unchanged. Never an exception, never a
    /// silent default.
    type Recognized<'T> =
        | Recognized of 'T
        | Unrecognized of string

    // ── Input / result ──

    /// The four levers for one finding (research D5). `BaseSeverity` and `Maturity` come from the
    /// rule engine / F014 facts; `Mode` and `Profile` come from the run context.
    type EnforcementInput =
        { BaseSeverity: Severity
          Maturity: Maturity
          Mode: RunMode
          Profile: Profile }

    /// The explainable per-finding decision (FR-010). Carries all six required fields: the unchanged
    /// base severity (echoed byte-identical, FR-009), run mode, profile, maturity, the derived
    /// effective severity, and a non-empty reason naming the responsible levers. No rollup, verdict,
    /// blockers, or exit code (FR-013).
    type EnforcementDecision =
        { BaseSeverity: Severity
          Maturity: Maturity
          Mode: RunMode
          Profile: Profile
          EffectiveSeverity: Severity
          Reason: string }

    // ── Ordering & profile/ProfileId mapping ──

    /// The intrinsic enforcement ordinal of a run mode (`Sandbox` 0 .. `Release` 5). Total; exposed
    /// because the ordering IS part of the enforcement semantics (research D3).
    val runModeOrdinal: mode: RunMode -> int

    /// The canonical F014 `ProfileId` for a typed profile (`Light` -> `ProfileId "light"`, etc.).
    val profileToProfileId: profile: Profile -> ProfileId

    /// Recognize an F014 `ProfileId` as a canonical typed profile; non-canonical ids yield a total
    /// `Unrecognized` carrying the id's string (FR-011). Total, never throws.
    val profileOfProfileId: id: ProfileId -> Recognized<Profile>

    // ── Total string recognition (FR-011, US2) ──

    /// Recognize a caller/file-supplied string as a canonical run mode (`"sandbox"` .. `"release"`),
    /// or `Unrecognized` with the offending value. Total; never an exception, never a default.
    val recognizeMode: raw: string -> Recognized<RunMode>

    /// Recognize a caller/file-supplied string as a canonical profile (`"light"`/`"standard"`/
    /// `"strict"`/`"release"`), or `Unrecognized` with the offending value. Total.
    val recognizeProfile: raw: string -> Recognized<Profile>

    // ── The derivation (FR-005 total, FR-006 deterministic) ──

    /// Derive a finding's effective severity and the reason for it from its base severity, rule
    /// maturity, run mode, and profile. TOTAL over the complete cross-product of inputs (SC-001) and
    /// DETERMINISTIC (SC-004): identical inputs always yield identical effective severity and
    /// identical reason text, with no clock, environment, ordering, or host-path influence. Echoes
    /// the base severity unchanged (FR-009). `Observe`/`Warn` always derive `Advisory` regardless of
    /// mode/profile (FR-007); a base-blocking finding blocks iff the run mode reaches the maturity's
    /// profile-adjusted blocking boundary (FR-008); a base-advisory finding stays advisory (this
    /// core never escalates it — research D4). Never throws.
    val deriveEffectiveSeverity: input: EnforcementInput -> EnforcementDecision
