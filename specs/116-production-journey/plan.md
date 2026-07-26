# Implementation Plan: Production-Journey Governance Floor

**Branch**: `item/324-production-journey-gate` | **Date**: 2026-07-26 | **Spec**: `spec.md`

## Summary

Extend the pure SDD handoff reader with typed journey readiness, validate the SDD 0.30.x required
field fail-closed, project an actionable journey gate, and add the same gate as a non-lowerable
`game` profile/reference-set floor.

## Technical Context

**Language/Version**: F# / .NET 10

**Primary Dependencies**: `FS.GG.Contracts` 7.0.0, `System.Text.Json`, existing Governance gate model

**Testing**: Expecto semantic tests over public `.fsi` surfaces, the SDD 0.30 producer golden, and
synthetic disclosed negative JSON

**Constraints**: Pure/total adapter; no SDD/Game assembly reference; deterministic descriptions;
locked restore; only additive v2 JSON evolution.

## Constitution Check

- Specify before signatures, semantic tests, and implementation.
- Public journey types and projection are declared in `.fsi`.
- Parsing/projection stays pure; file I/O remains at host edges.
- The producer golden is copied from SDD commit `25c7380`; synthetic negative shapes are named.
- Malformed and incompatible states fail closed with actionable diagnostics.

## Design

1. Add typed `GeneratorVersion`, optional `JourneyReadiness`, and closed provenance disposition to
   `Model`.
2. Require journey counts for SDD generator 0.30+; preserve an explicit compatibility-window
   absence for older producers.
3. Correlate readiness blocking diagnostic ids with handoff diagnostic related ids and project
   `gameplay:production-journey:<work>` as warn/block-on-ship.
4. Keep the static inherited `gameplay:production-journey` floor separate from per-work evidence;
   the handoff gate supplies the current evidence verdict.
5. Add the reference check and bump the content package from 1.4.0 to 1.5.0.

## Verification

Run focused adapter/inheritance/reference-set tests, then locked solution restore/build/test and the
reference package guard/pack.
