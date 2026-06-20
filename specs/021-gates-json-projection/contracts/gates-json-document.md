# Contract: the `gates.json` document (F021)

The observable wire contract `GatesJson.ofGateRegistry` produces. This fixes **field order**, **tokens**,
and **shape** — the part of the artifact consumers depend on (the spec's "observable document
contract"). It is the design's `.fsgg/gates.json` artifact (`docs/initial-design.md`, the data-artifacts
table), restricted to the fields F018 has already typed (cache eligibility, profile-adjusted
enforcement, per-change selection, and ship verdict are deferred — see [plan](../plan.md) Summary and
[research D5](./research.md)).

Output is **compact** (non-indented) UTF-8 from a default `Utf8JsonWriter`; the indented form below is
for documentation only. Field order is exactly the writer's call order and is part of the contract
(FR-007). Collections are in the order the `GateRegistry` already fixed — gates by `GateId` ordinal,
each gate's prerequisites in their carried order (FR-007).

**Relationship to `route.json` (F020).** This is the **whole-catalog** view; `route.json` is the
**per-change** view. The `gates[*]` entry below is exactly the F020 `selectedGates[*]` gate entry
**minus** the route-specific `selectingPaths` field — every shared field has the same name, position,
and token vocabulary, so a consumer can reuse one gate-entry parser across both artifacts.

## Top-level object — field order: `schemaVersion`, `gates`

| Field | JSON type | Source | Notes |
|---|---|---|---|
| `schemaVersion` | string | `GatesJson.schemaVersion` | The declared contract version `"fsgg.gates/v1"` (FR-013). Fixed constant. |
| `gates` | array\<gate\> | `registry.Gates` | In `GateId` ordinal order; **empty array** for an empty registry (FR-009) — never a placeholder gate. |

## `gates[*]` — gate entry, field order as listed

| Field | JSON type | Source (`Gate g`) | Token / rendering |
|---|---|---|---|
| `id` | string | `g.Id` | `Gates.gateIdValue` — declared `GateId` string verbatim (FR-002, FR-010). Never re-parsed to recover a domain. |
| `domain` | string | `g.Domain` | `DomainId` unwrapped verbatim (FR-010). |
| `description` | string | `g.Description` | Carried verbatim (FR-002). JSON-escaped by the writer (FR-012). |
| `cost` | string | `g.Cost` | `cheap` \| `medium` \| `high` \| `exhaustive`. Declared tier — never a weighted scalar (FR-005). |
| `timeout` | number (int) | `g.Timeout` | `TimeoutLimit` seconds (int), verbatim — never re-derived or re-defaulted (FR-006). |
| `owner` | string | `g.Owner` | `Owner` unwrapped verbatim. |
| `maturity` | string | `g.Maturity` | `observe` \| `warn` \| `blockOnPr` \| `blockOnShip` \| `blockOnRelease`. Declared maturity carried verbatim — **not** translated to enforcement (FR-005, FR-011). |
| `productCheck` | boolean | `g.ProductCheck` | Carried verbatim — not re-derived from the environment (FR-014). |
| `prerequisites` | array\<object\> | `g.Prerequisites` | Each `RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`, in carried order. **Present-and-empty** array when none (FR-004) — never an omitted field. |
| `freshnessKey` | object | `g.FreshnessKey` | Carried key **inputs** only — never a cache verdict (FR-014). Shape below. |

### `freshnessKey` — field order: `check`, `domain`, `cost`, `environment`, `command`

| Field | JSON type | Source (`FreshnessKey k`) | Token / rendering |
|---|---|---|---|
| `check` | string | `k.Check` | `CheckId` unwrapped verbatim. |
| `domain` | string | `k.Domain` | `DomainId` unwrapped verbatim. |
| `cost` | string | `k.Cost` | Same `Cost` token vocabulary as the gate `cost`. |
| `environment` | string | `k.Environment` | `local` \| `ci` \| `localOrCi` \| `release`. |
| `command` | string \| null | `k.Command` | `CommandId` string when `Some`; explicit JSON `null` when `None` (FR-014) — distinguishable from a present command, never dropped. |

## Excluded from every document (FR-011, FR-012, SC-007)

No `severity`, `profile`, `mode`, enforcement, cache-eligibility verdict, per-change gate selection,
`selectingPaths`, route trace, `findings`, cost rollup, ship verdict, `blockers`, `warnings`,
`exitCode`, `expectedArtifacts`, raw YAML, host/absolute path, wall-clock timestamp, or
environment-derived value. Only declared id strings, the declared `Cost`/`Maturity`/`EnvironmentClass`
vocabulary, the carried gate metadata, the carried free-text description, and the carried freshness-key
inputs appear.

## Worked sample (indented for readability; the emitted form is compact)

A registry of two gates — one `build:tests` with a command prerequisite and a `Release`-environment
freshness key, one `docs:lint` with no command — assembled by the real F018 `Gates.buildRegistry`:

```json
{
  "schemaVersion": "fsgg.gates/v1",
  "gates": [
    {
      "id": "build:tests",
      "domain": "build",
      "description": "run tests for domain build",
      "cost": "medium",
      "timeout": 600,
      "owner": "team-a",
      "maturity": "blockOnShip",
      "productCheck": false,
      "prerequisites": [ { "requiresCommand": "dotnet-test" } ],
      "freshnessKey": {
        "check": "tests",
        "domain": "build",
        "cost": "medium",
        "environment": "local",
        "command": "dotnet-test"
      }
    },
    {
      "id": "docs:lint",
      "domain": "docs",
      "description": "run lint for domain docs",
      "cost": "cheap",
      "timeout": 300,
      "owner": "team-c",
      "maturity": "warn",
      "productCheck": false,
      "prerequisites": [],
      "freshnessKey": {
        "check": "lint",
        "domain": "docs",
        "cost": "cheap",
        "environment": "local",
        "command": null
      }
    }
  ]
}
```

The empty registry projects to:

```json
{ "schemaVersion": "fsgg.gates/v1", "gates": [] }
```

> Sample field *values* (description text, timeout numbers, command ids) illustrate the shape; the
> authoritative values are whatever the real F018 `buildRegistry` carries at test time. The contract
> this file fixes is the **field set, order, and tokens**, not the sample's literal strings.
