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
- CQ-002: What source text is evidence of an effect, and what identity must a diagnostic retain?
- CQ-003: How must interpolation holes differ from interpolated literal text during executable-call sensing?

## Answers
- CA-001 [CQ-001]: A marker binds only the immediately following same-named compiled `let`. Options are exact `key=value` tokens: `kind`, `edge`, `success`, `failure`, `retry`, `idempotency`, and the three `exemption-*` fields. Unknown, duplicate, partial exemption, missing-symbol, or unresolved-edge declarations are malformed input.
- CA-002 [CQ-002]: Only recognized executable call forms in lexically visible code are effect evidence. Comments, ordinary/verbatim/triple/interpolated string contents, character literals, and identifier-shaped text are not calls. Each effect fact records its category, matched call symbol, and one-based line and column.
- CA-003 [CQ-003]: Interpolated literal segments remain masked, escaped braces remain literal, and each single-brace interpolation expression is scanned as executable code through its balanced closing brace. Nested strings and comments inside the expression retain the same masking rules as outer code.

## Decisions
- DEC-001 [CQ-001]: `kind=transition` is the default; `kind=parser`, `kind=validator`, and `kind=thin-adapter` are the only non-applicable kinds. Delivery values are retained from visible declarations and never synthesized from `edge`.
- DEC-002 [CQ-002]: The lightweight source sensor masks comments and literals while preserving positions, then matches a closed set of call-shaped patterns with token boundaries. Diagnostics name both the declared boundary and the actual call/location.
- DEC-003 [CQ-003]: The lexical scanner uses distinct code and interpolated-literal states. Entering an interpolation hole returns to code state with balanced-brace tracking, so executable effect calls preserve their original one-based positions without making surrounding literal text visible.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 369-effect-boundary`.
