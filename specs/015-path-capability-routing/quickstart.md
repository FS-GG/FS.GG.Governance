# Quickstart / Validation Guide: Path-to-Capability Routing (F015)

How to build, exercise, and prove this feature end-to-end. Implementation bodies belong in
`tasks.md` and the `.fs` files; this is the run/validate guide. Authoritative behaviour is in
[`contracts/glob-precedence.md`](./contracts/glob-precedence.md).

## Prerequisites

- .NET SDK with `net10.0` (per `Directory.Build.props`).
- `FS.GG.Governance.Config` builds (F014; provides the typed-fact model this feature consumes).

## Build

```sh
dotnet build src/FS.GG.Governance.Config        # dependency (typed facts)
dotnet build src/FS.GG.Governance.Routing       # this feature
```

## FSI design pass (Principle I)

`scripts/prelude.fsx` is extended with a routing sketch that references the BUILT library and
exercises the public surface exactly as a downstream consumer would:

```sh
dotnet fsi scripts/prelude.fsx
```

Expected: build typed facts with a small path map (`src/**` → `core`,
`src/Adapters/**` → `adapters`, `src/Kernel/Eval.fs` → `kernel-eval`), call
`Routing.route facts [ GovernedPath "src/Adapters/SpecKit.fs"; GovernedPath "src/Kernel/Eval.fs";
GovernedPath "README.md" ]`, and observe:

- `src/Adapters/SpecKit.fs` → `Routed (DomainId "adapters", GovernedPath "src/Adapters/**", MoreSpecific)`
- `src/Kernel/Eval.fs` → `Routed (DomainId "kernel-eval", GovernedPath "src/Kernel/Eval.fs", ExactLiteral)`
- `README.md` → `UnmatchedInRoot`
- a report whose `Routings` are sorted by path and whose `Diagnostics` are empty.

## Test (semantic, through the public surface)

```sh
dotnet test tests/FS.GG.Governance.Routing.Tests
```

The suite proves each spec obligation against real fixture strings and fixture `TypedFacts`:

| Test file | Proves | Spec link |
|---|---|---|
| `GlobMatchTests.fs` | one accepting match per MVP construct (literal, `?`, `*`, `**`) + representative rejects; `**` matches deep and shallow | FR-002, SC-006 |
| `PrecedenceTests.fs` | one fixture per FR-005 rung: exact-literal wins, more-literal-segments wins, single-segment `*` beats `**`, ordinal tiebreak | FR-005, US2, SC-006 |
| `RoutingTests.fs` | `route` over fixture facts: routed-to-domain, in-root-unmatched, out-of-scope | FR-004/007/008, US1/US3, SC-001 |
| `AmbiguityTests.fs` | `AmbiguousRoute` (with deterministic winner), `ConflictingGlobBinding`, `UnsupportedGlobSyntax` | FR-006/009/010, US3, SC-004 |
| `DeterminismTests.fs` | route twice → byte-identical report; FsCheck permutation of path-map entries → identical report | FR-012, SC-002/SC-003 |
| `SurfaceDriftTests.fs` | public surface matches `surface/FS.GG.Governance.Routing.surface.txt` | Principle II |

## Surface baseline (Principle II)

After the public surface stabilizes, (re)generate and commit
`surface/FS.GG.Governance.Routing.surface.txt`; `SurfaceDriftTests.fs` fails on uncommitted drift.

## Acceptance check (maps to spec Success Criteria)

- **SC-001** — every path matching ≥1 glob returns exactly one domain (`RoutingTests`, `PrecedenceTests`).
- **SC-002** — identical inputs → byte-identical report (`DeterminismTests`).
- **SC-003** — path-map permutation leaves the report unchanged (`DeterminismTests`, FsCheck).
- **SC-004** — co-specific match → one stable domain **and** an `AmbiguousRoute` diagnostic (`AmbiguityTests`).
- **SC-005** — each routed path names its glob + reason; no raw YAML / foreign vocabulary (`RoutingTests`).
- **SC-006** — a fixture per glob construct and per precedence rung (`GlobMatchTests`, `PrecedenceTests`).

## Evidence to record (during tasks, under `readiness/`)

Clean Release build; the focused test files above; full-suite pass count; the FSI prelude
transcript; a short SC traceability note. No synthetic evidence is expected (no agent, network,
clock, or filesystem); disclose loudly per Principle V if any appears.

## Out of scope (do not add here — FR-016)

git/CI changed-path sensing, unknown-path **finding** severity, surface-class assignment, the
gate registry, profile/mode enforcement, and the `route`/`ship` commands and their JSON.
