// Curated public signature contract for the audit.json projection (F025).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching AuditJson.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here. Every JSON writer and closed-enum token helper lives ONLY in
// the .fs and is absent here, exactly as `FS.GG.Governance.Kernel.Json`, `FS.GG.Governance.RouteJson`,
// and `FS.GG.Governance.GatesJson` keep their writer/token plumbing off their .fsi.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any AuditJson.fs
// body exists (Principle I). `ofShipDecision` is the PURE, TOTAL projection (FR-008): it renders one
// already-typed, already-ordered F024 `ShipDecision` into the deterministic, versioned `audit.json`
// document text — the stable machine-readable WHOLE-CHANGE verdict contract the later `fsgg ship`
// command, CI gates, branch-protection checks, agents, generated readiness views, and humans read
// instead of an in-memory value. It performs no I/O, no git, no clock, never throws, and is
// byte-for-byte identical for identical input (FR-007, SC-002). It re-derives, re-classifies,
// re-partitions, and re-orders nothing (the `ShipDecision` already fixed the verdict, the exit-code
// basis, the three-way partition, and each section's composite order); it recomputes no verdict from
// the item lists (FR-002), derives no numeric process exit code (FR-003), and emits no raw YAML, host
// path, timestamp, environment value, provenance reference, or cache-eligibility verdict (FR-012).
// Serialization uses the net10.0 shared-framework `System.Text.Json` — NO new `PackageReference`
// (FR-014).
//
// Sibling of F020 `FS.GG.Governance.RouteJson` (the per-change route view) and F021
// `FS.GG.Governance.GatesJson` (the whole-catalog gate view): this projects the WHOLE-CHANGE ship
// verdict (`ShipDecision`). Every item carries the full six-field F023 enforcement detail so a relaxed
// blocker rendered in the warnings section is always self-explaining and a profile can never hide the
// underlying verdict (the design's no-hide rule, FR-011, US3).

namespace FS.GG.Governance.AuditJson

open FS.GG.Governance.Ship.Model
open FS.GG.Governance.CacheEligibility.Model   // F045: CacheEligibilityReport (via the F041 ProjectReference)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AuditJson =

    /// The declared schema-version token stamped into every emitted document and recorded as the
    /// document's `schemaVersion` field (FR-013), so consumers can branch on the contract version and
    /// detect changes without string-scraping the output. A fixed, deterministic constant — never
    /// derived from a clock, environment, or input value. Bumped to `"fsgg.audit/v2"` for the F045
    /// embedded cache-eligibility contract so consumers detect the new shape.
    val schemaVersion: string

    /// Project an F024 `ShipDecision` together with an OPTIONAL F041 `CacheEligibilityReport` into its
    /// deterministic, versioned `audit.json` document text.
    ///
    /// Emits one top-level JSON object with fields in the FIXED order `schemaVersion`, `verdict`,
    /// `exitCodeBasis`, `blockers`, `warnings`, `passing`, `cacheEligibilityEvaluated` (the wire
    /// contract is fixed in contracts/audit-json-document.md):
    ///   • `verdict`       — the decision's `Verdict` verbatim: `pass` | `fail` (FR-002). NEVER
    ///                       recomputed from the rendered item sections.
    ///   • `exitCodeBasis` — the decision's `ExitCodeBasis` verbatim: `clean` | `blocked` (FR-003).
    ///                       NO numeric process exit code is derived (that is the later `fsgg ship`
    ///                       host edge), and NO basis is invented.
    ///   • `blockers`, `warnings`, `passing` — the decision's mutually-exclusive, jointly-exhaustive
    ///                       partition, each an ALWAYS-PRESENT array in the `ShipDecision`'s composite
    ///                       order (gates before findings, gates by `GateId`, findings by path then
    ///                       finding-id token) preserved verbatim (FR-005, FR-007). An empty section
    ///                       renders as a present, empty array (FR-009) — never omitted, never a
    ///                       placeholder item. Every item carries its identity (a gate by `kind:"gate"`
    ///                       + declared `GateId` via `gateIdValue`; a finding by `kind:"finding"` +
    ///                       declared `FindingId` token via `findingIdToken` + its governed `path`,
    ///                       neither re-parsed — FR-004, FR-010) and a nested `enforcement` object
    ///                       carrying all SIX F023 fields VERBATIM in record order `baseSeverity`,
    ///                       `maturity`, `mode`, `profile`, `effectiveSeverity`, `reason` — so a
    ///                       relaxed base-`blocking` warning shows both its base and effective severity
    ///                       and the no-hide rule is observable (FR-006, FR-011). (F045) every
    ///                       `kind:"gate"` item carries, as its last field, the per-gate
    ///                       `cacheEligibility` verdict object matched by `GateId`; every
    ///                       `kind:"finding"` item carries NONE — cache is gate-scoped (F045 FR-004,
    ///                       SC-002).
    ///   • `cacheEligibilityEvaluated` — (F045) the always-present cache-eligibility section flag:
    ///                       `false` for `cache = None`, `true` for `cache = Some _`. It survives the
    ///                       empty/clean decision and distinguishes "no cache step ran" from "an
    ///                       evaluated report with no reusable gate".
    ///
    /// `cache = None` is the NOT-EVALUATED state (today's `fsgg ship`, which resolves no freshness
    /// inputs): `cacheEligibilityEvaluated: false` and every GATE item's `cacheEligibility` is
    /// `{ kind:"notEvaluated" }` (FR-012). `cache = Some report` renders `cacheEligibilityEvaluated:
    /// true` and, per GATE item (in any of blockers/warnings/passing) matched by `GateId`, that gate's
    /// verdict — `reusable` (+ the opaque evidence reference), `mustRecompute` (+ its no-hide cause), or
    /// `notEvaluated` for a gate item absent from the report (FR-005). `Some (CacheEligibilityReport
    /// [])` is an evaluated-but-empty report — distinct from `None`.
    ///
    /// ADDITIVE / NO-HIDE (F045 FR-008, FR-011): the `verdict`, `exitCodeBasis`, every item's section,
    /// and the six-field `enforcement` detail are byte-identical to the F025-only projection of the same
    /// `ShipDecision` (modulo the new per-gate `cacheEligibility` field, the top-level
    /// `cacheEligibilityEvaluated` flag, and the bumped `schemaVersion`). A `reusable` verdict on a
    /// base-`blocking` gate leaves it in the blockers section with full enforcement detail — the cache
    /// verdict relaxes, hides, or alters NO enforcement, severity, section, or ship outcome.
    ///
    /// PURE and TOTAL (FR-008, FR-009, F045 FR-010): no file, process, clock, network, or git access;
    /// the opaque evidence reference is rendered verbatim and never dereferenced (F045 FR-011); computes
    /// no freshness key, hash, or cache decision; never throws for any well-typed inputs (including
    /// `None`, an empty report, and an EMPTY/CLEAN decision — no items; verdict `Pass`; basis `Clean` —
    /// which projects to a valid document with three present, empty sections, `verdict:"pass"` /
    /// `exitCodeBasis:"clean"`, and the cache section present, a success, never an error and never a
    /// "fail by default" fallback). DETERMINISTIC (FR-007, SC-002): identical decision inputs yield
    /// byte-for-byte identical text; the projection adds no ordering decision beyond the fixed field
    /// sequence, preserving each section's already-fixed composite order verbatim (so two decisions
    /// equal as values but assembled from differently-ordered route inputs project identically — SC-003);
    /// cache entries follow the document's existing composite item order; a duplicate `GateId` in the
    /// report resolves to the first entry by report order. The document carries NO numeric process exit
    /// code, provenance/attestation reference, raw YAML, host/absolute path, wall-clock timestamp, or
    /// environment value (FR-012, SC-007).
    val ofShipDecision: decision: ShipDecision -> cache: CacheEligibilityReport option -> string
