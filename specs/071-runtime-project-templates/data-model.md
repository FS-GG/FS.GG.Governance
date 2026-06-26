# Phase 1 Data Model: Runtime Project Templates

**Feature**: `071-runtime-project-templates` · Derives the spec's Key Entities into
the typed values of `FS.GG.Governance.Scaffold.Model`. All types are immutable
records / closed DUs; every match in the implementation is exhaustive and
wildcard-free so a new case is a compile error, never a silently mistyped field.

## 1. Provider identity & contract version

```fsharp
/// Stable, provider-supplied identifier. The tool never interprets its content
/// (FR-003) — it only records and reports it (FR-005, FR-012).
type ProviderId = ProviderId of string

/// The provider-declared contract version it was authored against. A simple
/// (major, minor) pair; the tool supports a fixed inclusive range (D1/D4).
type ProviderContractVersion = { Major: int; Minor: int }
```

- **Compatibility rule** (pure, FR-009): a provider is compatible iff
  `Major = SupportedMajor` and `Minor <= SupportedMinor`. Incompatible ⇒ refuse
  *before* invocation (`ContractMismatch`), no writes.

## 2. The request the tool hands a provider

```fsharp
/// The bounded scaffold request. `Target` is the operator-chosen project root the
/// provider may populate; the provider returns paths RELATIVE to it and never sees
/// or writes anything outside it. `ReservedPaths` are lifecycle-skeleton paths the
/// host already owns (target-relative) so a provider can avoid them; the tool also
/// treats any of them as a hard collision (D3).
type ScaffoldRequest =
    { Target: string
      ReservedPaths: string list }
```

## 3. What a provider emits (declarative — it never writes)

```fsharp
/// One file the provider wants laid down, addressed RELATIVE to the target. The
/// provider supplies content as data; the TOOL writes it (D1). Provider-owned.
type EmittedFile =
    { RelativePath: string
      Contents: string }

/// The provider's complete description of the runtime skeleton. Pure data.
type ProviderEmission =
    { Files: EmittedFile list }

/// Why a provider's own `Emit` failed (its internal error), surfaced verbatim by
/// the tool. Distinct from the tool's safety refusals in §5.
type ProviderError =
    | Unresolvable of detail: string      // the provider could not be produced/run (FR-009)
    | EmitFailed of detail: string        // the provider errored mid-description (FR-008)
```

## 4. The in-process provider port

```fsharp
/// A resolved, selectable provider (D1). `Emit` is PURE-SHAPED from the tool's
/// view: given a request, it returns a description or an error — it performs NO
/// filesystem writes. Third-party providers implement this in a .NET assembly;
/// discovery/loading is a deferred host concern (the core gets a resolved value).
type TemplateProvider =
    { Id: ProviderId
      ContractVersion: ProviderContractVersion
      Emit: ScaffoldRequest -> Result<ProviderEmission, ProviderError> }
```

## 5. Safety refusals (tool-owned, pre-write)

```fsharp
/// Why the TOOL refused to scaffold — decided in pure `update` BEFORE any write
/// (D4). Each is explicit and actionable (Principle VI, SC-005).
type Refusal =
    | ContractMismatch of declared: ProviderContractVersion   // FR-009
    | ProviderUnavailable of detail: string                   // wraps ProviderError.Unresolvable (FR-009)
    | OutOfTarget of paths: string list                       // emitted path escapes the target (FR-009, D5)
    | Collision of paths: string list                         // path already exists / reserved (FR-007, D3)
    | ProviderErrored of detail: string                       // wraps ProviderError.EmitFailed (FR-008)
```

- **All-or-nothing**: any non-empty `OutOfTarget` or `Collision` set refuses the
  **whole** batch — zero files are written (SC-005). The host-owned lifecycle
  skeleton is never touched by the seam, so a refusal always leaves it valid (FR-008).

## 6. Outcome & manifest (the provenance record)

```fsharp
/// The closed outcome of one seam run.
type ScaffoldOutcome =
    | NoProvider                          // FR-002: nothing selected; seam is a no-op, no manifest write
    | Scaffolded                          // provider emission written in full
    | Refused of Refusal                  // §5 — explicit, recoverable

/// One generated path, marked provider-owned so later steps never mistake it for a
/// lifecycle-authored source (FR-005, FR-006). Target-RELATIVE for determinism (D6).
type GeneratedPath =
    { RelativePath: string
      Ownership: PathOwnership }          // always `ProviderOwned` here; the type leaves room for future kinds
and PathOwnership = ProviderOwned

/// The deterministic record of one scaffold run — the provenance other steps and
/// automation consume (FR-005, FR-010, FR-012). Carries NO absolute target path,
/// clock, or environment value (D6, SC-004).
type ScaffoldManifest =
    { Provider: (ProviderId * ProviderContractVersion) option   // None only for NoProvider
      Outcome: ScaffoldOutcome
      Generated: GeneratedPath list        // written paths, ascending by RelativePath; [] unless Scaffolded
      Collisions: string list }            // pre-existing/reserved paths that caused a refusal, ascending; [] otherwise
```

## 7. State transitions (pure `update`, MVU — see contracts/provider-contract.md)

```text
init(request, providerOpt)
  │
  ├─ None ───────────────────────────────► Done(NoProvider, manifest = no-provider)        [FR-002]
  │
  └─ Some p
        │  version check (§1)
        ├─ incompatible ─────────────────► Done(Refused (ContractMismatch …))              [FR-009]
        │
        │  Effect: InvokeProvider(p, request)
        ├─ Error (Unresolvable d) ────────► Done(Refused (ProviderUnavailable d))           [FR-009]
        ├─ Error (EmitFailed d) ──────────► Done(Refused (ProviderErrored d))               [FR-008]
        └─ Ok emission
              │  path-boundary check (§5, D5) over every RelativePath
              ├─ any out-of-target ───────► Done(Refused (OutOfTarget …))                   [FR-009]
              │
              │  Effect: ProbeCollisions(resolved paths ∪ reserved)
              ├─ any exists ──────────────► Done(Refused (Collision …))   (NO writes)       [FR-007]
              └─ none exist
                    │  Effect: WriteAll(files)        (atomic, all-or-nothing)
                    ├─ Error ─────────────► Done(Refused (ProviderErrored …))  (no partial) [FR-008, SC-005]
                    └─ Ok ────────────────► Done(Scaffolded, manifest lists every path)     [FR-005, SC-001]
```

Every terminal state folds a `ScaffoldManifest`; only `Scaffolded` and the refusal
states (when a provider was selected) carry a `Some Provider`. `NoProvider` writes
**no** manifest at all (the host's init output stays byte-identical — FR-002).

## 8. Validation rules summary

| Rule | Where | Requirement |
|------|-------|-------------|
| Contract compatibility | pure `update`, pre-invoke | FR-009 |
| Path-boundary (relative, no escape, not rooted) | pure `update`, post-emit | FR-009, D5 |
| Collision / reserved-path refusal (all-or-nothing) | pure decision + edge probe | FR-007, D3 |
| Provider failure ⇒ recoverable refusal, no writes | pure `update` | FR-008 |
| No-provider ⇒ no effects, no manifest | `init` | FR-002 |
| Generated paths marked provider-owned | manifest fold | FR-005, FR-006 |
| Manifest deterministic, no abs-path/clock/env | projection (D6) | SC-004, SC-006 |
