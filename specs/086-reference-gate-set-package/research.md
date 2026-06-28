# Phase 0 Research: Publish the Reference Gate Set as a Content Package

All NEEDS CLARIFICATION resolved. Decisions below are grounded in the existing repo
(079 reference set + G1–G7 guard, 080 `build.fsx`, 085 shared build config) and the spec's
documented assumptions.

## D1 — Content-package mechanism (FR-001/FR-005/FR-007)

**Decision**: ship the four YAMLs under `contentFiles/any/any/.fsgg/` in a NuGet package built
by an SDK-style project with `<IncludeBuildOutput>false</IncludeBuildOutput>` and
`<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>`, no `PackageReference`,
no `Compile` items. Result: a `.nupkg` with **no `lib/` and no dependency group** — installing it
adds no assembly and no governance runtime reference (FR-007/SC-005).

**Rationale**: `IncludeBuildOutput=false` is the canonical way to produce an asset/content-only
package; `contentFiles/any/any/` is the framework-agnostic content path NuGet restores to a stable,
version-relative location (`<globalpackages>/fs.gg.governance.referencegateset/<version>/contentFiles/any/any/.fsgg/`).
A consumer's `git diff --exit-code`-style gate (Templates#14) can point at that folder directly.

**Alternatives considered**:
- *Classic `content/` folder* — ignored by modern `PackageReference`; rejected.
- *`build/`+`.targets` that copy files into the consumer* — imposes MSBuild behavior and a build hook
  on consumers; heavier than "read the restored files," and not needed for a drift comparison. Rejected.
- *`tools/` or a real assembly carrying embedded resources* — violates content-only (FR-007). Rejected.

## D2 — Single source, zero duplication, byte-identity (FR-002/FR-003/SC-002)

**Decision**: the `.fsproj` packs the files **in place** from the sample directory:
`<None Include="../../samples/sdd-reference-gate-set/.fsgg/*.yml" Pack="true" PackagePath="contentFiles/any/any/.fsgg/" />`.
No copy step, no second checked-in set. A guard test unzips the produced `.nupkg` and asserts each
entry's bytes equal the on-disk file's bytes.

**Rationale**: FR-002 forbids a duplicated copy; packing from the canonical path makes the sample
directory the literal source. Byte-identity is then a property of "pack copied the file verbatim,"
which the test verifies against the real artifact (Principle V — real evidence, no fixture).

**Alternatives considered**: a `pre-pack` copy into the project dir (creates a second source that can
drift — exactly what FR-002 forbids). Rejected.

## D3 — Pack gated on G1–G7 (FR-004/US2)

**Decision**: packing is driven by `pack-reference-gate-set.fsx`, which **first runs the existing
reference-set guard** (`dotnet fsi build.fsx test` filtered to the ReferenceGateSet guard, or a direct
`dotnet test` of that project) and **aborts with a non-zero exit before `dotnet pack`** if it is red.
The CI `reference-gate-set-pack` job runs the same script, so a red guard blocks publish in CI too.

**Rationale**: FR-004/US2 require that the shipped artifact cannot be produced when its frozen
invariants fail — "the tested artifact and the shipped artifact are the same thing." Gating in the one
script that everyone (local + CI) uses keeps that property in a single place and fails loud (Principle VI).

**Alternatives considered**: an MSBuild `BeforeTargets="Pack"` dependency on the test project — F# test
projects don't expose a clean "fail pack if tests fail" target, and it would couple pack to the test
build graph awkwardly; the script is plainer and matches the repo's `build.fsx` convention. Rejected.

## D4 — Version-derivation rule (FR-006/SC-003) — **the one documented informed-guess from the spec**

**Decision**: the package version is the **four contained `schemaVersion` values composed as a 4-segment
NuGet version in a fixed, documented file order**:

```
Version = {governance}.{capabilities}.{policy}.{tooling}
        =      1      .      2       .   1    .   1        ->  1.2.1.1   (current)
```

File order is the canonical bundle order (manifest root first, then the order of the README "four files"
table): `governance.yml` → `capabilities.yml` → `policy.yml` → `tooling.yml`. A bump to **any one**
file's `schemaVersion` changes **exactly one** segment, so the package version is deterministic,
reversible, and **distinguishable on every bump** (SC-003). Consumers pin exact (`[1.2.1.1]`), which
is the intended "pin a coherent set" use (US3).

**Rationale**: the spec's assumption asks for "the highest-fidelity coherent scheme available — the
leading components track the config schema generation, and a bump to any contained `schemaVersion`
produces a distinguishable package version." A 1:1 segment-per-file mapping is the maximal-fidelity
choice: every file's schema generation is independently visible in the version, and distinguishability
is structural (not probabilistic). `governance.yml` (the root manifest that refs the others) leads, so
the leading component tracks the bundle/manifest generation as the spec describes.

NuGet accepts 4-segment versions (`Major.Minor.Patch.Revision`, `System.Version` semantics); exact-pin
ranges work. The derivation lives in `pack-reference-gate-set.fsx` and is read via a regex over the
`schemaVersion:` line of each file (BCL only).

**Alternatives considered**:
- *Single SemVer `MAJOR=max(schemaVersions)=2`, build metadata encodes the tuple* — loses fidelity
  (two different bumps can collide in `MAJOR`/`MINOR`), and build metadata is ignored by version
  precedence, weakening SC-003's distinguishability guarantee. Rejected.
- *Hash of the four files as a suffix* — distinguishable but illegible and not "schema-version derived"
  as FR-006 requires. Rejected.
- *3-segment `gov.caps.(policy*10+tooling)`* — encodes four numbers in three segments with a lossy
  packing that breaks once a schema exceeds 9. Rejected as fragile.

This decision is recorded as an ADR in `FS-GG/.github` (FR-008) so the numbering rule is a registered,
referenceable contract, not an implementation detail.

## D5 — Test/version single-sourcing via a `--print-version` hook (SC-003, Principle V)

**Decision**: `pack-reference-gate-set.fsx` supports `--print-version` (and `--print-command`): it
computes and prints the derived version **without packing**, exactly as `build.fsx` exposes
`--print-command`. The guard test shells `dotnet fsi pack-reference-gate-set.fsx --print-version` and
asserts the value; for SC-003 it copies the four files to a temp dir, bumps one `schemaVersion`, points
the script at the temp dir, and asserts the printed version changed in the expected segment.

**Rationale**: this keeps **one** implementation of the rule (the script) and tests the *actual emitted*
version rather than a re-encoded duplicate of the rule in the test — the same anti-drift pattern 080
used for the build-node bound. Real I/O, no mock.

**Alternatives considered**: duplicate the derivation in F# inside the test (two sources of the rule that
can silently diverge). Rejected — contradicts the single-source spirit of the whole feature.

## D6 — Where the new guard lives (Principle II, build graph)

**Decision**: add `ReferenceGateSetPackageTests.fs` to the **existing**
`tests/FS.GG.Governance.ReferenceGateSet.Tests` project (it is already the home of the reference-set
invariants and is `IsPackable=false`). It needs only BCL + Expecto + the pack script on disk; it does
**not** reference the packaging project (it asserts over the produced `.nupkg` and the script output),
so there is no new project reference into the restore graph.

**Rationale**: co-locating the "is the published artifact the validated artifact" guard with the G1–G7
"is the validated artifact correct" guard keeps the reference-set story in one test project. No `.fsi`,
no public surface (Tier 2 for the test code; the *feature* is Tier 1 by virtue of the package contract).

**Alternatives considered**: a separate `…ReferenceGateSet.Pack.Tests` project — more ceremony for one
file; only worth it if the pack guard needs a different dependency closure, which it does not. Kept as a
fallback if solution-build ordering makes the `.nupkg`-producing step awkward to sequence before tests.

## D7 — Registration & feed (FR-008/SC-006 + feed deferral)

**Decision**: register `FS.GG.Governance.ReferenceGateSet` as a versioned contract in
`FS-GG/.github` `registry/dependencies.yml` (consumed by `FS.GG.Templates`, Templates#14) and
regenerate `docs/registry/compatibility.md`, plus an ADR for the version rule (D4) — all via the
**cross-repo-coordination** skill/protocol (ADR-0001). The **org GitHub Packages feed push is deferred**
(admin-blocked, .github#21): the feature's done-definition is a consumable artifact produced by
`dotnet pack` to `~/.local/share/nuget-local/` (local + CI), per the spec's Distribution-scope
assumption. The registry entry notes the deferred-feed status and links its registry PR.

**Rationale**: the spec explicitly scopes feed provisioning out and registration in; the registry entry
is what makes the package a recognized cross-repo surface that Templates#14 can legitimately pin.

**Alternatives considered**: blocking the feature on the feed (would stall the H3 unblocker behind an
admin task, contradicting the board rationale). Rejected.

## Resolved unknowns summary

| Unknown | Resolution |
|---------|-----------|
| Package kind / how files reach the consumer | D1 — content-only, `contentFiles/any/any/.fsgg/`, no `lib/` |
| Avoid a duplicated copy | D2 — pack in place from `samples/.../.fsgg/*.yml` |
| Prove shipped == tested | D2 (byte-identity test) + D3 (pack gated on G1–G7) |
| Exact version-derivation numbering | D4 — `gov.caps.policy.tooling` 4-segment, `1.2.1.1` today |
| Keep rule single-sourced & tested | D5 — `--print-version` hook + temp-dir bump test |
| Where the new test lives | D6 — existing ReferenceGateSet.Tests project |
| Registry / ADR / feed | D7 — register in FS-GG/.github (ADR-0001); feed push deferred (.github#21) |
