# Phase 0 Research: The `fsgg release` Host Command

All Technical Context unknowns are resolved below. There were no open `NEEDS CLARIFICATION` items
after the one planning decision (release-declaration surface) was confirmed with the requester.

## D1 — Host shape: standalone executable mirroring `ship`

**Decision**: Add a new standalone executable project `FS.GG.Governance.ReleaseCommand` with the exact
module shape of `FS.GG.Governance.ShipCommand`: a pure `Loop` (MVU) module, an edge `Interpreter`
module, and a thin `Program.fs` entry.

**Rationale**: Every existing host command (`RouteCommand` F022, `ShipCommand` F026,
`CacheEligibilityCommand` F044) is a standalone executable with the identical
`Program.fs → Loop.parse → Interpreter.realPorts → Interpreter.run → Loop.exitCode` flow. There is no
central `fsgg` dispatcher, so a subcommand-of-a-dispatcher option does not exist to mirror. The MVU
boundary satisfies Constitution IV for a stateful, I/O-bearing workflow.

**Alternatives considered**: (a) A subcommand under a new shared dispatcher — rejected: no such
dispatcher exists; inventing one is out of scope and not the established precedent. (b) Folding the host
into F054 — rejected: F054 is a pure/edge sensing library with no host, by design.

**Evidence**: `ShipCommand/{Program.fs,Loop.fsi,Interpreter.fsi}`; `RouteCommand`, `CacheEligibilityCommand`.

## D2 — Release declaration surface: row-local `release.yml` adapter (CONFIRMED with requester)

**Decision**: Introduce a new declared file `.fsgg/release.yml` read through the established
`Loader.FileReader` port and parsed by a row-local `Declaration` module inside `ReleaseCommand` into the
inputs the cores need: `ReleaseRule list` (F053), `ReleaseExpectations`, and `SourceLayout` (F054).
**F014 `Config`'s frozen four-file schema, schema version, and surface baselines are NOT edited.**

**Rationale**: The F014 catalog already declares the release *enums* (`ReleaseSurface` class,
`BlockOnRelease` maturity, `Release` environment) but carries **none** of the actual release inputs —
no per-family rule severities, no `VersionBaseline`/`RequiredMetadataFields`/`ExpectedPins`/posture
expectations, and no per-family source paths (verified: `grep` finds no `ReleaseRule`/
`ReleaseExpectations`/`SourceLayout` mapping anywhere in `src/`). A surface for these must be added.
The requester chose the row-local adapter over a Tier 1 F014 schema extension: it keeps the frozen
core product-neutral and untouched, bounds the change to the two new projects, and still satisfies the
spec's "declarations come from the governed catalog, read via the established loader" — the adapter
reuses `Loader.fileSystemReader`/`FileReader` to read the bytes; only the *parse/validation* of the new
release section is row-local.

**Alternatives considered**: Extend the F014 `Config` schema (new typed facts on `TypedFacts`, new
diagnostics, schema-version bump, baseline updates) — rejected by the requester as a larger, contracted
change to the frozen core for a surface only this command consumes today.

**Evidence**: `Config/Model.fsi` (no release inputs), `Config/Schema.fs:173,182,191` (enums only),
`Config/Loader.fsi` (`FileReader`, `fileSystemReader`, `readSource`).

## D3 — Reuse F054 sensing and F053 evaluation verbatim

**Decision**: Sensing is `ReleaseFactsSensing.realPort repoDir layout` → `senseRelease port expectations`
→ `SensedRelease { Facts; Snapshot }`. Evaluation is `Release.evaluateRelease rules sensed.Facts` →
`ReleaseDecision`. No re-derivation, re-sorting, or re-classification in this row.

**Rationale**: FR-003/FR-004 mandate verbatim reuse; F054's `SensedRelease.Facts` *is* the F053
`ReleaseFacts` value handed straight to `evaluate` with zero adaptation (stated in F054's Model.fsi).
F053 already emits exactly one finding per rule, sorted by stable composite key, and rolls up via F023
`deriveEffectiveSeverity` + F024 partition/verdict.

**Alternatives considered**: A bespoke combined "sense+evaluate" helper — rejected: redundant; the two
public entry points compose directly.

**Evidence**: `ReleaseFactsSensing/Interpreter.fsi` (`realPort`, `gather`, `senseRelease`),
`ReleaseFactsSensing/Model.fsi` (`SensedRelease`), `ReleaseRules/Release.fsi`
(`evaluate`/`rollup`/`evaluateRelease`).

## D4 — `release.json` projection: separate pure library mirroring `AuditJson`

**Decision**: Add a pure library `FS.GG.Governance.ReleaseJson` with
`ofRelease : ReleaseDecision -> SensedRelease -> string` and a fixed `schemaVersion` literal (e.g.
`"fsgg.release/v1"`). It renders via a hand-driven `System.Text.Json.Utf8JsonWriter` walk — compact,
non-indented, emit-only (re-derives/re-sorts/re-classifies nothing). It contains the overall verdict and
exit-code basis; per rule the base + effective severity, the `Satisfied/Violated` outcome, the
`Met/Unmet/Unrecoverable` fact state (from `sensed.Facts.States`), and the reason; and the per-family
observed-evidence snapshot (from `sensed.Snapshot`).

**Rationale**: Every deterministic JSON projection in the repo is a separate pure library
(`AuditJson`/`RouteJson`/`GatesJson`/`CacheEligibilityJson`) using `Utf8JsonWriter` with default
(non-indented) options — no new dependency, byte-deterministic, total, never throws. Exhaustive
no-wildcard token helpers make a future enum case a compile error rather than a silent mis-token. The
decision's sections and the snapshot's diagnostics are already deterministically ordered upstream, so
the projection only walks them.

**Alternatives considered**: (a) Inline the JSON in `ReleaseCommand` — rejected: breaks the
separate-projection precedent and the pure/host split, and makes the schema harder to test in isolation.
(b) `System.Text.Json` serializer with reflection/attributes — rejected: ordering/determinism guarantees
are clearer with the established hand-driven writer, and it avoids reflection (Constitution III).

**Evidence**: `AuditJson/AuditJson.fs` (`writeToString`, `schemaVersion`, exhaustive token helpers,
fixed field order, "PURE and TOTAL … never throws … re-derives nothing").

## D5 — Exit-code scheme: five-way `ExitDecision` mirroring `ship`

**Decision**: Reuse the `ShipCommand` `ExitDecision` shape and integer mapping:
`Success → 0`, `Blocked → 1`, `UsageError' → 2`, `InputUnavailable → 3`, `ToolError → 4`. Map
`ReleaseDecision.ExitCodeBasis` (`Clean`/`Blocked`) to `Success`/`Blocked`. `Blocked` (1) is distinct
from every failure-to-run code (FR-005).

**Rationale**: FR-005/SC-005 require five distinguishable classes; the `ship` `ExitDecision` already
encodes exactly these five with the "blocked is distinct from tool failure" invariant. Reusing the same
mapping keeps pipeline semantics consistent across `fsgg` commands.

**Alternatives considered**: A new bespoke code scheme — rejected: gratuitous divergence from the
established `ship` convention the spec assumptions name.

**Evidence**: `ShipCommand/Loop.fsi` (`ExitDecision`), `Loop.fs` `exitCode` (0/1/2/3/4).

## D6 — Atomic `release.json` write

**Decision**: Reuse the temp-then-rename `ArtifactWriter` edge from `ShipCommand` (`writeAtomic`) so a
failed/interrupted write never leaves a truncated `release.json` (FR-012). The writer is an injected port
(`string -> string -> Result<unit, string>`) so tests can capture or fault it.

**Rationale**: FR-012 demands atomic replacement; `ShipCommand`'s `ArtifactWriter`/`writeAtomic` already
implements and tests exactly this (PersistenceEdgeTests).

**Evidence**: `ShipCommand/Interpreter.fsi` (`ArtifactWriter`), `PersistenceEdgeTests.fs`.

## D7 — Fail-safe and input-vs-defect diagnostics

**Decision**: An absent/invalid `release.yml` ⇒ `InputUnavailable` (exit 3) with an actionable
diagnostic; an absent/unreadable/unexpected per-family source ⇒ that family `Unrecoverable` (via F054)
⇒ its rule unmet (never fabricated `Met`) while the command still returns a complete six-family verdict;
bad argv ⇒ `UsageError'` (exit 2); a genuine tool exception (e.g. unwritable output path) ⇒ `ToolError`
(exit 4). All six families always appear in the verdict (FR-013/SC-006).

**Rationale**: Constitution VI and FR-010/FR-011/FR-013 require distinguishing missing/malformed input
from a tool defect and never dropping a family or fabricating a pass. F054's `deriveFacts` already maps
absent expectation/source to `Unrecoverable`; the host maps declaration/argv/IO failures to the distinct
non-`Blocked` codes.

**Evidence**: F054 `Model.fsi` (`Unrecoverable` semantics, `SensingDiagnostic`), `ShipCommand` degrade
paths (`InputUnavailable`/`ToolError`), Constitution VI.

## D8 — Testing strategy: real fixtures, faked ports over real cores, network-free guard

**Decision**: A `withTempRepo` helper builds a real temp `.fsgg/` (incl. `release.yml`) + source files;
most tests fake the injected ports while calling the real F053/F054/`ReleaseJson` cores; one end-to-end
test runs entirely on the real filesystem and asserts verdict, exit code, and that `release.json` bytes
equal a recomputed `ReleaseJson.ofRelease (evaluateRelease …) (senseRelease …)`; a determinism test
asserts byte-identical re-runs; a scope-guard test asserts no network dependency (the F054 precedent).

**Rationale**: Constitution V prefers real evidence; this mirrors `ShipCommand.Tests` (`Support.fs`
`withTempRepo`, `EndToEndTests`, `DeterminismTests`, `FailureTests`, `DegradeTests`, `SurfaceDriftTests`)
and F054's network-free scope guard.

**Evidence**: `ShipCommand.Tests/Support.fs:586` (`withTempRepo`), `EndToEndTests.fs`,
`DeterminismTests.fs`; F054 scope-guard test.

## D9 — Determinism inputs

**Decision**: No clock, no random, no environment, no absolute host paths in any emitted artifact or
text. `GovernedPath`/`SurfaceId` carry only normalized relative paths. The `Utf8JsonWriter` uses default
(non-indented) options. Collection order comes from the already-sorted F053 decision and F054 snapshot.

**Rationale**: FR-008/SC-003 require byte-identical output for identical repository state; the upstream
cores already fix ordering and the projection is emit-only.

**Evidence**: F053 `Release.fsi` (sorted findings), F054 `Model.fsi` (ordinal-sorted diagnostics, sorted
fact fields), `AuditJson.fs` (default writer options).
