# Phase 0 Research: Verify & Release Publication Boundary (F26)

All Technical Context unknowns are resolved; the spec named no `NEEDS CLARIFICATION`. The spec deferred four
concrete shaping decisions to `/speckit-plan` (the precise attestation field shape; sidecar-vs-embedded; the
declare/trigger mechanism for the scheduled matrix; report-object boundaries). Those are D1–D4 below; D5–D7
record the reuse boundary and the safe-failure/determinism stances.

Every dependency this row builds on is already implemented and was read while planning: `ReleaseRules`
(`ReleaseRuleKind`, `FactState`, `ReleaseFacts`, `ReleaseDecision`, `evaluateRelease`), `ReleaseFactsSensing`
(`SensedRelease`, `ReleaseSnapshot`, `ReleaseExpectations`, `SourceLayout`), `ReleaseCommand` (`Loop`/
`Interpreter`/`Declaration`, five exit codes), `ReleaseJson` (`fsgg.release/v1`), `VerifyCommand` /`VerifyJson`,
`CommandKind` (`Pack`, `KindedCommandRun`, `AuditSnapshot`, `auditSnapshot`), `Provenance`
(`Provenance`, `BuilderIdentity`), `ProvenanceJson` (`fsgg.provenance/v1`), `GateExecution` (`ExecutionPort`,
`GateCommand`, `ExecutionOutcome`, sentinel codes), `CostBudget` (ordered `Cost` ceiling). A key observed fact:
**F25's `CommandKind` / `AuditSnapshot` / `ProvenanceJson` cores are pure leaves that no host references yet** —
F26 is the first row to drive the provenance snapshot at a host edge.

---

## D1 — The packed artifact's version is the source of truth for releasability

**Decision.** `PackEvidence.versionPolicy` evaluates the version-bump rule against the **packed artifact's**
version, not the source-declared version. The packed `FactState` contributions for `VersionBump`,
`PackageMetadata`, and `Provenance` are computed from the real pack output and **merged over** the F54 sensed
`ReleaseFacts` (packed evidence wins on those three families); the merged facts are handed unchanged to
`Release.evaluateRelease`.

**Rationale.** The spec's central edge case — "version bumped in source but not in the packed artifact (or vice
versa) ⇒ evaluated against the **packed** artifact's version" — only resolves correctly if the artifact is the
truth. A `Pack` that exits zero but emits no artifact is `PackOutcome.PackedNoArtifact` → contributes `Unmet`
(blocks, "packed but no artifact produced"), never an assumed `Met`. A non-zero pack is
`PackOutcome.PackFailed(sentinel)` → `Unmet` + the failed run still recorded. A packed version equal-to-or-below
its baseline is `VersionVerdict.Unbumped`/`Downgraded` → `Unmet`. This grounds the existing F53 families in real
output without adding a family (FR-002, FR-003).

**Alternatives considered.** *Add a new `PackOutput` release-rule family* — rejected: FR-003 forbids a new
family; the existing `VersionBump`/`PackageMetadata`/`Provenance` already model exactly these expectations and
only lacked real evidence. *Evaluate against the source-declared version* — rejected: it cannot detect the
"bumped in source, stale in artifact" case the spec calls out as the reason packing exists.

## D2 — Attestation is its own `attestation.json` sidecar; `release.json` bumps to v2 additively

**Decision.** The `AttestationSummary` projects to a dedicated `attestation.json` (`fsgg.attestation/v1`) via a
new `AttestationJson.ofAttestation`, exactly as F25 gave the `AuditSnapshot` its own `provenance.json`.
`release.json` becomes `fsgg.release/v2`, **adding** `packageEvidence` (per-project artifact/version/digest/
outcome), `versionPolicy` (per-project verdict + baseline), and an `attestation` block (the summary's identity +
the not-formal-compliance marker, so `release.json` is self-contained for a consumer that does not read the
sidecar). `verify.json` gains an additive `releaseReadiness` preview block.

**Rationale.** The attestation is a *projection of the F25 snapshot*, the same shape relationship F25 already
solved with a dedicated sidecar — keeping the projections one-concern-each and the goldens small. A bumped
schema version with additive-only fields is the established `route.json` v1→v2 migration precedent (F23) and is
explicitly permitted by FR-015; every existing `route.json`/`ship.json` golden stays byte-identical, and the
release/verify goldens change only by the new fields under the new version.

**Alternatives considered.** *Embed the full attestation inside `release.json`* — rejected: it bloats the
release document, couples two schema versions, and breaks the F25 one-projection-per-snapshot precedent.
*Leave `release.json` at v1 and ship only the sidecar* — rejected: a release consumer must see *that* an
attestation exists and *what* it covers without a second fetch; the additive `attestation` reference + the
`packageEvidence`/`versionPolicy` the verdict now depends on belong in the release document under a bumped
version.

## D3 — The report object is the single source of truth; JSON renders from it

**Decision.** `ReleaseReport` is an immutable, presentation-free record carrying the F53 `ReleaseDecision`
(verbatim — verdict + `ExitCodeBasis`), the `PackEvidenceSet`, the version policy, the F54 publish-plan/posture/
pins `PreconditionEvidence`, and the `AttestationSummary`. `ReleaseJson.ofReleaseReport` and the verify
`releaseReadiness` preview render *from this object*; the F27 human projections will too. The verify preview is
`ReleaseReport.preview : ReleaseReport -> VerifyReleasePreview`, an advisory subset that drops nothing but is
explicitly non-blocking.

**Rationale.** FR-012 requires a single source of truth so JSON automation and later human projections never
diverge. Carrying the `ReleaseDecision` verbatim (never re-deriving the verdict) preserves the F53/F24 partition
and exit-code basis exactly. Keeping the report presentation-free (no ANSI, no host paths, no clock) is what lets
F27 layer text/Spectre/TUI without a second truth.

**Alternatives considered.** *Let each projection re-read the decision + sensed snapshot + pack evidence
independently* — rejected: three projections (release.json, verify preview, F27 human) re-deriving the same
roll-up is exactly the divergence FR-012 forbids. *Make the report mutable / carry rendered strings* — rejected:
violates "immutable, presentation-free" (FR-012) and determinism (FR-010).

## D4 — Scheduled matrix = a declared marker + a boundary gate reusing the F25 budget; no scheduler in the cores

**Decision.** `ValidationMatrix.decideMatrix : CostBudget -> MatrixBoundary -> ExhaustiveMatrix option ->
MatrixPlan` is a pure decision. A **declared** `Exhaustive`-cost matrix yields `RunNow` only when the boundary's
F25 `CostBudget` ceiling admits `Exhaustive` (the `Release`/scheduled boundary); at an inner-loop boundary it is
`Deferred deferReason` (named: "exhaustive matrix deferred to the scheduled/release boundary"); `None` (no
declaration) yields `NotDeclared` — never an invented matrix. The actual CI cron/trigger that *invokes* the
scheduled boundary is a host/CI concern (a CI workflow calling `fsgg release` at the scheduled boundary) and is
**out of this row's scope** — F26 supplies the declaration surface and the pure boundary decision only.

**Rationale.** This reuses the F25 ordered `Cost` ceiling verbatim (no new tier, no scheduler dependency, no
network), keeps the cores pure (FR-014), and makes the inner-loop/scheduled split deterministic and testable
without standing up a scheduler. It matches the spec's framing: "a run may **declare** … to be executed on a
schedule"; declaring + deciding is in scope, cron plumbing is not.

**Alternatives considered.** *Build a scheduler / cron core* — rejected: out of scope, would add I/O and a
dependency to a pure-decision row. *Always run the broad matrix in the inner loop* — rejected: defeats the
entire P3 value (keep the inner loop fast). *Invent a default matrix when none is declared* — rejected: FR-009/
SC-006 forbid inventing an undeclared matrix.

## D5 — Reuse F053 / F054 / F055 / F056 / F25 / F033 unchanged

**Decision.** No new release-rule family; `ReleaseRuleKind` / `evaluateRelease` / the F53 three-way partition
are untouched. F54 sensing is untouched (pack evidence is merged at the host edge as already-typed
`FactState`s). The verify/release five-exit-code schemes are untouched (FR-005). `CommandRecord` / `Provenance`
/ `AuditSnapshot` identity is reused verbatim — the attestation is a *projection*, not a new identity. Pack
duration stays sensed metadata excluded from identity (F25/F032). No new dependency anywhere.

**Rationale.** The spec's scope decision #1 is explicit: "reuses all of that unchanged." The only genuinely new
things are the enforced pack act, the report object, the attestation projection, the publish/posture/pin
surfacing, and the matrix decision — every one a composition over existing cores.

## D6 — Safe failure: input vs tool defect, never a fabricated pass

**Decision.** Each publication input maps to a clear signal, never a swallowed error or fabricated pass
(FR-011): **no packable project** ⇒ the pack precondition is *vacuously satisfied* and the report states "no
packable projects" (never a fabricated pack); a **pack that exits zero with no artifact** ⇒
`PackedNoArtifact` → `Unmet` (blocks); a **failed pack** ⇒ `PackFailed(sentinel)` → `Unmet`, the failed run
still recorded in the snapshot/attestation; an **absent provenance/builder input** ⇒ the attestation is *not
produced as hollow* — the release blocks with a named input signal; a **missing/unresolved publish plan /
unconfigured posture / drifted pin** ⇒ the existing F53 `Unrecoverable`/`Unmet` → blocks naming the family.
Distinguishing a missing/malformed input from a tool defect is preserved at the `ReleaseCommand` edge exactly as
F55 does (declaration-load `Error` ⇒ `InputUnavailable` exit 3; write `Error` ⇒ `ToolError` exit 4).

**Rationale.** Constitution VI + FR-011 + the F14–F60 safe-failure discipline: a governance tool that fabricates
a pack or a hollow attestation is worse than useless. Every "absent" maps to a fail-safe `Unmet`/`Unrecoverable`
or a named diagnostic.

## D7 — Determinism: byte-identical, order-independent, duration excluded

**Decision.** Every produced value normalizes ordering and paths and excludes wall-clock/username/environment:
`PackEvidenceSet` is sorted by project surface id then artifact path; `factContributions` is a `Map` (order-
free); `AttestationSummary` reuses the F25 snapshot's set/order discipline (artifact digests as a set,
command runs order-significant exactly as `Provenance` fixes); the `ReleaseReport` preserves the F53 partition
order; JSON fields are emitted in a fixed order. Pack duration is carried only inside the embedded F032
`CommandRecord` (`SensedDuration`), structurally excluded from every identity and emitted only as clearly-sensed
metadata (`durationNanos`) in `attestation.json`, exactly as `provenance.json` does. Presenting packable
projects / publish-plan entries / command runs in a different order yields byte-identical output (SC-005,
SC-007, the "determinism under reordering" edge case).

**Rationale.** FR-010 + SC-005 + SC-007 + the byte-identical discipline every F42–F60 projection holds. Reusing
the F25/F033 identity rules verbatim means the attestation's determinism is the snapshot's determinism, already
proven.
