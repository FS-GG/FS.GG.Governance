# 0012 — Epic #367's four F# constitution rule packs are composed into one named profile, single-sourced in `SurfaceChecks.Profile`

**Status**: Accepted · **Date**: 2026-08-04 · **Item**: FS-GG/FS.GG.Governance#385
(satisfies epic [#367](https://github.com/FS-GG/FS.GG.Governance/issues/367)'s unowned integration
criterion)

**Builds on** [`0011-generated-reference-gate-set.md`](0011-generated-reference-gate-set.md). It
supersedes and amends nothing: 0011's derivation mechanism, its fail-closed drift gate, and its
consumer resolution verb are reused verbatim. This record adds a **second bound profile** to the
machinery 0011 built, and answers the question 0011 explicitly deferred — its last Consequence reads
"the four rule packs' incompatible finding shapes, and the two packs with no production consumer,
remain `#385` implementation work."

## Context

Epic #367 turned the F# SDD constitution into an organization-wide profile and shipped it as **four
separate rule packs**, each with its own child item, its own acceptance, and its own green suite:

| pack | child | implemented by | rules | maturity | production consumer |
|---|---|---|---|---|---|
| public surface | #366 | `DesignChecks.FSharpSurface` | 7 | `warn` | `fsgg verify` sweep |
| idiomatic simplicity | #368 | `CodeChecks` | 12 | *none* | **none** |
| effect boundary | #369 | `DesignChecks.FSharpEffectBoundary` | 10 | `block-on-pr` | `fsgg verify` sweep |
| evidence boundary | #370 | `DesignChecks.EvidenceBoundary` | 16 | *none* | **none** |

All four closed. The epic's fifth acceptance line — *"#366, #368, #369, and #370 are integrated into
one reusable F# profile/reference gate consumed by generated products and ordinary repositories"* —
was owned by **no child**, so nothing scheduled it and the roll-up graph, which only sees closed
children, could not see it was unmet.

The measured evidence that it was unmet: **all four children declared `reference-gates/` in their
`Paths:`, and `reference-gates/` did not exist.**

```
$ git ls-tree -r origin/main --name-only | grep -c '^reference-gates/'
0
```

Four consequences followed, and each is a thing this record has to decide rather than describe:

1. **No artifact anywhere enumerated the four packs together.** The only document naming all four was
   `.fsgg/constitution.md`, as prose principles. There was no `Pack` type, no profile, no registry,
   and no list of any pack's rule identities outside its own emit sites (except #368's, which has a
   closed DU).
2. **Two incompatible finding shapes.** #366 and #369 emit `SurfaceChecks.Model.SurfaceFinding`,
   which carries a `Maturity` and therefore reaches `Enforcement.deriveEffectiveSeverity`. #368 emits
   `ArchitectureFinding` and #370 emits `{ Code; Subject; Correction }` — **neither has a severity
   axis at all**, so neither could produce a verdict at any maturity.
3. **Two maturities decided independently, at two emit sites, with nothing relating them.** #366's
   `migrationMaturity = Warn` and #369's hard-coded `BlockOnPr`. Nothing recorded whether that was a
   decision or an accident, and nothing would have noticed if a third pack disagreed with both.
4. **No published gate set contained any of them.** The org's reference gate set bound one profile
   (`game`), and none of the four packs appeared in it — so no generated product and no ordinary
   repository consumed the constitution as a unit, which is exactly what the epic asked for.

## Decision

### 1. One composed profile, single-sourced in `SurfaceChecks.Profile`

`FS.GG.Governance.SurfaceChecks.Profile` (`src/FS.GG.Governance.SurfaceChecks/Profile.fs`) is the ONE
place the composition is authored: the four packs, the child that authored each, each pack's complete
closed rule-identity set, the maturity each binds at, and the reason it binds there.

**Why `SurfaceChecks`.** It is already the shared cross-domain finding vocabulary every pack must
agree on (`Model.SurfaceFinding`, `Model.mkFinding`), and it is the lowest project all four packs can
reach: `DesignChecks` already referenced it, and `Inheritance` reaches it with one new edge and no
cycle (`SurfaceChecks → {Config, Enforcement}`, neither of which reaches `Inheritance`). Composing
here adds **no new package and no new layer**.

*Rejected:* a new `FS.GG.Governance.FSharpConstitution` project. It would have to be packable —
`DesignChecks` is packed and a packed project referencing an unpacked one ships a broken package — so
it would add a published artifact, a version, and a release cadence to hold a table that has a
natural home in a project the packs already reference.

*Rejected:* authoring the table in `Inheritance.ReferenceProfile` next to the `game` floor. The packs
cannot read their maturity from there without `DesignChecks → Inheritance`, which inverts the layering
(a checks pack depending on the gate/inheritance layer) for no gain.

### 2. The packs READ their maturity from the profile — one authority, not a fifth copy

`FSharpSurface.migrationMaturity` and `FSharpEffectBoundary`'s emit wrapper now call
`Profile.declaredMaturity`. Both still evaluate to their shipped values (`Warn`, `BlockOnPr`) — **no
posture was revised** — but the value has one home, so the two can no longer drift apart with nothing
able to see it.

This is the property 0011 established for the `game` floor, applied to the constitution packs, and it
is why the composition is not simply a fourth restatement of what the packs believe. `ReferenceProfileComposition`
C5 asserts it by comparing what the **real evaluators emit** against `declaredMaturity`, so moving the
profile's value moves the pack's.

### 3. Rule identities are the packs' OWN codes, unchanged

`fsharp.signature-missing`, `inheritance-hierarchy`, `fsharp.effect-in-transition`,
`evidence.observed-outcome-missing` — the composition renames nothing. #367's criterion asks for
identities that are **stable**; a composition that renamed them would break every existing finding,
receipt, and `RuleIdentity.surface` wire token to buy tidiness.

Traceability is `Profile.ruleOwner : string -> Pack option`, a **total** declared lookup, plus
`Profile.authoringItem : Pack -> string` naming the child. That combination is what makes AC1's
"traceable back to the child that authored each" an executed fact (C7) rather than a comment.

*Rejected:* prefixing #368's unprefixed tokens (`module-size-review` → `simplicity.module-size-review`)
for symmetry. It would make one pack's published identity differ from what its own
`findingIdToken` emits, which is a second identity vocabulary — the failure mode this whole line of
decisions exists to end.

### 4. The packs' disagreement is resolved as APPLICABILITY, and recorded as data

#366 binds at `warn` and #369 at `block-on-pr`. **The composition deliberately does not flatten them.**
They are not the same rule, and what differs is applicability, not severity taste:

- **#366 sweeps every compiled `.fsproj` unconditionally** (`VerifyCommand/Interpreter.fs` runs it
  whether or not a product declared a design surface, on purpose, so a Rogue3-shaped executable is
  governed). Binding that at `block-on-pr` would block every adopting repository on the day it
  resolved the profile. It is a dated migration at `warn` — #366's shipped posture, preserved.
- **#369 fires only where an author opted a transition in** with an in-source `// fsgg:effect-boundary`
  marker; parsers, validators and thin adapters are excluded by sensed fact. `block-on-pr` therefore
  binds only work that declared itself in scope, and **no repository acquires a blocking gate merely
  by resolving the profile**.

#368 and #370 join at `warn`: both are new to the profile and neither yet has a production consumer
(§6), so advisory first, per #366's precedent. `Profile.maturityRationale` carries each reason as
**data**, because a reader of the profile must be able to see that the difference was decided.

None of the four is a permanent blanket exemption. The dated route to blocking is the profile's own
maturity, raised in `Profile.declaredMaturity` and republished; every consumer picks it up by
re-pinning.

*Rejected:* "strictest wins" as a blanket rule, raising #366 to `block-on-pr`. It would discard #366's
deliberate migration posture and block every adopter on adoption day, which is the opposite of what
#385's own Notes require.

### 5. Genuine collisions resolve UP; a duplicate identity is an error

Where two packs would declare the **same** identity, `Profile.composeMaturity` takes the
higher-`maturityRank` one — ADR-0009 §Decision 3's non-lowerable rule reused verbatim, not a second
precedence invented here. It is commutative, so the result cannot depend on fold order.

But that tie-break is a safety net, not the design. `Profile.collisions` reports every identity
declared by more than one pack in the declarations it is given, and **C3 asserts
`collisions declarations` is empty**: if no two packs ever share an identity, there is no last-writer
to be, and "whichever pack loads last" is unrepresentable rather than merely discouraged. C4 passes
it a *planted* duplicate to prove the detector detects, because an empty list is also what a broken
detector returns.

`collisions` takes its input rather than reading the table, and that signature is load-bearing: as a
nullary `unit -> Collision list` over the fixed table it could only ever return `[]`, so the test
claiming to prove it had to re-implement the grouping locally — and breaking the real function left
that test green (measured in #385's first review round). A detector that cannot be handed a positive
case is not a detector anybody has checked.

Note which test carries which guarantee, because they are not the same one. The property *"no
identity is silently owned by two packs"* is enforced by **C3's ownership loop**, which asserts
`ruleOwner id = Some pack` for every declared identity; a planted duplicate reds C3 even with the
detector fully broken. C4's job is narrower and still necessary: it proves the reported diagnostic —
the thing a human reads when arbitration is needed — is real.

### 6. The published gate set gains four command-free gates, and a second generated region

`Profile.checks` projects the four packs into four `Check` records in a new `fsharp` domain
(`fsharp:public-surface`, `fsharp:idiomatic-simplicity`, `fsharp:effect-boundary`,
`fsharp:evidence-boundary`). `ReferenceProfile.boundProfiles` gains `fsharp-constitution`, and
`ReferenceProfile.checksFor` **binds** `Profile.checks` rather than restating them, so 0011's
`capabilitiesRegion` projection renders the second region and D1–D4 gate it with no new mechanism.

**Command-free**, like the gameplay floor: each pack's own sensing and the handoff evidence satisfy
them. A `tooling.yml` binding would leave a dangling command reference in every repository that
resolved the published set without adopting this repo's tooling.

`Profile.findingOf` is what puts #368's and #370's findings on the severity axis for the first time —
the "incompatible finding shapes" 0011 deferred. An identity the profile does not declare still
produces a finding, marked `IsInputState` and naming the id: unattributable is **malformed input**,
never a silent pass and never a fabricated rule violation (C8).

**Closed by FS-GG/FS.GG.Governance#390.** This paragraph recorded that #368 and #370 had no production
consumer — nothing in `src/` called `CodeChecks.analyze` or `EvidenceBoundary.evaluate`, so two of the
four published gates could not produce a finding at all. `VerifyCommand`'s `CodeSweep` and
`EvidenceSweep` are those callers, reached from `Interpreter.senseSurfacesReal` in the same sweep and
folded through the same `SurfaceFold` as #366's and #369's evaluators, with findings normalized through
`Profile.findingOf` so they carry this profile's declared maturity.

Both packs are **applicable only where a repository declares their scope** — `.fsgg/fsharp-simplicity.json`
for #368, `.fsgg/evidence-boundary.json` for #370 — which is #369's shape rather than #366's unconditional
sweep, and for these two it is a correctness requirement rather than a preference:

- `CodeChecks.analyzeDocument` type-checks each document **standalone, as a script**
  (`GetProjectOptionsFromScript`), with no project references. An unconditional sweep would report
  `compiler-analysis-failed` for every source that references a sibling project — noise, not governance.
- `EvidenceBoundary.evaluate` requires semantic-regression, boundary-fixture and golden-or-schema evidence
  **unconditionally**, so an unconditional sweep would give every repository three findings for having no
  evidence obligation at all.

A malformed declaration is reified as a Blocking, input-state `surface.sense-error` finding on the pack's
own surface, per-pack, so it neither collapses that pack to `[]` (ADPT-1) nor erases the other packs'
findings from the same run. No declaration ⇒ no findings, which is the repository's recorded choice rather
than a silent skip. The published gate set is unchanged by #390: the same four checks, the same ids, the
same maturities — what changed is that all four are now evaluated.

*Still open, and filed rather than implied:* `CodeChecks.analyze` type-checks each document as a
standalone script, so a source that references a sibling project short-circuits to
`compiler-analysis-failed` instead of being governed by the pack's real rules. That bounds what #368 can
govern in a multi-project repository. It is tracked at its root cause as
FS-GG/FS.GG.Governance#391; the declared-scope applicability above contains it, and does not fix it.

### 7. The composed profile's path map is its own territory, because routing is a partition

`Routing.route` returns `Routed of domain` — **one** capability domain per path, by specificity with
an ordinal tiebreak. `src/**/*.fs` and `src/**` are co-specific (one literal segment and one `**`
each), so binding the profile to F# sources would not take them from `build`; it would lose the
tiebreak and raise an `AmbiguousRoute` diagnostic for every source file.

A cross-cutting domain therefore **cannot** share a path with `build`. The published skeleton routes
`fsharp` on `surface/**` (the curated public-surface baselines #366 governs) and `readiness/**` (where
#366's receipt and #370's evidence records land) — the same shape as the gameplay floor routing on
`specs/**`, where its requirements live rather than where gameplay code does.

*Rejected:* re-pointing the skeleton's `src/**` to `fsharp`. That would take `build` away from source
changes in every repository that resolved the set. A consumer that wants source-level routing changes
**its own** map; the published skeleton does not make that choice on its behalf.

### 8. `reference-gates/` exists, and is a front door rather than a second home

All four children declared it and none landed there, so #385's verification line asks for it to exist
and be non-empty. It holds `reference-gates/README.md`: the profile's human entry point, naming the
authoritative module, the published package, the resolution verbs, and the four packs with their
authoring items and rule inventories. It is **not** a second copy of the table — that is the failure
mode 0011 exists to prevent — and the README says so.

## Consequences

- **`FS.GG.Governance.SurfaceChecks` 0.1.0 → 0.2.0** (MINOR, ADR-0055): new public `Profile` module,
  no existing member changed. `surface/FS.GG.Governance.SurfaceChecks.surface.txt` grows by additions
  only.
- **`FS.GG.Governance.Inheritance` 0.1.0 → 0.2.0** (MINOR): `boundProfiles` grows a second profile and
  the package gains a `FS.GG.Governance.SurfaceChecks` dependency. The new dependency is why it is not
  a PATCH.
- **`FS.GG.Governance.ReferenceGateSet` 1.6.0 → 1.7.0** (MINOR): a new domain, two path-map globs, and
  four checks — all additive. **No existing check changed**, so no consumer's current gate set changes;
  three of the four new gates are advisory and the fourth blocks only opted-in work, so re-pinning
  cannot red a repository that was green.
- The bless path and **all four** derivation invariants now iterate `boundProfiles` rather than naming
  `game` — so the set the derivation is generated from and the set it is guarded over are one list,
  and a third profile is covered by adding one entry with no second edit that could be forgotten.
  D2's generalization was found by #385's first review round and matters most: while D2 alone bound
  `game`, mis-mapping the `costToken` cases only the new profile uses (`Cheap`/`Medium`) and
  regenerating shipped `cost: exhaustive` on three of four `fsharp:*` gates with the whole suite
  green. The token-renderer comment in `ReferenceProfile.fs` claims this test prevents exactly that,
  and for the added profile it did not. What bless still cannot do, on purpose, is **create** a
  marker pair: a newly bound profile reds D1/D3 until a human places the markers and the surrounding
  hand-authored context, because a generator choosing its own insertion point in a consumer's file is
  a worse contract than a fail-closed one.
- `#367`'s acceptance checklist is updated from evidence, so the epic can be rolled up.
- **No change to `FS-GG/FS.GG.SDD`.** The generated-product route is proven here by scaffolding with
  the real `fsgg-sdd init` and running the published resolution verb into the workspace it produced.
  Teaching `init` to perform that resolve itself is
  [FS-GG/FS.GG.SDD#845](https://github.com/FS-GG/FS.GG.SDD/issues/845), and remains that repository's
  decision.
