# Phase 0 Research: Freshness Key Computation Core

All Technical-Context unknowns are resolved here. There were no open `NEEDS CLARIFICATION` markers in the
spec; the three decisions the spec deferred to planning (module home/tier, output-digest treatment,
base/head representation) are settled below as D1/D4/D3, plus two derived decisions (D2 encoding, D5 cost).

## D1 — Home, shape, and dependency: a new `Config`-only pure core (Tier 1)

**Decision.** Add a new packable library `src/FS.GG.Governance.FreshnessKey` with two compile units —
`Model.fsi/fs` (types) and `FreshnessKey.fsi/fs` (the pure operations) — referencing **only**
`FS.GG.Governance.Config`. No new third-party `PackageReference`. Tier 1 (new public surface + new
`surface/*.surface.txt` baseline).

**Rationale.** This is the exact shape every Phase-2/5 pure core used (F018 `Gates`: `Model` + `Gates`;
F019 `Route`: `Model` + `Route`; F023 `Enforcement`). The freshness inputs are expressed entirely in the
F014 typed-fact newtypes (`CheckId`, `DomainId`, `CommandId`, `EnvironmentClass`) — which is precisely the
vocabulary the F018 gate `FreshnessKey` is *built from* — so referencing `Config` alone lets the inputs
speak the gate's language **without** coupling to the Gates assembly. The Constitution's Engineering
Constraint is explicit: "the rule/evidence helper core ... MUST NOT depend on FAKE, git, filesystem
scanning, …". A `Config`-only graph is the minimal honest dependency.

**Alternatives considered.**
- *Reference Gates and embed `Gates.Model.FreshnessKey` directly.* Rejected: drags the Gates assembly in for
  no semantic gain (we need the component newtypes, not the record wrapper) and would also drag `Cost` into
  the inputs (see D5). FR-009's "consume the carried vocabulary verbatim" is satisfied by reusing the F014
  newtypes the carried key is made of.
- *Extend the kernel `Freshness` module (currency over instants).* Rejected: that module answers a
  different question (has a covered artifact changed *since* the evidence was recorded — a temporal compare
  of instants). This feature answers an *identity* question (are two runs even asking the same thing). They
  are companions, not the same module; conflating them would overload one name with two semantics.
- *Tier 2 (no public surface).* Rejected: unlike F028's in-test generator, this core is consumed by a later
  cache row and a host edge, so it is genuine public API — Tier 1 with a curated `.fsi` and a baseline.

## D2 — The key is a canonical tagged, length-prefixed string (no cryptographic digest)

**Decision.** `compute` renders `FreshnessInputs` to a deterministic canonical **string** and wraps it as
`Key of string`. Each field is encoded as a tagged, length-prefixed segment and the segments are joined in a
fixed order with `\n`. No SHA/MD5/hash function is applied. See `contracts/freshness-key-format.md` for the
byte-level rules.

**Rationale.**
- **Determinism + injectivity for free.** Length-prefixing each value (`tag=<len>:<value>`) makes the
  encoding injective: no value can contain a delimiter that lets it masquerade as another field or span a
  category boundary (FR-006). Fixed field order + `\n` joins give byte-stability (FR-003).
- **Inspectability (FR-007).** A structured canonical string is parseable back to its fields; a digest is
  opaque and would force a separate "accompanying record" just to explain a non-match. The plainer design
  keeps the explanation in `diff` over the `FreshnessInputs` themselves and the key human-diffable.
- **No dependency, trivial cost.** BCL string building only (FR-013); the input set is a handful of short
  strings, so a fixed-length digest buys nothing.

**Alternatives considered.**
- *SHA-256 of the canonical string.* A reasonable future optimization if keys ever need to be fixed-length
  cache filenames, but it adds opacity now and is a pure function of the canonical string anyway — it can be
  layered later without changing this contract. Out of scope.
- *Structural equality of `FreshnessInputs` with no string key.* `matches` could be plain `=`, but a
  committed/comparable **key value** is what the later cache row stores and looks up across runs and
  machines; the canonical string is that portable artifact. We provide both: `matches` is defined as key
  equality so the two can never disagree.

## D3 — Base/head are a local `Revision` newtype, not `Snapshot.Model.CommitId`

**Decision.** Define `type Revision = Revision of string` in this core's `Model`. The base and head inputs
are `Revision`. The later edge that assembles `FreshnessInputs` from a `RepoSnapshot` maps each
`Snapshot.Model.CommitId` → `Revision`.

**Rationale.** `CommitId` lives in the `Snapshot` assembly, whose purpose is git sensing (its `Interpreter`
shells out to git). Even though `CommitId` itself is a pure string newtype, referencing the assembly couples
this pure key core to the git-sensing project — exactly what the Constitution's core-minimalism constraint
forbids. A local opaque `Revision` keeps the dependency graph at `Config` only and keeps base/head
product-neutral (any revision identity, not strictly a git commit). F016 already foresaw this hand-off: its
`CommandRunDigest` doc says "A later phase (Phase 11 freshness keys) consumes these" — consumption happens
at the edge that builds the inputs, not inside this pure core.

**Alternatives considered.**
- *Reuse `CommitId`.* Rejected for the coupling above; the one-line edge mapping is a small, honest price for
  a minimal pure core. (Recorded so a future reader doesn't "DRY-refactor" the two into one and re-introduce
  the dependency.)

## D4 — "Output digest" is out of scope: a verification companion, not a lookup input

**Decision.** The freshness **key** is computed over **inputs** only. The "output digest" named in the
Phase-11 plan line is **not** part of this key and is out of scope for F029.

**Rationale.** Cause vs effect. The freshness key answers "may I reuse?" — a question of the **inputs** that
determine whether a prior run's world matches this run's. An output digest is produced **by running** the
gate; it is what a later cache-write row records *alongside* reused evidence to verify integrity ("the
cached output still hashes to what we stored"). Folding an output into the input-identity key would be
circular (you'd need to run the gate to compute the key that decides whether you may skip running the gate).
Phase 7's generation-manifest shape (source, generated view, generator version, source digest, **output
digest**, currency gate) confirms the output digest's home is the manifest/verify side, not the reuse key.

**Alternatives considered.**
- *Include output digest in the key.* Rejected as circular (above). The later cache row owns it.

## D5 — Cost is not a freshness input

**Decision.** `FreshnessInputs` carries Check / Domain / Command / Environment from the gate identity and
**omits `Cost`**, even though the F018 carried `FreshnessKey` record includes it.

**Rationale.** Reuse validity depends on whether the *world the evidence describes* matches — the rule, the
covered artifacts, the command and its version, the generator version, the revision range, the environment.
A gate's **cost classification** (`Cheap`/`Medium`/…) is a scheduling/budgeting attribute; reclassifying a
gate's cost without changing what it checks does not make last run's evidence stale. Including cost would
spuriously invalidate caches on a pure catalog-tuning edit. The spec's enumerated input categories
(FR-001) list "environment class" and "the carried gate identity (check/domain/command)" — not cost — so
omitting cost matches the contract. (A catalog edit that *does* change what a gate checks will change the
rule hash or command, which the key already captures.)

**Alternatives considered.**
- *Include cost for "carry verbatim" fidelity.* Rejected: "consume verbatim" (FR-009) is about not
  re-deriving/re-validating the typed values, not about copying every field into a different concept; cost is
  semantically irrelevant to reuse and would harm cache hit-rate.

## Derived facts (no decision needed)

- **Input categories (closed).** The comparable categories, each a `diff` result case: rule hash, covered
  artifact set, command version, generator version, base revision, head revision, environment class, and the
  three gate-identity fields (check, domain, command). The covered-artifact category compares as a **set**
  (dedup + ordinal sort) so order/duplication never register as a difference (FR-004).
- **Option semantics.** `Command : CommandId option` and `CommandVersion : CommandVersion option` are
  `None` exactly when the gate declares no command. The encoding distinguishes `None` from any `Some`
  (D2 / format contract), so "no command version" never collides with "some command version" (Edge case,
  FR-011). `None`-vs-`None` matches; `None`-vs-`Some` does not.
- **New newtypes.** `RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision` — all
  single-case `of string`, opaque and comparable, carrying no raw bytes/paths/clock. The actual digests are
  computed at the edge (interpreter/snapshot) and supplied as data (FR-008); this core never opens a file or
  runs git.
- **Testing stack.** Expecto + FsCheck (determinism/injectivity/totality as properties; distinction and
  inspection as example tests), VSTest adapters for `dotnet test` — all already on the central feed; the
  surface-drift test uses reflection in the test assembly only, with the `BLESS_SURFACE=1` re-bless path
  (AuditJson/Gates precedent).
- **Determinism evidence (SC-006).** Purity is structural (no clock/fs/git/env/network calls); the
  PurityTests recompute a fixed input's key after changing the working directory and unrelated filesystem
  state and assert byte-equality, demonstrating no ambient influence.
