---
schemaVersion: 1
workId: 369-effect-boundary
title: "F# functional-core/effect-edge governance gate"
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/369-effect-boundary/spec.md
publicOrToolFacingImpact: true
---

# F# functional-core/effect-edge governance gate Clarifications

## Source Specification
- work/369-effect-boundary/spec.md

## Clarification Questions
- CQ-001: How are applicability, delivery, and exemptions declared without substring or naming inference?

## Answers
- CA-001 [CQ-001]: A marker binds only the immediately following same-named compiled `let`. Options are exact `key=value` tokens: `kind`, `edge`, `success`, `failure`, `retry`, `idempotency`, and the three `exemption-*` fields. Unknown, duplicate, partial exemption, missing-symbol, or unresolved-edge declarations are malformed input.

## Decisions
- DEC-001 [CQ-001]: `kind=transition` is the default; `kind=parser`, `kind=validator`, and `kind=thin-adapter` are the only non-applicable kinds. Delivery values are retained from visible declarations and never synthesized from `edge`.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 369-effect-boundary`.
