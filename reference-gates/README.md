# `reference-gates/` — the org's reusable F# constitution profile

This directory is the **front door** to the composed F# constitution profile: where to find it, how
to consume it, and what it contains. It is deliberately **not** a second copy of the profile's
definition. The whole point of
[`docs/decisions/0011`](../docs/decisions/0011-generated-reference-gate-set.md) and
[`docs/decisions/0012`](../docs/decisions/0012-composed-fsharp-constitution-profile.md) is that this
org profile has exactly **one** authority; a table restated here would be the failure mode those
records exist to end. Every table below is a pointer, and the file it points at is the fact.

## Why this directory exists at all

Epic [#367](https://github.com/FS-GG/FS.GG.Governance/issues/367) shipped the F# constitution as four
rule packs — #366, #368, #369, #370 — and every one of them declared `reference-gates/` in its
touch-set. None landed here:

```
$ git ls-tree -r origin/main --name-only | grep -c '^reference-gates/'
0
```

That count was the measured evidence for the epic's one unowned acceptance line: *"integrated into
one reusable F# profile/reference gate consumed by generated products and ordinary repositories."*
[#385](https://github.com/FS-GG/FS.GG.Governance/issues/385) composed them, and this README is the
place all four children were reaching for.

## Where the profile actually lives

| what | where | why there |
|---|---|---|
| **The composed profile** — packs, rule identities, authoring child, maturity, rationale, collision resolution | [`src/FS.GG.Governance.SurfaceChecks/Profile.fs`](../src/FS.GG.Governance.SurfaceChecks/Profile.fs) (`.fsi` beside it) | the lowest project all four packs reach, and the finding vocabulary they already share — so composing adds no package and no layer |
| **The gate binding** | [`ReferenceProfile.checksFor`](../src/FS.GG.Governance.Inheritance/ReferenceProfile.fs), profile `fsharp-constitution` | binds `Profile.checks`; it does not restate them |
| **The published YAML** | the generated `fsgg-reference-profile:fsharp-constitution` region of [`samples/sdd-reference-gate-set/.fsgg/capabilities.yml`](../samples/sdd-reference-gate-set/.fsgg/capabilities.yml) | derived, with provenance, by the same projection the `game` floor uses |
| **The package** | `FS.GG.Governance.ReferenceGateSet`, packed by [`pack-reference-gate-set.fsx`](../pack-reference-gate-set.fsx) | one packaging path, reused — not a second one |

## Consuming it

Both consumer populations use the **same published package and the same MSBuild verb**; what differs
is the state of the destination.

**An ordinary repository** — nothing scaffolded a `.fsgg/` for it, so the destination is empty:

```xml
<PackageReference Include="FS.GG.Governance.ReferenceGateSet" Version="1.7.0" />
```

```bash
dotnet restore
dotnet msbuild -t:FsggResolveReferenceGateSet
```

**A generated product** — `fsgg-sdd init` already owns the destination and has written `project.yml`,
`sdd.yml` and four more files into it. Resolve without clobbering another owner's files:

```bash
fsgg-sdd init --root .
dotnet msbuild -t:FsggResolveReferenceGateSet -p:FsggReferenceGateSetOverwrite=false
```

Both routes are executed end-to-end against the **installed package** from a clean consumer (cleared
NuGet sources, fresh `NUGET_PACKAGES`) by `ReferenceGateSetResolution` R6 and R7 in
[`tests/FS.GG.Governance.ReferenceGateSet.Tests/`](../tests/FS.GG.Governance.ReferenceGateSet.Tests/ReferenceGateSetResolutionTests.fs).
R7 runs the real `fsgg-sdd init` when it is on `PATH`, and otherwise the captured-real scaffold in
[`fixtures/generated-product-scaffold/`](../fixtures/generated-product-scaffold/README.md) — disclosed
at runtime, never skipped.

> **Resolution is distribution, not enforcement.** The inherited floor is embedded in the governance
> runtime and read from no file. Editing, downgrading, deleting or never resolving a `.fsgg/` changes
> what a product *declares*; it cannot change what it *inherits* (ADR-0009 §Decision 3, preserved by
> `docs/decisions/0011` and executed as `ReferenceGateSetResolution` R4).

## What the profile contains

Four gates in the `fsharp` domain, one per pack. Command-free by design — each pack's own sensing and
the handoff evidence satisfy them, so a repository that resolves the set without adopting this repo's
tooling has no dangling command reference.

| gate | pack | authored by | maturity | posture |
|---|---|---|---|---|
| `fsharp:public-surface` | curated `.fsi` surfaces, visibility, XML docs, surface baselines | [#366](https://github.com/FS-GG/FS.GG.Governance/issues/366) | `warn` | sweeps every compiled project unconditionally, so it is a dated migration |
| `fsharp:idiomatic-simplicity` | idiomatic simplicity, evidence-bound exceptions | [#368](https://github.com/FS-GG/FS.GG.Governance/issues/368) | `warn` | evaluated by the `fsgg verify` sweep, over the sources a repository declares in `.fsgg/fsharp-simplicity.json` |
| `fsharp:effect-boundary` | functional core / effect edge | [#369](https://github.com/FS-GG/FS.GG.Governance/issues/369) | `block-on-pr` | applies only where an author opted a transition in with an in-source marker |
| `fsharp:evidence-boundary` | behavior evidence, contract goldens, safe failure | [#370](https://github.com/FS-GG/FS.GG.Governance/issues/370) | `warn` | evaluated by the `fsgg verify` sweep, where a repository declares an obligation in `.fsgg/evidence-boundary.json` |

The exact rule identities each pack contributes are `Profile.ruleIds`; they are the packs' own codes,
unchanged. `Profile.ruleOwner` maps any identity back to its pack and `Profile.authoringItem` to its
child. **Do not copy that inventory here** — `ReferenceProfileComposition` C1 checks the profile
against what the real evaluators emit, and a copy in this file would be checked by nothing.

### Why one pack blocks and three warn

It is a decision, not an accident, and the reason is **applicability** rather than severity taste:
#369 only fires on work that declared itself in scope, so blocking it cannot surprise an adopter,
while #366 governs every compiled project and would block every repository on adoption day. Each
reason is carried as data in `Profile.maturityRationale`, and
`docs/decisions/0012` §4 records the alternative that was rejected. None of the four is a permanent
blanket exemption: the dated route to blocking is raising the profile's own maturity and
republishing.

## Changing the profile

1. Edit `src/FS.GG.Governance.SurfaceChecks/Profile.fs` — the rule table, a maturity, or a pack.
2. Regenerate the published region:
   `BLESS_REFERENCE_GATE_SET=1 dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests`
3. Bump `packageVersion` in `pack-reference-gate-set.fsx` (and the pin in
   `docs/tutorials/adopter-onboarding.md` and `expectedVersion` in the package tests).
4. Run the gates: `ReferenceProfileComposition` (the composition), `ReferenceGateSetGuard` G1–G7 and
   `ReferenceGateSetGuardDerivation` D1–D4 (the published set), `ReferenceGateSetResolution` R1–R7
   (both consumer routes).

A hand edit to the generated region reds D1 and `pack-reference-gate-set.fsx` then **refuses to pack
at all** — the derivation gate is its pre-pack filter.

**Adding a fifth pack or a new bound profile fails closed on purpose.** The bless path regenerates
every profile in `boundProfiles`, but it can only *replace* a marker pair, never create one: a newly
bound profile reds D1/D3 until a human places the markers and the hand-authored context around them.
A generator picking its own insertion point in a consumer's file would be the worse contract.
