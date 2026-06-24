# Feature Specification: The `fsgg release` Host Command

**Feature Branch**: `055-release-command`

**Created**: 2026-06-24

**Status**: Planned

**Input**: User description: "next item in the plan" — Phase 13 (Release And Distribution Readiness),
the release-rules row, currently 🟡 partial. F053 (`FS.GG.Governance.ReleaseRules`) landed the pure
release-gate core and F054 (`FS.GG.Governance.ReleaseFactsSensing`) landed the six-family sensing into
the exact F053 `ReleaseFacts`. The two named pending sub-items are the `fsgg release` host command wiring
(sense → evaluate → exit code) and the `release.json` projection. This feature is that host command,
**including** its deterministic `release.json` audit projection.

## Overview

F053 and F054 are both pure/edge libraries with no host: F053 evaluates *provided* release rules against
*provided* facts, and F054 senses the six release-rule families from a real repository into those exact
facts — but **nothing yet runs them end to end** from a real governed repository to an operator-facing
verdict and process exit code. This feature is the missing host: a single `fsgg release` command that, for
a governed repository, **reads the declared release configuration**, **senses** the current state of each
release-rule family, **evaluates** the declared rules against those sensed facts, **reports** the verdict
(human text and a deterministic `release.json` audit artifact), and **exits** with a code that lets CI block
a non-compliant release.

Two decisions were confirmed with the requester at specification time:

1. **Declarations come from a row-local `.fsgg/release.yml`, read via the F014 file-read port.** The release
   rules, the per-family expectations, and the on-disk source layout are declared in the governed
   repository's `.fsgg` directory and read through the established F014 `Loader.FileReader` port — the same
   low-level read precedent the existing host commands share — then parsed by this command's own
   `Declaration` adapter. The planning stage resolved this as a **row-local surface**: F014 `Config`'s frozen
   four-file schema and surface baselines are NOT edited. This row owns the new *release* declaration surface.
2. **The deterministic `release.json` audit projection ships in this row.** `fsgg release` emits both a
   human-readable summary and, when requested, a byte-deterministic `release.json` audit artifact projecting
   the verdict, the per-rule findings, and the observed evidence snapshot.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gate a release from a real repository in CI (Priority: P1)

A release engineer (or a CI pipeline) runs `fsgg release` against a governed repository that declares its
release rules and expectations. The command senses the repository's current release readiness across all six
families (version bump, package metadata, template pins, publish plan, trusted publishing, provenance),
evaluates the declared rules against what it sensed, prints a clear pass/fail verdict with the blocking and
advisory findings, and exits non-zero when a blocking rule is unmet so the pipeline halts the release.

**Why this priority**: This is the entire reason the feature exists and the only slice that delivers
end-to-end value — it is the first time a real governed repository can be checked for release readiness from
one command. Every other story refines or hardens this flow.

**Independent Test**: Point the command at a fixture repository that satisfies every declared release rule
and confirm it prints a passing verdict and exits 0; point it at a fixture whose version was not bumped and
confirm it prints a failing verdict naming the version-bump rule as the blocker and exits with the
distinct "release blocked" code.

**Acceptance Scenarios**:

1. **Given** a governed repository whose declared release rules are all satisfied by its current state,
   **When** the operator runs `fsgg release` against it, **Then** the command reports a passing verdict, lists
   each rule as satisfied, and exits 0.
2. **Given** an otherwise-compliant repository whose version was **not** bumped past the declared baseline,
   **When** the operator runs `fsgg release`, **Then** the command reports a failing verdict, names the
   version-bump rule among the blockers and the other five rules as passing, and exits with the distinct
   release-blocked code (not the tool-error code).
3. **Given** a repository in which several declared rules are unmet, **When** the operator runs `fsgg release`,
   **Then** every unmet blocking rule appears in the blockers list with its reason and the command exits
   blocked.
4. **Given** a repository whose unmet rules are all advisory under the active maturity posture,
   **When** the operator runs `fsgg release`, **Then** the command surfaces them as warnings, still reports an
   overall passing verdict, and exits 0.

---

### User Story 2 - Produce a deterministic `release.json` audit artifact (Priority: P2)

A pipeline needs a machine-readable, archivable record of the release check — the verdict, the per-rule
findings with effective severity, and the observed evidence behind each fact — to attach to the build, feed a
dashboard, or diff against a prior run. The operator requests JSON output (and/or an output path) and the
command writes a deterministic `release.json` whose bytes are identical for identical repository state.

**Why this priority**: The audit artifact is the durable, automatable output and the second confirmed
in-scope deliverable, but the command is already useful for blocking with text output alone, so it ranks
below the end-to-end gate.

**Independent Test**: Run the command twice against the same fixture repository requesting JSON and confirm
the two `release.json` outputs are byte-identical; confirm the JSON contains the verdict, every rule's
finding and effective severity, and the per-family observed evidence, and that it validates against the
committed schema/golden baseline.

**Acceptance Scenarios**:

1. **Given** any governed repository, **When** the operator requests JSON output, **Then** the command emits a
   `release.json` containing the overall verdict, every declared rule's finding (base and effective severity,
   met/unmet/unrecoverable state, reason), and the observed evidence snapshot per family.
2. **Given** the same repository state, **When** the command is run twice requesting JSON, **Then** the two
   `release.json` outputs are byte-for-byte identical (deterministic ordering, no timestamps or
   machine-specific content).
3. **Given** a requested output path, **When** the command writes `release.json`, **Then** a failed or
   interrupted write never leaves a truncated or partial artifact in place.
4. **Given** the text and JSON outputs of the same run, **When** they are compared, **Then** they report the
   same verdict and the same per-rule outcomes (the human view never contradicts the audit truth).

---

### User Story 3 - Fail safe on a missing, malformed, or unreadable repository (Priority: P3)

An operator runs `fsgg release` against a repository whose declared configuration is missing or invalid, or
whose governing source files are absent or unreadable. The command must distinguish a missing/malformed input
from a tool defect, never fabricate a passing verdict, and exit with a code that the pipeline can tell apart
from both "release blocked" and "release passed".

**Why this priority**: Correct, legible failure is essential for trust in a gate but only matters once the
happy path exists; it hardens rather than creates the core value.

**Independent Test**: Run the command against a repository with no declared release configuration and confirm
it reports an actionable "declaration unavailable/invalid" message and exits with the input-unavailable code
(distinct from blocked and from tool error); run it against a repository missing a governing source file and
confirm the affected family is reported unrecoverable (never silently passing) while the command still
completes.

**Acceptance Scenarios**:

1. **Given** a repository with no declared release configuration (or an invalid one), **When** the operator
   runs `fsgg release`, **Then** the command reports an actionable diagnostic identifying the missing/invalid
   declaration and exits with the input-unavailable code, not the blocked code and not the tool-error code.
2. **Given** a repository whose configuration is valid but one governing source file is absent or unreadable,
   **When** the operator runs `fsgg release`, **Then** the affected rule's fact is reported unrecoverable with
   a diagnostic naming the family and reason, that rule is treated as unmet (never fabricated as satisfied),
   and the command still produces a complete verdict over all six families.
3. **Given** invalid command-line arguments, **When** the operator runs `fsgg release`, **Then** the command
   prints usage guidance and exits with the usage-error code, distinct from all of the above.

---

### Edge Cases

- **No baseline declared for version comparison**: a family with no caller-declared expectation is sensed as
  unrecoverable (per F054), so its rule is reported unmet with a diagnostic, never fabricated as satisfied.
- **All six families unrecoverable** (e.g. a bare repository): the command still returns a complete verdict
  with every rule unmet/unrecoverable and exits blocked (or input-unavailable if even the declaration could
  not be read) — never a crash and never a fabricated pass.
- **Mixed blocking and advisory unmet rules**: the verdict is blocked if any *effective-blocking* rule is
  unmet; advisory-only unmet rules surface as warnings without blocking.
- **Output path is unwritable or its directory is missing**: reported as a tool/IO failure with the
  appropriate exit code; no partial `release.json` is left behind.
- **Repeated runs with no repository change**: identical text verdict and byte-identical `release.json`.
- **A declared rule references a family the catalog gives no expectation for**: that rule evaluates against an
  unrecoverable fact (unmet), consistent with the no-fabrication rule.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single `fsgg release` command that, given a governed repository,
  produces a release-readiness verdict and a corresponding process exit code.
- **FR-002**: The command MUST load the declared release configuration — the release rules, the per-family
  expectations, and the on-disk source layout — from a declaration that lives in the governed repository's
  `.fsgg` directory, read through the established F014 `Loader.FileReader` file-read port (the same low-level
  read precedent the existing host commands use) rather than reaching the filesystem directly. The
  declaration is a row-local surface (`.fsgg/release.yml`) parsed by this command; F014 `Config`'s frozen
  four-file schema and surface baselines are NOT edited (see the confirmed planning decision in plan.md).
- **FR-003**: The command MUST sense the current state of all six release-rule families from the real
  repository by reusing the F054 sensing edge (the injected repository port and `senseRelease`), producing the
  exact F053 `ReleaseFacts`.
- **FR-004**: The command MUST evaluate the declared rules against the sensed facts by reusing the F053
  release-gate core (`evaluate`/`rollup`/`evaluateRelease`) verbatim, producing one finding per rule and an
  overall release decision (verdict, blockers, warnings, passing, exit-code basis) with no re-derivation.
- **FR-005**: The command MUST map the evaluation outcome to a process exit code with distinct codes for, at
  minimum: release passed, release blocked, usage error, input unavailable (missing/invalid declaration or
  inputs), and tool error — so a pipeline can tell these states apart. The "release blocked" code MUST be
  distinct from every failure-to-run code.
- **FR-006**: The command MUST print a human-readable summary of the verdict, including the blocking findings,
  the advisory/warning findings, and the passing rules, each with enough reason to act on.
- **FR-007**: The command MUST, when JSON output is requested, emit a deterministic `release.json` audit
  artifact containing the overall verdict, every rule's finding (base severity, effective severity,
  met/unmet/unrecoverable state, and reason), and the observed evidence snapshot per family.
- **FR-008**: The `release.json` artifact MUST be byte-for-byte identical for identical repository state —
  deterministic ordering of every collection, no timestamps, paths, or machine-specific content that would
  vary between runs or machines.
- **FR-009**: The human text output and the `release.json` audit output MUST report the same verdict and the
  same per-rule outcomes for a given run; the human view MUST NOT contradict the audit truth.
- **FR-010**: The command MUST fail safe: a missing, unreadable, or unparseable governing source, or a
  declared family with no expectation, MUST result in an unrecoverable fact and an unmet rule with a
  diagnostic — never a fabricated satisfied rule and never an unhandled crash.
- **FR-011**: The command MUST distinguish a missing/malformed **input** (absent or invalid declaration,
  absent source files, bad arguments) from a **tool defect** in both its diagnostics and its exit code, per
  the project's safe-failure principle.
- **FR-012**: When writing `release.json` to a path, the command MUST never leave a truncated or partial
  artifact on a failed or interrupted write (atomic replacement).
- **FR-013**: The command MUST return a complete verdict over all six families on every successful run,
  including when some or all families are unrecoverable; it MUST NOT drop or omit a family.
- **FR-014**: The command MUST hardcode no product identity, version, metadata field, pin, posture, source
  path, or layout; all such values come from the caller-supplied declaration and repository (product-neutral,
  consistent with F053/F054).
- **FR-015**: The command MUST NOT reach any network, package registry, or publishing provider; it operates
  only on the local repository (consistent with F054's network-free guarantee).
- **FR-016**: Running the command MUST NOT mutate the governed repository other than writing the explicitly
  requested `release.json` output artifact.

### Key Entities *(include if feature involves data)*

- **Release run request**: the operator's invocation — which repository, the desired output format (text
  and/or JSON), and the `release.json` output destination.
- **Declared release configuration**: the release rules, per-family expectations, and source layout read from
  the governed repository's row-local `.fsgg/release.yml` (via the F014 `Loader.FileReader` read port); the
  typed input to sensing and evaluation.
- **Sensed release** (reused from F054): the exact F053 `ReleaseFacts` plus the observed-evidence snapshot per
  family.
- **Release decision** (reused from F053): the overall verdict, the blocking/warning/passing findings with
  base and effective severity, and the exit-code basis.
- **`release.json` audit artifact**: the deterministic projection of the release decision and the evidence
  snapshot — the durable, machine-readable record.
- **Exit code**: the process result classifying passed / blocked / usage-error / input-unavailable /
  tool-error.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a single `fsgg release` invocation against an all-compliant fixture repository, an operator
  gets a passing verdict and a 0 exit code with no further steps.
- **SC-002**: For a fixture whose version was not bumped, the command exits with the release-blocked code and
  names the version-bump rule as the blocker, distinct from any failure-to-run exit code.
- **SC-003**: Running the command twice against the same repository state yields byte-for-byte identical
  `release.json` output (100% reproducible) and an identical text verdict.
- **SC-004**: A removed or corrupted governing source for any family results in that family being reported
  unrecoverable/unmet — in 0% of such cases does the command report that family as satisfied or crash.
- **SC-005**: Across the five outcome classes (passed, blocked, usage error, input unavailable, tool error)
  the command emits five distinguishable exit codes, verifiable by a pipeline.
- **SC-006**: Every `fsgg release` run returns a verdict covering all six release-rule families — the
  per-rule outcome count equals six on every successful run, including all-unrecoverable repositories.
- **SC-007**: The `release.json` audit artifact validates against its committed schema/golden baseline and
  reports the same verdict and per-rule outcomes as the human text output for the same run.
- **SC-008**: The command performs no network access during any run (verifiable by the project's network-free
  dependency guard), consistent with F054.

## Assumptions

- **Scope boundary — host command + `release.json` projection only.** This row delivers the `fsgg release`
  command and its deterministic `release.json` audit artifact. It does **not** add new release-rule kinds,
  change the F053 evaluation semantics, or change the F054 sensing semantics; it composes them. (Confirmed
  with the requester.)
- **Declarations live in a row-local `.fsgg/release.yml`, read via the F014 file-read port.** The release
  rules, per-family expectations, and source layout are declared in the governed repository's `.fsgg`
  directory and read through the established F014 `Loader.FileReader` port (the low-level read precedent the
  existing host commands share), then parsed by this command's own `Declaration` adapter. The planning stage
  resolved the "versioned schema addition" question: this is a **row-local surface** — F014 `Config`'s frozen
  four-file schema, schema version, and surface baselines are NOT edited, keeping F014 product-neutral and
  bounding the blast radius to this row's two new projects. (Confirmed with the requester; see plan.md.)
- **Reuse over reinvention.** Sensing reuses F054 (`senseRelease`/`realPort`/the injected `RepositoryPort`)
  and evaluation reuses F053 (`evaluate`/`rollup`/`evaluateRelease`) verbatim; the verdict, severity, and
  exit-code-basis vocabulary reuses the existing F023/F024 ship-verdict types, as F053 already does.
- **Host shape follows the established command precedent.** The command follows the same pure-core +
  injected-edge shape the existing `ship`/`route` host commands use (a pure parse → wire → render → exit-code
  core, with all I/O — catalog reads, repository sensing, artifact write — behind injected ports that tests
  back with real temp-repository fixtures). Whether it is a standalone host project or a subcommand of the
  existing CLI dispatch is a planning decision.
- **Exit-code scheme follows the existing ship convention.** Passed→0, blocked→distinct non-zero,
  usage-error, input-unavailable, and tool-error each get their own code, mirroring the established
  `ExitDecision` precedent.
- **Real-fixture testing.** The command is exercised end to end against real temporary fixture repositories
  (the F016/F054 `withTempDir` precedent); no network, registry, or publishing provider is contacted in any
  test.
- **Out of scope (following rows):** publishing or signing artifacts, contacting a registry/provenance
  service, adding new release-rule families beyond the six, and any rich Spectre rendering of the release
  report beyond the human text summary.
