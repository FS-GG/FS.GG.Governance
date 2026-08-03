# Feature Specification: F# Simplicity Architecture Gate

**Feature Branch**: `item/368-fsharp-simplicity-gate`

**Created**: 2026-08-03

**Status**: Complete

**Input**: FS-GG/FS.GG.Governance#368

## User Scenarios & Testing

### User Story 1 - Detect unjustified structural complexity (Priority: P1)

A reviewer receives stable, compiler-backed findings for inheritance, abstract classes,
reflection/metaprogramming, public classes, shared mutation, and imperative work in a declared pure
domain without treating every class, loop, mutation, or reflection use as categorically forbidden.

**Independent Test**: Analyze planted source documents and prove an unjustified domain hierarchy plus
shared mutable registry fails while the equivalent DU/module design passes.

### User Story 2 - Bind bounded exceptions to material code (Priority: P1)

An author can supply a structured justification naming the path, symbol, reviewed head, simpler
alternative, and measured or interoperability reason. A material source change invalidates it.

**Independent Test**: A measured array loop passes with a matching justification and becomes stale
after either its source digest or reviewed head changes.

### User Story 3 - Tune review triggers and primitive reuse (Priority: P2)

Repository owners can set explicit module/type/member-size and dependency-fan-out review thresholds
and declare approved primitives for capabilities, avoiding hidden universal limits and duplicate
home-grown frameworks.

**Independent Test**: The same source passes with disabled thresholds, produces review findings when
configured limits are crossed, and produces a prohibited-structure finding for a declared duplicate.

### User Story 4 - Preserve structural invariants across renames (Priority: P2)

A reviewer can rely on compiler symbol kind and relationship rather than identifier spelling. Renaming
a non-contractual symbol does not require editing the invariant; changing the guarded relationship does.

**Independent Test**: Rename a DU and its cases and retain a pass; replace the DU with an inheritance
hierarchy and receive the same stable structural finding independent of names.

### Edge Cases

- Parse/type-check failures produce explicit non-passing diagnostics and never an empty success.
- Missing, duplicate, empty, or stale justifications do not suppress findings.
- Disabled thresholds produce no size or fan-out findings.
- Generated files can be excluded explicitly; there is no implicit filename convention.

## Requirements

### Functional Requirements

- **FR-001**: Analyze F# source through compiler parse/type-check facts and detect inheritance and
  abstract classes, reflection/expression-tree/metaprogramming, module/global mutation, public classes,
  custom abstraction declarations, and imperative loops/mutation in declared pure domains.
- **FR-002**: Emit separate stable categories for prohibited structure and
  `complexity-requires-justification`; classes, loops, mutation, and reflection are not universal bans.
- **FR-003**: Require structured path/symbol/head/source-digest-bound justifications containing the
  simpler alternative considered and a measured or interoperability reason.
- **FR-004**: Treat a head or material-source digest mismatch as a stale justification.
- **FR-005**: Support optional, explicitly configured module/type/member line-count and dependency
  fan-out thresholds; no threshold is silently enabled.
- **FR-006**: Detect a candidate home-grown abstraction when its capability and candidate symbol are
  declared alongside an approved organization/BCL/FSharp.Core primitive.
- **FR-007**: Emit deterministic typed findings and actionable diagnostics and document the default and
  bounded exception route.
- **FR-008**: Cover idiomatic modules/functions/records/DUs, legitimate adapters, justified hot loops,
  unjustified inheritance/reflection, stale justifications, threshold controls, duplicates, parse
  failures, and rename-safe structural invariants.

### Key Entities

- **Analysis request**: Reviewed head, source documents, pure-domain paths, thresholds, justifications,
  exclusions, and approved-primitive declarations.
- **Justification**: Path, compiler symbol key, head, source digest, simpler alternative, reason kind,
  and evidence.
- **Architecture finding**: Stable id/category, path, symbol key, source range, diagnostic, and optional
  justification disposition.

## Success Criteria

- **SC-001**: All planted positive, negative, stale-binding, and false-positive fixtures are
  deterministic and pass in the focused suite.
- **SC-002**: Every detected source construct is backed by a successful compiler parse/type-check;
  compiler failure is explicit and non-passing.
- **SC-003**: Renaming non-contractual identifiers changes no finding category or structural verdict.
- **SC-004**: Identical requests produce identical ordered findings and diagnostics.

## Change Classification

Tier 1: adds a public architecture-analysis contract and an SDK compiler-service dependency at the
adapter layer. It requires `.fsi`, surface baseline, semantic tests, SDD artifacts, and guidance.

## Assumptions

- Callers provide source text and reviewed head; repository scanning remains outside this pure analyzer.
- Duplicate capability declarations are explicit configuration because semantic equivalence cannot be
  inferred safely from names alone.
- Compiler SDK facts are the authoritative source; bounded lexical context only labels compiler-backed
  ranges and never creates a finding on its own.
