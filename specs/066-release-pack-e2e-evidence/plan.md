# Implementation Plan: Release-Provenance End-to-End Pack Evidence and Byte-Identity Goldens

**Branch**: `066-release-pack-e2e-evidence` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/066-release-pack-e2e-evidence/spec.md`

## Summary

The F26 release-provenance host wiring (`065-release-provenance-host-wiring`) landed and is green
(2051 tests), but three follow-ups were explicitly deferred as `065` tasks **T009 / T018 / T023 /
T024** and tracked as a "Partial follow-ups" note in `docs/initial-implementation-plan.md`. Today the
release pack/version boundary is proven only by pure-MVU transition tests and emitted-effect assertions
over the real F26 cores, with the per-project pack execution supplied through a **disclosed-synthetic**
fake (`portsWithPacks` / `PackRead` replay in `ReleaseCommand.Tests/Support.fs`). This feature closes
the three deferrals and upgrades that evidence from synthetic-pack proof to **real-`dotnet pack`** proof,
plus it pins the four no-change contracts byte-for-byte against frozen baselines.

This is a **test-evidence-and-goldens-only** feature. It adds **no** new product behaviour, **no** new
pure core, **no** new schema, exit code, verdict, or public surface; it touches **no** `.fsi` and **no**
surface baseline. The work is concentrated in three places:

1. **A real-`dotnet pack` pack-boundary fixture** (US1, closes `065` T018, SC-001/SC-003) — extend the
   release host's `.Tests` project to drive `Interpreter.run` over a real temporary multi-project tree
   through the **real** F51 execution port (`GateExecution.Interpreter.realPort`) and a **real**
   pack-output reader, asserting the verdict, exit code, recorded `Pack` runs, and written
   `release.json` v2 + `attestation.json` for the bumped / failed-pack / unbumped-or-downgraded /
   no-baseline cases.
2. **A mergeable-vs-releasable fixture pair** (US2, closes `065` T023, SC-002, FR-008) — contrast
   `fsgg ship` (exit 0) against `fsgg release` (exit 1, distinct basis) for the same product, and assert
   the publish-plan / trusted-publishing / template-pin preconditions surface as named
   `PreconditionEvidence` in `release.json` v2 in the correct satisfied/unmet states.
3. **Four frozen pre-wiring byte-identity goldens** (US3, closes `065` T009/T024, SC-005) — commit
   `route.json`, `ship.json`, a no-declaration `verify.json`, and an empty-additive `release.json` v2
   baseline, and assert each producing command is byte-identical to its baseline for identical
   repository state.

**The central correctness obligation (research.md D1) — the goldens must be honestly *pre-wiring*.** The
spec's edge case is explicit: re-deriving the goldens from the post-wiring code makes the check vacuous.
But `065` already landed, so "pre-wiring" can no longer mean "current `main`". The reconnaissance pins
the resolution: the genuine pre-wiring anchor is commit **`5a0cb28`** (the F25/`064` commit — the parent
of `065`'s single commit `1ddf169`). `route.json`, `ship.json`, and a no-declaration `verify.json` are
byte-identical at `5a0cb28` and at `main` *by construction* (RouteCommand/ShipCommand are untouched by
`065`; the `verify.json` `releaseReadiness` block is emitted only when a declaration is present), so
their goldens are frozen from a `5a0cb28` checkout to make the regression check non-vacuous. The
empty-v2 `release.json` is the exception: `release.json` v2 is *introduced* by F26/`065`, so there is no
true pre-wiring v2 — its golden is the F26-blessed empty-additive v2 contract captured from current
code (exactly as `065` T024 scoped it), pinning the additive contract going forward.

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **The real-pack harness reuses the wired host verbatim — only the faked edge ports go real (D1).**
   `065` already built the release host's `Execute` / `PackRead` / `SenseHead` / `SenseEnvironment` /
   `SenseBuilder` ports and proved the pure transition. This feature swaps the disclosed-synthetic
   `portsWithPacks` for the **real** `GateExecution.Interpreter.realPort` and a **real** `PackRead` that
   locates the produced `.nupkg`, reads its packed version, and computes its digest. No host or core code
   changes; the only new code is fixture-construction and assertion.
2. **Goldens are frozen from the pre-wiring anchor `5a0cb28` (route/ship/no-decl-verify) or the blessed
   v2 contract (empty-v2 release) (D2).** The freeze is a one-time capture committed as a fixture file;
   the test re-runs the producing command and asserts byte-equality. The three construction-identical
   goldens are captured from a `5a0cb28` worktree to keep the check honest; the empty-v2 release golden
   is the F26 contract pin.
3. **Each byte-identity golden lives in its producing host's `.Tests` project (D3).** `route.json` →
   `RouteCommand.Tests`, `ship.json` → `ShipCommand.Tests`, empty-v2 `release.json` →
   `ReleaseCommand.Tests`, no-declaration `verify.json` → `VerifyCommand.Tests`. Each test runs the
   **real** producing command over a fixed fixture repo (no cross-host reference, no faked producer),
   superseding `065` T024's centralized-in-ReleaseCommand.Tests placement note.
4. **The real-pack fixtures are deterministic and SDK-gated (D4).** The temporary project tree is
   generated from explicit literals (no machine paths, usernames, or wall-clock in any asserted output);
   pack duration is sensed metadata only and is excluded from the byte-identity assertions. An
   environment lacking a working `dotnet pack` surfaces a disclosed Expecto skip with a diagnostic
   (`FR-008`), never a silent green.
5. **No product surface moves; the `065` deferrals are flipped and the roadmap note closed (D5).** When
   the evidence lands, `065` tasks.md T009/T018/T023/T024 are marked complete and the
   `docs/initial-implementation-plan.md` F26 "Partial follow-ups" note is rewritten to record the
   real-pack evidence + frozen goldens as closed (`FR-007`, SC-005).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).
The real-pack fixture generates `net10.0` library projects (the repo standard).

**Primary Dependencies**: **No new external/NuGet dependency, no new ProjectReference, no source
change.** The real-pack harness consumes the **already-wired** F26 release host and its ports
(`FS.GG.Governance.ReleaseCommand`, the F51 `FS.GG.Governance.GateExecution.Interpreter.realPort:
ExecutionPort`, the F16 `Snapshot` head sense, the normalized `064` environment/builder senses) verbatim
through the existing `.Tests` projects. The byte-identity goldens run the existing `RouteCommand` /
`ShipCommand` / `ReleaseCommand` / `VerifyCommand` hosts unchanged. JSON comparison is plain
`string`/byte equality over the BCL-`System.Text.Json` output the hosts already produce.

**Storage**: Four **committed golden fixture files** (test data, not product artifacts) under the
producing hosts' `.Tests` projects: a frozen `route.json`, `ship.json`, no-declaration `verify.json`
(captured from the pre-wiring anchor `5a0cb28`), and an empty-additive `release.json` v2 (the F26-blessed
contract). The real-pack fixture writes real `.nupkg` artifacts to the constitution's
`~/.local/share/nuget-local/` through the existing F51 execution port (the host edge, no new write path)
and writes `release.json` v2 + `attestation.json` to a temp repo. No product schema, write path, or
persisted contract changes.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). New fixtures: (a) a
**real-`dotnet pack` pack-boundary** fixture in `ReleaseCommand.Tests` driving `Interpreter.run` over a
real temp multi-project tree through `GateExecution.Interpreter.realPort` + a real `PackRead`, covering
bumped (pass, packs recorded) / failed-pack (block, sentinel run recorded) / unbumped-or-downgraded
(block, project+version named) / no-baseline (first release, not a downgrade), with `release.json` v2 +
`attestation.json` byte-identical on re-run; (b) a **mergeable-vs-releasable** pair contrasting `fsgg
ship` exit 0 vs `fsgg release` exit 1 and asserting the publish-plan / trusted-publishing / template-pin
`PreconditionEvidence` states in `release.json` v2; (c) **four byte-identity golden** tests, one per
producing host, comparing real command output to the frozen baseline. The pure F26 cores and the wired
host's pure transition are already covered by the existing `065` suites and are reused, not re-tested.
Real cores and the real host edge are never mocked here — the whole point is the real `dotnet pack`. The
SDK-absent path surfaces a disclosed Expecto skip (`FR-008`); any literal stand-in used to provoke a
pack failure carries `Synthetic` in the test name with a use-site disclosure (Constitution V).

**Target Platform**: Cross-platform .NET CLI (Linux/macOS/Windows). `release.json` v2 and
`attestation.json` are normalized by the F26 cores (no path/username/clock/environment leakage; pack
duration retained as sensed metadata only) so the real-pack outputs are byte-identical across machines
and re-runs (SC-003); the four frozen goldens are byte-identical for identical repository state (SC-004).

**Project Type**: Test evidence + committed golden baselines over the already-wired F26 release/verify
hosts. **No** source project, pure core, report object, schema, verdict, exit code, or public surface is
added or changed. Extends **4** existing `.Tests` projects (`ReleaseCommand.Tests`, `VerifyCommand.Tests`,
`RouteCommand.Tests`, `ShipCommand.Tests`); adds **4** committed golden fixture files.

**Performance Goals**: Not a hot path. The only real expense is the per-project `dotnet pack` the
real-pack fixture runs — the act the release boundary exists to enforce — through the existing F51 port;
it runs in CI/dev test scope, not an inner loop. The byte-identity golden tests are a single command run
plus a string compare each.

**Constraints**: The real-pack outputs (`release.json` v2, `attestation.json`) MUST be deterministic —
stable ordering, normalized paths, no wall-clock/username/environment dependence, pack duration as sensed
metadata only excluded from identity (FR-006, SC-003). The four frozen goldens MUST be byte-identical for
identical repository state and frozen from genuinely pre-wiring state (route/ship/no-decl-verify from
`5a0cb28`; empty-v2 release from the F26-blessed contract) so the regression check is non-vacuous (FR-005,
SC-004, edge case). A test environment lacking a working `dotnet pack` MUST surface a disclosed skip/fail
diagnostic, never a silent pass (FR-008). This feature adds **no** product behaviour, schema, exit code,
verdict, or public surface (FR-007); `065` T009/T018/T023/T024 are flipped and the roadmap note closed.

**Scale/Scope**: Extends **4 test projects** and adds **4 golden fixture files**. `ReleaseCommand.Tests`
gains the real-`dotnet pack` pack-boundary fixture (four cases + determinism) and the
mergeable-vs-releasable pair + the empty-v2 `release.json` golden; `VerifyCommand.Tests` gains the
no-declaration `verify.json` golden; `RouteCommand.Tests` / `ShipCommand.Tests` each gain one frozen
golden. **No** new source project, pure evaluation core, report object, verdict, exit-code scheme,
release-rule family, schema, or external dependency.
P1 = US1 (real-`dotnet pack` pack-boundary) — the MVP; P2 = US2 (mergeable-vs-releasable + FR-008
preconditions); P3 = US3 (four frozen byte-identity goldens).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. No new public surface is introduced; the
  F26 cores and the wired release/verify hosts already exist as curated `.fsi` and are exercised here
  through their loaded host surface (`Interpreter.run`, the real CLI commands), not private helpers — the
  honest FSI audience. This feature is the *semantic-test* layer of the existing surface.
- **II. Visibility Lives in `.fsi`** — PASS. No `.fsi` changes; no surface baseline is touched. Adding
  test code and golden data files does not alter any module's public surface (Tier 2).
- **III. Idiomatic Simplicity** — PASS. Fixtures are plain F#: literal project trees, plain command
  runs, `Expect.equal` byte comparisons, exhaustive matches on `PackOutcome`/`PreconditionEvidence`. No
  SRTP/reflection/type-providers/non-trivial CEs/active patterns. No new external dependency.
- **IV. Elmish/MVU Is the Boundary** — PASS. No new workflow. The tests exercise the **existing** MVU
  hosts at their interpreter edge with the real ports swapped in (real `dotnet pack`, real atomic write,
  real producing commands) — exactly the "interpreter tests: execute effects against a real filesystem,
  process … where safe" the principle mandates. No pure core gains any I/O.
- **V. Test Evidence Is Mandatory** — PASS. This feature *is* the real-evidence upgrade: it replaces the
  disclosed-synthetic pack execution with a real `dotnet pack` over a real tree, and pins the four
  no-change contracts to frozen pre-wiring baselines. The SDK-absent path is a disclosed skip with a
  diagnostic (never a silent green); any literal stand-in for a forced pack failure is `Synthetic`-named
  and disclosed.
- **VI. Observability and Safe Failure** — PASS. The fixtures assert the existing safe-failure
  behaviour holds under real packing: a failed pack blocks with a named reason and the failed `Pack`
  run recorded with its sentinel (never dropped, never a fabricated pass); a zero-exit-no-artifact pack
  blocks with "packed but no artifact produced"; a missing SDK surfaces a clear diagnostic.

**Change Classification: Tier 2 (internal change)** — adds test evidence and committed golden data
files with **no** behavioural change, **no** public API/`.fsi` change, and **no** surface-baseline
change. Per the constitution's Change Classification, a Tier 2 change "requires spec and tests; `.fsi`
and baselines remain untouched" — which is exactly this feature's shape. The spec's `FR-007` makes the
no-new-surface guarantee a first-class requirement.

**Result: PASS — no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/066-release-pack-e2e-evidence/
├── plan.md                          # This file (/speckit-plan output)
├── research.md                      # Phase 0 — D1..D5 (pre-wiring anchor, golden placement, SDK gating)
├── data-model.md                    # Phase 1 — fixture entities: real-pack tree, precondition pair, golden set
├── quickstart.md                    # Phase 1 — per-story validation scenarios (SC-001..SC-005)
├── contracts/                       # Phase 1
│   ├── real-pack-boundary.md        #   real dotnet pack via realPort + real PackRead → four cases + determinism
│   ├── mergeable-vs-releasable.md   #   ship exit 0 vs release exit 1; PreconditionEvidence states in release.json v2
│   └── byte-identity-goldens.md     #   four frozen baselines; pre-wiring anchor 5a0cb28; per-host placement
└── tasks.md                         # Phase 2 (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

**No `src/` changes.** This feature adds no source project, core, host edit, `.fsi`, or surface baseline.
All work lands in existing `tests/` projects plus four committed golden fixture files.

```text
src/
└── (UNCHANGED — the F26 cores and the two wired hosts ReleaseCommand/VerifyCommand are consumed verbatim;
     RouteCommand/ShipCommand are untouched and only exercised by the byte-identity golden tests)

tests/
├── FS.GG.Governance.ReleaseCommand.Tests/
│   ├── RealPackTests.fs                 # NEW — real-`dotnet pack` pack-boundary fixture (US1): generate a real
│   │                                    #   temp multi-project tree, drive Interpreter.run through
│   │                                    #   GateExecution.Interpreter.realPort + a real PackRead; assert the
│   │                                    #   bumped / failed-pack / unbumped-or-downgraded / no-baseline cases +
│   │                                    #   recorded Pack runs + written release.json v2/attestation.json;
│   │                                    #   re-run byte-identity (SC-001, SC-003). SDK-gated skip (FR-008).
│   │                                    #   (mergeable-vs-releasable pair, US2 — same file or MergeableTests.fs)
│   ├── PersistenceEdgeTests.fs          # EXTEND — empty-additive `release.json` v2 byte-identical to the frozen
│   │                                    #   F26-blessed golden (US3, SC-004)
│   ├── Support.fs                        # EXTEND — real-pack tree generator + real PackRead helper + SDK probe
│   │                                    #   (the disclosed-synthetic portsWithPacks stays for the `065` pure tests)
│   └── goldens/
│       └── release.empty-v2.json         # NEW golden — F26-blessed empty-additive `fsgg.release/v2`
├── FS.GG.Governance.VerifyCommand.Tests/
│   ├── PersistenceEdgeTests.fs          # EXTEND — no-declaration `verify.json` byte-identical to the frozen
│   │                                    #   pre-wiring golden (US3, SC-004)
│   └── goldens/
│       └── verify.no-declaration.json    # NEW golden — frozen from the pre-wiring anchor 5a0cb28
├── FS.GG.Governance.RouteCommand.Tests/
│   ├── PersistenceEdgeTests.fs          # EXTEND — `route.json` byte-identical to the frozen pre-wiring golden
│   └── goldens/
│       └── route.json                    # NEW golden — frozen from the pre-wiring anchor 5a0cb28
└── FS.GG.Governance.ShipCommand.Tests/
    ├── PersistenceEdgeTests.fs          # EXTEND — `ship.json` byte-identical to the frozen pre-wiring golden
    └── goldens/
        └── ship.json                     # NEW golden — frozen from the pre-wiring anchor 5a0cb28

docs/initial-implementation-plan.md       # EDIT — flip the F26 "Partial follow-ups" note to closed
specs/065-release-provenance-host-wiring/tasks.md  # EDIT — mark T009/T018/T023/T024 complete, citing this row
```

**Structure Decision**: Evidence and goldens only — change no product code. The real-pack proof reuses
the already-wired F26 release host (`065`) and swaps its faked execution/pack-read edge for the **real**
F51 `GateExecution.Interpreter.realPort` + a real `.nupkg` reader, so the genuinely-new code is fixture
construction and assertion in `ReleaseCommand.Tests`. Each byte-identity golden lives in its **producing**
host's `.Tests` project (route→Route, ship→Ship, release→Release, verify→Verify) so every comparison runs
the real producing command with no cross-host reference. The honesty anchor (research.md D2) bounds the
goldens: the three construction-identical contracts are frozen from the pre-wiring commit `5a0cb28`, and
the empty-v2 `release.json` is the F26-blessed additive contract — the only non-vacuous way to pin a
contract that `065` itself introduced.

## Complexity Tracking

> No Constitution Check violations. **No new external dependency, no new project, no source change.** The
> one subtlety — that "pre-wiring" goldens can no longer be captured from `main` because `065` already
> landed — is resolved by freezing from the explicit pre-wiring anchor commit `5a0cb28` (research.md D2),
> not by any escape hatch or vacuous self-comparison. This section is intentionally empty.
