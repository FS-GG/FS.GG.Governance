# Contract: the `route.json` document (F020)

The observable wire contract `RouteJson.ofRouteResult` produces. This fixes **field order**, **tokens**,
and **shape** — the part of the artifact consumers depend on (the spec's "observable document
contract"). It is the design's `readiness/<id>/route.json`, restricted to the fields the upstream rows
have typed (cache eligibility and profile-adjusted enforcement are deferred — see [plan](../plan.md)
Summary and [research D6](../research.md)).

Output is **compact** (non-indented) UTF-8 from a default `Utf8JsonWriter`; the indented form below is
for documentation only. Field order is exactly the writer's call order and is part of the contract
(FR-007). Collections are in the order `RouteResult` already fixed — selected gates by `GateId`, each
gate's selecting paths by normalized path, findings in F017 order (FR-005/FR-007).

## Top-level object — field order: `schemaVersion`, `selectedGates`, `findings`, `cost`

| Field | JSON type | Source | Notes |
|---|---|---|---|
| `schemaVersion` | string | `RouteJson.schemaVersion` | The declared contract version (FR-013). Fixed constant. |
| `selectedGates` | array\<gate\> | `result.SelectedGates` | In `GateId` order; empty array for an empty route (FR-009). |
| `findings` | array\<finding\> | `result.Findings.Findings` | Carried unchanged in F017 order; present-and-empty when none (FR-005). |
| `cost` | object | `result.Cost` | Per-tier rollup, every tier present (FR-006). |

## `selectedGates[*]` — gate entry, field order as listed

| Field | JSON type | Source (`SelectedGate sg`) | Token / rendering |
|---|---|---|---|
| `id` | string | `sg.Gate.Id` | `Gates.gateIdValue` — declared `GateId` string verbatim (FR-002, FR-010). |
| `domain` | string | `sg.Gate.Domain` | `DomainId` unwrapped verbatim (FR-010). |
| `description` | string | `sg.Gate.Description` | Carried verbatim (FR-002). JSON-escaped by the writer. |
| `cost` | string | `sg.Gate.Cost` | `cheap` \| `medium` \| `high` \| `exhaustive`. |
| `timeout` | number (int) | `sg.Gate.Timeout` | `TimeoutLimit` seconds (int), verbatim. |
| `owner` | string | `sg.Gate.Owner` | `Owner` unwrapped verbatim. |
| `maturity` | string | `sg.Gate.Maturity` | `observe` \| `warn` \| `blockOnPr` \| `blockOnShip` \| `blockOnRelease`. Declared maturity carried verbatim — **not** translated to enforcement (FR-011). |
| `productCheck` | boolean | `sg.Gate.ProductCheck` | Carried verbatim. |
| `prerequisites` | array\<object\> | `sg.Gate.Prerequisites` | Each `RequiresCommand c` → `{ "requiresCommand": "<commandId>" }`, in declared order. Empty array when none. |
| `freshnessKey` | object | `sg.Gate.FreshnessKey` | Carried key **inputs** only — never a cache verdict (FR-014). Shape below. |
| `selectingPaths` | array\<object\> | `sg.SelectingPaths` | Each `{ "path": "<governedPath>", "matchedGlob": "<governedPath>" }`, in normalized-path order. One entry per selecting path; the gate appears once however many paths reached it (FR-004). |

### `freshnessKey` — field order: `check`, `domain`, `cost`, `environment`, `command`

| Field | JSON type | Source (`FreshnessKey k`) | Token / rendering |
|---|---|---|---|
| `check` | string | `k.Check` | `CheckId` unwrapped verbatim. |
| `domain` | string | `k.Domain` | `DomainId` unwrapped verbatim. |
| `cost` | string | `k.Cost` | Same `Cost` token vocabulary as the gate `cost`. |
| `environment` | string | `k.Environment` | `local` \| `ci` \| `localOrCi` \| `release`. |
| `command` | string \| null | `k.Command` | `CommandId` string when `Some`; JSON `null` when `None`. |

## `findings[*]` — finding entry, field order: `id`, `path`, `zone`, `message`

| Field | JSON type | Source (`UnknownGovernedPathFinding f`) | Token / rendering |
|---|---|---|---|
| `id` | string | `f.Id` | `Findings.findingIdToken` — `unknownGovernedPath` \| `unknownProtectedBoundaryPath`. |
| `path` | string | `f.Path` | `GovernedPath` unwrapped verbatim. |
| `zone` | string \| object | `f.Zone` | `GovernedRootUnknown` → `"governedRootUnknown"`; `ProtectedBoundaryUnknown sid` → `{ "protectedBoundary": "<surfaceId>" }`. |
| `message` | string | `f.Message` | Carried verbatim (FR-005). JSON-escaped by the writer. |

## `cost` — object, field order: `cheap`, `medium`, `high`, `exhaustive`

| Field | JSON type | Source | Notes |
|---|---|---|---|
| `cheap` | number (int) | `result.Cost.Cheap` | Count of distinct selected gates in the tier (FR-006). |
| `medium` | number (int) | `result.Cost.Medium` | Including zero when the tier is absent. |
| `high` | number (int) | `result.Cost.High` | |
| `exhaustive` | number (int) | `result.Cost.Exhaustive` | |

## Excluded from every document (FR-011, FR-012, SC-007)

No `severity`, `profile`, `mode`, enforcement, cache-eligibility verdict, ship verdict, `blockers`,
`warnings`, `exitCode`, `expectedArtifacts`, raw YAML, host/absolute path, wall-clock timestamp, or
environment-derived value. Only declared id strings, the declared `Cost`/`Maturity`/`EnvironmentClass`
vocabulary, the carried gate metadata, the carried freshness-key inputs, and the carried findings
appear.

## Worked sample (indented for readability; the emitted form is compact)

A route selecting one `build:tests` gate reached by one path, one `docs:lint` gate, with one carried
governed-root finding — the F019 prelude fixture (`f19Result`):

```json
{
  "schemaVersion": "fsgg.route/v1",
  "selectedGates": [
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
      },
      "selectingPaths": [
        { "path": "src/build/Core.fs", "matchedGlob": "src/build/**" }
      ]
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
      },
      "selectingPaths": [
        { "path": "src/docs/Guide.md", "matchedGlob": "src/docs/**" }
      ]
    }
  ],
  "findings": [
    {
      "id": "unknownGovernedPath",
      "path": "src/loose/x.fs",
      "zone": "governedRootUnknown",
      "message": "path src/loose/x.fs is inside the governed root but no capability glob classified it"
    }
  ],
  "cost": { "cheap": 1, "medium": 1, "high": 0, "exhaustive": 0 }
}
```

The empty route projects to:

```json
{ "schemaVersion": "fsgg.route/v1", "selectedGates": [], "findings": [], "cost": { "cheap": 0, "medium": 0, "high": 0, "exhaustive": 0 } }
```

> Sample field *values* (description text, timeout numbers, command ids) illustrate the shape; the
> authoritative values are whatever the real upstream chain carries on `f19Result` at test time. The
> contract this file fixes is the **field set, order, and tokens**, not the sample's literal strings.
