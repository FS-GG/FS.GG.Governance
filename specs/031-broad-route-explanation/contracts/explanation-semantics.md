# Contract: explanation semantics — decision tables (F031)

The exact behavior of `RouteExplain.explain`, expressed as tables over the closed F014 `Cost` /
`EnvironmentClass` classes. These tables are the source of the worked-example tests.

## 1. High-cost filter (which selected gates produce a finding) — D3

A selected gate produces a `HighCostFinding` iff its declared `Cost >= highCostThreshold` (`= High`).

| `Gate.Cost` | `>= High`? | Produces a finding? |
|---|---|---|
| `Cheap` | no | **no** |
| `Medium` | no | **no** |
| `High` | yes | **yes** |
| `Exhaustive` | yes | **yes** |

- Applied to `route.SelectedGates` only — unselected catalog gates are never findings (they are only
  candidate *alternatives*, table 2).
- `Cost` comparison is the DU's structural ordering (`Cheap < Medium < High < Exhaustive`).

## 2. Cheaper-local-alternative resolution (per high-cost finding gate `h`) — D4/D6

A registry gate `g` is a **candidate** iff **all three** hold:

| Condition | Rule | Source |
|---|---|---|
| Same capability | `g.Domain = h.Domain` | `DomainId` equality |
| Strictly cheaper | `g.Cost < h.Cost` | `Cost` structural order (strict) |
| Locally runnable | `g.FreshnessKey.Environment ∈ { Local; LocalOrCi }` | `EnvironmentClass` (D6) |

Outcome:

- **≥ 1 candidate** → `CheaperLocalAlternative gᵐⁱⁿ`, where `gᵐⁱⁿ` is the candidate with the **lowest `Cost`**,
  ties broken by **least `GateId` ordinal**.
- **0 candidates** → `NoCheaperLocalAlternative`.

### Local-permission truth table (the third condition)

| `g.FreshnessKey.Environment` | Locally runnable? |
|---|---|
| `Local` | **yes** |
| `LocalOrCi` | **yes** |
| `Ci` | no |
| `Release` | no |

### Worked example

Catalog (domain `build`) and a route selecting the `Exhaustive` gate `build:full`:

| Gate | Domain | Cost | Environment | Candidate for `build:full`? |
|---|---|---|---|---|
| `build:full` (the high-cost finding gate) | build | `Exhaustive` | `Ci` | — (it is `h`) |
| `build:unit` | build | `Cheap` | `Local` | **yes** (same domain, cheaper, local) |
| `build:integration` | build | `Medium` | `LocalOrCi` | yes (same domain, cheaper, local) |
| `build:smoke-ci` | build | `Medium` | `Ci` | no (not local) |
| `build:release-verify` | build | `Exhaustive` | `Local` | no (not strictly cheaper) |
| `docs:links` | docs | `Cheap` | `Local` | no (different domain) |

Resolution: candidates are `build:unit` (`Cheap`) and `build:integration` (`Medium`); the cheapest is
`build:unit` → `CheaperLocalAlternative build:unit`. If `build:unit` were removed, the alternative would be
`build:integration`. If both were removed, the outcome would be `NoCheaperLocalAlternative` (the remaining
same-domain gates fail strictly-cheaper or local).

## 3. Ordering & degenerate cases — D5 / FR-011

| Situation | Result |
|---|---|
| Several high-cost gates | one finding each, `Findings` sorted by `Selected.Gate.Id` ordinal |
| Input selected gates / registry gates / selecting paths reordered or duplicated | identical `RouteExplanation` (order/dup-invariant) |
| `route.SelectedGates = []` | `{ Findings = [] }` |
| Selected gates all `< High` | `{ Findings = [] }` |
| High-cost gate, empty registry | one finding, `Alternative = NoCheaperLocalAlternative` |
| High-cost gate, registry has only equal/higher-cost or cross-domain or non-local same-domain gates | one finding, `Alternative = NoCheaperLocalAlternative` |

## 4. What `explain` never does (negative contract) — FR-010

Renders no JSON/artifact, persists nothing, computes no numeric cost weight/budget, no severity, no
enforcement, no freshness verdict, no ship verdict; runs no gate; reads no clock/filesystem/git/environment/
network; adds no CLI. Sole output is the `RouteExplanation` value.
</content>
