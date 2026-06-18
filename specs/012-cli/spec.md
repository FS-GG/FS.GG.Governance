# Feature Specification: The CLI Tool - Route, Explain, Contract, and Evidence Reports for a Repo Snapshot

**Feature Branch**: `012-cli`

**Created**: 2026-06-18

**Status**: Draft

**Input**: User description: "start the next item in docs/2026-06-18-governance-kernel-speckit-implementation-plan.md" (resolved to F12 - `012-cli` from the dated Spec Kit implementation plan)

**Change Classification**: **Tier 1** - introduces the first end-user command surface for the governance product: an optional command-line tool that lets a person or CI run `route`, `explain`, `contract`, and `evidence` against a repository snapshot and receive text or JSON output. It wires the already-shipped F08 effects edge to the F09 composition root and the F10/F11 adapters, adds a new public command/runner surface, publishes a new packable tool artifact, and records a new surface-area baseline. This is an I/O-bearing feature, so Constitution Principle IV applies: command orchestration, output selection, and exit-code decisions must be modeled through an Elmish/MVU-style boundary, with real filesystem/process effects interpreted only at the edge.

## User Scenarios & Testing *(mandatory)*

The users of this feature are maintainers and CI jobs who need a small, optional way to ask the governance system four practical questions about a snapshot: **what proof does this change need?** (`route`), **why did the system reach that conclusion?** (`explain`), **what does the current rule catalog claim to enforce?** (`contract`), and **what evidence state is the run resting on?** (`evidence`). The CLI is the product's human and automation boundary; it is not a new kernel, not a new adapter, and not a replacement for Spec Kit artifacts.

The key promise is that the command line exposes the already-built system without making it heavy by default. A local user can run in advisory mode and get a readable answer without blocking their work. CI can run the same surface in gate mode and receive a nonzero exit only when a blocking gate actually fails. Both humans and scripts can switch between stable JSON and compact text. Every output must be explainable: a route names the fence and rule, an explanation names the proof tree, a contract is folded from the same checks that are evaluated, and an evidence report distinguishes real, synthetic, stale, pending, skipped, failed, and auto-synthetic inputs.

This feature also revisits decision #5 from the open questions. The CLI owns the cost/latency posture at the user boundary: fresh agent review dispatch is never silently unbounded. By default, a run is cache-only for agent reviews; the caller must explicitly grant a review budget before the tool spends fresh judge calls. When the budget is exhausted, the result stays pending or uncertain and the report says so. Full external-customer validation and issue/task conversion remain F13; F12 supplies the tool surface that F13 will exercise.

### User Story 1 - A maintainer runs `route` and gets a short, explainable routing decision (Priority: P1)

A maintainer asks for the route of the current repository snapshot or a named change scope. The tool reports whether the change is light, advisory, or blocked at a gate. The text form is short enough to read in a terminal; the JSON form is stable enough for automation. A light change explicitly says it has no gates. A fenced gate names the matched fence, the rule, severity, run mode, and rendered check so the maintainer can understand the cost before doing more work.

**Why this priority**: Routing is the first practical question the product exists to answer. Without a clear `route` command, all the kernel, adapter, and host work remains library-only and CI cannot consume the light-by-default contract.

**Independent Test**: Run `route` against fixture snapshots and this repository's own `.specify` tree in `Inner` and `Gate` modes; verify the text names the light/advisory/blocking state and the JSON carries the same route facts deterministically.

**Acceptance Scenarios**:

1. **Given** a repo snapshot with no changed fenced surface, **When** the user runs `route`, **Then** the output explicitly reports a light/no-gates route and exits successfully.
2. **Given** a repo snapshot that matches a fenced surface in `Inner` mode, **When** the user runs `route`, **Then** the output lists advisory and would-block items without stopping the run.
3. **Given** a repo snapshot that matches a fenced surface in `Gate` mode with a failing blocking rule, **When** the user runs `route`, **Then** the output names the fence, rule, severity, and rendered check, and the exit decision is blocking.

---

### User Story 2 - CI runs the same command surface and receives deterministic exit decisions (Priority: P1)

A CI job runs governance in `Gate` mode and treats the process result as an automation contract. Advisory findings exit successfully and can be uploaded as reports. A failing blocking gate exits nonzero. Bad CLI usage and tool defects are distinguishable from governed findings so CI can tell "the command failed to run" apart from "the governed change failed a gate."

**Why this priority**: F12 is the bridge from a library to an operational tool. CI needs an unambiguous process contract before the tool can be used at a merge boundary.

**Independent Test**: Drive the command runner over fixtures covering advisory-only, blocking-failure, malformed invocation, and input-unavailable cases; assert the exit decision and output category for each.

**Acceptance Scenarios**:

1. **Given** advisory findings in `Gate` mode, **When** CI runs `route --mode gate`, **Then** the command exits successfully and records the advisory findings in output.
2. **Given** a failing blocking gate in `Gate` mode, **When** CI runs `route --mode gate`, **Then** the command exits nonzero and identifies the blocking rule.
3. **Given** malformed command input, **When** CI runs any command, **Then** the command exits with a usage/tool error distinct from a governed blocking result.

---

### User Story 3 - A maintainer asks `explain` and `contract` to audit why and what the tool enforces (Priority: P1)

A maintainer can ask for an explanation of the current run and for the published contract of the active rule catalog. The explanation reflects the actual evaluated checks and evidence. The contract is folded from the same reified rules that evaluation used, so it cannot drift into a hand-maintained promise. Both commands support text for people and JSON for machines.

**Why this priority**: The design treats opacity as a defect. A CLI that only returns pass/fail would repeat the prior failure mode; `explain` and `contract` make the route auditable by humans and scripts.

**Independent Test**: Run `explain` and `contract` against fixtures, parse their JSON forms, and confirm repeated runs over the same snapshot produce byte-for-byte identical JSON and the rendered contract statements match the checks used by the run.

**Acceptance Scenarios**:

1. **Given** a governed snapshot, **When** the user runs `explain`, **Then** the output includes the evaluated proof/explanation for the active rules and the top-level verdicts match the route decision.
2. **Given** the active composed catalog, **When** the user runs `contract`, **Then** every listed statement is derived from the same rule checks the evaluator uses.
3. **Given** `--json`, **When** the user runs `explain` or `contract` twice against the same snapshot, **Then** the JSON is stable and parseable.

---

### User Story 4 - A maintainer asks `evidence` to see taint, freshness, cache, and safe-failure state (Priority: P1)

A maintainer asks for the evidence report behind a route. The report shows declared evidence state, computed synthetic taint, freshness, recorded review cache hits and misses, pending reviews, disclosures, and safe failures. Synthetic or stale evidence is never presented as real or fresh; missing inputs are reported as missing input rather than a tool defect.

**Why this priority**: Governance is only useful if users can see what its conclusions rest on. Evidence reporting is the difference between an explainable route and an opaque oracle.

**Independent Test**: Run `evidence` against fixtures with real evidence, synthetic roots, auto-synthetic downstream nodes, stale records, cache hits, cache misses, and failed reads; verify every state appears in text and JSON without collapsing distinct states.

**Acceptance Scenarios**:

1. **Given** a graph with a synthetic root and real downstream nodes, **When** the user runs `evidence`, **Then** the report shows the root as synthetic and the affected downstream nodes as auto-synthetic.
2. **Given** a recorded review that matches the current cache key, **When** the user runs `evidence`, **Then** the report shows a cache hit and no fresh review dispatch is required.
3. **Given** a missing artifact or unavailable review store, **When** the user runs `evidence`, **Then** the report shows a safe failure tied to that input and does not silently pass the affected conclusion.

---

### User Story 5 - A user controls run mode and fresh-review budget explicitly (Priority: P2)

A user chooses `Sandbox`, `Inner`, or `Gate` mode and optionally grants a fresh-review budget. `Sandbox` is loud and local-only. `Inner` reports but does not block. `Gate` enforces blocking failures. Fresh agent review dispatch is cache-only by default; if the user grants a budget, the command may dispatch at most that many fresh review requests. If the budget is exhausted, the affected conclusion stays pending or uncertain and the report states the budget decision.

**Why this priority**: This story locks the practical part of decision #5 without making the tool expensive by surprise. The CLI is where a human or CI job consents to cost and latency.

**Independent Test**: Run commands with cache-only budget, limited nonzero budget, exhausted budget, and `Sandbox`/`Inner`/`Gate` modes; assert no run dispatches more fresh reviews than allowed and that mode semantics match routing rules.

**Acceptance Scenarios**:

1. **Given** a rule that needs an agent review and no cached verdict, **When** the user runs with the default cache-only budget, **Then** no fresh review is dispatched and the result remains pending or uncertain with an explicit budget message.
2. **Given** the same rule and a budget of one fresh review, **When** the user runs the command, **Then** at most one fresh review is dispatched and the budget use is reported.
3. **Given** `Sandbox`, `Inner`, and `Gate` runs over the same fenced change, **When** the route is evaluated, **Then** `Sandbox` is loud and non-enforcing, `Inner` is advisory, and only `Gate` can produce a blocking exit.

---

### User Story 6 - The optional tool is packaged and dogfooded without becoming a project dependency (Priority: P2)

The repository publishes the CLI as an optional tool artifact and dogfoods it against its own `.specify` tree plus existing fixtures. Installing or removing the tool does not change how this repository or any consumer project builds, tests, documents, packages, or releases. The tool inspects snapshots read-only and produces reports; it does not rewrite Spec Kit artifacts, design-system fixtures, source files, or generated issues.

**Why this priority**: The product needs to be usable outside a test process, but the operating rule is still one-way: governance may inspect a project; a project must not require governance.

**Independent Test**: Pack the tool to the local package feed, install/run it from that artifact against fixtures and this repository, then verify the inspected tree has no content changes from the read-only commands.

**Acceptance Scenarios**:

1. **Given** the packaged tool, **When** it is installed from the local feed and run against the fixture tree, **Then** each command executes successfully from the packaged artifact.
2. **Given** this repository's `.specify` tree, **When** the packaged tool runs `route`, `explain`, `contract`, and `evidence`, **Then** it produces reports without modifying repository files.
3. **Given** a consumer repository that does not install the tool, **When** it builds and tests normally, **Then** no governance dependency is required.

---

### Edge Cases

- **No governed inputs**: a snapshot with no applicable adapters, no matching fences, or no changed governed artifacts reports a light/no-gates route and exits successfully.
- **Fenced but advisory in the inner loop**: a fenced change in `Inner` mode reports would-block information without returning a blocking exit.
- **Gate with unresolved agent review**: a blocking rule that needs an unavailable or over-budget review remains pending/uncertain; the gate output explains the pending decision rather than silently passing.
- **Cache hit suppresses dispatch**: a recorded verdict for the current cache key prevents a fresh review dispatch even when a nonzero budget is available.
- **Budget exhausted**: a run never dispatches more fresh reviews than the granted budget; remaining reviews are reported as pending/uncertain with a budget-exhausted reason.
- **Sandbox mode**: the output clearly marks the run as local/non-enforcing and never presents it as a merge-ready gate result.
- **Malformed invocation**: unknown command names, invalid mode values, and invalid roots are usage/tool errors, not governed blocking findings.
- **Missing or unreadable artifact**: the tool reports absent/bad input as a safe failure in the evidence and explanation output; it does not crash or silently pass.
- **Stale or synthetic evidence**: stale, synthetic, and auto-synthetic evidence remain distinct in both text and JSON output.
- **Stable JSON**: repeated JSON output over an unchanged snapshot and unchanged explicit inputs is byte-for-byte stable.
- **Read-only guarantee**: route/explain/contract/evidence commands do not rewrite Spec Kit artifacts, adapter fixtures, source files, or consumer repository content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide the user-facing command surface `route`, `explain`, `contract`, and `evidence`, each runnable against a repository snapshot and each supporting text output and a stable JSON output option.
- **FR-002**: The feature MUST translate a user invocation into a run request containing at minimum: repository root, command kind, run mode (`Sandbox`/`Inner`/`Gate`), output format, change scope, review budget, and selected domain inputs needed to compose the available adapters.
- **FR-003**: The command runner MUST wire commands to the already-shipped F08 host loop and interpreter and the composed F09/F10/F11 adapter catalog; it MUST NOT introduce a second evaluator, router, evidence engine, contract renderer, or explanation engine.
- **FR-004**: The feature MUST model CLI orchestration through an Elmish/MVU-style boundary: durable command model, accepted messages, command effects, initialization, a pure update/decision step, and an edge interpreter that executes filesystem/process/output effects.
- **FR-005**: The `route` command MUST report the computed route, including run mode, stakes, matched fences, blocking entries, advisory entries, rule ids, severities, and rendered checks; a light/no-gates route MUST be explicit, not silent.
- **FR-006**: The `explain` command MUST report the evaluated explanation/proof information for the active rules in the current run, and the reported top-level verdicts MUST agree with the route decision for the same snapshot and mode.
- **FR-007**: The `contract` command MUST report the published contract folded from the active composed rule catalog, with each statement derived from the same reified checks used by evaluation so advertised and enforced behavior cannot drift.
- **FR-008**: The `evidence` command MUST report declared evidence states, computed effective states including `AutoSynthetic`, freshness, recorded review cache hits/misses, pending reviews, disclosures, and safe failures without collapsing distinct states.
- **FR-009**: Text output MUST be readable for a human terminal and JSON output MUST be deterministic for automation: fixed field names, deterministic ordering, parseable values, and no implicit wall-clock fields unless supplied as explicit input to the run.
- **FR-010**: Exit decisions MUST be deterministic and documented: advisory-only findings exit successfully; failing blocking findings in `Gate` mode exit nonzero; malformed invocation/tool errors are distinguishable from governed blocking results.
- **FR-011**: Run modes MUST preserve the F07 contract: `Sandbox` is loud and non-enforcing, `Inner` reports without blocking, and `Gate` enforces blocking failures from the gate snapshot independently of any local mode.
- **FR-012**: Fresh agent review dispatch MUST be budgeted at the CLI boundary. The default MUST be cache-only for fresh reviews; a caller-granted budget MUST cap the number of fresh review dispatches; budget exhaustion MUST leave affected conclusions pending/uncertain and visible in the report.
- **FR-013**: The CLI MUST surface cost/latency-relevant review facts: requested review count, cache hits, cache misses, fresh dispatches attempted, pending reviews, and budget exhaustion. It MUST NOT hide unreviewed agent questions behind a passing result.
- **FR-014**: The CLI MUST be read-only with respect to governed repositories for the four commands in this feature: it may read snapshots and write requested output/report files, but it MUST NOT rewrite Spec Kit artifacts, adapter fixtures, source files, generated issue/task lists, or consumer repository content.
- **FR-015**: The feature MUST dogfood the command surface against this repository's own `.specify` tree and the existing fixture artifacts, proving that the shipped Spec Kit and design-system adapters can be composed and run from the command boundary.
- **FR-016**: The feature MUST package the CLI as an optional tool artifact to the local package feed (`~/.local/share/nuget-local/`) so it can be installed and smoke-run from the packaged artifact; the repository and any consumer project MUST remain buildable/testable without installing the tool.
- **FR-017**: The feature MUST distinguish absent/bad governed input from tool defects in output and exit decisions. Missing artifacts, unreadable roots, unavailable review stores, and failed dispatches are reported as safe failures; unexpected command-runner defects fail as tool errors.
- **FR-018**: The public surface introduced by this feature MUST be declared in curated `.fsi` signature contracts and the API surface-area baseline MUST be added/updated for the CLI component.
- **FR-019**: The feature MUST keep dependency direction one-way: the CLI may depend on the Host, adapter SPI, concrete adapters, and Kernel; the Kernel, Host, SPI, and adapters MUST NOT depend on the CLI, and no consumer repository may require the CLI to perform ordinary work.
- **FR-020**: External-customer validation, running the tool against a separate checkout as adoption evidence, and converting findings into ordinary issues/tasks are out of scope for this feature and remain F13; F12 MUST supply the command surface that makes those follow-up scenarios possible.

### Key Entities *(include if feature involves data)*

- **Command Kind**: The user-visible operation: `route`, `explain`, `contract`, or `evidence`.
- **Run Request**: The normalized form of a command invocation: repository root, command kind, run mode, output format, change scope, review budget, and selected inputs.
- **CLI Model / Msg / Effect**: The command-boundary state, events, and requested I/O that keep orchestration testable under the constitution's MVU rule.
- **Repo Snapshot**: The read-only view of the repository and change scope inspected by the command.
- **Composed Catalog**: The active project-level rule catalog and fences assembled from the shipped adapters and any cross-domain rules.
- **Command Output**: The text or JSON report produced by a command, including route, explanation, contract, or evidence report content.
- **Evidence Report**: The combined view of evidence state, effective synthetic taint, freshness, review cache state, pending reviews, disclosures, and safe failures.
- **Review Budget**: The caller-granted cap on fresh agent-review dispatches for a run; cache hits do not consume it.
- **Exit Decision**: The process-level outcome category: success, governed blocking failure, usage/input error, or tool error.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `route`, `explain`, `contract`, and `evidence` all run successfully against this repository's `.specify` tree and the fixture suite, producing both text and JSON output for each command.
- **SC-002**: For identical snapshot inputs, mode, review budget, and output format, JSON output from each command is byte-for-byte identical across at least three repeated runs.
- **SC-003**: Exit decisions are correct for 100% of covered cases: advisory-only findings exit successfully, failing blocking findings in `Gate` mode exit nonzero, and usage/tool errors are categorized separately from governed failures.
- **SC-004**: The route output for 100% of blocking entries includes the matched fence, rule id, severity, run mode, and rendered check; light/no-gates routes produce an explicit light result.
- **SC-005**: The contract output for 100% of listed rules uses statements derived from the evaluated catalog's rendered checks, and the explanation output's top verdicts agree with the route decision for the same run.
- **SC-006**: The evidence report includes 100% of declared evidence nodes, effective evidence states, freshness decisions, cache hits/misses, pending reviews, disclosures, and safe failures present in the run, with synthetic, auto-synthetic, stale, failed, skipped, pending, and real states kept distinct.
- **SC-007**: Fresh review dispatch never exceeds the caller-granted budget in any tested run; the default cache-only budget dispatches zero fresh reviews; cache hits dispatch zero fresh reviews even when a nonzero budget is available.
- **SC-008**: The packaged tool can be installed or invoked from the local package feed and smoke-run against fixtures without referencing project-private test helpers.
- **SC-009**: Running all four commands in read-only mode changes zero governed repository files; any report-file output is written only to caller-selected output paths.
- **SC-010**: The CLI component has curated `.fsi` signatures, an updated surface-area baseline, and semantic tests that exercise the packaged/built command surface rather than private helpers.

## Assumptions

- This feature corresponds to **F12** (`012-cli`) in the dated implementation plan and starts **Milestone M4 - Tool + external validation**. F01-F11 are already merged: the pure kernel, Host effects edge, adapter SPI, Spec Kit adapter, and design-system adapter exist and are reused unchanged.
- The command names are fixed for this feature as `route`, `explain`, `contract`, and `evidence`. "Evidence report" in the roadmap is therefore the `evidence` command.
- The default local run mode is `Inner` unless the caller explicitly chooses `Sandbox` or `Gate`; CI is expected to choose `Gate` explicitly.
- The default fresh-review budget is cache-only (`0` fresh dispatches). A user or CI job must opt into fresh agent-review spend by granting a nonzero budget.
- F12 may inspect this repository and fixture snapshots read-only. Running against a separate external checkout as adoption evidence, and converting findings into issues/tasks, is **F13**.
- The exact command-line option spelling, output schema details, concrete project file layout, and installation command are planning/`.fsi` decisions constrained by this spec's behavior and compatibility requirements.
