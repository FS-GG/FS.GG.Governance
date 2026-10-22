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
| `FS.GG.Governance.FSharpSurfaceCommand` | **1.13.0** (was 1.12.1) | 1.12.1 | lockstep with the CLI |
| `FS.GG.Governance.CodeChecks` | **1.0.1** (was 1.0.0) | 1.0.0 | PATCH |
| `FS.GG.Governance.ReferenceGateSet` | 1.8.0 (`pack-reference-gate-set.fsx`, set by #442) | 1.7.0 | MINOR, already selected in source |
| `FS.GG.Governance.Config` | 0.3.0 | 0.3.0 | unchanged, already published |
| `FS.GG.Governance.Kernel` / `Adapters.Spi` | 0.1.1 / 0.1.1 | same | unchanged |

`publish.yml` packs `FSharpSurfaceCommand` at the resolved CLI version
(`-p:Version=<resolve-version output>`), so an `all` run publishes it as 1.13.0
regardless of its project file; its `<Version>` and the two install-version
literals in `FSharpSurfaceCommandTests.fs` are moved to match (the v1.12.1
release was likewise lockstep, `bd09e437`). That package's closure also gained
Config `CapabilityBindings` and the DesignChecks/SurfaceChecks changes.

Why MINOR for the CLI, not PATCH or MAJOR. The `surface/*.surface.txt`
baselines changed by 1801 insertions and zero deletions
(`git diff --stat v1.12.1 HEAD -- surface`; 1163 of those lines are in
assemblies of the CLI closure, the other 638 are the unshipped VerifyCommand
baseline): Config (`CapabilityBindings`),
GateExecution (bounded CurrentHost sessions and the cooperative held-cwd
profile, #447 and #453), GateRun (complete neutral capability request
planning, #446), CommandHost and Adapters.SddHandoff (strict provider source
projections, #452). All of these are in the CLI package's runtime closure.
Additive public API on shipped assemblies is a MINOR under the repo's own
precedent (for example 1.5.0, "additive API + enforcement fixes", #229). Nothing is removed or changed, the `fsgg-governance`
command surface (`Cli.fsi`) is unchanged apart from Fantomas formatting, and
legacy callers keep their behavior, so there is no basis for MAJOR. Nothing is
only a bug fix, so PATCH under-states the delta.

Dependency movement inside the CLI closure (`Cli/packages.lock.json`):
FS.GG.Contracts 7.0.0 -> 7.6.0 (same major), Config 0.2.0 -> 0.3.0,
FSharp.Core 10.1.302 -> 10.1.401, Adapters.Spi 0.1.0 -> 0.1.1. None changes the
MINOR conclusion.

CodeChecks: since 1.0.0 was published (`17c54591`) its declared dependencies
moved (FSharp.Compiler.Service 43.11.302 -> 43.12.401, FSharp.Core 10.1.302 ->
10.1.401, #434) in addition to the Fantomas 8 formatting pass (#427). Publishing
that source under 1.0.0 would be silently dropped by `--skip-duplicate` and leave
the feed disagreeing with the tagged source, so it moves to 1.0.1.

Kernel and Adapters.Spi have only the Fantomas 8 formatting change since their
publication (`fafab248`, `b20edb3c`) and no declared dependency change, so they
keep 0.1.1; their jobs retain the org-accepted bytes for the public push by
design. Config and ReferenceGateSet are covered above.

## What the release does not ship

`FS.GG.Governance.VerifyCommand` (the #448, #449, #450 and #462 provider
selector, capture and package-backed loader work) is `IsPackable=false` and is
neither in the CLI closure nor selected by `publish.yml`. A release from
`c80e8903` therefore *contains* #462 in its source anchor but publishes no
bytes that carry it. GOV-423-C2 remains open in the roadmap. Packaging the
Verify host is a separate delivery.

## Publisher repair found by the dry run

A non-pushing dispatch (`scope=all`, no `version`) on the first head of this PR
(run `39753274513`) failed in `resolve-version`: every `setup-dotnet` step in
`publish.yml` requested `10.0.x`, but `global.json` pins SDK `10.0.401` with
`rollForward: disable` (#382), so `dotnet msbuild` found no compatible SDK
before any pack or push. `gate.yml` already uses the literal `10.0.401`.
This release therefore also pins the ten `publish.yml` `setup-dotnet` steps to
`10.0.401`, matching `gate.yml`. No other publisher logic changes;
`tests/config-package-smoke/publish_contract.py --mutations` still passes.

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
  observation step). Immediately before the push observe, on both feeds,
  that `FS.GG.Governance.Cli` 1.13.0, `FS.GG.Governance.FSharpSurfaceCommand`
  1.13.0, `FS.GG.Governance.CodeChecks` 1.0.1 and
  `FS.GG.Governance.ReferenceGateSet` 1.8.0 are absent, and enumerate the
  complete remote tag namespace requiring `v1.13.0` absent.
