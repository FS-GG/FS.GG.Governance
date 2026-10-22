# Governance 1.14.0 release preparation

Issue FS-GG/FS.GG.Governance#470. Source preparation for one `publish.yml` release (`v*` tag route, as 1.13.0).
It publishes nothing and creates no tag; publication is a separate, separately admitted effect.

Base: `main` `4b2604d5` (contains #469). Predecessor: tag `v1.13.0` / `432de033`.

## Why a release is owed

Between `v1.13.0` and `4b2604d5` no shipped project changed: the delta was `VerifyCommand` (`IsPackable=false`,
in no published artifact), its tests and the roadmap. A release from that SHA would have published byte-equivalent
packages that carry neither #462 nor #469. #470 packages the Verify host through the existing
`FS.GG.Governance.Cli` tool (`fsgg-governance verify <args>`), which is new shipped surface.

## Version decision

| Artifact | Source version | Published | Decision |
|---|---|---|---|
| `FS.GG.Governance.Cli` | **1.14.0** (was 1.13.0) | 1.13.0 | MINOR: new `verify` verb and a larger bundled closure; nothing removed or changed for existing verbs |
| `FS.GG.Governance.FSharpSurfaceCommand` | **1.14.0** (was 1.13.0) | 1.13.0 | lockstep with the CLI (`publish.yml` packs it at the resolved CLI version; the project file and the two install-version literals follow) |
| `FS.GG.Governance.CodeChecks` | 1.0.1 | 1.0.1 | unchanged |
| `FS.GG.Governance.ReferenceGateSet` | 1.8.0 | 1.8.0 | unchanged |
| `FS.GG.Governance.Config` | 0.3.0 | 0.3.0 | unchanged (published only by `scope=config`) |
| `FS.GG.Governance.Kernel` / `Adapters.Spi` | 0.1.1 / 0.1.1 | same | unchanged |

`Cli.fsi` (the CLI's public .NET surface) is unchanged; the new verb is dispatched at the `Program` edge, so the
change is to the tool's behaviour and payload, not to a library API. The only other editorial change to the
dispatcher is its missing-command help text, which now lists `verify`.

## Publication notes for the admitting owner

* Same route as 1.13.0: a `v*` tag push is a real publish from the CLI `<Version>`; Config is not selected by an
  `all` run. Before the push observe on both feeds that `FS.GG.Governance.Cli` 1.14.0 and
  `FS.GG.Governance.FSharpSurfaceCommand` 1.14.0 are absent, and enumerate the remote tag namespace requiring
  `v1.14.0` absent. `CodeChecks` 1.0.1, `ReferenceGateSet` 1.8.0, Kernel and Adapters.Spi 0.1.1 already exist and are
  skipped as duplicates.
* The CLI tool package now bundles `FS.GG.Governance.VerifyCommand` and the SDD 2.3.0 `Commands`/`Artifacts`/
  `Knowledge` assemblies; it still declares no package dependency and creates no new package id.
* Acceptance after publication is a cold install of the published tool and a `fsgg-governance verify` run, not the
  green workflow alone.

## What the release does not ship

The 20 physical blocked-read / uncertain-close cases, whole-worker teardown with a named supplier, the runtime
carrier of `FSGG_VERIFY_WORK_SECONDS` / `FSGG_VERIFY_CLEANUP_SECONDS` and the real SDD to Verify to GateExecution
journey remain open (Governance#468, SDD#928 C3.1-03).
