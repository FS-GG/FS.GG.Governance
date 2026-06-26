# Feature Specification: Release-Provenance End-to-End Pack Evidence and Byte-Identity Goldens

**Feature Branch**: `066-release-pack-e2e-evidence`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "work on the next backlog item"

## Context

The F26 release-provenance host wiring (`065-release-provenance-host-wiring`) landed the
`fsgg release` pack/version boundary, the `attestation.json` sidecar, `release.json` v2, and the
`fsgg verify` release-readiness preview. Its behaviour is currently proven by pure-MVU transition
tests and emitted-effect assertions over the real F26 cores, with the per-project pack execution
supplied through a **disclosed-synthetic** fake. Three follow-up tasks were explicitly deferred and
tracked as partial in `specs/065-release-provenance-host-wiring/tasks.md` (T009/T018/T023/T024) and
in the roadmap note in `docs/initial-implementation-plan.md`:

- the real-filesystem `dotnet pack` pack-boundary end-to-end fixture (T018, SC-001),
- the mergeable-but-not-releasable + fully-releasable pair with named preconditions (T023, SC-002, FR-008),
- the frozen pre-wiring byte-identity goldens and the test that asserts them (T009/T024, SC-005).

This feature closes those three deferrals. It adds **no new product behaviour, no new schema, no new
exit code, and no new public surface** — it is real test evidence and committed golden baselines that
upgrade the existing release/verify guarantees from synthetic-pack proof to real-`dotnet pack` proof,
and that pin the no-change contracts byte-for-byte against frozen baselines.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Real pack/version boundary proven against a real `dotnet pack` (Priority: P1)

A maintainer relying on `fsgg release` needs confidence that the release boundary actually packs every
declared packable project and blocks publication when a pack fails, is unbumped, or is downgraded —
not just that the wiring emits the right effects against a faked packer. The evidence must exercise a
real `dotnet pack` over a real (temporary) multi-project tree and observe the real verdict, exit code,
and recorded pack runs.

**Why this priority**: This is the core promise of the release boundary — "pack every project at a
bumped version before release gates can pass." Until it is proven end to end with a real packer, the
boundary's central guarantee rests only on a synthetic stand-in. It is the highest-value remaining
evidence and the MVP of this feature.

**Independent Test**: Run `fsgg release` over a temporary tree of several declared packable projects
through a real `dotnet pack` and assert the verdict, exit code, recorded pack runs, and written
artifacts for each of the bumped / failed / unbumped-or-downgraded / no-baseline cases.

**Acceptance Scenarios**:

1. **Given** a product with multiple declared packable projects each at a bumped version, **When**
   `fsgg release` runs and every project packs successfully, **Then** the pack/version preconditions
   are `Met`, the command exits `0`, each project is recorded as a `Pack` run, and `release.json` v2 +
   `attestation.json` are written.
2. **Given** the same product where one project's pack fails, **When** `fsgg release` runs, **Then**
   release is blocked with a reason naming the failing project, the failed pack is recorded with its
   non-zero sentinel exit (never dropped), and no fabricated pass is emitted.
3. **Given** a product where one project packs at an unbumped or downgraded version relative to its
   released-version baseline, **When** `fsgg release` runs, **Then** release is blocked with a reason
   naming the project and the offending version.
4. **Given** a declared packable project that has no released-version baseline, **When** it packs at a
   first version, **Then** it is treated as a first release and is **not** blocked as a downgrade.

---

### User Story 2 - Mergeable but not releasable, with named preconditions (Priority: P2)

A maintainer needs to see that the release boundary is genuinely distinct from the ship/merge boundary:
a change can be mergeable (`fsgg ship` succeeds) while still not being releasable (`fsgg release`
blocks), and the specific unmet publication precondition — publish plan, trusted-publishing posture,
or template pin — must be named in `release.json` v2 rather than hidden behind a bare verdict.

**Why this priority**: This proves the boundary distinction and the first-class precondition reporting
(FR-008) that justify having a separate release gate at all. It is high value but depends on the same
real-pack harness as US1, so it follows P1.

**Independent Test**: Run both `fsgg ship` and `fsgg release` over (a) a mergeable-but-not-releasable
product and (b) a fully-releasable product, and compare exit codes plus the precondition states recorded
in `release.json` v2.

**Acceptance Scenarios**:

1. **Given** a mergeable-but-not-releasable product, **When** `fsgg ship` and `fsgg release` both run,
   **Then** `fsgg ship` exits `0` while `fsgg release` exits `1` with a release exit-code basis distinct
   from the ship verdict.
2. **Given** that same product, **When** `release.json` v2 is inspected, **Then** the publish plan,
   trusted-publishing posture, and template pins each appear as named preconditions, with the failing
   one in an unmet state carrying a named reason.
3. **Given** a fully-releasable product, **When** `fsgg release` runs, **Then** it exits `0` cleanly and
   the same preconditions appear in a satisfied state in `release.json` v2.

---

### User Story 3 - Frozen byte-identity goldens for the unchanged contracts (Priority: P3)

A maintainer needs a durable guarantee that wiring the release/verify provenance changes did not perturb
the contracts that were supposed to stay identical: `route.json`, `ship.json`, a `verify.json` produced
with no release declaration, and a `release.json` whose additive v2 fields are empty. These must be
pinned to committed pre-wiring baselines and checked byte-for-byte so any future drift fails loudly.

**Why this priority**: This is a regression-safety guarantee (SC-005) rather than a new behaviour. It is
essential to close the row but lower urgency than the two behavioural-evidence stories, and it is
independent of the real-pack harness.

**Independent Test**: Commit the four frozen baselines, then run each producing command for identical
repository state and assert the output is byte-identical to its frozen baseline.

**Acceptance Scenarios**:

1. **Given** the frozen pre-wiring `route.json` and `ship.json` baselines, **When** the route and ship
   commands run over identical repository state, **Then** their output is byte-identical to the baselines.
2. **Given** the frozen no-declaration `verify.json` baseline, **When** `fsgg verify` runs on a product
   with no `.fsgg/release.yml`, **Then** `verify.json` is byte-identical to the baseline — no
   `releaseReadiness` block and no schema bump.
3. **Given** the frozen empty-additive `release.json` v2 baseline, **When** `fsgg release` runs on a
   product whose additive v2 fields are empty, **Then** `release.json` is byte-identical to the baseline.

### Edge Cases

- A real `dotnet pack` is environment-sensitive: the fixtures must remain deterministic and not leak
  machine paths, usernames, wall-clock, or pack duration into the asserted contract outputs (pack
  duration is sensed metadata only, excluded from identity).
- A zero-exit pack that produces no artifact must be distinguished from a failed pack and must block
  release with a "packed but no artifact produced" reason rather than passing.
- The byte-identity goldens must be frozen from **pre-wiring** state so they prove the wiring left those
  contracts untouched; re-deriving them from the post-wiring code would make the check vacuous.
- A test environment without a working .NET SDK / `dotnet pack` must surface a clear skip or failure
  diagnostic, never a silent green.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The release pack/version boundary MUST be proven end to end against a real `dotnet pack`
  over a real temporary multi-project tree, covering the bumped (pass), failed-pack (block),
  unbumped-or-downgraded (block), and no-baseline-first-release (not a downgrade) cases.
- **FR-002**: Each pack attempt MUST be recorded as a `Pack` run in every case, including a failed pack
  recorded with its non-zero sentinel exit; no pack run may be dropped and no fabricated pass may be
  emitted on failure.
- **FR-003**: The evidence MUST demonstrate that `fsgg ship` can exit `0` (mergeable) while `fsgg
  release` exits `1` (not releasable) for the same product, with a release exit-code basis distinct from
  the ship verdict.
- **FR-004**: The publish plan, trusted-publishing posture, and template pins MUST each surface as named
  preconditions in `release.json` v2 — in a satisfied state for a fully-releasable product and in an
  unmet state with a named reason for a not-releasable product.
- **FR-005**: Frozen pre-wiring byte-identity baselines MUST be committed for `route.json`, `ship.json`,
  a no-declaration `verify.json`, and an empty-additive-field `release.json` v2, and a test MUST assert
  each producing command is byte-identical to its baseline for identical repository state.
- **FR-006**: All new real-pack fixtures and golden checks MUST be deterministic — no path, username,
  wall-clock, environment, or pack-duration leakage into any asserted contract output.
- **FR-007**: This feature MUST add no new product behaviour, schema, exit code, verdict, or public
  surface; it only adds test evidence and committed golden baselines. The `065` deferred tasks
  (T009/T018/T023/T024) MUST be marked complete and the roadmap note updated once the evidence lands.
- **FR-008**: A test environment lacking a working `dotnet pack` MUST produce a clear, disclosed
  skip-or-fail diagnostic rather than a silent pass.

### Key Entities *(include if feature involves data)*

- **Real pack-boundary fixture**: a temporary, self-contained multi-project tree with a declared
  `.fsgg/release.yml` (packable projects + version baselines) exercised through a real `dotnet pack`.
- **Mergeable-vs-releasable fixture pair**: two product states — one mergeable-but-not-releasable, one
  fully-releasable — used to contrast `fsgg ship` and `fsgg release` outcomes and precondition states.
- **Frozen contract goldens**: committed pre-wiring baselines of `route.json`, `ship.json`, a
  no-declaration `verify.json`, and an empty-v2 `release.json`, plus the byte-identity comparison test.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The real-`dotnet pack` pack-boundary fixture passes for all four cases (bumped ⇒ exit 0
  with packs recorded; failed pack ⇒ blocked, failed run recorded; unbumped/downgraded ⇒ blocked naming
  project + version; no-baseline ⇒ first release, not a downgrade).
- **SC-002**: The mergeable-vs-releasable pair demonstrates `fsgg ship` exit 0 with `fsgg release` exit 1
  (distinct basis) for the not-releasable product and `fsgg release` exit 0 for the fully-releasable
  product, with publish-plan / trusted-publishing / template-pin preconditions named in `release.json` v2
  in the correct satisfied/unmet states.
- **SC-003**: Re-running the real-pack fixture over unchanged inputs yields byte-identical `release.json`
  v2 and `attestation.json` (pack duration excluded from identity).
- **SC-004**: The four frozen byte-identity goldens (`route.json`, `ship.json`, no-declaration
  `verify.json`, empty-v2 `release.json`) match their producing commands byte-for-byte for identical
  repository state.
- **SC-005**: The full solution build + test sweep is green, the `065` deferred tasks T009/T018/T023/T024
  are marked complete, and the roadmap note records the F26 release host evidence as closed.

## Assumptions

- The real-`dotnet pack` fixtures run in an environment with a working .NET SDK (`net10.0`, the repo
  standard); where one is unavailable the fixture surfaces a disclosed skip/fail per FR-008.
- The pack artifacts continue to be written to the constitution's local NuGet location
  (`~/.local/share/nuget-local/`) through the existing F51 execution port; no new write path is added.
- The frozen byte-identity baselines are captured from pre-wiring state (the `065` plan's SC-005 anchors)
  and stored under the relevant host `Tests` fixtures, as already scoped by `065` tasks T009/T024.
- No new external/NuGet dependency is introduced; this is test evidence and goldens over the existing
  F26 cores and the two already-wired hosts (`ReleaseCommand`, `VerifyCommand`).
- Any synthetic input used to provoke a failure case carries `Synthetic` in the test name with a
  use-site disclosure, per Constitution V; the pack execution itself is real.
