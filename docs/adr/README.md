# Architecture Decision Records — local index

FS.GG.Governance follows **org-level** ADRs that live in the shared
[`FS-GG/.github`](https://github.com/FS-GG/.github) repository, not in this repo. This page
is a local pointer so a reader can resolve an `ADR-00NN` citation without leaving the repo
(100/M-ARCH-3 low add-on). It is an index, not a source of truth — the canonical text is in
`FS-GG/.github`.

| ADR | Subject | Where it binds this repo |
|---|---|---|
| **ADR-0002** | SDD → Governance handoff **consumer** (`governance-handoff@1`) | `Adapters.SddHandoff`; the route handoff-gate |
| **ADR-0003** | Package IDs are permanent; tool identity / planned `fsgg` multiplexer | package IDs never change; the single-`fsgg`-owner fence (this feature) |
| **ADR-0005** | `.fsgg/` slot ownership (SDD `project.yml` / Governance `governance.yml`, no shadowing) | `Config` loader + the four `.fsgg` files |
| **ADR-0007** | Reference-gate-set version is **schema-derived** | `FS.GG.Governance.ReferenceGateSet` packaging |
| **ADR-0012** | Byte-identical pack; §5 NuGet listing metadata | `publish.yml`; `Directory.Build.local.props` listing metadata |
| **ADR-0013** | Trusted Publishing (OIDC) — no long-lived key | `publish.yml` nuget.org leg |

To read an ADR: see `FS-GG/.github` (the org governance/protocol repo). When a new
org-level ADR is cited from this repo's code or specs, add a row here.
