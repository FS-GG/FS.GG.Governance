# Phase 1 Data Model: SDD First-Class Reference Integration

**Feature**: `072-sdd-first-class-integration`. This feature introduces **no new value
types in the generic core** (SC-006). It *consumes* 071's types
(`FS.GG.Governance.Scaffold.Model`) and adds exactly one new public binding — the reference
provider value — plus repository artifacts (an emitted-skeleton file set, a lifecycle
precondition stand-in, a manifest golden, a tutorial set). Entities below describe those.

## 1. Reference Template Provider (new public surface)

The single new public module. Module `SddReferenceProvider` exposes:

```fsharp
namespace FS.GG.Governance.Sample.SddReferenceProvider

module SddReferenceProvider =
    /// Stable id the seam records but never interprets (071 FR-003).
    val providerId : Model.ProviderId          // ProviderId "fsgg.sample.sdd-reference"
    /// The resolved, selectable reference provider. Declares contract v1.0 and a PURE Emit.
    val provider   : Model.TemplateProvider
```

- `provider.ContractVersion = { Major = 1; Minor = 0 }` — the seam's supported range (071
  C2). A deliberately-incompatible clone (`Major = 2`) is used only in a failure-path test
  (FR-011); the shipped reference is compatible.
- `provider.Emit : ScaffoldRequest -> Result<ProviderEmission, ProviderError>` is **pure**:
  it returns a fixed `ProviderEmission` derived from `request.Target`'s implied app name,
  touches no filesystem/clock/env, and never throws (071 C1, research D6).

**Validation rules** (enforced by the seam, asserted by tests):
- Every emitted `RelativePath` is relative, `..`-free, and not rooted (else 071
  `Refusal.OutOfTarget`).
- No emitted path falls under `request.ReservedPaths` (else 071 `Refusal.Collision`).
- Emission is deterministic: identical request ⇒ identical `Files` list, same order.

## 2. Runtime Project Skeleton (the emitted file set — provider-owned)

A `ProviderEmission` of target-relative `EmittedFile`s describing a buildable F#/.NET
skeleton (research D2). Owned by the provider; the **seam** writes it, marking each path
`ProviderOwned` in the manifest.

| RelativePath | Required role (FR-001) | Notes |
|---|---|---|
| `<App>.sln` | package/manifest | the documented build unit |
| `src/<App>/<App>.fsproj` | source project | `net10.0`; FSharp.Core only |
| `src/<App>/Program.fs` | entry point | `[<EntryPoint>] let main _ = 0` |
| `tests/<App>.Tests/<App>.Tests.fsproj` | test project | references the source project + FSharp.Core |
| `tests/<App>.Tests/Tests.fs` | test body | trivial, buildable |
| `README.md` | provenance | lists generated paths + `dotnet build <App>.sln` |

`<App>` is the leaf name of the target directory (the only request-derived variation).
**Dependency closure = FSharp.Core only** ⇒ offline, first-attempt `dotnet build` (SC-002).

## 3. Lifecycle Governance Skeleton — precondition stand-in (test fixture, disclosed)

A small literal set of representative lifecycle paths the worked example seeds **before**
running the seam, passed as `ScaffoldRequest.ReservedPaths` (research D4). Examples:
`.fsgg/policy.fsgg`, `work/<id>/spec.md`, `readiness/<id>/`. In production this layer is
authored by sibling-owned `fsgg-sdd init`; here it is a disclosed stand-in only, used to
demonstrate layering and the reserved-path collision contract. **Not** a core type — fixture
data in the test.

## 4. Scaffold Manifest (consumed from 071) + the committed golden

The terminal `ScaffoldManifest` (071) folded by `Loop.update`, projected by
`ScaffoldManifestJson.ofManifest` to JSON. Its committed, byte-stable form lives at
`fixtures/sdd-reference/scaffold-manifest.golden.json` (research D5). For the reference run:

- `Provider = Some (providerId, { Major=1; Minor=0 })`
- `Outcome = Scaffolded`
- `Generated` = the §2 paths, ascending by `RelativePath`, each `Ownership = ProviderOwned`
- `Collisions = []`

The golden is regenerated with `BLESS_FIXTURES=1` and asserted byte-for-byte (drift guard,
FR-008/SC-005).

## 5. Tutorial Set (documentation artifacts)

Three markdown pages under `docs/tutorials/`, each anchored to the executable example
(FR-008):

| Page | Audience | Maps to | Anchored to |
|---|---|---|---|
| `adopter-onboarding.md` | adopter | FR-005 / US1 | the worked-example run + §4 golden |
| `provider-author.md` | provider author | FR-006 / US2 | the §1 provider as clone target + §2 emission |
| `sdd-governance-handoff.md` | integrator | FR-007 / US3 | ADR 0002 (local, verifiable) + the sibling **`FS.GG.SDD` repo's** `017-governance-handoff` (external cross-repo pointer) |

## 6. SDD↔Governance readiness mapping (handoff tutorial content — from ADR 0002)

The mapping the handoff tutorial documents (verbatim from ADR 0002 / sibling contract,
research D8). No code; a reproducible table.

| `governance-handoff.json` field/state | Governance outcome |
|---|---|
| `evidence.nodes[].state` ∈ `{pending, real, synthetic, failed, skipped}` | maps straight through to `Kernel.EvidenceState`; tokens identical |
| `deferred` / `accepted-deferral` (SDD) | → `skipped` (a `[-]` skip with rationale, **not** `pending`) — ADR 0002 Decision |
| `autoSynthetic` | **invalid in a produced handoff**; Governance derives it via `Evidence.effective` (taint closure) |
| `stale` | underlying declared state **+** a `staleEvidence` diagnostic (Governance-owned freshness) |
| `governedReferences[*]` | optional routing **enrichment**; Governance MAY ignore (F016 snapshot is primary) |
| `readiness.*` (`shipDisposition`, `verificationReadiness`, counts, `blockingDiagnosticIds`, `perViewState`) | **advisory declared inputs** to a Governance decision, never an enforcement verdict |
| unknown `contractVersion` **major** | version-mismatch finding, never a silent misread (pin v1.x) |

## 7. State transitions

This feature adds **no** new state machine. The scaffold transition is 071's
`init → Invoking → Probing → Writing → Done` (or a terminal refusal). The reference run
exercises the full happy path to `Done(Scaffolded)`; failure-path tests exercise
`Done(Refused (ContractMismatch …))` (FR-011) and `Done(Refused (Collision …))`; the
no-provider parity test exercises the immediate `Done(NoProvider)` with zero effects
(FR-010, SC-007).
