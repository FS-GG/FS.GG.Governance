# Feature Specification: Digest Captured Output And Assemble A Command Record From An Execution Outcome

**Feature Branch**: `050-execution-record`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved (via AskUserQuestion, choosing the
**pure execution-record core** over the impure gate-execution port and the full host-wiring row) to the next
deferred step of the cache/evidence-reuse thread: the value-only bridge that turns a gate's **captured raw
output** into the byte-stable `OutputDigest`s F032 requires and **assembles** the complete F032 `CommandRecord`
from an already-captured execution outcome, ready to hand to F049 `EvidenceCapture.referenceOf` / `capture`.
Mirrors how F047/F049 delivered the pure halves before their impure edges were wired: the **impure** edge
(actually spawning a gate's process, reading its real stdout/stderr, timing the run) is the **following** row
(the gate-execution port) and is out of scope here.

## Context

The cache/evidence-reuse thread (F029–F049) can now sense each selected gate's freshness facts, resolve them to
a complete `FreshnessInputs` world (F043), evaluate reuse against the loaded evidence-reuse store (F030 →
F041), embed the per-gate `reusable` / `mustRecompute` verdict into `route.json` / `audit.json` (F045/F046),
persist the bounded, pruned store across runs (F047/F048), and — as of F049 — **derive a reproducible
`EvidenceRef` from a `CommandRecord` and fold it into the store** (`EvidenceCapture.referenceOf` / `capture`).

So the pure write path is complete *from a `CommandRecord` onward*. But **nothing produces a `CommandRecord`
from a real execution.** F032 `CommandRecord.build` assembles the ten run facts into a record, and it is
explicit that the two output digests are **supplied, already-computed** values — its `OutputDigest` is "a
supplied, already-computed digest of stdout OR stderr. Opaque — **no hashing happens here** (FR-010, D3)." F032
deliberately deferred the digesting so a dedicated row could own it. No row has done so yet: **there is no
operation anywhere in the codebase that turns a gate's captured output bytes into an `OutputDigest`**, and no
operation that assembles a record from a captured *execution outcome* (raw output + run facts) rather than from
pre-digested values.

The missing piece is the **content-addressing bridge**: a pure, deterministic digest of a gate's captured
output bytes, and an assembly that pairs those two digests with the caller's already-sensed reproducible facts
and the sensed duration to produce a complete F032 `CommandRecord`. With it, the chain
`recordOf` (this row) → `referenceOf` (F049) → `capture` (F049) → `serialise`/persist (F047/F048) runs from
**raw captured output** all the way to a durable store entry — every step pure except the as-yet-unbuilt
process spawn.

This row delivers that bridge as a new value-only core (`FS.GG.Governance.ExecutionRecord`), referenced by
nothing yet (exactly as F047/F049 were on landing). It is the **first and only** place in the codebase that
hashes output bytes — that is precisely the gap F032 left open. It spawns **no** process, reads **no**
clock/filesystem/git/environment/network, bumps **no** schema version, and changes **no** existing core, host
command, or golden baseline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Captured output becomes a complete, reproducible command record (Priority: P1)

As the governance cache author, given a gate's **captured execution outcome** — the program and arguments it
ran, its working directory, environment delta, timeout, exit code, captured-output outcome, sensed duration,
and the **raw bytes** it wrote to stdout and stderr — I can assemble a complete F032 `CommandRecord` in which
the stdout and stderr digests are deterministic, byte-stable digests of those raw bytes, so the record is ready
to hand to F049 `referenceOf` / `capture` and the run's evidence reference is itself reproducible.

**Why this priority**: This is the whole point of the row and the only step that closes the last pure gap
between a real execution and a store entry. Without it, F032 `build` can only be fed hand-written digest
literals, F049 can only derive references from synthetic records, and the store can never gain an entry that
reflects what a gate actually produced. It is the smallest standalone slice that bridges raw output to a
reproducible record.

**Independent Test**: In FSI / a semantic test, build a captured execution outcome (raw stdout/stderr bytes
plus the reproducible run facts and a duration), call `recordOf` to assemble the `CommandRecord`, and assert
that `CommandRecord.canonicalId` of the result, and `EvidenceCapture.referenceOf` over it (F049), are defined,
reproducible values — and that `EvidenceCapture.capture` of that record makes its freshness world reusable. No
I/O.

**Acceptance Scenarios**:

1. **Given** a captured execution outcome (raw stdout/stderr bytes plus the seven supplied run facts and a
   sensed duration), **When** `recordOf` assembles the record, **Then** the result is a complete F032
   `CommandRecord` whose `StdoutDigest` is the digest of the supplied stdout bytes, whose `StderrDigest` is the
   digest of the supplied stderr bytes, and whose every other fact (executable, arguments **in order**, working
   directory, the env delta's three classes, timeout, exit code, captured-output outcome) and the sensed
   duration are carried verbatim — exactly as F032 `build` carries them.
2. **Given** the same captured outcome, **When** `recordOf` runs twice on two machines / processes, **Then**
   both produce the byte-identical `CommandRecord` (determinism, no clock/GUID/path/locale/env leakage).
3. **Given** a record assembled by `recordOf`, **When** it is handed to F049 `referenceOf` and `capture` over
   the empty store, **Then** `EvidenceReuse.decide` for that gate's world returns `Reuse r` with
   `r = referenceOf record` — the captured run is now reusable (the raw-output-to-store-entry round-trip).

---

### User Story 2 - The digest is content-addressed and reproducible: same bytes agree, different bytes diverge (Priority: P1)

As the cache author, two executions of the *same* gate that produced the **same output bytes** must yield the
**same** stdout/stderr digests (so a faithful re-run records the same reference and the cache can serve it),
while any difference in the produced bytes must yield a **different** digest (so a changed output is never
mistaken for an unchanged one) — and the digest must depend only on the byte **content**, never on the sensed
duration or on any wall-clock, GUID, path, locale, or environment value.

**Why this priority**: A digest that varied with anything but the output content would defeat reuse (a faithful
re-run would record a different reference and never be served) or, worse, mask a real output change (a changed
gate output mistaken for cache-fresh). Content-addressing is a correctness contract. Because the digest feeds
F032 `canonicalId` (a reproducible fact) and the duration is excluded from that identity by construction (F032
D2), the assembled reference is reproducible end-to-end.

**Independent Test**: Digest two byte sequences that are equal and assert the digests are byte-identical; digest
two that differ by a single byte and assert the digests differ. Assemble two outcomes identical in every
reproducible fact (including identical output bytes) but differing only in sensed duration and assert
`recordOf`'s `canonicalId` (and F049 `referenceOf`) are byte-identical.

**Acceptance Scenarios**:

1. **Given** two captured outputs with byte-identical content, **When** each is digested, **Then** the two
   `OutputDigest`s are byte-identical.
2. **Given** two captured outputs differing in **any** byte (a single byte changed, added, removed, or
   reordered), **When** each is digested, **Then** the two `OutputDigest`s differ.
3. **Given** two captured outcomes identical in every reproducible fact (including identical stdout and stderr
   bytes) but differing **only** in sensed duration, **When** `recordOf` assembles each, **Then** the two
   records' `canonicalId` (and F049 `referenceOf`) are byte-identical — the digest and identity never read the
   duration.
4. **Given** two captured outcomes differing in **any one** reproducible fact (executable, an argument or its
   order, working directory, the env delta as a set, timeout, exit code, **a byte of either output**, or the
   captured-output outcome), **When** `recordOf` assembles each, **Then** the two records' `canonicalId` (and
   F049 `referenceOf`) differ.

---

### User Story 3 - Assembly is purely additive: no new record shape, no policy, no I/O (Priority: P2)

As the maintainer, assembling a record from a captured outcome must **delegate to F032 `build` verbatim** for
the carriage of every fact — introducing no new `CommandRecord` representation, no normalization, and no reuse
or success policy — so the only new computation this row adds is the two output digests, and the result is a
plain F032 record indistinguishable from one `build` would have produced from the same (already-digested)
inputs.

**Why this priority**: It guards the additive guarantee the whole thread inherits — the record-assembly bridge
must not fork F032's record shape, re-canonicalize anything, or smuggle in a policy decision (such as
suppressing a failed run); it records whatever outcome it is handed, exactly as F049 `capture` does.

**Independent Test**: For an outcome whose stdout/stderr bytes have known digests, assert `recordOf outcome`
equals `CommandRecord.build` applied to the same facts with those known digests substituted for the raw bytes —
i.e. `recordOf` is `build` composed with the digest on the two output fields, nothing more.

**Acceptance Scenarios**:

1. **Given** a captured outcome and the digests of its stdout and stderr bytes, **When** `recordOf` assembles
   the record, **Then** the result equals `CommandRecord.build` of the same nine reproducible facts and
   duration with those two digests in the stdout/stderr positions (no field reordered, no env-delta class
   merged or split, arguments in the same order, duration only in `Duration`).
2. **Given** a captured outcome with a **non-zero** exit code, **When** `recordOf` assembles the record,
   **Then** it produces an ordinary complete record (a failed run is recorded, not rejected — F032 FR-003); any
   success / exit-code gating is a host-row reuse-policy decision, out of scope here.

### Edge Cases

- **Empty captured output**: stdout and/or stderr with **zero bytes** is digested to a defined, fixed-form
  `OutputDigest` (the operation is total over empty input), and that digest is distinct from the digest of any
  non-empty output.
- **Identical stdout and stderr bytes**: when the two streams carried the same bytes, their two digests are
  equal (the digest is a function of content alone); F032 `canonicalId` still distinguishes the two fields
  positionally, so the record and its identity are unaffected.
- **Binary / non-textual output**: output bytes that are not valid text are digested as raw bytes — no
  decoding, no locale, no normalization — totally and deterministically.
- **Large output**: arbitrarily large captured output is digested totally and deterministically (the result is
  a fixed-form digest regardless of input size); no truncation changes the contract.
- **Failed run / applied timeout**: a non-zero exit code, or a run whose timeout applied, assembles to an
  ordinary complete record (inherited from F032 `build`); this core gates nothing.
- **Duration variation**: two outcomes identical in all reproducible facts (including output bytes) but
  differing only in sensed duration assemble to records with byte-identical `canonicalId` and F049 reference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a pure operation that derives a deterministic, byte-stable `OutputDigest`
  (F032, reused) from a gate's **captured output bytes** — the first and only place in the codebase that hashes
  output bytes, owning the digesting F032 explicitly deferred (D3). The digest MUST be a function of the byte
  **content** only.
- **FR-002**: The digest MUST agree for equal content and diverge for different content: two captured outputs
  with byte-identical content MUST yield the byte-identical `OutputDigest`; two outputs differing in any byte
  (changed, added, removed, or reordered) MUST yield different `OutputDigest`s.
- **FR-003**: The digest operation MUST be total over **empty** captured output (zero bytes), yielding a
  defined, fixed-form `OutputDigest` distinct from the digest of any non-empty output, and never throwing.
- **FR-004**: The system MUST provide a pure operation that assembles a complete F032 `CommandRecord` from a
  **captured execution outcome** — the seven run facts the caller supplies (executable, ordered
  arguments, working directory, environment delta, timeout, exit code, captured-output outcome) plus the sensed
  duration, together with the **raw stdout and stderr bytes** (which it digests into the two remaining
  reproducible facts) — by digesting the stdout bytes into the record's
  `StdoutDigest`, digesting the stderr bytes into its `StderrDigest`, and delegating the assembly to F032
  `CommandRecord.build` **verbatim**. It MUST introduce no new record representation, no normalization, and no
  reuse/success policy.
- **FR-005**: The assembly MUST place the digest of stdout in `StdoutDigest` and the digest of stderr in
  `StderrDigest` (never swapped), and MUST carry every other supplied fact into the record exactly as F032
  `build` does — arguments in supplied order, the env delta's three classes preserved (a `Changed` entry never
  split into `Added` + `Removed`), and the sensed duration placed in `record.Duration` and nowhere in
  `record.Reproducible`.
- **FR-006**: Neither operation MUST read the sensed duration when computing a digest or any reproducible fact:
  two captured outcomes identical in all reproducible facts (including output bytes) and differing only in
  sensed duration MUST assemble to records with the byte-identical `CommandRecord.canonicalId` (and therefore,
  via F049, the byte-identical `EvidenceRef`).
- **FR-007**: The assembled record MUST close the loop with F032 and F049: for any captured outcome,
  `CommandRecord.canonicalId` of `recordOf outcome` MUST be defined and reproducible, `EvidenceCapture.referenceOf`
  over it MUST yield a reproducible reference, and `EvidenceCapture.capture` of that record MUST make the gate's
  freshness world reusable for exactly that derived reference. Any single-reproducible-fact perturbation
  (including a single output byte) MUST change the identity and the reference.
- **FR-008**: Both operations MUST be **pure and total**: defined for every input (including empty output
  bytes, a non-zero exit code, an applied timeout, an empty environment delta, and every captured-output
  outcome), never throwing; reading no clock, filesystem, git, environment, or network; spawning no process;
  and computing only over the supplied in-memory bytes and facts (hashing supplied bytes is pure computation, not
  I/O).
- **FR-009**: The `OutputDigest` and the assembled `CommandRecord` MUST be deterministic and byte-stable:
  identical input bytes and facts produce the byte-identical digest and the byte-identical record on every run,
  process, and machine — no wall-clock, GUID, path, locale, or environment leakage, and no dependence on input
  collection identity (only on byte content and fact values).
- **FR-010**: The change MUST be additive and self-contained: a new value-only library reusing F032
  `CommandRecord` vocabulary verbatim (`OutputDigest`, `CommandRecord`, the reproducible-fact types,
  `SensedDuration`, the F014 `TimeoutLimit`); **no** new third-party dependency; **no** schema-version bump;
  **no** edit to any existing core, host command, golden baseline, or reader-accepted shape. The library is
  referenced by nothing on landing (the gate-execution port and the host wiring are following rows).
- **FR-011**: The digest scheme MUST be fixed and internal — not exposed as a policy knob, a configurable
  algorithm, or a value a caller can vary. Callers supply bytes and receive an opaque `OutputDigest`; the
  scheme's only contract is determinism, byte-stability, totality, and content-sensitivity (FR-001–FR-003).

### Key Entities *(include if feature involves data)*

- **Captured output bytes** (new input vocabulary, this row): the raw bytes a gate's execution wrote to stdout
  or stderr, as captured by the (out-of-scope) executor. The **input** to the digest; carries no clock, path,
  or product vocabulary — just bytes.
- **OutputDigest** (F032, reused): the opaque, comparable, already-computed digest of stdout or stderr the
  record holds — no hashing was done by F032 itself (D3). The **derived output** of the digest operation and an
  input field of the assembled record.
- **CommandRecord** (F032, reused): an executed command's reproducible facts (executable, arguments, working
  directory, environment delta, timeout, exit code, stdout/stderr digests, captured-output outcome) held apart
  from the one sensed fact (`SensedDuration`); foldable to a byte-stable `CommandIdentity` via `canonicalId`.
  The **assembled output** of this row and the input to F049 `referenceOf` / `capture`.
- **SensedDuration** (F032, reused): the one non-deterministic, sensed wall-clock fact, carried as metadata and
  excluded from the canonical identity. Carried verbatim into the assembled record; never read by the digest or
  the identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (close the loop): For every captured execution outcome, `recordOf` produces a complete F032
  `CommandRecord` over which `CommandRecord.canonicalId` and F049 `EvidenceCapture.referenceOf` are defined,
  and `EvidenceCapture.capture` makes the gate's world reusable — 100% of captured outcomes become
  reference-able store entries.
- **SC-002** (content agreement): For every pair of captured outputs with byte-identical content, the two
  derived `OutputDigest`s are byte-identical — 0% digest variation attributable to anything but content.
- **SC-003** (content sensitivity): For every single-byte perturbation of captured output (and every
  single-field perturbation of a reproducible fact), the derived digest — and the assembled record's
  `canonicalId` and F049 reference — change — 100% of distinct contents/identities map to distinct digests/references.
- **SC-004** (duration-invariance): For every pair of outcomes identical in all reproducible facts (including
  output bytes) and differing only in sensed duration, the digests, the record's `canonicalId`, and the F049
  reference are byte-identical — 0% variation attributable to the sensed wall-clock.
- **SC-005** (determinism / byte-stability): Re-running the digest and `recordOf` on identical inputs yields
  byte-identical output across runs, processes, and machines — 100% reproducible, with no clock/GUID/path/locale/env input.
- **SC-006** (totality): The digest and `recordOf` are defined (non-throwing) for empty output bytes, a
  non-zero exit code, an applied timeout, an empty environment delta, and every captured-output outcome — 100%
  of well-typed inputs produce an ordinary value.
- **SC-007** (additive): The library adds zero third-party dependencies, bumps zero schema versions, and edits
  zero existing cores, host commands, or golden baselines; the full solution build and test suite stay green and
  every pre-existing artifact remains byte-identical.
- **SC-008** (no I/O in the core): Every semantic test of this core runs with no filesystem, clock, process, or
  network access — digesting in-memory byte buffers and asserting over the returned values only.

## Assumptions

- **Pure-execution-record-core scope is intentional and maintainer-confirmed** (this session, via
  AskUserQuestion): this row is the value-only digest + record-assembly bridge, mirroring F047/F049's pure
  halves. The impure rows that follow are the **gate-execution port** (the first process-spawning capability,
  behind an injected port — the Snapshot `GitPort` / F046 `realSensor` precedent) and the **host wiring** that
  records during a `route`/`ship` run and persists the grown store.
- **The caller supplies the captured bytes and the already-sensed reproducible facts.** This core senses
  nothing, runs no gate, spawns no process, and times nothing — it consumes raw output bytes and already-built
  F032 run facts plus an F032 `SensedDuration`. Producing those is the gate-execution port's job (out of scope).
- **The digest is a deterministic, byte-stable, content-addressed digest of the supplied bytes** — the cleanest
  reproducible one-way digest of output content. F032 deferred digesting precisely so a dedicated row would own
  it (D3); this row reuses the F032 `OutputDigest` newtype verbatim rather than inventing a new digest type.
  The exact digest algorithm is an implementation choice fixed by the plan; the spec constrains only its
  determinism, byte-stability, totality, and content-sensitivity.
- **Assembly delegates to F032 `build` verbatim.** The only new computation this core adds is the two output
  digests; everything else is F032 `build`'s carriage. No new `CommandRecord` representation, no
  re-canonicalization (that is `canonicalId`'s job), no normalization.
- **Capture is mechanical, not policy.** It assembles whatever outcome it is handed; gating on exit code,
  success, or freshness-resolution outcome is a host-row reuse-policy decision (out of scope), preserving the
  no-new-policy guarantee F049 `capture` established.
- **Standard determinism / no-clock discipline** applies as to every core in this thread.

## Out of Scope

- **Spawning a gate's process / reading its real stdout/stderr / timing the run / sensing the reproducible
  facts** — the impure **gate-execution port** row (the first process-spawning capability) that produces the
  captured outcome this core consumes. This row consumes already-captured bytes and already-sensed facts; it
  produces none.
- **Wiring into `fsgg route` / `fsgg ship`** to assemble records, capture evidence (F049), and persist the
  grown store (F047/F048) during a real run — the host-wiring row.
- **Reuse policy over captured evidence** — whether a *failed* (non-zero exit) gate's outcome should be
  recorded or suppressed, and any success/exit-code gating. This core records what it is given.
- **Any change to the F032 `CommandRecord` / `canonicalId` surface, the F049 `EvidenceCapture` surface, the
  `fsgg.evidence-reuse-store/v1` schema, the F046 reader's accepted shape, the F047 `serialise`/`retain`/`prune`
  policy, or the `route.json` / `audit.json` schema or content.**
- **Exposing the digest algorithm as a configurable policy** or supporting multiple digest schemes — the scheme
  is fixed and internal (FR-011).
- **Editing merged F029–F049 cores, their golden baselines, or anything in Phase 13.**
