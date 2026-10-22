# Governance 1.13.0 release preparation

Issue FS-GG.Governance#464. This is source preparation for one `publish.yml`
release. It publishes nothing and creates no tag; publication is a separate,
separately admitted effect.

Base: `main` `c80e8903d99ddb41fa63d577eb07efa874cfa3b4` (contains #442 at
`09a20140` and #462 at `c80e8903`). Predecessor: tag `v1.12.1` /
`388819d0e060c11f53e5ca2df3277a54a13f9e75`.

## Version decision

| Artifact | Source version | Published | Decision |
|---|---|---|---|
| `FS.GG.Governance.Cli` | **1.13.0** (was 1.12.1) | 1.12.1 | MINOR |
| `FS.GG.Governance.ReferenceGateSet` | 1.8.0 (`pack-reference-gate-set.fsx`, set by #442) | 1.7.0 | MINOR, already selected in source |
| `FS.GG.Governance.Config` | 0.3.0 | 0.3.0 | unchanged, already published |
| `FS.GG.Governance.FSharpSurfaceCommand` | 1.12.1 | 1.12.1 | unchanged |
| `FS.GG.Governance.Kernel` / `Adapters.Spi` / `CodeChecks` | 0.1.1 / 0.1.1 / 1.0.0 | same | unchanged |

Why MINOR for the CLI, not PATCH or MAJOR. The `surface/*.surface.txt`
baselines changed by 1801 insertions and zero deletions
(`git diff --stat v1.12.1 HEAD -- surface`): Config (`CapabilityBindings`),
GateExecution (bounded CurrentHost sessions and the cooperative held-cwd
profile, #447 and #453), GateRun (complete neutral capability request
planning, #446), CommandHost and Adapters.SddHandoff (strict provider source
projections, #452). All of these are in the CLI package's runtime closure.
Additive public API on shipped assemblies is a MINOR under the repo's own
precedent (for example 1.5.0, "additive API + enforcement fixes", #229). Nothing is removed or changed, the `fsgg-governance`
command surface (`Cli.fsi`) is unchanged apart from Fantomas formatting, and
legacy callers keep their behavior, so there is no basis for MAJOR. Nothing is
only a bug fix, so PATCH under-states the delta.

Unchanged packages keep their published versions. Their only source changes
since publication are the Fantomas 8 formatting pass (#427); they carry no
behavior change, so no new identity is warranted and the publisher's existing
`--skip-duplicate` / accepted-bytes handling applies.

## What the release does not ship

`FS.GG.Governance.VerifyCommand` (the #448, #449, #450 and #462 provider
selector, capture and package-backed loader work) is `IsPackable=false` and is
neither in the CLI closure nor selected by `publish.yml`. A release from
`c80e8903` therefore *contains* #462 in its source anchor but publishes no
bytes that carry it. GOV-423-C2 remains open in the roadmap. Packaging the
Verify host is a separate delivery.

## Publication notes for the admitting owner

* `publish.yml` resolves the version from the CLI `.fsproj`. A `v*` tag push or
  a published GitHub Release is a real publish; a dispatch with `version` is a
  real publish; a dispatch without `version` packs and pushes nothing.
* The `all` scope also runs `publish-config`. Config 0.3.0 is already on both
  feeds with nuspec repository commit `8b3a6d41`; the job packs again at the
  release SHA, so its `collision` check compares unequal payloads and is
  expected to refuse (`pre-existing version payload conflict`). The other jobs
  are independent and are not cancelled by it. The decision on how to handle
  this is recorded on #464, not made here.
* Strict occupancy is not built into the `all` route (only Config has an
  observation step). Observe the exact versions on both feeds and the complete
  remote tag namespace immediately before the push.
