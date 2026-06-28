# Phase 0 Research: Breaking-Change (API-Compat) Gate

All NEEDS CLARIFICATION from Technical Context resolved below. Each decision records what was chosen, why, and the alternatives rejected.

## D1 — Detector mechanism: ApiCompat / Package Validation, NOT PublicApiAnalyzers

**Decision**: Use assembly/package-level **API compatibility validation** — the .NET SDK's **Package Validation** (`<EnablePackageValidation>` + `<PackageValidationBaselineVersion>`) and/or the **`Microsoft.DotNet.ApiCompat`** task/tool — as the breaking-change detector. Compare each freshly-built/packed assembly (inside the `.nupkg`) against the baseline package.

**Rationale**:
- The entire repo is **F#**. `Microsoft.CodeAnalysis.PublicApiAnalyzers` is a **Roslyn (C#) source analyzer**; it does not run on F# compilations and cannot produce/consume `PublicAPI.Shipped.txt` for F# projects. Adopting it literally (as issue #20's title suggests) is not possible for these packages.
- ApiCompat / Package Validation operates on **compiled assemblies / packages**, which is **language-agnostic** — it works for F#-produced assemblies. It detects removed/changed public members (the breaking class of changes this feature exists to catch) and is the SDK-native way to "force a SemVer major on a detected break."
- The constitution already mandates the F# analog of a committed public-API list: every public module has a curated **`.fsi`**, and the reflective **`surface/*.surface.txt`** baselines are the committed, reviewable surface snapshot. US3's "committed, reviewable baseline" is therefore **already satisfied** for the *source* surface; this feature does not add a parallel `PublicAPI.txt`.

**Alternatives rejected**:
- *PublicApiAnalyzers* — C#-only; inapplicable to F#. Recorded as "not covered" per FR-007 rather than forced.
- *Roll our own assembly differ* (extend the reflective surface generator to diff against a published `.nupkg`) — duplicates a mature, maintained SDK capability; rejected by dependency-minimalism/idiomatic-simplicity. ApiCompat is the maintained, standard tool.
- *Source-only diff of `.fsi` against a published baseline* — `.fsi` isn't shipped in the package and source-text diffing can't classify binary/source break severity as reliably as the assembly differ.

## D2 — Where the verdict lives: extend the existing release-rules core (additive new kind)

**Decision**: Add an **additive** `ReleaseRuleKind.ApiCompatibility` case to `FS.GG.Governance.ReleaseRules.Model`, with a matching `factFor`/sensing key. Its governing `FactState` is derived from the break signal combined with the semantic-version delta. Leave `VersionBump` and `Pack.versionPolicy` semantics **unchanged**.

**Rationale**:
- `Model.fsi` documents the closed `ReleaseRuleKind` set as extending **additively** ("a new case + a new `factFor` key … without changing existing behavior"). This is exactly that.
- The verdict pipeline already exists and is reused verbatim: `FactState (Met/Unmet/Unrecoverable)` → `evaluate` (one finding per rule) → `rollup`/`evaluateRelease` (Blockers/Warnings/Passing). No new rollup logic.
- Keeping `VersionBump` ("was the version bumped at all") distinct from `ApiCompatibility` ("is the bump magnitude adequate for the detected API delta") avoids changing existing `VersionBump` tests/behavior and gives a precise, self-explaining finding.

**Alternatives rejected**:
- *Strengthen `VersionBump` in place* — would change existing rule semantics and tests, and conflate two distinct questions; rejected.
- *A standalone gate outside the release core* — duplicates fact→finding→rollup + the maturity ratchet; rejected.

## D3 — Advisory → required ratchet: the rule's `Maturity` field (native), not CI continue-on-error

**Decision**: Use the existing `ReleaseRule.Maturity` lever (an F014 `Maturity`, fed verbatim into F023 enforcement) as the advisory→required ratchet. Ship the `ApiCompatibility` rule with a maturity that makes violations **Advisory** (visible, non-blocking — appears in `Warnings`), then promote it to **`BlockOnRelease`** (violations land in `Blockers`, `Verdict=Fail`) once existing surfaces are clean (SC-005). Mirror this at the infra layer by adding the CI job to branch-protection **required checks** on promotion.

**Rationale**:
- `Model.fsi` states relaxing `Maturity` "makes a violation advisory WITHOUT changing its satisfied/violated truth or visibility" — this is precisely US1→US2, already first-class. The rollup's `Warnings` bucket is literally "base `Blocking` relaxed to effective `Advisory` by the declared maturity … the advisory violation the spec keeps visible."
- Using the native lever means the *single source of enforcement truth* is the governance verdict (`fsgg release`/`verify` exit code), not a CI flag — consistent with "this repo governs itself with its own release rules."

**Note on `AdvisoryPromotion` (F039)**: that module governs promotion of **agent-reviewed** findings (bases: deterministic backing evidence / repeated-review confidence / human sign-off). It is **not** used here — the ApiCompatibility verdict is a deterministic check, not an agent verdict, so its ratchet is the declared `Maturity`, decided by a human maintainer when SC-005 is met. Recorded to avoid conflation.

**Alternatives rejected**:
- *CI `continue-on-error` for advisory* — duplicates the `Maturity` concept and splits the enforcement decision across two systems; rejected. (CI required-vs-non-required is kept only as the *infra mirror* of the in-product maturity.)

## D4 — Break signal × version delta → FactState (the verdict table)

**Decision**: The sensor yields a per-package break signal; the pure helper combines it with the semantic-version delta (reusing the existing semantic comparator behind `Pack.versionPolicy`) into the `ApiCompatibility` `FactState`:

| Break signal | Version delta vs baseline | `FactState` | Bucket (at `BlockOnRelease`) |
|---|---|---|---|
| No breaking changes | any forward bump (or equal) | `Met` | Passing |
| Breaking changes | **major** bump | `Met` | Passing |
| Breaking changes | minor / patch / none | `Unmet` | **Blockers** (Warnings while Advisory) |
| No baseline (never published) | n/a | `Met` (vacuous, FR-009) | Passing |
| Indeterminate (feed unreachable / package unreadable / tool error) | n/a | `Unrecoverable` (fail-safe, FR-008) | Blockers (Warnings while Advisory) |
| Not packable | n/a | (no rule fact emitted; not covered) | — |

**Rationale**: Matches spec FR-002/FR-008/FR-009 exactly, reuses the existing `FactState` tri-state (`Unmet` vs `Unrecoverable` distinction lets the finding reason separate "breaking under-bump" from "could not compare"). Deterministic and pure.

## D5 — Baseline source ("last published version")

**Decision**: The baseline package is the latest version of the same package id available on the **`~/.local/share/nuget-local/`** folder feed (the constitution-mandated pack output / local feed), resolved via `PackageValidationBaselineVersion` (or an explicit `--baseline` to the detector). When the org **GitHub Packages** feed lands (epic Pillar 4 / H4), it becomes the authoritative baseline source; the detector takes the feed as configuration, not hard-coded.

**Rationale**: There is no repo `nuget.config` declaring a consumer feed for `FS.GG.Governance.*` today; the local folder feed is the only place published versions exist. Tying "last published" to the configured feed keeps the detector correct now and after H4 with no logic change.

**Consequence (recorded honestly)**: most packages have **no** prior published version on any feed → at rollout they resolve **NoBaseline** ⇒ `Met` (vacuous). The gate becomes materially enforcing per-package as baselines accrue. SC-001/SC-005 are framed as "every package is *either covered or reported NoBaseline/not-covered* — zero silent passes," which this satisfies; FR-007 requires the NoBaseline/not-covered set be reported, not hidden.

## D6 — Where configuration & the tool live (drift-lock constraint)

**Decision**: All new MSBuild properties (`EnablePackageValidation`, `PackageValidationBaselineVersion`, any `ApiCompat*` settings) and any committed `CompatibilitySuppressions.xml` go in **repo-owned** files — `Directory.Build.local.props` (or a dedicated `.props` imported only by packable projects) and per-project/`surface`-adjacent paths. If the ApiCompat **tool** is needed explicitly, install it **job-scoped** in CI (`dotnet tool install`), not in `.config/dotnet-tools.json`.

**Rationale**: `Directory.Build.props`, `Directory.Packages.props`, and `.config/dotnet-tools.json` are **org-synced and byte-identity drift-checked** (Job 2 of `gate.yml`, feature 085). Editing them fails the drift gate. Repo-owned `*.local.props` are explicitly exempt. (If org-wide adoption is later desired, that is a separate cross-repo change to `FS-GG/.github` `dist/dotnet/`, sequenced on the board — out of scope here.)

## D7 — Keeping the main build green while Advisory (no hard-fail)

**Decision**: Do **not** rely on `<EnablePackageValidation>` failing the normal `dotnet build`/pack while the gate is Advisory. Instead invoke the detector in a **dedicated detector step** (a `pack-and-apicheck` `.fsx` mirroring `pack-reference-gate-set.fsx`, or the ApiCompat tool run explicitly) that **captures** the break result as data and feeds it to the governance rule. The block/allow decision is rendered solely by the rule's `Maturity` in `fsgg release`/`verify`. Existing breaks/no-baseline states are absorbed by `Met`/Warnings, not by failing the build.

**Rationale**: Package Validation emits MSBuild **errors** by default (hard build break) — incompatible with an Advisory phase and with a single governance-owned enforcement point. Running the detector as a sensor keeps `dotnet build` green, puts the verdict in one place, and lets the `Maturity` ratchet do the advisory→required transition. (A committed `CompatibilitySuppressions.xml` baseline is the fallback if a project ever does turn on inline Package Validation; not required for the sensor approach.)

**Alternatives rejected**:
- *Inline Package Validation as a build error from day one* — would block the main build immediately (no advisory phase) and split enforcement from the governance verdict; rejected.

## D8 — Relationship to the existing `surface.txt` drift guard (FR-010)

**Decision**: **Complement**, do not replace. `surface/*.surface.txt` (+ `SurfaceDriftTests`, `BLESS_SURFACE=1` to refresh) stays as the HEAD-vs-committed-snapshot tripwire; the new gate adds the packed-vs-last-published break classification tied to the version bump. The two never contradict because they compare different baselines; a legitimate change has one documented remediation path (update `.fsi` → refresh `surface.txt` via `BLESS_SURFACE=1` → bump version appropriately; if breaking, bump major).

**Rationale**: Matches the spec's recorded assumption (FR-010 resolution). Consolidating/retiring the snapshot guard is explicitly out of scope and can be revisited once `ApiCompatibility` is `BlockOnRelease`.

## D9 — Dependency record (Constitution dependency-minimalism)

- **Dependency**: .NET SDK **Package Validation / `Microsoft.DotNet.ApiCompat`**.
- **Need**: the only F#-viable breaking-change detector vs a published package.
- **Version-pinning**: SDK-bundled (tracks `global.json`/CI SDK `10.0.x`); if the standalone tool is used, pin its version in the job-scoped install string.
- **Maintenance owner**: Governance repo maintainer; the pure rule/evidence leaves take **no** new dependency — the tool lives only at the I/O edge + CI, preserving the core's BCL-only posture.

## Open items resolved in implementation

- **Sensor home (T001) — RESOLVED**: the ApiCompat output PARSE (`parseApiCompatOutput : string -> ApiBreakSignal`) is FOLDED INTO `FS.GG.Governance.ReleaseFactsSensing` (`Sensing.fs`), NOT a new `FS.GG.Governance.ApiCompat` leaf. This keeps the new dependency scope tightest (one added `FS.GG.Governance.*` project reference — PackEvidence, for the `ApiBreakSignal` vocabulary — which stays within the sensing leaf's existing dependency-scope guard's allow-list) and keeps the "all families always present" maintenance in one place. The pure verdict (`apiCompatibilityFact`), coverage builder, and rollup live in `PackEvidence/Pack.fs` next to the `versionPolicy` comparator they reuse. The break/verdict TYPES live in `PackEvidence/Model.fs`.
- **JSON projection — DONE (lightweight)**: the detector `.fsx` (`pack-and-apicheck.fsx --json`) emits the `ApiBreakSignal` set + coverage + rollup as JSON for CI; a full `*Json` projection-family module was not needed for the advisory MVP.

## Implementation note: where the verdict reaches the user (advisory MVP)

The advisory gate (US1) is delivered through the **detector `.fsx` + the advisory CI job**, which grade the per-package signal × SemVer delta through the REAL pure functions (`Pack.apiCompatibilityFact`/`coverageOutcome`/`apiCompatibilityRollup`) and report coverage + findings without reddening the build (D7). The **in-product MVU host overlay** (routing the `ApiCompatibility` fact into `fsgg verify`/`release`'s Warnings/Blockers via the `ReleaseCommand` Loop) is the mechanism that matters at the **REQUIRED** phase (US2, `BlockOnRelease`) — it is deferred to the required-promotion work and tracked, since at advisory rollout it changes no verdict and every package resolves `NoBaseline` (D5). `deriveFacts` emits `ApiCompatibility = Unrecoverable` (fail-safe by construction) so a declared rule is `Violated` until the host overlay supplies the real value.
