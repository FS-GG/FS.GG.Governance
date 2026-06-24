# Phase 0 Research: Release-Facts Sensing for the Repository Boundary

**Feature**: `054-release-facts-sensing` | **Date**: 2026-06-24

All NEEDS CLARIFICATION are resolved below. Each decision records what was chosen, why, and the
alternatives rejected. The dominant constraint is that this row must produce **exactly the F053
`ReleaseFacts` value** while confining all impurity to one injected boundary — the same sensing
discipline F016 Snapshot and F046/F045 FreshnessSensing already established.

---

## D1 — Reuse the F053 output vocabulary verbatim; do not redefine it

**Decision.** The sensing output's facts value is the F053 `FS.GG.Governance.ReleaseRules.Model.ReleaseFacts`
(`{ States: Map<ReleaseRuleKind, FactState> }`), with `FactState` (`Met` / `Unmet` / `Unrecoverable`)
and the six-case `ReleaseRuleKind` reused **unchanged** by `ProjectReference`. This row introduces **no
new fact vocabulary** — only the *inputs* it senses against (caller expectations), the *recovered
evidence* it reads, and the *snapshot* it surfaces alongside the facts.

**Rationale.** FR-002 / SC-001 require the output to be exactly the F053 `evaluate` input shape, handed
over "with no adaptation or reshaping." Reusing the type *is* that guarantee — it is enforced by the
compiler, not by a translation step that could drift. The F053 spec named this row as its next row and
the F053 `Model.fsi` already documents `FactState`'s `Unmet`/`Unrecoverable` split as "what the sensing
row will inspect."

**Alternatives rejected.** (a) A sensing-local `SensedFactState` mirror mapped into F053 at the seam —
adds a translation layer FR-002 explicitly forbids and invites multi-copy type-conflict (the F046
prelude already documents that hazard for `SensedFacts`/`ReuseStore`). (b) Extending `ReleaseRuleKind`
— out of scope and would edit a frozen merged core.

---

## D2 — Sensing boundary shape: F016 port + pure derivation + edge interpreter, not a full Elmish Program

**Decision.** Model the feature as the established **sensing-port shape**: a single injected port value
(a record of read functions, FreshnessSensing precedent), an intermediate `RecoveredEvidence` bundle
(Snapshot's `RawSensing` precedent), a **pure** `deriveFacts` over that bundle, and one edge
`senseRelease` that runs the port, gathers the bundle, and applies `deriveFacts`. No `Program`, `Cmd`,
`Msg`, or `update` loop.

**Rationale.** This is a **single-shot sense**: read the six governing sources once, derive six facts.
There is no durable workflow state, no retries, no user interaction, no background work — exactly the
case Principle IV names as *not* needing full Elmish ceremony, and exactly what F016 Snapshot
(`Ports` → `RawSensing` → `assemble` → edge `senseSnapshot`) and FreshnessSensing
(`FreshnessSensor` record → pure `senseFreshness`) already do. The Principle IV separation is honored:
I/O is represented as data behind an injected port, the derivation is pure, and interpretation happens
only at the edge.

**Alternatives rejected.** A full MVU `Program`/`Cmd` loop — over-ceremony for a stateless single read;
no sibling sensing feature uses it, and the constitution explicitly blesses the local port/derivation
shape for "libraries, CLIs, and small tools."

---

## D3 — The port recovers *structured* evidence; the pure core *classifies* it

**Decision.** The injected `RepositoryPort` is a record of six per-family read functions, each returning
`Result<<structured evidence>, string>` (FreshnessSensing's `FreshnessSensor` precedent) — e.g.
`ReadVersion: unit -> Result<VersionEvidence, string>`. The edge `gather`s the six results into a
`RecoveredEvidence` record (Snapshot's `RawSensing` precedent). The pure `deriveFacts` consumes
`RecoveredEvidence` + caller `ReleaseExpectations` and **compares** recovered evidence to the
expectation per family — it does not parse a file format.

**Rationale.** Splitting *recovery* (read + structure the bytes — impure, at the port) from
*classification* (compare to expectation — pure) keeps `deriveFacts` genuinely pure and
unit-testable with a hand-built `RecoveredEvidence` (no disk), while the structuring read is exercised
against a real temp fixture repository through `realPort` (Principle V, US3). It also means the library
**invents no product manifest schema**: the realPort reads simple neutral fixture files; a later host
row can supply a port that reads real manifests — additive, no core change. This is the exact division
FreshnessSensing uses (`SenseRuleHash`/`SenseCoveredArtifacts` return structured primitives; the pure
`resolve` compares).

**Alternatives rejected.** Port returns raw file text (`Result<string,string>`) and `deriveFacts`
parses it (Snapshot's `-z` precedent). Rejected because it forces the pure core to assume a concrete
on-disk **format** for six heterogeneous sources — coupling the generic core to a product layout the
operating rule forbids (D5). The recovered-evidence split keeps format knowledge in the swappable port.

---

## D4 — Read-only and network-free by construction; proven by a scope-guard test

**Decision.** `realPort` reads only local files via BCL `System.IO` over the caller-supplied repo
directory; the production port issues no process, opens no socket, and references no
hosting/registry/publishing SDK. The new surface-drift test carries a **dependency scope guard**
(Snapshot's precedent) whose `banned` list includes `System.Net.Http`, `Octokit`, and `LibGit2Sharp`,
asserting the assembly's referenced set stays within an `allowed` allow-list (FSharp.Core, the
reused governance cores, BCL).

**Rationale.** FR-007 / SC-004 require zero network and zero registry/provider calls. Banning the
network assemblies at the surface-test level makes "no network" a build-enforced property, not a
review promise — exactly how F016 Snapshot bans `Octokit`/`LibGit2Sharp`. The fail-safe (D6) means a
missing or unreadable local file is a normal `Unrecoverable`, never a reach for a live lookup.

**Alternatives rejected.** A runtime network sentinel/firewall in tests — heavier and less honest than
proving the network assemblies are not even referenced.

---

## D5 — Expectations *and* source layout are caller-supplied; nothing product-specific is hardcoded

**Decision.** Two caller inputs carry everything product-specific: `ReleaseExpectations` (the per-family
criteria that define "met" — the version baseline, the required metadata field set, the expected pin
set, the required publish posture, the required trusted-publishing tokens, the required provenance) and
`SourceLayout` (the per-family relative paths the `realPort` reads). The library hardcodes **no**
package id, version baseline, field name, pin, posture, path, or layout.

**Rationale.** FR-011 and the constitution's one-way operating rule: generic code MUST NOT assume
rendering's package ids, template names, target names, or directory layout — "rendering is one
external customer, not this tool's internal shape." The governed identity is a caller-supplied F014
`SurfaceId`. Per-family expectations are modeled as **optional** fields: an expectation absent for a
family means "met cannot be decided," which resolves to `Unrecoverable` (D6), never an assumed `Met`
(edge case "caller-supplied expectation absent for a family").

**Alternatives rejected.** Conventional default paths / a built-in manifest schema — convenient but a
direct operating-rule violation; the host row, which *does* know a customer's layout, supplies the
`SourceLayout`.

---

## D6 — The fail-safe classification rule (the heart of the pure core)

**Decision.** For each of the six families, `deriveFacts` classifies exactly one `FactState`:

| Situation | `FactState` | Snapshot evidence |
|-----------|-------------|-------------------|
| Expectation for the family is **absent** (caller gave no criterion) | `Unrecoverable` | `None` + a diagnostic |
| Source read returned `Error` (file absent / unreadable / unparseable) | `Unrecoverable` | `None` + a diagnostic |
| Evidence recovered **and** it satisfies the expectation | `Met` | `Some` observed evidence |
| Evidence recovered **but** it does not satisfy the expectation | `Unmet` | `Some` observed evidence |

`Unmet` and `Unrecoverable` are kept **distinct** (reused F053 split): `Unmet` means "we read it and it
genuinely fell short"; `Unrecoverable` means "we could not read it / had no criterion." `deriveFacts`
**never throws**, **never fabricates `Met`** for an unreadable source, and **always** emits all six
families (FR-003, FR-004, FR-009).

Per-family "met" comparison (neutral, no dependency):
- **VersionBump** — `Met` iff the declared version sorts strictly **after** the baseline under a
  dotted-numeric compare (equal ⇒ `Unmet`, per US1.2 / US2.2). The compare is a small total function in
  the pure core; no semver package.
- **PackageMetadata** — `Met` iff every required field is present; `Missing = required \ present`.
- **TemplatePins** — `Met` iff every expected pin is resolved to its expected version;
  drift (missing/mismatched keys) ⇒ `Unmet`.
- **PublishPlan / TrustedPublishing / Provenance** — uniform present-token comparison: `Met` iff the
  required token set is a subset of the observed token set; missing required tokens ⇒ `Unmet`.

**Rationale.** FR-003/FR-004/FR-009 and the edge cases. The uniform present-token rule for the three
posture/config/provenance families keeps the core small and deterministic while still surfacing
observed-vs-required in the snapshot. The dotted-numeric version compare is the one family needing an
ordering; keeping it a documented total helper avoids a dependency.

**Alternatives rejected.** Folding `Unrecoverable` into `Unmet` — loses the audit distinction the F053
finding reason needs ("not met" vs "no recoverable evidence"). Throwing on a malformed source —
violates FR-004's "never a thrown exception."

---

## D7 — Determinism: all six families every run, every collection ordered

**Decision.** `Facts.States` always contains all six `ReleaseRuleKind`s on every run (never partial).
Every collection in the snapshot is emitted in a fixed order: metadata `Present`/`Missing` fields sorted
ordinally, pins rendered as a key-sorted association list, posture observed/required/missing sorted,
and `Diagnostics` ordered by `releaseRuleKindOrdinal` (reused from F053). Identical repository state +
identical expectations ⇒ a structurally identical `SensedRelease` (the two values compare equal).

**Rationale.** FR-008 / FR-009 / SC-003 / SC-006. Reusing `releaseRuleKindOrdinal` ties the sensing's
family ordering to the same key F053 sorts findings by, so the facts and any later finding line up.
Sorting every surfaced collection removes the only nondeterminism risk (filesystem/`Map` enumeration
order), the same hazard the spec calls out in US3.2.

**Alternatives rejected.** Preserving on-disk/insertion order — non-deterministic across filesystems;
SC-003 forbids it.

---

## D8 — Snapshot is additive evidence layered on the bare facts

**Decision.** The output is `SensedRelease = { Facts: ReleaseFacts; Snapshot: ReleaseSnapshot }`. The
`Facts` field alone is the P1/MVP value handed straight to F053 `evaluate`; the `Snapshot` is the P2
observed-evidence layer (per-family `Some`/`None` evidence + diagnostics) built from the *same*
recovered evidence. They are produced in one pass but separable.

**Rationale.** US1 (bare facts) is independently valuable and is all F053 needs to roll up a verdict;
US2 (snapshot) layers the auditable specifics later rows render. Bundling both in one return keeps the
single sense atomic while letting the host row consume just `Facts` for the gate and `Snapshot` for the
report. The `fsgg release` host command and the `release.json` projection that consume these are
explicitly later rows (FR-012, Out of Scope).

**Alternatives rejected.** Emitting only the bare facts now (defer the snapshot entirely) — US2 is in
scope this row; the snapshot reuses the already-recovered evidence at near-zero extra cost.

---

## Resolved Technical Context

- **No NEEDS CLARIFICATION remain.** Language/stack, dependencies, testing, and structure are the repo
  standard (see plan.md Technical Context).
- **No new third-party dependency, no schema, no schema-version bump** (Assumptions): the sensing is a
  new pure-derivation + thin-edge library layered on the merged thread.
- **Closest precedents to mirror:** F016 Snapshot (`src/FS.GG.Governance.Snapshot/`:
  `Model`/`Snapshot`/`Interpreter` triple, `RawSensing`→`assemble`, read-only-by-construction port,
  scope-guard surface test, real temp-repo fixtures) and FreshnessSensing
  (`src/FS.GG.Governance.FreshnessSensing/`: record-of-read-functions port returning structured
  evidence, `realSensor repoDir`, fail-safe `None`/`Error`).
