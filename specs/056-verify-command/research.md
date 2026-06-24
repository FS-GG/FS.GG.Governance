# Phase 0 Research: The `fsgg verify` Host Command

All Technical-Context unknowns resolved. Each decision is grounded in an existing core or the
`ShipCommand`/`ReleaseCommand` precedent. No NEEDS CLARIFICATION remains.

## D1 — Verify is the ShipCommand pipeline, not a new evaluator

- **Decision**: Build `VerifyCommand` as a structural clone of `ShipCommand`: pure `Loop`
  (parse/init/update/render/exitCode + `applyExecution`) + edge `Interpreter` (identical `Ports`,
  `realPorts`/`step`/`run`) + thin `Program`. The pipeline is verbatim ShipCommand:
  `SenseScope → LoadCatalog → (Routing.route → Gates.buildRegistry → Findings.findUnknownGovernedPaths →
  Route.select → Ship.rollup) → SenseFreshness + LoadStore → executionPlan/classify → ExecuteGates →
  capture + build GateOutcomes → applyExecution → project verify.json → WriteArtifact → (PersistStore) →
  EmitSummary → exit-from-basis`.
- **Rationale**: The spec mandates "compose the existing selection, execution, freshness/reuse, and severity
  cores; it adds no new sensing." `ShipCommand` already composes all of them (`src/FS.GG.Governance.ShipCommand/Loop.fs`).
  Verify and ship "may share evaluation machinery" (spec Overview) and differ by stage, not mechanism.
- **Alternatives rejected**: A bespoke verify evaluator (duplicates `Ship.rollup`/`applyExecution`, drifts
  from the frozen severity semantics, violates "compose, don't duplicate"). A library shared with ship
  factored out first (scope creep into a frozen core for no behavioral gain — deferred).

## D2 — `RunMode.Verify`, and no `--mode` flag, expresses FR-017

- **Decision**: Thread `RunMode.Verify` (F023 `Enforcement`, ordinal 3, below `Gate`) into `Ship.rollup`.
  Verify exposes **no `--mode` flag** — the mode is fixed; `--profile` is overridable (default `Standard`).
- **Rationale**: FR-017 demands verify never be the merge-boundary authority. The enforcement enum already
  has a dedicated `Verify` mode for exactly this stage (`src/FS.GG.Governance.Enforcement/Enforcement.fsi:41`).
  Fixing the mode and omitting `--mode` makes it structurally impossible to turn verify into the `Gate`-mode
  merge verdict, while still letting a blocking-severity check at `RunMode.Verify` drive `Blocked`. The spec's
  own UsageError edge list names "unrecognized **profile**" but not "unrecognized mode" — corroborating that
  verify takes no `--mode`.
- **Alternatives rejected**: Exposing `--mode` like ship (lets a developer escalate to `Gate`, blurring the
  ship/verify boundary FR-017 draws). Defaulting to `Gate` (wrong stage; would make verify a second merge
  authority).

## D3 — Currency findings are a projection of the existing freshness/reuse evaluation

- **Decision**: A *currency finding* is the per-selected-check freshness/cache-eligibility disposition,
  surfaced as a first-class report/JSON section. Three dispositions, all already computed by ship's
  `executionPlan`/`cacheLinesOf`:
  - **fresh/reused** — cache verdict `Reusable ref` ⇒ evidence reused, check not re-run.
  - **stale/recomputed** — cache verdict `MustRecompute cause` ⇒ check re-run; `cause` (`NoPriorEvidence` |
    `InputsChanged categories`) names *why*, and the changed `categories` are the "generated view out of date
    relative to its declared sources" signal.
  - **recompute-by-default (unresolved)** — freshness facts could not be resolved for the check.
- **Rationale**: The spec assumption is explicit: "It adds no new repository sensing; 'validate generated
  views/evidence currency' is the existing freshness/reuse evaluation surfaced (and acted on) by this
  command." The exploration confirmed there is **no** separate "generated readiness view → declared sources"
  staleness core; view staleness reduces to the F043 `FreshnessResolution`/F041 `CacheEligibility` verdicts
  over each selected check's covered artifacts. Verify *labels and elevates* that existing signal; it senses
  nothing new.
- **Alternatives rejected**: A new view-freshness sensor comparing a generated file's mtime to its sources
  (new sensing — forbidden by the spec assumption and the constitution's "no new core sensing for this row").

## D4 — Currency severity and contribution to `Blocked` go through the existing rollup

- **Decision**: The verdict and blocking/advisory split are decided **only** by `Ship.rollup` (at
  `RunMode.Verify`) plus `applyExecution` (the F052 relocation of passing command-gates). A currency finding
  carries the **gate's** enforcement-assigned severity (the `EnforcedItem.Decision` the rollup already
  produced). A stale-and-unmet blocking check is a Blocker through that rollup; a stale-and-recomputed-to-pass
  check is relocated to `Passing` by `applyExecution`; an advisory currency finding is a warning only. Verify
  introduces **no** second path to `Blocked`.
- **Rationale**: Honors "partition findings into blocking and advisory using the established
  enforcement-severity dials" (FR-005) and "a blocking-severity currency finding contributes to Blocked"
  (edge case / Currency-severity assumption) **without** editing or bypassing the frozen severity cores. The
  exploration confirmed `EnforcedItem`/`EnforcedItemId`/`EnforcementDecision` are plain, externally
  constructible values, but verify needs none of that: it reuses the items `Ship.rollup` already built.
- **Alternatives rejected**: Constructing synthetic `EnforcedItem`s for currency and re-running a custom
  rollup (duplicates severity policy; risks double-counting a check as both a gate finding and a currency
  finding). Letting staleness independently block regardless of the recomputed verdict (would block a check
  that recomputed to pass — contradicts Story 2 and SC-003).

## D5 — `verify.json` is a new pure projection library, mirroring ReleaseJson

- **Decision**: New `FS.GG.Governance.VerifyJson` with
  `ofVerifyDecision : ShipDecision -> CacheEligibilityReport option -> (GateId * GateOutcome) list -> string`
  and `schemaVersion = "fsgg.verify/v1"`. It reuses the `AuditJson` document shape (verdict, exitCodeBasis,
  blockers/warnings/passing each carrying its enforcement decision + per-gate cache verdict + execution
  outcome) **and adds a top-level `currency` section** projecting the D3 dispositions (counts + per-check
  fresh/recomputed/unresolved + changed categories). Emit-only via `Utf8JsonWriter` (compact, default
  options), exhaustive token helpers with no wildcard, fixed field order — the byte-stability recipe shared
  by `AuditJson`/`RouteJson`/`ReleaseJson`/`CacheEligibilityJson`.
- **Rationale**: FR-006/FR-007/FR-008 require a separate, versioned, byte-deterministic `verify.json` whose
  printed form equals the persisted file. The precedent is a per-command projection library
  (`audit`/`route`/`gates`/`cache-eligibility`/`release` each have one). Reusing `ShipDecision` as the input
  (the way `AuditJson.ofShipDecision` does) avoids inventing a parallel decision type; the verify framing
  lives in the schema id, the `currency` section, and the command's text render.
- **Alternatives rejected**: Reusing `AuditJson` directly (wrong schema id, no `currency` section, conflates
  the ship-merge audit with the verify pre-flight). A new `VerifyDecision` record wrapping ship's decision +
  currency (unnecessary indirection; currency is fully derivable from the cache report + outcomes the
  projection already receives).

## D6 — Five-way exit-code contract, mirroring ship/release

- **Decision**: `ExitDecision = Success | Blocked | UsageError' | InputUnavailable | ToolError` →
  `0 | 1 | 2 | 3 | 4` (`exitCode`, total, no wildcard). `Blocked` (1) is reserved for an unmet blocking-
  severity check and is distinct from every failure-to-run code. Empty selection ⇒ `Success` (0) with a
  "nothing to verify" report.
- **Rationale**: FR-009/SC-002 require exactly this five-way contract, identical to `ShipCommand`/
  `ReleaseCommand` so CI usage is consistent across the suite (`ShipCommand/Loop.fs:135`).
- **Alternatives rejected**: Folding empty-selection into a distinct code (spec: empty selection is Success,
  not an error — FR-012). Reusing ship's `Blocked` semantics under `Gate` mode (wrong stage — see D2).

## D7 — Scope selectors, defaults, and degrade policy reuse ship verbatim

- **Decision**: `ScopeSelector = ExplicitPaths | Since | DefaultRange`; default `DefaultRange` (the
  locally-changed set via F016 `Snapshot`); `--paths` + `--since` together ⇒ `PathsAndSinceTogether`; empty
  `--paths` ⇒ `EmptyPaths`. Default artifact path `readiness/verify.json` (overridable `--verify-out`);
  default store `readiness/evidence-reuse.json` (overridable `--store`); `--persist-store` opt-in. Freshness/
  store sensing failures **degrade** to a safe default (`emptySensedFacts` / empty store) plus a non-fatal
  currency note, never perturbing the verdict or exit (FR-010/FR-013).
- **Rationale**: FR-015 requires the same change-scope selectors the existing host commands accept, a
  documented default, and mutually-exclusive-selector rejection — all already implemented in
  `ShipCommand/Loop.fs` (`parse`, `init`, the `FreshnessSensed`/`StoreLoaded` `Error` arms). Verify copies
  the policy and only renames the default output file.
- **Alternatives rejected**: A verify-specific scope grammar (gratuitous divergence from FR-015's "same
  selectors"). Treating a sensing degrade as a tool error (would fabricate failure where ship correctly
  degrades).

## D8 — Output format: `Text` default, `--json` = the verify.json verbatim

- **Decision**: `OutputFormat = Text | Json`. `Text` (default) prints the verdict, the blocking/advisory/
  passing partition with base+effective severity, the unknown-governed-path findings, the **currency**
  section, the execution summary, and the written path + schema id. `--json` prints the `verify.json` document
  **verbatim** (stdout == persisted file — FR-007), suppressing the text form.
- **Rationale**: FR-006/FR-007 — text by default, deterministic machine artifact on request, one source of
  truth. Exactly `ShipCommand`'s `render`/`renderJson` shape (`ShipCommand/Loop.fs:765`).
- **Alternatives rejected**: A `TextAndJson` combined mode (ReleaseCommand has one, but it complicates the
  "stdout equals the file verbatim" guarantee; verify keeps the simpler ship contract).

## D9 — Parse is pure, total, and decides usage errors before any port is built

- **Decision**: `parse : string list -> Result<RunRequest, UsageError>`, total, tolerating a leading
  `verify` verb. `UsageError = UnknownFlag | MissingValue | PathsAndSinceTogether | EmptyPaths |
  UnrecognizedProfile` (no `UnrecognizedMode` — verify has no `--mode`). A usage error exits 2 in `Program`
  **before** `realPorts` is built, so a typo writes no artifact and starts no git/filesystem work.
- **Rationale**: The edge case "Invalid command-line arguments … unrecognized profile ⇒ UsageError (2); a
  typo writes no artifact" and the `ShipCommand`/`Program.fs` precedent (parse-rejection exits before ports).
- **Alternatives rejected**: Validating the profile at the edge (a recognizer belongs in `parse` so the
  error is pre-port — the ship precedent).

## D10 — Determinism, no-mutation, and network-free are test-guarded, not asserted

- **Decision**: Test projects mirror ShipCommand/ReleaseCommand: a `DeterminismTests` (two runs over
  identical fixture+outcomes ⇒ byte-identical `verify.json`, and `--json` stdout == file), a `NoMutationTests`
  (only `verify.json` and the opt-in store change), a `ScopeGuardTests` (no `System.Net`/`HttpClient` in the
  command's own IL/source), a `SurfaceDriftTests` per new public module, plus `Failure`/`Degrade`/`EndToEnd`/
  `Execution` suites. Synthetic substitutes, if any, are disclosed and carry `Synthetic` in the name.
- **Rationale**: SC-003/SC-004/SC-005/SC-007 are measurable and must be proven by real evidence
  (Constitution V); the scope guard is the network-free proof (SC-007). The `verify.json` byte-stability
  inherits from the `Utf8JsonWriter` recipe (D5).
- **Alternatives rejected**: Asserting determinism by inspection only (the constitution requires automated
  evidence).
