# Feature Specification: CLI production-correctness edges

**Feature Branch**: `108-cli-correctness-edges`

**Created**: 2026-07-03

**Status**: Draft

**Input**: Governance issue #55 (Epic #44) — review findings M-CLI-2 (Medium) + F10/F12/F14 (Low). Close four production-correctness edges in the `fsgg-governance` CLI: fixture backdoors in the shipped binary, an undetectable review-cache key collision, a dead artifact-`present` check, and misleading dispatcher help / argument-error classification.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A colliding review key never returns the wrong cached verdict (Priority: P1)

An adopter runs the agent-reviewed gate flow across many review keys. Two distinct keys must never resolve to the same cached review file, and a cache read must never hand back a verdict that was stored under a *different* key.

**Why this priority**: This is a silent correctness failure. `ReviewStore.safeFileName` maps every character outside `[A-Za-z0-9_-]` to `_`, so distinct keys (e.g. `rule:a/b` vs `rule:a b`) collide onto one `*.txt` file. The stored file records only rule + verdict, never the key, and `loadReview` sets `Key = key` from the *requested* key — so a collision returns a stale, wrong verdict undetectably. A governance tool returning the wrong cached judgement is the worst failure mode it has.

**Independent Test**: Store a review under key K1, then load under a distinct key K2 that currently sanitizes to the same filename; assert the load does **not** return K1's verdict (either a miss or a detected mismatch), and that both keys can coexist with their own verdicts.

**Acceptance Scenarios**:

1. **Given** two distinct keys that sanitize identically today, **When** each stores a different verdict, **Then** each key loads back its own verdict (no collision).
2. **Given** a stored file whose recorded key does not match the requested key, **When** `loadReview` reads it, **Then** it does not return that review as a hit (treated as a miss / mismatch, not a silent wrong-verdict).
3. **Given** an existing valid store entry, **When** it is loaded under its own key, **Then** the verdict round-trips unchanged (no regression for the common path).

### User Story 2 - The shipped binary has no test-fixture backdoor (Priority: P1)

A real adopter whose repository path happens to contain the substring `review-store-unavailable` or `review-dispatch-failed` must get normal behavior — not a hard-coded failure baked into the released binary.

**Why this priority**: `ReviewStore.fs:41,63` and `Program.fs` short-circuit on `snapshot.Root.Contains("review-store-unavailable" / "review-dispatch-failed")`. Any production repo whose path matches fails for no legitimate reason — a backdoor shipped to users purely so end-to-end tests can inject failure via magic paths. Test concerns must not leak into production code paths.

**Independent Test**: Point the CLI at a real repo whose path contains `review-store-unavailable`; assert the review store loads/saves normally. Separately, assert the failure behaviors are still exercised in tests through a legitimate seam (a substituted failing store / a genuinely broken store root), not a path substring.

**Acceptance Scenarios**:

1. **Given** a repo whose root path contains `review-store-unavailable`, **When** a review is loaded/saved, **Then** it behaves as any other repo (no fixture failure).
2. **Given** a repo whose root path contains `review-dispatch-failed`, **When** a fresh dispatch is requested, **Then** the reason is the normal "not configured" reason, not the fixture reason.
3. **Given** the test suite, **When** store-unavailable and dispatch-failed behaviors are asserted, **Then** they are injected through the ports/store seam (or a real unwritable store), and the two fixture *directories* keyed on magic paths are removed.

### User Story 3 - Dispatcher help and argument errors are accurate (Priority: P2)

A user who mistypes a command or passes a stray argument gets help that lists all real subcommands and an error that names the actual problem.

**Why this priority**: Observability/UX, low severity. `CliRender.fs:62` `MissingCommand` help omits `watch` and `tui`; a stray *positional* argument is reported as `UnknownOption` ("unknown option"), which is wrong — it is an unexpected argument, not an option.

**Independent Test**: Invoke with no command and assert the help lists `watch` and `tui`; invoke with a stray non-`--` token and assert the error identifies it as an unexpected argument, not an unknown option.

**Acceptance Scenarios**:

1. **Given** no subcommand, **When** the dispatcher renders `MissingCommand`, **Then** the message enumerates every dispatchable subcommand including `watch` and `tui`.
2. **Given** a stray positional token (not starting with `--`), **When** parsing fails, **Then** the error distinguishes "unexpected argument" from "unknown option".

### User Story 4 - The dead artifact-`present` check is removed (Priority: P3)

**Why this priority**: Dead code, lowest severity. `ArtifactReading.fs:257-259` matches `present` but both arms are no-ops (`-> ()`), so a `{"present": false}` design artifact still yields `ArtifactPresent`. The EvidenceCommand copy already dropped this (per #49 dedup); the Cli original is the last copy.

**Independent Test**: The `present` match no longer exists in `ArtifactReading.fs`; design-fact extraction output is unchanged for real artifacts (the dead branch never altered output).

**Acceptance Scenarios**:

1. **Given** a design artifact JSON, **When** design facts are extracted, **Then** the output is identical to before removing the dead branch (byte-identical), and the `present` no-op is gone.

### Edge Cases

- **Cache format change**: any filename-scheme change makes pre-existing cached `*.txt` entries un-found (cache miss → recompute). Acceptable for a cache; must not error. Old entries are simply ignored, never mis-read as another key's verdict.
- **Key with only illegal characters**: must still produce a stable, unique, filesystem-safe name (no empty filename, no path traversal).
- **Fixture removal**: removing the two fixture directories must not orphan other tests that reused them for unrelated reasons (verify no other suite depends on those exact paths).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ReviewStore` MUST guarantee that two distinct review keys never resolve to the same stored file (e.g. by appending a stable hash of the full key to the sanitized name).
- **FR-002**: `ReviewStore.loadReview` MUST verify that a stored entry belongs to the requested key and MUST NOT return a review whose recorded key differs from the requested key (persist the key in the file and check it on read).
- **FR-003**: The common store round-trip (store then load under the same key) MUST return the same verdict — no regression for valid entries.
- **FR-004**: The shipped binary MUST NOT branch on `snapshot.Root` containing `review-store-unavailable` or `review-dispatch-failed`; those substring checks MUST be deleted from production code.
- **FR-005**: The store-unavailable and dispatch-failed failure behaviors MUST remain testable through a legitimate seam (substituted failing store / real unwritable store root), not a repository-path substring.
- **FR-006**: The two path-keyed fixture directories (`review-store-unavailable`, `review-dispatch-failed`) MUST be removed once their tests are migrated to the seam.
- **FR-007**: The dispatcher `MissingCommand` help MUST enumerate all dispatchable subcommands, including `watch` and `tui`.
- **FR-008**: A stray positional argument MUST be reported as an unexpected argument, distinct from an unknown `--option`.
- **FR-009**: The dead `present` match in `Cli/ArtifactReading.fs` MUST be removed with no change to extracted design-fact output.
- **FR-010**: Each behavior change MUST have a test that fails before and passes after (RED→GREEN); output-preserving changes (FR-009) MUST have an unchanged-output assertion.

### Key Entities

- **RecordedReview**: rule + key + verdict, persisted per key. Gains an on-disk key record (FR-002).
- **Review store file**: the `*.txt` cache entry. Its name gains a key hash (FR-001); its contents gain the key (FR-002).
- **CliPorts / ReviewStore seam**: the existing injection point (`Cli.fsi:137`) through which tests substitute failing store behavior (FR-005).
- **ParseError**: gains (or reuses) a case distinguishing an unexpected positional argument from an unknown option (FR-008).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 0 substring backdoors remain in shipped `src/` code (grep for the two fixture tokens in production paths returns nothing).
- **SC-002**: 100% of distinct review keys map to distinct store files; a mismatched-key read is never returned as a hit (proven by RED→GREEN tests).
- **SC-003**: A user who runs `fsgg-governance` with no subcommand sees `watch` and `tui` in the help; a stray argument is named as such.
- **SC-004**: The full test suite, surface-drift, and API-compat gates are green; any `.fsi`/baseline change (if a new `ParseError` case or seam field is added) is intentional and baselined.

## Assumptions

- **Change classification: mixed.** FR-009 and the help/error-message fixes are Tier 2 (no surface change). FR-001/002 change the on-disk store format but not necessarily the `.fsi`; FR-005/008 may add a `ParseError` case or a seam field — those are **Tier 1** and update the surface baseline in lockstep.
- **The review store is a cache**, so a one-time format change that invalidates old entries is acceptable (recompute, never mis-read).
- **The existing `CliPorts` seam is the intended injection point** for store/dispatch failure; the fix follows the issue's prescription rather than inventing a new abstraction.
- **The two fixture directories exist only to trigger the backdoor**; once their assertions move to the seam they can be deleted (verified against other suites first).
