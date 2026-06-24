# Implementation Plan: Digest Captured Output And Assemble A Command Record From An Execution Outcome

**Branch**: `050-execution-record` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/050-execution-record/spec.md`

## Summary

The cache/evidence-reuse thread (F029–F049) can now sense each gate's freshness facts, resolve a complete
`FreshnessInputs` world, evaluate reuse, embed the verdict into `route.json` / `audit.json`, persist the bounded
store across runs, and — as of F049 — derive a reproducible `EvidenceRef` from a `CommandRecord` and fold it
into the store. So the pure write path is complete **from a `CommandRecord` onward**. But **nothing produces a
`CommandRecord` from a real execution**: F032 `CommandRecord.build` takes its two `OutputDigest`s as *supplied,
already-computed* values ("no hashing happens here" — F032 FR-010, D3), and **no operation anywhere hashes a
gate's captured output bytes into an `OutputDigest`**, nor assembles a record from a captured *execution
outcome* (raw bytes + run facts) rather than from pre-digested values.

This row delivers that missing **content-addressing bridge** as a new value-only library
`FS.GG.Governance.ExecutionRecord`. It is the **first and only** place in the codebase that hashes output bytes
— precisely the gap F032 left open (D3). Two pure, total functions over already-merged F032 vocabulary:

1. `digestOf : byte[] -> OutputDigest` — the deterministic, byte-stable, content-addressed digest of a gate's
   captured output bytes. SHA-256 over the raw bytes, rendered as lowercase hex, wrapped in the F032
   `OutputDigest` newtype **reused verbatim**. A function of byte **content** only (FR-001): equal content →
   byte-identical digest, any byte difference → a different digest (FR-002); total over empty input — the
   well-defined SHA-256-of-empty hash, distinct from every non-empty digest (FR-003); it reuses the codebase's
   existing SHA-256 precedent (Snapshot interpreter), adds no third-party dependency, and exposes no policy knob
   (FR-011).
2. `recordOf` — assembles a complete F032 `CommandRecord` from a captured execution outcome by digesting the
   stdout bytes into `StdoutDigest`, the stderr bytes into `StderrDigest`, and **delegating the rest to F032
   `CommandRecord.build` verbatim**. Its argument list is `CommandRecord.build`'s **exactly**, with the two
   `OutputDigest` positions replaced by two `byte[]` positions — so `recordOf` is literally `build` composed
   with `digestOf` on the two output fields, nothing more (FR-004, US3). Every other fact (executable, ordered
   arguments, working directory, the env delta's three classes, timeout, exit code, captured-output outcome) and
   the sensed `SensedDuration` are carried verbatim by `build` (FR-005); the duration is never read by the
   digest or by `canonicalId` (FR-006).

Because `recordOf` produces an ordinary F032 record, the chain closes: `recordOf` (this row) →
`EvidenceCapture.referenceOf` (F049) → `EvidenceCapture.capture` (F049) → F047 `serialise` / persist runs from
**raw captured output** all the way to a durable store entry (FR-007, SC-001) — every step pure except the
as-yet-unbuilt process spawn.

The library introduces **no new type** (it reuses `OutputDigest`, `CommandRecord`, the reproducible-fact types,
`SensedDuration`, and the F014 `TimeoutLimit` verbatim), so — like F049 — it needs no `Model.fs(i)`: only
`ExecutionRecord.fsi` + `ExecutionRecord.fs`. It references **only** F032 `CommandRecord`; F014 `Config`
(`TimeoutLimit`) arrives transitively. It adds **no** third-party dependency, bumps **no** schema version, edits
**no** existing core, host command, or golden baseline, and is **referenced by nothing on landing** (exactly as
F047/F049 were). The impure **gate-execution port** (spawning the process, reading real stdout/stderr, timing
the run, sensing the facts) and the **host wiring** are the following rows and are out of scope here.

The committed contract (`ExecutionRecord.fsi`) lives in [contracts/](./contracts/); the digest and
record-assembly semantics in [data-model.md](./data-model.md); the build / exercise / test walkthrough in
[quickstart.md](./quickstart.md); and the resolved decisions in [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` from
`Directory.Build.props`). This row adds one new **pure value-only library**, `FS.GG.Governance.ExecutionRecord`,
in the same packable shape as F049 `EvidenceCapture` and F047 `EvidenceReuseStore`. No new command, no MVU
boundary, no host edit.

**Primary Dependencies**: `ProjectReference`s only; **no new third-party `PackageReference`**. The core
references exactly one on-graph project: `FS.GG.Governance.CommandRecord` (F032 — `build`, `OutputDigest`, the
reproducible-fact types, `SensedDuration`, `CapturedOutput`). `FS.GG.Governance.Config` (F014 — `TimeoutLimit`)
arrives **transitively** through F032. Its own code is BCL + FSharp.Core only: the single new computation is a
SHA-256 hash via `System.Security.Cryptography.SHA256.HashData` rendered with `System.Convert.ToHexString` —
both in the BCL, both already used in the codebase (the F040 `Snapshot` interpreter hashes content the same
way). Test frameworks unchanged (Expecto, Expecto.FsCheck, FsCheck, Microsoft.NET.Test.Sdk,
YoloDev.Expecto.TestSdk).

**Storage**: N/A. This core reads and writes no file. It consumes in-memory `byte[]` buffers and already-built
F032 run facts plus an F032 `SensedDuration`, and returns an `OutputDigest` / `CommandRecord` value. Hashing
supplied bytes is **pure computation, not I/O** (FR-008). The persistence round-trip referenced by the
close-the-loop story (SC-001) is exercised by reusing the already-merged F049 `referenceOf`/`capture`, F030
`decide`, and (optionally) F047 `serialise` against a record this core assembled — no new persistence is
introduced here.

**Testing**: Expecto + FsCheck, in a new `FS.GG.Governance.ExecutionRecord.Tests` project. The core tests run
with **no** filesystem, clock, process, or network access (SC-008): build `byte[]` buffers and F032 run facts
in memory, call `digestOf` / `recordOf`, and assert over the returned values, `CommandRecord.canonicalId`, and
— for the close-the-loop scenarios — F049 `EvidenceCapture.referenceOf` / `capture` and F030
`EvidenceReuse.decide`. The test project references ExecutionRecord + CommandRecord + Config and — **for the
close-the-loop round-trip only** — EvidenceCapture (F049) + EvidenceReuse (F030) + FreshnessKey (F029), exactly
as F049's test project pulled in extra projects for its round-trip. Output digests in tests are **derived from
real byte buffers**, not synthetic literals — that is the whole point of this row — so the `Synthetic` token is
not needed on the digest path. The new public surface is guarded by a reflective surface-drift baseline
(`surface/FS.GG.Governance.ExecutionRecord.surface.txt`).

**Target Platform**: Developer / CI .NET SDK running `dotnet test`. No OS-specific surface; no I/O at all. The
SHA-256 implementation is the framework's, identical across platforms (byte-stability, SC-005).

**Project Type**: A single new pure value-only library. **Principle IV does not apply** (no state, no I/O, no
multi-step workflow): the entire public surface is two pure total functions, exactly as F049 `EvidenceCapture`
and F047 `EvidenceReuseStore` are — those rows also carried no MVU ceremony. The impure gate-execution port that
*would* need an MVU boundary (spawning a process and sensing its output) is the explicitly out-of-scope
following row.

**Performance Goals**: N/A. The added cost is one SHA-256 pass over each captured stream plus one F032 `build`.
SHA-256 is linear in input size and streams arbitrarily large output totally (Edge case "Large output"); no
truncation changes the contract. The contracts are determinism, byte-stability, content-sensitivity, totality,
duration-invariance, and verbatim delegation — not latency.

**Constraints**: Content-addressed (FR-001/FR-002): the digest is a pure function of byte content; equal bytes
agree, any byte difference diverges. Total (FR-003/FR-008): defined for empty bytes, binary/non-textual bytes,
arbitrarily large bytes, a non-zero exit code, an applied timeout, an empty env delta, and every captured-output
outcome; never throwing. Verbatim delegation (FR-004/FR-005): `recordOf` is `CommandRecord.build` with `digestOf`
composed on the two output positions — no new record shape, no normalization, no reuse/success policy; stdout's
digest in `StdoutDigest`, stderr's in `StderrDigest`, never swapped; arguments in supplied order; the env
delta's three classes preserved; duration only in `record.Duration`. Duration-invariant (FR-006): neither
operation reads `SensedDuration`; two outcomes differing only in duration assemble to byte-identical
`canonicalId` (and therefore byte-identical F049 `EvidenceRef`). Close-the-loop (FR-007): `canonicalId` of
`recordOf outcome` is defined and reproducible, F049 `referenceOf` over it is reproducible, and F049 `capture`
makes the gate's world reusable; any single reproducible-fact perturbation (incl. one output byte) changes the
identity and the reference. Deterministic/byte-stable (FR-009): identical bytes + facts → byte-identical digest
and record on every run/process/machine, with no clock/GUID/path/locale/env leakage and no dependence on input
collection identity. Additive (FR-010): a new value-only library reusing F032 vocabulary verbatim, no new
third-party dependency, no schema bump, zero edits to any existing core/host/golden-baseline/reader-shape,
referenced by nothing on landing. Fixed internal scheme (FR-011): the digest algorithm is not a policy knob,
configurable algorithm, or caller-varied value — callers supply bytes and receive an opaque `OutputDigest`.

**Scale/Scope**: Additive only. New files: `src/FS.GG.Governance.ExecutionRecord/` (`.fsi`, `.fs`, `.fsproj`),
`tests/FS.GG.Governance.ExecutionRecord.Tests/` (`.fsproj` + test files),
`surface/FS.GG.Governance.ExecutionRecord.surface.txt`, a `scripts/prelude.fsx` section, the two `.sln`
entries, and the `CLAUDE.md` plan pointer. **Zero** edits to F029/F030/F032/F041–F049 cores, host commands, or
their golden baselines; **zero** new third-party dependencies; **no** schema bump; **no** new type.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still PASS.*

| Principle | Status | Justification |
|-----------|--------|---------------|
| I. Spec → FSI → Semantic Tests → Implementation | PASS | FSI-first is satisfied by committing `contracts/ExecutionRecord.fsi` **before any `.fs` body** and writing public-surface semantic tests (driving `ExecutionRecord.digestOf` / `recordOf`, `CommandRecord.canonicalId`, and F049 `referenceOf`/`capture` through the packed library, never private helpers) that fail before implementation. The `scripts/prelude.fsx` section is the documentation-of-record FSI transcript — the runnable honest-audience exercise of the shipped surface — not the design-time sketch (the `.fsi` is that). |
| II. Visibility lives in `.fsi` | PASS | The two functions `digestOf` / `recordOf` are declared in the curated `ExecutionRecord.fsi`; the `.fs` carries no access modifiers; the new `surface/FS.GG.Governance.ExecutionRecord.surface.txt` baseline is guarded by the existing reflective drift-test pattern. No new type is introduced — the `.fsi` declares only the two functions. |
| III. Idiomatic Simplicity | PASS | The plainest F#: `digestOf` is a four-step BCL pipeline (`SHA256.HashData` → `Convert.ToHexString` → `ToLowerInvariant` → `OutputDigest`); `recordOf` is one expression — `CommandRecord.build … (digestOf stdout) (digestOf stderr) …`. No custom operators, SRTP, reflection (outside tests), type providers, mutation, recursion, or non-trivial CEs. SHA-256 over raw bytes is the BCL standard library, reused from the existing Snapshot precedent rather than a clever abstraction. |
| IV. Elmish/MVU boundary | N/A (does not apply) | Pure, total, stateless, I/O-free value transformation — a content digest and a record assembly. No multi-step state, external I/O, retries, or background work; hashing in-memory bytes is pure computation, not I/O. The same shape as F049 `EvidenceCapture` and F047 `EvidenceReuseStore`, which also carry no MVU ceremony. The impure gate-execution edge that *would* need an MVU boundary (spawning a process, capturing real streams, timing) is the explicitly out-of-scope following row. |
| V. Test Evidence | PASS | Semantic tests fail before the bodies exist and pass after, driving the public FSI surface against **real** F032 `build`/`canonicalId` and **real** F049/F030 operations for the close-the-loop scenarios — all with no I/O (SC-008). Output digests are **derived from real byte buffers**, not synthetic literals — this row *removes* the last synthetic-evidence stand-in on the capture path (F049 could only derive references from hand-written digests; now the digests are real), so the disclosure discipline is satisfied by absence of synthetic data on this path. |
| VI. Observability & Safe Failure | PASS | The core is pure and total — no failure mode to swallow. It never throws and degrades nowhere: every input (empty bytes, binary bytes, arbitrarily large bytes, a non-zero exit code, an applied timeout, an empty env delta, every captured-output outcome) yields an ordinary value (FR-008). `recordOf` introduces no silent weakening or policy gate (FR-004); it records whatever outcome it is handed. |

**Change Classification**: **Tier 1 (contracted change)** — adds new public API surface (a new packable library
with two public functions and a new surface baseline). Requires the full artifact chain: spec, plan, `.fsi`,
surface baseline, and test evidence. **No** new third-party dependency is added; **no** schema version is
bumped; **no** existing public surface is modified (the library is referenced by nothing on landing).

**Engineering Constraints**: net10.0 ✅; the new public module carries a curated `.fsi` ✅; a surface baseline is
added ✅; no new dependency ✅ (BCL `SHA256` + FSharp.Core, layered on the already-on-graph F032 core);
`FS.GG.Governance.*` namespace ✅; pack output of existing packages unaffected ✅ (the new library is
independently packable, like F049/F047); one-way operating rule unaffected (no rendering coupling) ✅. No
violations → **Complexity Tracking is empty**.

## Project Structure

### Documentation (this feature)

```text
specs/050-execution-record/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (input)
├── research.md          # Phase 0 output — the resolved decisions
├── data-model.md        # Phase 1 output — digest + record-assembly semantics
├── quickstart.md        # Phase 1 output — build/exercise/test walkthrough
├── contracts/
│   └── ExecutionRecord.fsi   # Phase 1 output — the curated public surface of the new library
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.Governance.ExecutionRecord/                  # NEW (this row)
├── ExecutionRecord.fsi    # curated surface: digestOf, recordOf (no new type — Model-less)
├── ExecutionRecord.fs     # digestOf: SHA-256 byte pipeline; recordOf: build ∘ digestOf on two fields
└── FS.GG.Governance.ExecutionRecord.fsproj   # ProjectReference CommandRecord (F032)

surface/
└── FS.GG.Governance.ExecutionRecord.surface.txt       # NEW reflective baseline (digestOf, recordOf)

tests/FS.GG.Governance.ExecutionRecord.Tests/          # NEW test project
├── Support.fs             # NEW: real literally-constructible byte buffers + F032 run-fact / FreshnessInputs builders + FsCheck generators (no mocks)
├── DigestTests.fs         # NEW: content agreement (US2/SC-002), single-byte sensitivity incl. add/remove/reorder (US2/SC-003), empty-input totality + distinctness (FR-003), binary/large totality (Edge), determinism (SC-005), identical-stdout/stderr equality (Edge)
├── RecordTests.fs         # NEW: verbatim delegation = build ∘ digestOf (US3/SC-007), digest-in-correct-position never swapped (FR-005), arguments/env-delta carriage (FR-005), non-zero exit recorded (US3 AC2), duration carried only in Duration (FR-005)
├── CloseLoopTests.fs      # NEW: canonicalId + F049 referenceOf reproducible (US1/SC-001), capture makes world reusable (US1 AC3), single-reproducible-fact perturbation changes identity+reference (US2/SC-003), duration-invariance of identity+reference (US2/SC-004, FR-006)
├── SurfaceDriftTests.fs   # NEW: reflective surface baseline + scope-hygiene assertion (Principle II)
├── Main.fs                # NEW: Expecto entry point
└── FS.GG.Governance.ExecutionRecord.Tests.fsproj

scripts/prelude.fsx                                     # + an ExecutionRecord walkthrough section

FS.GG.Governance.sln                                    # + the new src + test project entries

# Untouched (additive guarantee): F032 CommandRecord, F030 EvidenceReuse, F029 FreshnessKey, F049
# EvidenceCapture, F047 EvidenceReuseStore, F046 FreshnessSensing + its reader, all F041–F049 cores and host
# commands, every route.json/audit.json/cache-eligibility golden baseline, the fsgg.evidence-reuse-store/v1
# schema, the command-record-identity-format contract.
```

**Structure Decision**: Deliver a **new standalone pure library** layered on top of the already-merged F032 core
(constitution: heavier capabilities layer on top, not into the core; the F042/F047/F049 precedent), rather than
adding `digestOf`/`recordOf` to `CommandRecord` — that would edit a frozen merged surface and its golden/surface
baseline, violating the additive guarantee (FR-010), and would force F032 to start hashing bytes, contradicting
its explicit D3 ("no hashing happens here"). The library is **Model-less** (introduces no new type — it reuses
F032's `OutputDigest`/`CommandRecord`/reproducible-fact vocabulary verbatim), so it carries only
`ExecutionRecord.fsi` + `ExecutionRecord.fs`, exactly as F049 did. It is referenced by nothing on landing; the
gate-execution port and the host wiring that consume it are the explicitly out-of-scope following rows.

## Complexity Tracking

> No Constitution Check violations. This section is intentionally empty.
