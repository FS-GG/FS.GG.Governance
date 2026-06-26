# Phase 0 Research: SDD First-Class Reference Integration

**Feature**: `072-sdd-first-class-integration` · Resolves every Technical-Context unknown
before design. Each decision is **Decision / Rationale / Alternatives considered**. The
governing constraints are the constitution's genericity operating rule (provider knowledge
stays out of the core) and the spec's SC-006 (generic-core public surface unchanged).

The integration point — 071's `FS.GG.Governance.Scaffold` (`Model`/`Loop`/`Interpreter`)
and `FS.GG.Governance.ScaffoldManifestJson` — is treated as **stable and consumed
unchanged**. No NEEDS CLARIFICATION remains.

---

## D0 — Boundary: this feature ships a reference + tutorials, not `fsgg-sdd init` wiring (carried)

**Decision**: Production wiring of the seam into `fsgg-sdd init` stays owned by the sibling
`FS.GG.SDD` repo (071 D0). The Governance-side deliverable is the reference provider, the
executable worked example, and the three tutorials. Tutorials state this boundary plainly
(FR-013) so no reader believes `fsgg-sdd init` already invokes this provider.

**Rationale**: The constitution forbids Governance owning SDD product identity; 071
deliberately deferred host wiring. "First-class" here = *documented, tested, reproducible*
reference, not ownership (spec Overview, Assumptions).

**Alternatives considered**: Add a real `fsgg-sdd init` provider-selection flag here —
rejected: out of repo ownership and contradicts 071 D0/FR-013.

---

## D1 — Reference-provider packaging: non-packable `samples/` library WITH its own `.fsi` + additive baseline

**Decision**: Ship the reference provider as a **library** at
`samples/FS.GG.Governance.Sample.SddReferenceProvider/` with a curated
`SddReferenceProvider.fsi` exposing a single public value `val provider : TemplateProvider`
(plus a `val providerId : ProviderId` convenience). It carries its **own** additive
`surface/FS.GG.Governance.Sample.SddReferenceProvider.surface.txt` baseline and drift test,
is `IsPackable=false`, and lives under a **new top-level `samples/`** directory — not
`src/`. This resolves spec line 183's open question (packed `.fsi` surface vs. pure example).

**Rationale**:
- **Principle II is non-negotiable** for any public F# module: a faithful clone target must
  itself model the `.fsi` + baseline discipline an author will copy. A "pure example with no
  `.fsi`" would teach the wrong shape.
- **FR-002 / SC-006** are satisfied structurally: a *separate top-level `samples/` tree* +
  `IsPackable=false` makes "example, not product" obvious, and giving the sample its **own**
  baseline means the generic-core baselines (`Scaffold`, `ScaffoldManifestJson`) are never
  edited — the surface-drift check reports **no delta** on the core (SC-006). The new
  baseline is *additive*, exactly the Tier-1 shape the spec declares.
- Keeping it non-packable avoids implying a supported published provider while still being a
  real, buildable, contract-conforming artifact third parties compile and clone.

**Alternatives considered**:
- *Pure example project, no `.fsi`/baseline* — rejected: violates Principle II and models
  bad practice for the very audience (provider authors) it serves.
- *Put it in `src/`* — rejected: blurs the product/example line FR-002 demands; `samples/`
  reads as "example" at a glance.
- *Make it packable* — rejected: would imply a first-class published provider and invite the
  "is `fsgg-sdd init` using this?" confusion FR-013 guards against.

---

## D2 — Emitted runtime skeleton: source + test + manifest + entry point, **FSharp.Core-only** closure

**Decision**: The provider's `Emit` describes a minimal F#/.NET skeleton (all paths
target-relative, content as literal strings):

| Path (target-relative) | Role (FR-001) |
|---|---|
| `<App>.sln` | package/**manifest** (the documented build unit) |
| `src/<App>/<App>.fsproj` | **source project** (`net10.0`, references FSharp.Core only) |
| `src/<App>/Program.fs` | **entry point** (`[<EntryPoint>]` returning 0) |
| `tests/<App>.Tests/<App>.Tests.fsproj` | **test project** (references the source project + FSharp.Core) |
| `tests/<App>.Tests/Tests.fs` | a buildable trivial assertion module |
| `README.md` | what was generated + the documented build command |

Every emitted project's dependency closure is **FSharp.Core only** (bundled with the SDK).
The **documented toolchain** is `dotnet build <App>.sln`.

**Rationale**: FR-001 requires source project, test project, package/manifest, and entry
point — all four present. Restricting the closure to SDK-bundled FSharp.Core makes
`dotnet build` succeed **on the first attempt, offline, every time** (SC-002), with no
NuGet restore that could flake or require network — the single biggest threat to a "100% of
runs build" criterion. Literal content (no clock/guid/env) keeps the manifest byte-stable
(SC-003, D6).

**Alternatives considered**:
- *Emit an Expecto/`Microsoft.NET.Test.Sdk` test project and run `dotnet test`* — rejected:
  external-package restore is non-deterministic offline and would make SC-002 flaky; the
  spec requires the skeleton to **build**, not that its tests run (FR-004). Running the
  emitted tests is explicitly deferred (plan Out of Scope).
- *Single project only* — rejected: FR-001 explicitly lists a test project among the
  required parts.

---

## D3 — Build verification & the missing-toolchain edge case

**Decision**: The worked-example test runs `dotnet build <App>.sln` (process at the test
edge) against the scaffolded temp target and asserts exit 0 with no hand-editing (FR-004,
SC-002). If the .NET SDK is absent/unusable, the test **skips with a named rationale**
("prerequisite: .NET SDK not found") rather than failing — an actionable prerequisite
distinguishable from a tool defect (Principle VI). The adopter tutorial states the SDK
prerequisite up front and shows the same build command.

**Rationale**: Real evidence is preferred (Principle V) and the repo standard is a present
net10.0 SDK, so the build runs for real in normal CI/dev. The skip path honors the spec's
"missing toolchain" edge case: a prerequisite gap must read as a prerequisite gap, never as
a scaffolder bug.

**Alternatives considered**:
- *Synthetic "build" (parse/typecheck in-proc)* — rejected: weaker evidence; `dotnet build`
  is the operator's real toolchain and the honest audience (Principle I).
- *Hard-fail when SDK missing* — rejected: misreports an environment prerequisite as a tool
  defect, violating Principle VI's "distinguish defect from missing input."

---

## D4 — Lifecycle-layer precondition: a disclosed minimal stand-in, sibling-owned in production

**Decision**: The worked example seeds a tiny, **disclosed** lifecycle-layer stand-in (a few
representative `.fsgg/`/`work/`/`readiness/` paths) on disk and passes them as the seam's
`ReservedPaths`, to demonstrate (a) layering the runtime skeleton *on top of* an existing
lifecycle layer and (b) the seam treating a reserved/lifecycle path as a hard collision. The
stand-in carries a `// SYNTHETIC:`/precondition comment at its use site and the tutorials
state that in production this layer is authored by sibling-owned `fsgg-sdd init` — never by
this seam or a provider.

**Rationale**: `fsgg-sdd init` is not owned here, so the test must not depend on it (spec
Assumptions); a small literal stand-in lets the example demonstrate the *precondition* and
the reserved-path contract (071 C5/D3) without importing the sibling. Disclosure keeps it
honest (Principle V). The seam itself runs for real against a real temp dir.

**Alternatives considered**:
- *Shell out to `fsgg-sdd init`* — rejected: cross-repo dependency the constitution's
  operating rule forbids ("rendering MUST NEVER require governance to build/test").
- *Skip the lifecycle layer entirely* — rejected: the spec's headline story is "empty
  governed directory → buildable governed product"; omitting the layer would not demonstrate
  layering or the reserved-path edge case.

---

## D5 — Doc/example drift guard: a committed manifest golden the test asserts and the tutorials show

**Decision**: Commit a deterministic `fixtures/sdd-reference/scaffold-manifest.golden.json`
(the `ScaffoldManifestJson.ofManifest` output of the reference run), regenerated by a
`BLESS_FIXTURES=1` env switch (mirroring `fixtures/enforcement/`). The worked-example test
asserts the live projection equals the golden **byte-for-byte**; the adopter and
provider-author tutorials embed/link that exact golden and present each documented step as a
command/assertion the e2e test also runs. Any provider/seam change that alters output fails
the golden assertion before a tutorial can rot (FR-008, SC-005).

**Rationale**: "100% of steps covered by the automated check" (SC-005) needs a single source
of truth shared by doc and test. A committed golden is the repo's established drift idiom
(`fixtures/enforcement/truth-table.md` + BLESS). Because the manifest is target-relative and
clock/env-free (071 D6), the golden is stable across machines.

**Alternatives considered**:
- *Prose-only tutorials* — rejected: nothing fails the build when they drift (defeats
  FR-008/SC-005).
- *Extract code blocks from the markdown and execute them* — rejected: heavier harness than
  needed; a shared golden + mirrored steps achieves the same guarantee far more simply.

---

## D6 — Determinism: a pure provider over the 071 deterministic manifest

**Decision**: The reference provider's `Emit` is a **pure function of the request** —
literal file contents, no clock, guid, environment, or absolute path, and a stable file
order. Combined with 071's clock/env-free, target-relative manifest projection, re-running
over a fresh empty target yields a **byte-identical** manifest and an identical buildable
tree (SC-003).

**Rationale**: SC-003 demands byte-identical re-runs; the seam already guarantees manifest
determinism (071 D6/SC-004), so the only remaining variable is the provider's own output —
made deterministic by construction. Determinism also makes the build reproducible (D2).

**Alternatives considered**:
- *Templated content with a generated timestamp/project-guid* — rejected: defeats SC-003 and
  the golden in D5; F# SDK projects need no GUIDs.

---

## D7 — Test placement & MVU reuse (no new product workflow)

**Decision**: Reuse 071's `Loop` (pure) + `Interpreter` (edge) **verbatim**; add **no** new
product MVU. The worked example is a `tests/` harness that selects the resolved reference
provider, calls `Interpreter.run (realPorts target) request`, then performs test-edge I/O
(`dotnet build`, fixture seeding, golden compare). Tests live under `tests/` per repo
convention; the reference provider lives under `samples/`.

**Rationale**: Principle IV applies to *product* stateful/I/O workflows; the scaffold
workflow is already an MVU boundary (071). Adding a second MVU for a test harness would be
ceremony the constitution's Principle III discourages. The provider's `Emit` is pure I/O-free
data, so it needs no boundary of its own.

**Alternatives considered**:
- *Wrap the worked example in a new MVU `Program`* — rejected: no new stateful product
  surface ships here; it would duplicate 071's loop for a test.
- *Co-locate tests under `samples/`* — viable, but `tests/` matches the repo's uniform layout
  and the surface-drift `findRepoRoot` walk works from anywhere; chose convention.

---

## D8 — Handoff tutorial: explanatory cross-reference to ADR 0002, no consumer code

**Decision**: The SDD↔Governance handoff tutorial is **documentation only**. It maps each
`governance-handoff.json` readiness field/state to its Governance routing/evidence/
enforcement outcome by quoting ADR 0002 and the sibling `017-governance-handoff` contract
verbatim — including the `deferred → skipped` row and the `evidence.nodes[].state` token set
`{pending, real, synthetic, failed, skipped}` (no `autoSynthetic` in a produced handoff). It
ships **no** parser/consumer; that is ADR 0002's explicitly queued, separate Governance
feature.

**Rationale**: FR-007/SC-008 require a *correct, ADR-0002-consistent mapping a reader can
reproduce*, not a running consumer. ADR 0002 already records the accepted mapping and the
version-pin posture; the tutorial's job is to make it legible against the worked subject. A
doc-level mapping anchored to the accepted ADR keeps Governance from prematurely owning a
consumer the ADR scopes as future work.

**Alternatives considered**:
- *Build the `governance-handoff.json` reader here* — rejected: out of scope (ADR 0002
  queued work; plan Out of Scope) and would balloon a docs/reference feature into a kernel
  consumer feature.
- *Paraphrase the mapping* — rejected: SC-008 wants 100% agreement with ADR 0002; quoting the
  accepted tokens verbatim is the safe path against drift.

---

## Consolidated decisions

| # | Decision | Drives |
|---|----------|--------|
| D0 | Reference + tutorials only; `fsgg-sdd init` wiring stays sibling-owned | FR-013 |
| D1 | Non-packable `samples/` library, own `.fsi` + **additive** baseline, core baselines untouched | FR-002, SC-006, II |
| D2 | Emit source+test+manifest+entry point; **FSharp.Core-only** closure | FR-001, FR-004, SC-002 |
| D3 | `dotnet build` real-evidence check; SDK-missing ⇒ named skip, not failure | FR-004, SC-002, VI |
| D4 | Disclosed minimal lifecycle-layer stand-in as `ReservedPaths`; sibling-owned in prod | FR-003, US1, V |
| D5 | Committed BLESS-regenerated manifest golden, asserted by test + shown in tutorials | FR-008, SC-005 |
| D6 | Pure, clock/env-free provider `Emit` ⇒ byte-identical re-runs | SC-003 |
| D7 | Reuse 071 MVU; no new product workflow; tests under `tests/` | IV, III |
| D8 | Handoff tutorial = ADR-0002 cross-reference, no consumer code | FR-007, SC-008 |
