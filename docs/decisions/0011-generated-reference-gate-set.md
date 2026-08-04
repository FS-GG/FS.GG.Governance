# 0011 — The embedded F# profile is the single source of the org gate set; the published YAML is generated from it

**Status**: Accepted · **Date**: 2026-08-04 · **Item**: FS-GG/FS.GG.Governance#386
(blocks [#385](https://github.com/FS-GG/FS.GG.Governance/issues/385), and through it #367's
integration criterion)

**Amends** [`0009-profile-bound-gate-inheritance.md`](0009-profile-bound-gate-inheritance.md)
§Decision 1. It does **not** supersede it: §1's rejection of package-resolved *enforcement* stands
unchanged, and §§2–4 are untouched. What this record adds is the answer to the question §1 left
open — *what, then, is the published package?*

The decision itself was made by the repository owner on 2026-08-04 and is recorded durably at
[FS.GG.Governance#386 (comment)](https://github.com/FS-GG/FS.GG.Governance/issues/386#issuecomment-5176869657).
This file is the repo-local record that decision asked for; the choice is not reopened here.

## Context

ADR-0009 §1 made the reference floor **embedded**: `Inheritance.referenceGatesFor` reads an in-code
table, never a file, because *a floor a product can escape by deleting a file is not a floor*. That
reasoning was sound and remains so.

It also left the repository with **two independently-authored copies of the same org profile**:

| | what it was | what it did |
|---|---|---|
| `Inheritance.referenceChecks` (F#) | an in-code `Check list` keyed by `TemplateProfile` | ENFORCED — reached a product's gate set through `applyInheritance` before `Ship.rollup` |
| `samples/sdd-reference-gate-set/.fsgg/capabilities.yml` | hand-authored YAML, packed as `FS.GG.Governance.ReferenceGateSet` | DISTRIBUTED — the published, versioned artifact a consumer pins |

Nothing derived one from the other, and nothing checked that they agreed. The agreement was recorded
as a *comment*: the old `capabilities.yml` note said its checks carried "the identical shape the
embedded reference floor (`Inheritance.referenceChecks`) inherits onto every `game` product", and
`0010-production-journey-floor.md` §4 recorded the same duplication for
`gameplay:production-journey`. Two authorities agreeing by habit is exactly the shape this org
already treats as a defect elsewhere — it is why `scripts/generate-projections` exists in
`FS-GG/.github`, after seven separate repairs to hand-written copies of one rule.

There was a second, compounding gap: **no consumer could resolve the published package at all.** Its
payload shipped under `contentFiles/any/any/`, which a modern SDK-style `PackageReference` does not
materialize into a working tree, and the package carried no `build`/`buildTransitive` targets. The
only documented adoption route was copying `samples/sdd-reference-gate-set/` by hand out of a clone
of this repository — which is not a resolution of the package, cannot be pinned, and cannot be
re-run.

## Decision

### 1. The embedded F# profile is the ONE authoritative source

`ReferenceProfile.checksFor : TemplateProfile -> Check list`
(`src/FS.GG.Governance.Inheritance/ReferenceProfile.fs`) is the single place the org profile's rule
set is authored. It is the table that was `Inheritance.referenceChecks`, moved to a named home a
decision record, a projection, and a gate can all cite; `Inheritance.referenceChecks` now calls it,
so enforcement reaches the same records through the same `Gates.buildRegistry` projection as before.

*Rejected:* making the YAML authoritative and having the runtime load and validate it. That would
supersede ADR-0009 §1 and reintroduce precisely the I/O edge it rejected.

### 2. The published YAML is a GENERATED artifact derived from that source

The `game` profile's gameplay entries in `samples/sdd-reference-gate-set/.fsgg/capabilities.yml` are
a marked, machine-authored region rendered by `ReferenceProfile.capabilitiesRegion`, carrying
provenance in the file itself (source, projection, regeneration command, and the gate that guards
it). Regenerate with:

```
BLESS_REFERENCE_GATE_SET=1 dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests
```

— the same deliberate, opt-in bless idiom this repo already uses for its public-surface baselines
(`BLESS_SURFACE=1`).

The surrounding prose stays hand-authored. The generated region is the machine-owned *fact*; the
comment above it is human *context*. Mixing the two would make every explanatory edit look like
drift.

*Rejected:* keeping both copies with an explicit resolution contract that says which wins. #386
already states that convention-kept duplicates are not an acceptable end state; that option would
re-file itself later.

### 3. A gate fails closed when the two disagree — in every direction

A derivation nothing checks is the convention this record exists to end, so the derivation is
checked by `ReferenceGateSetGuardDerivation`
(`tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetDerivationTests.fs`):

- **D1** the region's bytes equal the projection's bytes;
- **D2** the region **parsed back by the real `Config.Loader`** yields `Check` records
  field-identical to `ReferenceProfile.checksFor`. This is what makes the YAML token vocabulary in
  `ReferenceProfile.fs` safe to have at all: `Config` exposes no public renderer for
  `Cost`/`EnvironmentClass`/`Maturity`, so the projection necessarily restates those closed sets — and
  D2 means a restatement that disagrees with the parser reds a gate instead of publishing a quietly
  wrong profile;
- **D3** every profile in `boundProfiles` has a region, **and** the published set declares no check
  in a floor-owned domain that the code does not bind — so a second hand-authored authority cannot
  reappear beside the generated one;
- **D4** a missing, duplicated, or inverted marker pair is an **error**, never a silent pass. Fail
  closed, executed rather than asserted in prose.

The test list is named `ReferenceGateSetGuardDerivation` deliberately: `pack-reference-gate-set.fsx`
runs `--filter FullyQualifiedName~ReferenceGateSetGuard` as its pre-pack gate, so a drifted
derivation does not merely go red in CI — **the pack refuses to produce the package at all**
(measured: exit 1, zero `.nupkg` written).

### 4. The consumer resolution contract is an explicit MSBuild verb

The package now ships
`buildTransitive/FS.GG.Governance.ReferenceGateSet.targets`, defining one target:

```
dotnet restore
dotnet msbuild -t:FsggResolveReferenceGateSet
```

It copies the package's published `.fsgg/` into the consuming repository
(`FsggReferenceGateSetDestination`, default `$(MSBuildProjectDirectory)/.fsgg`) and fails loudly if
the payload is empty rather than reporting a resolution that produced nothing.

`buildTransitive/` rather than `build/`: buildTransitive assets are imported by the direct consumer
*and* flow to projects referencing it transitively, and shipping the same file name under both
folders makes MSBuild import both and fail on the duplicate target.

Deliberately **not** hooked to `Build` or `Restore`. Resolving writes into the consumer's source
tree; a build that silently rewrites tracked files is a worse contract than a missing one. The
consumer runs the verb, and the write lands in its diff where it can be reviewed.

*Rejected:* a `dotnet tool` verb. It would need a tool install, a second published artifact, and a
release cadence, to do what one MSBuild target already does inside the restore the consumer performs
anyway.

### 5. ADR-0009 §Decision 3's non-lowerable property is preserved — and proven

This is the constraint that defeated the package route the first time, so it is answered rather than
dropped: **resolution is distribution, never enforcement.** The enforced floor stays embedded. A
product may delete, edit, downgrade, or never resolve the `.fsgg/` this target writes; that changes
what the product **declares**, and cannot change what it **inherits**.

`ReferenceGateSetResolution` R4 executes the escape attempt against a real installed copy: it binds
the resolved profile to `templateProfile: game`, strips the entire generated gameplay region out of
it, loads it through `Config.Loader.loadAndValidate`, and asserts the product still inherits
`gameplay:fr-covered` and `gameplay:production-journey` at `block-on-ship`.

## Consequences

- `ReferenceProfile` is new public surface on `FS.GG.Governance.Inheritance`
  (`surface/FS.GG.Governance.Inheritance.surface.txt` updated). `Inheritance`'s own five-function
  surface is unchanged, so every existing caller is unaffected.
- `FS.GG.Governance.ReferenceGateSet` goes to **1.6.0** (MINOR, ADR-0055): the package shape grows
  by a first-class MSBuild asset, and exact-pin consumers re-pin deliberately to adopt the
  resolution verb. The `game` floor's check *values* are byte-identical to 1.5.0's — only the
  surrounding comments moved — so no consumer's gate set changes.
- `#385` can now be authored against a named source of truth. Its AC1/AC2 ("compose one profile")
  target `ReferenceProfile.checksFor`; its AC4 (ordinary-repository resolution) is discharged by
  `FsggResolveReferenceGateSet`.
- **The generated-product route (`#385` AC3) is cross-repo and is NOT decided here.** `.fsgg/` is
  authored by `fsgg-sdd init`, owned by `FS-GG/FS.GG.SDD`, which today writes only
  `.fsgg/project.yml` and `.fsgg/sdd.yml` and treats the three Governance files as
  `optionalGovernance*` / `notEvaluated`. Sequenced as
  [FS-GG/FS.GG.SDD#845](https://github.com/FS-GG/FS.GG.SDD/issues/845), which carries the resolution
  verb this record defines and leaves the mechanism to that repository's owner. It is deliberately
  **not** a blocker of this record: because resolution is distribution and not enforcement, a
  generated product that has not yet adopted a route still inherits the floor.
- The four rule packs' incompatible finding shapes, and the two packs with no production consumer,
  remain `#385` implementation work. This record does not address them.
