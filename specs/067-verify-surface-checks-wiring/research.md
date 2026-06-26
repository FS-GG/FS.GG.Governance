# Phase 0 Research: `fsgg verify` Surface-Checks Host Wiring

All decisions are grounded in the existing code. No `NEEDS CLARIFICATION` markers remain.

## D1 — Where the sense+run edge attaches in the verify MVU loop

**Decision**: Add a new `Effect.SenseSurfaces` emitted by `update` once the verify scope is sensed (alongside
the existing `SenseProvenance` / `SenseScope` fan-out), and a `Msg.SurfacesSensed of SurfaceFinding list`
folded back in `update`. The interpreter (`Interpreter.fs`) handles `SenseSurfaces` by classifying, sensing the
four domains, and running `Composition.run`.

**Rationale**: The verify host already models every I/O step as an `Effect`/`Msg` pair
(`SenseScope`→`Sensed`, `SenseFreshness`→`FreshnessSensed`, `SenseProvenance`→`ProvenanceSensed`,
`SenseReleasePreview`→`ReleasePreviewSensed`). Surface sensing + `Composition.run` are I/O + (pure) aggregation
that must run at the edge per Constitution IV. Mirroring the established pair keeps the boundary observable and
the `update` pure. The verify projection is assembled only after all senses have arrived (the existing join
point that already gates `verify.json` on the release preview), so `SurfaceFindings` is guaranteed populated
before projection.

**Alternatives considered**:
- *Classify+run inside the existing `Sensed` handler*: rejected — it would bury an additional filesystem sense
  (the four domain `realPort`s read files) inside another effect's result handling, obscuring the boundary and
  complicating safe-failure attribution (D6).
- *Run `Composition.run` in `update`*: rejected — `run` is pure, but it needs the sensed `DomainFactBundle`,
  which is I/O; sensing in `update` violates Constitution IV. (`run` itself stays where it is: called from the
  interpreter after sensing, then only its `SurfaceFinding list` crosses back into `update`.)

## D2 — Source of the path set to classify

**Decision**: Classify the **declared product surfaces** from the loaded capability config
(`TypedFacts.Capabilities.Surfaces`) intersected with the verify scope, exactly as `ProductSurfaces.classify`
is fed on the route side. The interpreter already has the repo snapshot (`RepoSnapshot` from `SenseScope`) and
loads the capability config; `classify` produces the `ProductSurfaceReport`.

**Rationale**: `Composition.requestsOf`/`run` take a `ProductSurfaceReport` + `TypedFacts` and look up declared
evidence tags from `facts.Capabilities.Surfaces`. A surface absent from the declaration ⇒ no request ⇒ no
finding (FR-015 in the dispatch core). Reusing the same classification feed route uses guarantees identical
classification behavior (FR-001) and the no-surface byte-identity property (FR-004): no declared surfaces ⇒
empty report ⇒ `run` returns `[]`.

**Alternatives considered**:
- *Classify every changed path regardless of declaration*: rejected — diverges from route's behavior and the
  dispatch core's declared-surface contract; would manufacture requests for undeclared paths.

## D3 — Folding findings into the rollup without re-opening the truth table

**Decision**: For each `SurfaceFinding`, build an `EnforcementInput` via
`SurfaceChecks.Model.enforcementInputOf finding RunMode.Verify profile`, and feed those inputs into the
**existing** `Ship.rollup` path at `RunMode.Verify` (the same rollup the verify host already runs), so the
verdict/exit code are computed by the existing `deriveEffectiveSeverity`. The truth table is not touched.

**Rationale**: `SurfaceChecks/Model.fsi` documents exactly this: "`enforcementInputOf` builds the F023 input
from a finding; the verdict is computed by the existing `deriveEffectiveSeverity` (reuse only)." A blocking
finding yields a blocking input ⇒ fails at `RunMode.Verify`; an advisory finding yields an advisory input ⇒
never escalates (FR-007, SC-003). This is pure reuse — no new rule, no new severity, no truth-table edit
(FR-008).

**Alternatives considered**:
- *Map findings directly to `GateOutcome`s*: rejected — findings are surface checks, not gate runs; the
  `EnforcementInput` path is the modeled, tested bridge and keeps gate outcomes and surface findings distinct in
  the projection.

## D4 — The `Profile` passed to `enforcementInputOf`

**Decision**: Use the same active `Profile` the verify host already resolves for its gate rollup (the one fed to
the existing `RunMode.Verify` enforcement). Thread it from the model, not a fresh default.

**Rationale**: FR-007 requires the surface findings to fold into the *existing* rollup; using a divergent
profile could compute a different effective severity than the rest of the run. The profile is already resolved
once per run in the verify model; reuse it verbatim.

**Alternatives considered**:
- *Hardcode the default profile*: rejected — would ignore a configured profile and risk a verdict inconsistent
  with the gate findings in the same run.

## D5 — Byte-identity and golden strategy

**Decision**:
1. Freeze a **pre-wiring** `verify.json` golden for a repo with **no** declared surfaces (the empty case),
   captured from the current host before the `[] → findings` change, as the byte-identity anchor (FR-004,
   SC-002). Reuse the existing verify golden if one already encodes the no-surface case.
2. Freeze a **non-empty** `verify.json` golden produced by the stable `ofVerifyDecisionWithPreview … findings`
   over a fixed drifted-surface fixture (deterministic ordering per C2).
3. The two existing projection call sites change only `[]` → `model.SurfaceFindings`; with no declared surfaces
   `model.SurfaceFindings = []`, so the empty-case bytes are unchanged by construction.

**Rationale**: The projection already emits byte-identically when findings are empty (the `WithPreview`
overload was introduced in 065 with findings held empty); this feature only supplies real findings when
surfaces are declared. Freezing the empty golden *before* the change makes the no-regression claim falsifiable.

**Alternatives considered**:
- *Only test the non-empty case*: rejected — the empty byte-identity anchor is the core safety guarantee
  (SC-002) and the most likely regression.

## D6 — Safe failure on a sensing error

**Decision**: A domain sensor that cannot read its inputs (missing/unreadable file, path escaping the product
root) surfaces the existing disclosed outcome from that sensor (e.g. `Skill.PathEscapesBounds`,
`Docs.LinkDangling`) rather than reading outside bounds or fabricating a pass; an outright sensing exception at
the `SenseSurfaces` edge degrades to a disclosed diagnostic (no `surfaceChecks` fabricated, the run does not
crash), consistent with how `senseSnapshot` failures already surface as a `SensingDiagnostic` in this host.

**Rationale**: FR-010 + Constitution VI require distinguishing a genuine tool defect from missing/malformed
input, with no silent pass. The domain sensors already encode bounded, disclosed outcomes; the host edge only
needs to not swallow an unexpected exception.

**Alternatives considered**:
- *Treat any sensing failure as a clean pass*: rejected — a silent pass is the exact failure mode Constitution
  VI forbids.

## D7 — Read-only package port + the surface-sense port (resolves analyze I1/U1/A2)

**Decision**: Expose surface sensing as a **port on the verify `Ports` record** —
`SenseSurfaces: ProductSurfaceReport -> SurfaceFinding list` (or the composed bundle+run) — wired in
`realPorts (repo: string)` exactly like the existing `SenseRelease`/`Execute` ports, so the repo root and the
F051/F052 `ExecutionPort` are captured at port-construction time. Tests inject a synthetic port. The `Effect`
carries only the classification scope; the interpreter calls `ports.SenseSurfaces report`. Inside the real
port, the **package** domain is constructed **read-only**:

```text
let readOnlyPackagePort repo exec =
    let real = PackageChecks.Interpreter.realPort repo exec
    { real with
        WriteBaseline  = fun _ _ -> Ok ()      // no-op: absent baseline REPORTED, never written
        ListTranscripts = fun _ -> Ok [] }      // no transcript ⇒ no FSI spawned at verify
```

Docs/Skill/Design are read-only file readers (`realPort repo` / `realPort repo catalogLayout`) and are wired
unchanged.

**Rationale**: The package `realPort` (per `PackageChecks/Interpreter.fsi`) **shells FSI** through the
`ExecutionPort` to run transcripts and, on an **absent baseline**, *regenerates and **writes*** it
(`Interpreter.fs`: `WriteBaseline … Ok() -> BaselineAbsent generated`; `BaselineAbsent` is a hardcoded
**Blocking** finding). Wired naïvely, a first verify run would mutate the working tree (write `.baseline`) and a
second run would then see the baseline present — **breaking SC-004 determinism and the byte-identity goldens** —
and would spawn processes, contradicting the plan's read-only/cheap framing. Verify is the fast inner-loop check;
baseline establishment and transcript execution belong to the route/ship boundaries. The read-only port keeps
the existing `package.baseline-absent` blocking signal (no severity/core change — FR-008) while removing the
write and the exec, so two runs are byte-identical (FR-005, SC-004) and verify is side-effect-free (FR-012,
Constitution VI). Putting sensing on `Ports` (not a repo-root payload on the `Effect`) resolves where the repo
root comes from: it is captured in `realPorts repo`, mirroring `SenseRelease`.

**Alternatives considered**:
- *Wire the package `realPort` as-is*: rejected — writes the tree and spawns FSI at verify; breaks determinism
  and the no-mutation expectation.
- *Drop the package domain from verify entirely*: rejected — loses baseline-drift enforcement at verify; the
  read-only port keeps the static drift/absent checks while shedding only the write + exec.
- *Carry the repo root on `Effect.SenseSurfaces`*: rejected — `Ports` already closes over `repo` (the
  `realPorts repo` pattern); a payload would duplicate it and bypass test injection.

## Summary of reuse (no new cores, no new dependency)

| Concern | Reused from | Status |
|---|---|---|
| Classification → `ProductSurfaceReport` | `ProductSurfaces.classify` | built, used by route |
| Domain sensing (4) | `PackageChecks`/`DocsChecks`/`SkillChecks`/`DesignChecks` `Interpreter.{senseX,realPort}` | built, unit-tested |
| Dispatch + aggregation | `SurfaceChecks.Dispatch.Composition.run` | built, unit-tested (order-independent) |
| Finding → enforcement input | `SurfaceChecks.Model.enforcementInputOf` | built |
| Verdict | existing `deriveEffectiveSeverity` / `Ship.rollup` at `RunMode.Verify` | unchanged |
| `surfaceChecks` projection | `VerifyJson.ofVerifyDecisionWithPreview` (findings param) | built, currently fed `[]` |
