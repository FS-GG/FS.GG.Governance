# Phase 0 Research: Command-Record Core (F032)

All "NEEDS CLARIFICATION" from the Technical Context are resolved here. Each decision is stated as
**Decision / Rationale / Alternatives considered**, keyed to the spec's deferred planning details and the
established F029/F030/F031 precedents.

## D1 — Project home, dependency surface, change tier

**Decision.** A new packable pure-core library `src/FS.GG.Governance.CommandRecord`, namespace
`FS.GG.Governance.CommandRecord`, two compile units in order `Model` → `CommandRecord` (types, then
operations). It references **only `FS.GG.Governance.Config`**, reusing F014's `TimeoutLimit` newtype verbatim
for the *timeout* fact. New `.fsi` files + a new `surface/*.surface.txt` baseline ⇒ **Tier 1**, with **no new
third-party `PackageReference`** (BCL + `FSharp.Core` only). It references no Gates/Route/Snapshot/
FreshnessKey/EvidenceReuse and no host/edge assembly.

**Rationale.** This is the exact shape of F029 `FreshnessKey` (single `Config` reference, two-file Model→ops
core, packable, Tier-1 with no new package). The spec leaves "new pure-core module vs extend an existing core"
as the only home decision and notes "the established rhythm suggests a new minimal core" (Spec Assumptions);
F015–F031 all landed a fresh pure core per row. `Config` is the home of the F014 newtypes (FR-009); referencing
it is the same verbatim-reuse path F029 took.

**Alternatives considered.** *(a) Add the types into an existing core (Gates/Route).* Rejected — couples
command-record vocabulary to unrelated cores and would touch a merged `.fsi`/baseline (violates FR-009/SC-007).
*(b) No `Config` reference, redefine the timeout locally.* Rejected — FR-009 requires reusing F014 verbatim
where it fits, and `TimeoutLimit` fits the timeout fact exactly; redefining it would duplicate a merged
newtype. *(c) Reference Snapshot/host for sensing helpers.* Rejected — this is a pure core; sensing is the
later host edge (Principle IV, F016 precedent), and the constraint "the helper core MUST NOT depend on git/
filesystem scanning" forbids it.

## D2 — Reproducible / sensed split is structural

**Decision.** `CommandRecord = { Reproducible: ReproducibleFacts; Duration: SensedDuration }`, where
`ReproducibleFacts` carries the nine reproducible facts and `SensedDuration` is a distinct sensed type. The
duration is a separate field of a distinct, self-naming type — not a flat field among the others.
`canonicalId` takes the whole `CommandRecord` but reads **only** `record.Reproducible`.

**Rationale.** FR-004 requires the duration "marked as sensed / non-deterministic, distinguishable from the
reproducible facts," and US2 scenario 3 requires it "reachable as sensed metadata, distinguishable from the
reproducible facts — never silently folded in." A structural split makes that a property of the **type shape**:
it is impossible for the identity to include duration because identity reads a sub-record that does not contain
it, and `record.Reproducible` is itself the addressable "reproducible part" value. The `SensedDuration` name
flags the non-determinism at every read site (Principle VI honesty boundary, the contract Phase-11 row 6
applies across reports). This is strictly stronger than a flat record where identity must *remember* to skip a
field.

**Alternatives considered.** *(a) Flat 10-field record; identity skips the duration field by convention.*
Rejected — relies on the identity function's discipline rather than the type; a future edit could fold duration
in unnoticed, and "reachable as distinguishable metadata" is weaker (a plain field looks like the others).
*(b) Generic `Sensed<'T>` wrapper.* Rejected — adds a generic for a single sensed field (Principle III: plainer
F# preferred); a named `SensedDuration` newtype carries the same "this is sensed" signal with no generality
cost. *(c) Omit the duration from the record and return it alongside.* Rejected — FR-001 requires the record to
carry **all ten** facts; the duration is a declared fact, merely a sensed one.

## D3 — Opaque supplied values: digests and duration; no F029 cross-reference

**Decision.** stdout/stderr digests are a **local** opaque newtype `OutputDigest of string` (no validation, no
hashing — supplied by the edge). Duration is `SensedDuration of nanoseconds: int64`. The core does **not**
reference `FreshnessKey` to borrow a digest type.

**Rationale.** The spec states the digests are "opaque, already-computed tokens handed in" and "whether the
digest token type is reused from F029 or introduced minimally is a small planning detail" (Spec Assumptions,
FR-010). F029 set the precedent of introducing a *local* opaque token (`Revision`) rather than referencing the
assembly that would otherwise own it (`Snapshot`), precisely to keep the pure core's dependency surface
minimal (F029 research D3). Applying the same discipline, a local `OutputDigest` keeps `CommandRecord`'s only
project reference as `Config` and avoids coupling the command-record vocabulary to freshness-key vocabulary
(the two are sibling Phase-11 lines, not a dependency relationship). For duration, `int64` nanoseconds is a
pure, comparable, deterministic measure with no `float` rounding and no `DateTime`/clock dependency; "typed
span vs opaque measure" was left to planning (Spec Assumptions) and the opaque integer measure is the simplest
total choice. No wall-clock timestamp is carried — the design row lists *duration* only, and the spec says a
timestamp "is not required by this row" (if later added it is sensed metadata too).

**Alternatives considered.** *(a) Reference F029 and reuse `RuleHash`/`ArtifactHash`.* Rejected — those tokens
are freshness-specific names, and referencing `FreshnessKey` widens the dependency surface for no gain
(mirrors F029's own rejection of referencing `Snapshot`). *(b) `SensedDuration of System.TimeSpan`.* Rejected —
`TimeSpan` is a fine BCL value, but nanoseconds-as-`int64` is the minimal opaque measure and sidesteps any
`TimeSpan` formatting/precision questions; either is deterministic, the integer is plainer. *(c) `float`
seconds.* Rejected — floating-point equality is non-deterministic-feeling and risks identity instability if
duration were ever (mistakenly) compared; an integer measure is byte-stable.

## D4 — Environment delta: three-class partition, a change counted once

**Decision.**
`EnvironmentDelta = { Added: AddedVar list; Changed: ChangedVar list; Removed: RemovedVar list }` with
`AddedVar = { Name: EnvVarName; Value: EnvVarValue }`, `ChangedVar = { Name: EnvVarName; Old: EnvVarValue;
New: EnvVarValue }`, `RemovedVar = { Name: EnvVarName; Old: EnvVarValue }`. A changed variable appears exactly
once, in `Changed`, carrying both its baseline `Old` and run `New` value.

**Rationale.** FR-002 / SC-002 / Edge cases require added, changed, and removed reported as **distinct
classes**, with a changed variable counted once and "never double-counted as a removal-plus-addition." Three
typed lists make the partition the data shape itself; a consumer cannot conflate the classes. Carrying `Old` +
`New` on a change is the honest record of "what the run changed" and lets a downstream report explain the
delta; "how a changed variable records old-vs-new is a planning detail" (Spec Assumptions), and old+new is the
fullest faithful choice with no ambiguity. `Removed` carries `Old` (the baseline value that disappeared) for
symmetry and explanatory completeness. Modeling the *delta*, not a full environment snapshot, matches the
design wording ("environment delta") and avoids capturing unrelated/secret environment state (Spec
Assumptions).

**Alternatives considered.** *(a) A single `EnvVar list` tagged with an `add|change|remove` DU.* Rejected — a
flat tagged list invites a changed var being represented as two entries (an add + a remove), the exact failure
SC-002 forbids; three separate lists make that unrepresentable. *(b) `Changed` carries only the new value.*
Rejected — loses the baseline value a delta explanation wants; old+new is strictly more informative and still
total. *(c) `Removed` carries only the name.* Acceptable but asymmetric; carrying `Old` matches `Changed`'s
shape and aids explanation at no real cost. The core never reads the actual environment (FR-008) — these are
supplied facts.

## D5 — Captured-output path is a closed two-case outcome

**Decision.** `CapturedOutput = CapturedAt of CapturedOutputPath | NoCapturedOutput`, where
`CapturedOutputPath = CapturedOutputPath of string`.

**Rationale.** FR-011 / the final Edge case require an absent captured-output path modeled as an explicit,
total, *locatable* value "never an empty string that could collide with a real path," and absence "MUST
participate in the canonical identity unambiguously." A closed two-case DU makes absence a first-class case
(`NoCapturedOutput`) and a present path a wrapped string; the identity encoding gives the two cases distinct
length-prefixed segments (D6, `command-record-identity-format.md`), so `NoCapturedOutput` and `CapturedAt
(CapturedOutputPath "")` are distinguishable — exactly the F029 `None` vs `Some ""` presence-digit guarantee.

**Alternatives considered.** *(a) `CapturedOutputPath option`.* Functionally equivalent and idiomatic, but a
named DU reads more clearly at the use site and in the surface baseline (`NoCapturedOutput` states intent);
either satisfies FR-011 — the named DU is chosen for the explicit "no captured output" wire token. *(b) Plain
`string`, empty = absent.* Rejected outright — the precise collision FR-011 forbids. *(c) The whole
`CapturedOutput` excluded from identity.* Rejected — FR-005 lists the captured-output path among the
reproducible facts the identity is computed over.

## D6 — Canonical identity: F029 tagged, length-prefixed, injective string; args ordered, delta a set

**Decision.** `canonicalId : CommandRecord -> CommandIdentity` (`CommandIdentity of string`) renders
`record.Reproducible` to a byte-stable string using the F029 encoding discipline
(`contracts/command-record-identity-format.md`): each field is a `tag=<presence><byteLen>:<value>` segment,
fields joined in a fixed order. **Arguments are encoded in order** (order significant). **Each
environment-delta class is encoded as a SET** — each entry rendered to a canonical per-entry string,
deduplicated, ordinal-sorted. The duration is **not** rendered. `identityValue` unwraps to the string.

**Rationale.** FR-005/FR-006 require a byte-stable identity over exactly the reproducible facts that
distinguishes runs by those facts; FR-007 requires order-independence over the environment delta with
duplicates collapsed — "the established F029 canonical-string discipline." Reusing F029's exact
tagged/length-prefixed/injective format (so no value can masquerade as another field, and the same string in
two categories yields different identities) gives FR-006's "any reproducible difference ⇒ different identity"
for free and matches a format already proven and contracted in this repo. **Arguments differ from the delta**:
argument *order is semantically significant* (`gcc -c f.c` ≠ `gcc f.c -c` in general), so arguments are encoded
in their given order; the environment delta is a *set* of changes whose supply order is incidental, so each
class is deduped+ordinal-sorted (the F029 `artifactSet` treatment). Excluding duration from the rendering is
the structural D2 split expressed in the encoder (it reads `record.Reproducible`, which has no duration).

**Alternatives considered.** *(a) Hash the canonical string (e.g. SHA-256) into the identity.* Rejected —
FR-010 forbids computing a digest from bytes here; the canonical string *is* the byte-stable identity (F029
`Key` is likewise the string, not a hash), and a later row may hash it if it wants a fixed-width id. *(b)
Order-normalize the arguments too.* Rejected — would make two genuinely different commands share an identity
(SC-004 requires a differing reproducible fact to change the identity; argument order is such a fact). *(c)
`String` concatenation without length prefixes.* Rejected — loses injectivity (a value containing the
separator could masquerade as another field), the precise weakness F029's length-prefix design fixes.

## Cross-cutting facts (no decision required)

- **Totality.** `build` is defined for every well-typed input (failed, timed-out, argument-less, empty-delta);
  it constructs a record, never throws (FR-003). `canonicalId`/`identityValue` are total string operations.
- **Purity.** No clock, filesystem, git, environment, or network read; no process spawn; no byte hashing
  (FR-008/FR-010). All facts are supplied values; identical facts ⇒ identical record and identity (SC-005/06).
- **Naming note (avoid confusion, the F029 precedent).** The operations module is `CommandRecord` (so callers
  write `CommandRecord.build`), and the central record type is also `CommandRecord` (in module `Model`). These
  are distinct CLR entities (`...CommandRecordModule` via `[<CompilationRepresentation(ModuleSuffix)>]`, the
  repo-wide pattern, vs `...ModelModule+CommandRecord`); the `.fsi` carries an explicit note. The computed
  identity is `CommandIdentity` — deliberately **not** named `CommandRecord` — exactly as F029 named its
  fingerprint `Key`, not `FreshnessKey`.
- **No MVU.** No state, no I/O, no workflow — pure projection; Principle IV is N/A (F019/F029/F030/F031
  precedent).
