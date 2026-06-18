# FS.GG.Governance

Optional rule, evidence, and route-explanation tooling for the
[FS-GG](https://github.com/FS-GG) projects, developed as a normal tool product
using standard [Spec Kit](https://github.com/github/spec-kit).

This repository is the **governance** half of the FS-GG split. The other half,
the UI runtime, lives in
[**FS.GG.Rendering**](https://github.com/FS-GG/FS.GG.Rendering). The two are
deliberately separate so that rendering can be built, tested, documented,
packaged, and released without an experimental governance platform.

## Operating rule

> Governance tooling may *inspect* rendering; rendering must never *require*
> governance tooling to build, test, document, package, or release.

Generic code here must not assume rendering's package IDs, template names, target
names, or directory layout. Rendering is treated as one external customer, not as
this tool's internal shape.

## Scope

This project may own deterministic fact and rule evaluation, explanation and
diagnostics primitives, evidence-freshness helpers, route-analysis helpers,
package/docs/template drift analyzers, support-bundle tooling, and optional Spec
Kit extensions. It does **not** own rendering product identity, package IDs, docs
URLs, template profiles, design-system choices, controls, themes, or release
decisions.

## First useful product

The first target is a compact rule/evidence helper library with nominal IDs and
diagnostics, deterministic fact storage, fixed-point rule evaluation, provenance
for derived facts, JSON-friendly explanation output, and simple evidence-freshness
predicates — with no dependency on FAKE, git, filesystem scanning, Skia, NuGet
publishing, template profiles, or rendering project paths.

## Status

**Bootstrapping (Stage G1).** Fresh Spec Kit repository established; the first
narrow tool slice is not yet implemented. See the cross-repo plan below.

## Design & plans

The governance design notes now live in this repository under
[`docs/governance-design/`](docs/governance-design/index.md) (moved out of the org
`.github` repository). The remaining cross-repo plans still live in `.github`:

- [Governance design notes](docs/governance-design/index.md) — comprehensive kernel/adapter/rule design
- [Implementation plan (Spec Kit), 2026-06-18](docs/2026-06-18-governance-kernel-speckit-implementation-plan.md) — the design decomposed into ordered Spec Kit features (F01–F13)
- [Governance project scope & adoption bar](https://github.com/FS-GG/.github/blob/main/docs/governance-project.md)
- [Governance implementation plan (Stages G1–G5)](https://github.com/FS-GG/.github/blob/main/docs/governance-implementation-plan.md)
- [FS.GG project split index](https://github.com/FS-GG/.github/blob/main/docs/index.md)

## Workflow

Standard Spec Kit with the
[`fsharp-opinionated`](https://github.com/EHotwagner/speckit-fsharp-tooling)
preset. Use the `/speckit-*` skills to specify, plan, and implement features.

There is no evidence-audit or DAG-validation machinery in this repository: the
governance constitution
([`.specify/memory/constitution.md`](.specify/memory/constitution.md)) follows the
same lightweight, standard-Spec-Kit shape as the sibling
[FS.GG.Rendering](https://github.com/FS-GG/FS.GG.Rendering) constitution.

## License

[MIT](LICENSE)
