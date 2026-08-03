# Research: F# Simplicity Architecture Gate

## D1 — Compiler-backed source facts

**Decision**: Use the FSharp.Compiler.Service shipped with the pinned .NET SDK, parse/type-check each
supplied document, and derive declared entities, module values, symbol uses, and compiler source ranges.

**Rationale**: These are compiler facts and remain stable across formatting and identifier renames.

**Alternatives considered**: Regex-only scanning was rejected by acceptance; a separate analyzer process
would add I/O/MVU and deployment complexity without improving the fact source.

## D2 — Justification freshness

**Decision**: Bind a justification to normalized path, stable compiler symbol key, reviewed head, and
SHA-256 of the complete source document. Require non-empty alternative and evidence plus a closed reason
kind (`Measured` or `Interoperability`).

**Rationale**: A source digest makes any material edit stale without pretending a heuristic can identify
which edits matter to a construct.

## D3 — Thresholds and duplicates

**Decision**: Thresholds are `option<int>` and disabled by default. Duplicate abstractions require an
explicit capability declaration with approved and candidate compiler symbol keys.

**Rationale**: There is no universal correct size or semantic way to infer capability equivalence safely.

## D4 — Structural rename safety

**Decision**: Findings key on compiler entity kind, base relationship, mutability, and resolved symbol
ownership. Source names are diagnostic labels, never negative-name assertions.

**Rationale**: Rename-only changes must not make a structural invariant vacuous.
