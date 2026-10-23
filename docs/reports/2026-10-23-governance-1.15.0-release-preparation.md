# Governance 1.15.0 release preparation

Issue FS-GG/FS.GG.Governance#423 (GOV-423-C2 execution join). This prepares the source for one `publish.yml`
release through the `v*` tag route, the same route as 1.13.0 and 1.14.0. It publishes nothing and creates no tag.
Publication is a separate, separately admitted effect.

Predecessor: tag `v1.14.0` / `f97de157`.

## Why a release is owed

In Governance 1.14.0, `fsgg-governance verify` in provider mode stops after planning. Every valid plan exits 1 with
`provider planning completed; execution and evidence acceptance are not connected`. This was observed against the
published tool in FS-GG/FS.GG.SDD#928 C3.1-03. This change connects the published tool's provider mode to
GateExecution and evidence acceptance. The change ships through the existing `FS.GG.Governance.Cli` tool closure
(`VerifyCommand`).

## Version decision

| Artifact | Source version | Published | Decision |
|---|---|---|---|
| `FS.GG.Governance.Cli` | **1.15.0** (was 1.14.0) | 1.14.0 | MINOR. Provider-mode `verify` now executes and accepts evidence instead of always exiting 1. Legacy `verify` and every other verb are unchanged. |
| `FS.GG.Governance.FSharpSurfaceCommand` | **1.15.0** (was 1.14.0) | 1.14.0 | Lockstep with the CLI. `publish.yml` packs it at the resolved CLI version. |
| `FS.GG.Governance.CodeChecks` | 1.0.1 | 1.0.1 | unchanged |
| `FS.GG.Governance.ReferenceGateSet` | 1.8.0 | 1.8.0 | unchanged |
| `FS.GG.Governance.Config` | 0.3.0 | 0.3.0 | unchanged (published only by `scope=config`) |
| `FS.GG.Governance.Kernel` / `Adapters.Spi` | 0.1.1 / 0.1.1 | same | unchanged |

`Cli.fsi` is unchanged. The change is to the bundled Verify host's provider mode, plus its internal library surface
(`VerifyCommand` is bundled, not a package). The bundled SDD `Commands`/`Artifacts`/`Knowledge` packages stay at
2.3.0. Their schema-2 provenance, policy and admission codecs are unchanged in SDD 2.4.0, which is why 1.14.0 already
admitted SDD 2.4.0 provenance (decision recorded in the GOV-423 plan).

## Publication notes for the admitting owner

* The route is the same as 1.14.0. A `v*` tag push is a real publish from the CLI `<Version>`. Before the push:
  * Observe on both feeds that `FS.GG.Governance.Cli` 1.15.0 and `FS.GG.Governance.FSharpSurfaceCommand` 1.15.0 are
    absent.
  * Confirm that `v1.15.0` is absent from the remote tag namespace.
* An `all` run skips the unchanged packages as duplicates.
* Acceptance after publication is a cold install of the published tool from both feeds, with a byte comparison.
  Re-run the SDD#928 C3.1-03 journey with a fixture whose `run` writes the declared `fsgg.governance-handoff@2.0.0`
  evidence, plus its missing, stale and mismatched variants. A green workflow alone is not acceptance.

## What the release does not ship

* The physical blocked-read and uncertain-close cases, whole-worker teardown with a named supplier, and the runtime
  carrier of the outer ends remain open.
* The release does not qualify any ecosystem provider.
* Real held-cwd execution needs Linux-x64 with glibc 2.40 or later. With an older glibc, every provider run is
  reported as not launched and blocks.
