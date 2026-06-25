# Phase 1 Data Model: Package / Docs / Skills / Design Deterministic Checks (F24)

**Branch**: `059-package-docs-skills-design-checks` | **Spec**: [spec.md](./spec.md) |
**Research**: [research.md](./research.md)

All new vocabulary lives in the shared **`FS.GG.Governance.SurfaceChecks`** core and the four domain
libraries (`PackageChecks` / `DocsChecks` / `SkillChecks` / `DesignChecks`) — never in the kernel and never
in `Config` (FR-007, FR-013). Every collection is emitted in deterministic id/locus-sorted order (FR-010).
Signatures below are the intended `.fsi` shapes; F# `.fsi` is the sole visibility declaration
(Constitution II). Types reused verbatim from upstream are referenced, not redefined.

---

## 0. Reused upstream types (not redefined here)

From `FS.GG.Governance.Config.Model` (F014/F23): `TypedFacts`, `CapabilityFacts`, `Surface`, `SurfaceId`,
`SurfaceClass`, `EvidenceTag`, `GovernedPath`, `DomainId`, `normalizePath`, `surfaceClassToken`.
From `FS.GG.Governance.ProductSurfaces.Model` (F23): `ProductSurfaceReport`, `ProductClassification`.
From `FS.GG.Governance.Enforcement` (F023): `Severity` (`Advisory | Blocking`), `Maturity`,
`EnforcementInput`, `EnforcementDecision`, `deriveEffectiveSeverity`.
From `FS.GG.Governance.GateExecution.Model` (F051/F052): `ExecutionPort`, `CommandRecord` (transcript runner).

---

## 1. Shared core — `FS.GG.Governance.SurfaceChecks.Model`

The cross-domain finding vocabulary every pack produces. One closed `CheckDomain`, one `SurfaceFinding`, one
`SurfaceCheckRequest`.

```fsharp
namespace FS.GG.Governance.SurfaceChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// Which domain pack a request routes to. Closed; one case per F24 domain (FR-008).
    type CheckDomain =
        | PackageDomain
        | DocsDomain
        | SkillDomain
        | DesignDomain

    /// The precise locus a finding points at (FR-004/FR-006: name the exact thing).
    /// File is repo-relative, forward-slash normalized (normalizePath). Detail is the
    /// stable, domain-specific locus token (member name, transcript id, link target, entry id).
    type FindingLocation =
        { File: GovernedPath
          Detail: string }

    /// One deterministic-or-advisory finding from a surface check.
    /// BaseSeverity = Blocking for deterministic checks, Advisory for judgement-heavy (FR-011).
    /// EvidenceTag binds produced evidence back to the F23-declared tag (FR-009); None when the
    /// surface declared no tag (still a valid finding).
    type SurfaceFinding =
        { Domain: CheckDomain
          Surface: SurfaceId
          Code: string                 // stable per-rule code, e.g. "package.baseline-drift"
          Location: FindingLocation
          BaseSeverity: Severity
          Maturity: Maturity
          EvidenceTag: EvidenceTag option
          IsInputState: bool           // true ⇒ missing/malformed input, not a rule violation (FR-012)
          Message: string }            // deterministic; no abs-path/clock/username

    /// One unit of work derived from a single F23 ProductClassification (D4).
    /// The dispatcher builds one request per applicable routed surface and feeds it to the
    /// matching pack's evaluate. EvidenceTag is looked up from the surface declaration.
    type SurfaceCheckRequest =
        { Domain: CheckDomain
          Surface: SurfaceId
          Class: SurfaceClass
          Path: GovernedPath
          EvidenceTag: EvidenceTag option }

    /// Stable render helpers (token tables, no clock/locale).
    val checkDomainToken: domain: CheckDomain -> string
    val severityToken: severity: Severity -> string

    /// Build the rollup input for a finding under a run mode + profile (reuses F023 verbatim).
    val enforcementInputOf:
        finding: SurfaceFinding -> mode: RunMode -> profile: Profile -> EnforcementInput
```

### 1.1 `SurfaceChecks.Dispatch.Composition`

The pure dispatcher, in its own project **`FS.GG.Governance.SurfaceChecks.Dispatch`** (see the Dependency note
below). Given the F23 report and the per-domain sensed facts (a bundle the host fills via the sensors), it
builds one request per applicable classification, runs the matching pack, and aggregates.
**Order-independent and deterministic** (FR-008, SC-008): the result is sorted by `(Surface id, Domain
ordinal, Location)`, so the same change yields the same findings regardless of classification order.

```fsharp
namespace FS.GG.Governance.SurfaceChecks.Dispatch

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.SurfaceChecks

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Composition =

    /// The host fills only the domains whose surfaces were declared/routed (FR-015). A None
    /// field means "no facts sensed for this domain" ⇒ that domain contributes nothing.
    type DomainFactBundle =
        { Package: Map<SurfaceId, PackageChecks.Model.PackageFacts>
          Docs: Map<SurfaceId, DocsChecks.Model.DocsFacts>
          Skill: Map<SurfaceId, SkillChecks.Model.SkillFacts>
          Design: Map<SurfaceId, DesignChecks.Model.DesignFacts> }

    /// Map an F23 SurfaceClass to the F24 domain pack, when one exists.
    /// Non-product / boundary classes (Routine/GovernedRoot/…) ⇒ None (no pack, no finding).
    val domainOf: cls: SurfaceClass -> Model.CheckDomain option

    /// Derive the per-surface requests from the F23 report (one per applicable classification).
    val requestsOf:
        facts: TypedFacts -> report: ProductSurfaceReport -> Model.SurfaceCheckRequest list

    /// PURE and TOTAL: run every applicable pack, aggregate, sort. No I/O, no clock.
    /// Empty report or empty bundle ⇒ empty list (valid success, not an error).
    val run:
        facts: TypedFacts ->
        report: ProductSurfaceReport ->
        bundle: DomainFactBundle ->
            Model.SurfaceFinding list
```

> **Dependency note (resolved).** `DomainFactBundle` references the four domain `Model` types, and each domain
> library references `SurfaceChecks` for the `SurfaceFinding`/`SurfaceCheckRequest` shape. Putting `Model` and
> `Composition` in **one** `SurfaceChecks` project would therefore be a project-level reference cycle
> (`SurfaceChecks → domains → SurfaceChecks`) — F#/.NET references are project-level, so module-only
> granularity does not break it. **Resolution**: the shared core `FS.GG.Governance.SurfaceChecks` carries
> `Model` only (refs `Config` + `Enforcement`); `Composition` lives in `FS.GG.Governance.SurfaceChecks.Dispatch`
> (refs `SurfaceChecks` + `Config` + `ProductSurfaces` + the four domain libraries). The four domain libraries
> reference **only** `SurfaceChecks` (`Model`) — never `Dispatch`, never each other — so there is no cycle and
> no pack depends on another (FR-008). This is the structure plan.md and tasks.md adopt (6 `src` libraries).

---

## 2. Package domain — `FS.GG.Governance.PackageChecks` (P1)

### 2.1 `PackageChecks.Model` — facts (sensed input)

```fsharp
namespace FS.GG.Governance.PackageChecks

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A normalized public-surface token set for one module's .fsi (D5: token diff, not text diff).
    type SurfaceTokens = SurfaceTokens of string list   // sorted, normalized public declarations

    /// Result of comparing a regenerated .fsi surface against the committed baseline.
    type FsiBaselineFact =
        | BaselineMatches                                  // no drift
        | BaselineDrift of added: string list * removed: string list   // named members (sorted)
        | BaselineAbsent of generated: SurfaceTokens       // first-run: baseline produced, commit it (FR-002)
        | BaselineUnreadable of source: string             // input diagnostic (FR-012)

    /// Result of running one published FSI transcript (a public example / package contract).
    type TranscriptOutcome =
        | TranscriptPasses                                 // compiles AND evaluates to stated result
        | TranscriptCompileFailed of detail: string
        | TranscriptResultChanged of expected: string * actual: string
        | TranscriptUnlocatable of source: string          // input diagnostic (FR-012)

    type TranscriptFact =
        { ExampleId: string                                // stable id/ordinal of the example
          Source: GovernedPath
          Outcome: TranscriptOutcome }

    /// Everything the package sensor produced for one surface.
    type PackageFacts =
        { BaselineSource: GovernedPath
          Baseline: FsiBaselineFact
          Transcripts: TranscriptFact list }               // empty list ⇒ no transcripts declared (not an error)
```

### 2.2 `PackageChecks` — pure evaluate

```fsharp
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PackageChecks =

    /// PURE and TOTAL. Baseline drift + transcript findings, sorted by (member/example) locus.
    /// BaselineMatches / TranscriptPasses ⇒ no finding. BaselineDrift / compile-fail / result-change
    /// ⇒ Blocking deterministic findings naming what changed (FR-001, FR-003, SC-001).
    /// BaselineAbsent ⇒ an IsInputState finding ("baseline generated, commit it"), never a silent pass.
    /// Unreadable/Unlocatable ⇒ IsInputState findings naming the source (FR-012).
    val evaluate:
        request: SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.PackageFacts ->
            SurfaceChecks.Model.SurfaceFinding list
```

### 2.3 `PackageChecks.Interpreter` — host sensor (edge)

```fsharp
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// Injected I/O port (Constitution IV). The ONLY filesystem/process seam for this domain (FR-007).
    type PackagePort =
        { RegenerateSurface: GovernedPath -> Result<Model.SurfaceTokens, string>   // dotnet/FSI surface gen
          ReadBaseline: GovernedPath -> Result<Model.SurfaceTokens option, string> // None ⇒ absent (first-run)
          WriteBaseline: GovernedPath -> Model.SurfaceTokens -> Result<unit, string>
          RunTranscript: GovernedPath -> Result<Model.TranscriptOutcome, string> } // shells FSI via ExecutionPort

    /// Build the real port for a repo working dir, reusing the F051/F052 ExecutionPort for FSI runs.
    val realPort:
        repo: string -> exec: FS.GG.Governance.GateExecution.Model.ExecutionPort -> PackagePort

    /// TOTAL and SAFE: catches every exception, maps to *Unreadable/*Unlocatable input facts (FR-012).
    /// On absent baseline, regenerates + writes it and yields BaselineAbsent (FR-002).
    val sensePackage:
        port: PackagePort -> request: SurfaceChecks.Model.SurfaceCheckRequest -> Model.PackageFacts
```

---

## 3. Docs domain — `FS.GG.Governance.DocsChecks` (P2)

```fsharp
namespace FS.GG.Governance.DocsChecks
open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// A link target's currency (internal path, anchor, or external-style reference).
    type LinkOutcome =
        | LinkResolves
        | LinkDangling of target: string                   // unresolved (FR-004)

    type LinkFact =
        { Source: GovernedPath
          LinkText: string
          Target: string
          Outcome: LinkOutcome }

    /// A referenced symbol/anchor's currency.
    type ReferenceOutcome =
        | ReferenceResolves
        | ReferenceStale of symbol: string                 // removed/renamed symbol or anchor (FR-004)

    type ReferenceFact =
        { Source: GovernedPath
          Reference: string
          Outcome: ReferenceOutcome }

    type DocsFacts =
        { Sources: GovernedPath list                       // declared docs sources sensed
          Links: LinkFact list
          References: ReferenceFact list
          Unreadable: string list }                        // sources that could not be read (FR-012)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DocsChecks =
    /// PURE and TOTAL. Dangling link ⇒ Blocking "docs.link-currency" finding naming file+link+target;
    /// stale reference ⇒ Blocking "docs.reference-currency" finding; resolves ⇒ no finding (FR-004, SC-002).
    /// Unreadable source ⇒ IsInputState finding (FR-012). Sorted by (Source, locus).
    val evaluate:
        request: SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.DocsFacts ->
            SurfaceChecks.Model.SurfaceFinding list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =
    /// Injected port: read a docs source, resolve a path/anchor target, resolve a symbol. The only seam.
    type DocsPort =
        { ReadSource: GovernedPath -> Result<string, string>
          ResolveTarget: target: string -> bool             // internal path / anchor existence
          ResolveSymbol: symbol: string -> bool }            // symbol/anchor existence
    val realPort: repo: string -> DocsPort
    /// TOTAL/SAFE: scans declared docs sources, extracts links+references, resolves each. Catches all.
    val senseDocs:
        port: DocsPort -> request: SurfaceChecks.Model.SurfaceCheckRequest -> Model.DocsFacts
```

---

## 4. Skill domain — `FS.GG.Governance.SkillChecks` (P2)

```fsharp
namespace FS.GG.Governance.SkillChecks
open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// One claimed path of a skill's path contract.
    type PathContractOutcome =
        | PathHolds
        | PathUnresolved of claimed: string                // claimed but does not resolve (FR-005)
        | PathEscapesBounds of claimed: string             // resolves but outside declared bounds (FR-005)

    type PathContractFact = { Claimed: string; Outcome: PathContractOutcome }

    /// Task-skill-list internal consistency.
    type TaskListOutcome =
        | TaskListConsistent
        | TaskListInconsistent of detail: string

    /// Optional declared mirror state. NoMirrorDeclared is NOT an error (FR-005, SC-003).
    type MirrorOutcome =
        | NoMirrorDeclared
        | MirrorInSync
        | MirrorMissing of mirror: string
        | MirrorDrifted of mirror: string * detail: string

    type SkillFacts =
        { SkillId: string
          PathContract: PathContractFact list
          TaskList: TaskListOutcome
          Mirror: MirrorOutcome
          Unreadable: string list }                         // manifest/mirror could not be read (FR-012)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SkillChecks =
    /// PURE and TOTAL. Path unresolved/escapes ⇒ Blocking "skill.path-contract" naming skill+path;
    /// inconsistent task list ⇒ Blocking "skill.task-list"; missing/drifted mirror ⇒ Blocking
    /// "skill.mirror"; NoMirrorDeclared ⇒ no finding (FR-005, SC-003). Sorted by (skill, locus).
    val evaluate:
        request: SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.SkillFacts ->
            SurfaceChecks.Model.SurfaceFinding list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =
    type SkillPort =
        { ReadManifest: GovernedPath -> Result<string, string>
          ResolvePath: claimed: string -> Result<bool, string>   // resolves? within bounds checked in sensor
          ReadMirror: mirror: string -> Result<string option, string> }  // None ⇒ declared-absent
    val realPort: repo: string -> SkillPort
    val senseSkill:
        port: SkillPort -> request: SurfaceChecks.Model.SurfaceCheckRequest -> Model.SkillFacts
```

---

## 5. Design domain — `FS.GG.Governance.DesignChecks` (P3, render-fenced)

The pure pack and `Model` carry **no** rendering/UI/registry dependency (FR-007, SC-004): they consume plain
facts the sensor produced by reading catalog files via `System.IO`/`System.Text.Json` only. There is no
Skia/rendering reference anywhere in this library (verified by surface inspection in the design test).

```fsharp
namespace FS.GG.Governance.DesignChecks
open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ResolveOutcome =
        | Resolves
        | Absent of entry: string                          // not in catalog (FR-006)

    type TokenFact   = { Token: string;   Outcome: ResolveOutcome }
    type CaptureFact = { Capture: string; Outcome: ResolveOutcome }
    type ControlFact = { Control: string; Outcome: ResolveOutcome }

    /// Contrast pair measured against its declared threshold (deterministic numeric compare).
    type ContrastFact =
        { Pair: string
          Ratio: decimal
          Threshold: decimal
          Meets: bool }

    type DesignFacts =
        { Tokens: TokenFact list
          Captures: CaptureFact list
          Controls: ControlFact list
          Contrasts: ContrastFact list
          CatalogUnavailable: string list }                 // absent/unreadable catalog (FR-012)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesignChecks =
    /// PURE and TOTAL, NO I/O. Absent token/capture/control or sub-threshold contrast ⇒ Blocking
    /// "design.<kind>" finding naming the entry (FR-006, SC-004). All-resolve ⇒ no finding.
    /// CatalogUnavailable ⇒ IsInputState finding (FR-012). Sorted by (kind, entry id).
    val evaluate:
        request: SurfaceChecks.Model.SurfaceCheckRequest ->
        facts: Model.DesignFacts ->
            SurfaceChecks.Model.SurfaceFinding list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =
    /// The ONLY place a design catalog is read. System.IO/System.Text.Json only — NO rendering (FR-007).
    type DesignPort =
        { ReadTokenCatalog:    unit -> Result<Set<string>, string>
          ReadCaptureCatalog:  unit -> Result<Set<string>, string>
          ReadControlCatalog:  unit -> Result<Set<string>, string>
          ReadContrastCatalog: unit -> Result<Map<string, decimal * decimal>, string> }  // pair -> (ratio, threshold)
    val realPort: repo: string -> catalogLayout: (string * string * string * string) -> DesignPort
    val senseDesign:
        port: DesignPort -> request: SurfaceChecks.Model.SurfaceCheckRequest -> Model.DesignFacts
```

---

## 6. Host extension — `VerifyCommand` / `VerifyJson` (additive, D8)

No new public type in `VerifyCommand.Loop` beyond threading a `SurfaceFinding list` into `Model` and the
rendered summary. `VerifyJson` gains **one overload** that emits the additive section; the existing
`ofVerifyResult` stays byte-identical.

```fsharp
// FS.GG.Governance.VerifyJson
/// Existing entry point — UNCHANGED, byte-identical output.
val ofVerifyResult: (* existing parameters *) -> string

/// Additive: emits a `surfaceChecks` array ONLY when findings is non-empty; empty ⇒ identical to
/// ofVerifyResult on the same inputs (every existing golden untouched). schemaVersion unchanged (D8).
val ofVerifyResultWithSurfaceChecks:
    (* existing parameters *) -> findings: FS.GG.Governance.SurfaceChecks.Model.SurfaceFinding list -> string
```

---

## 7. Validation rules (cross-cutting)

| Rule | Source | Where enforced |
|---|---|---|
| Identical sensed facts ⇒ byte-identical findings | FR-010, SC-005 | every `evaluate`: stable sort + `normalizePath`; per-domain determinism test |
| `.fsi` unchanged ⇒ zero drift (no false positive) | SC-001 | token diff (D5), not text diff |
| Deterministic finding `BaseSeverity = Blocking`; judgement-heavy `= Advisory` | FR-011 | pack sets severity; `deriveEffectiveSeverity` never escalates Advisory (SC-006) |
| Produced evidence tied to declared `EvidenceTag` | FR-009, SC-007 | `SurfaceFinding.EvidenceTag` from `SurfaceCheckRequest`; `EvidenceCapture` at host edge |
| Missing/malformed input ≠ tool defect, names source, no fabricated pass | FR-012 | `IsInputState` findings + `Result`-per-source sensor (D7) |
| Packs compose, no pack depends on another | FR-008, SC-008 | one library per domain; `Composition.run` order-independent + sorted |
| No new `SurfaceClass`/schema/field/diagnostic/truth-table | FR-013, FR-014 | reuse only; `domainOf` maps existing classes |
| Uncovered domain stays F23 known non-error state | FR-015 | `domainOf` ⇒ `None` for non-product classes; absent surface ⇒ no request |
| Pure packs + core carry no render/registry/fs dependency | FR-007, SC-004 | only `Interpreter` modules reference `System.IO`; design surface inspection test |
| Standalone (no monorepo) | FR-016 | sensors read only under the `.fsgg` loader root |
