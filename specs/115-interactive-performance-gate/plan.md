# Implementation Plan: Interactive Performance Evidence Gate

**Spec**: [spec.md](./spec.md) · **Item**: FS-GG/FS.GG.Governance#306 · **Tier**: 1

## Design

1. Adopt `FS.GG.Contracts` 7.0.0 centrally and reference it from the handoff adapter.
2. Update the adapter `.fsi` first: handoff v2 projections plus a pure `Performance` evaluator with
   closed evaluation states and independently recomputed measurements.
3. Add semantic tests for malformed/mismatched evidence, M0 failure, M5/M6 pass, live/headless
   limitation, contamination, stale evidence, mixed hosts, and the empty profile-aware path.
4. Implement strict total JSON parsing and pure evaluation. Consumer maps each evaluation into a
   deterministic preselected gate whose description includes failures and remediation.
5. Validate the gate JSON carries the description verbatim and update the reference gate-set
   guidance for bounded CI versus capable live-compositor lanes.
6. Regenerate dependency locks and public-surface baselines; run focused and full Debug/Release
   gates, pack, and verify packaged reference assets.

## Constitution Check

- Spec → FSI → semantic tests → implementation is preserved.
- Public surface lives in `.fsi`; the evaluator is pure and total.
- No new third-party parser or runtime dependency is introduced.
- Malformed and environment-limited evidence fails explicitly with actionable diagnostics.
