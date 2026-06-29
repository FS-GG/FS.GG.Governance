# Data Model: Publish the Consumer-Bearing Governance CLI to the Org Feed

This feature adds no F# domain types. The "entities" are the release/coordination artifacts the plan operates on — their identifying fields, validation rules, and state transitions.

## Published CLI tool package

The installable artifact pushed to the org feed.

| Field | Value / rule |
|---|---|
| Package id | `FS.GG.Governance.Cli` (fixed; `PackageId` in the fsproj) |
| Tool command | `fsgg-governance` (`ToolCommandName`) |
| Version | `1.1.0` — sourced from fsproj `<Version>` via `dotnet msbuild -getProperty:Version`; MUST be strictly greater than every predecessor (`1.0.0`, `0.1.1`) (FR-004) |
| Kind | dotnet tool (`PackAsTool=true`, `IsPackable=true`) |
| Bundled consumer assembly | `FS.GG.Governance.Adapters.SddHandoff.dll` MUST be present in `tools/**` of the `.nupkg` (transitively via `RouteCommand`) (FR-002) |
| Feed | `https://nuget.pkg.github.com/FS-GG/index.json` |

**Validation**: the package MUST install via `dotnet tool install --global FS.GG.Governance.Cli --source <org feed>` and expose a runnable `fsgg-governance` (SC-002). Querying the feed for the id MUST no longer 404 (SC-001).

**State transitions**: `absent on feed (404)` → `packed` → `enforcement-smoke passed` → `pushed (present on feed)`. A failed smoke MUST stop the transition before push (no consumer-less publish — FR-008).

## Handoff product fixture

Committed test inputs for the enforcement smoke (real evidence).

| Fixture | Shape | Expected gate outcome |
|---|---|---|
| `failing-handoff/` | `readiness/<id>/governance-handoff.json` reporting a failing/not-ready state | `route --mode gate` blocks → exit `2` (`GovernedBlocking`) |
| `passing-handoff/` | `readiness/<id>/governance-handoff.json` reporting a ready/passing state | `route --mode gate` passes → exit `0` (`Success`) |

**Validation**: the failing fixture MUST produce a blocking route entry the consumer maps from the handoff; the passing fixture MUST NOT. Light/non-strict mode over the failing fixture MUST NOT block (exit `0`). See contracts/cli-enforcement.md for the exit-code contract.

## Registry coherence entry

Appended to `FS-GG/.github` `registry/dependencies.yml` `coherence:` list (cross-repo PR).

| Field | Value / rule |
|---|---|
| `id` | a new coherence id for this milestone (e.g. `governance-cli-handoff-consumer-published`) |
| `coherent` | `false` until the CLI is on the feed and the Templates probe flips; `true` on resolution |
| `owner` | `governance` |
| `summary` | what "coherent" means: the consumer-bearing `FS.GG.Governance.Cli@1.1.0` is on the org feed and `route --mode gate` enforces a produced `governance-handoff.json` |
| `resolved_by` | the publish PR/commit + `v1.1.0` tag |
| `impact` | FS.GG.Templates#25 composition stage flips SKIP → assert (strict blocks / light passes) |
| `tracking` | `https://github.com/FS-GG/FS.GG.Governance/issues/28` |

**Rule**: the `governance-handoff` **contract** entry (`version: "1.0.0"`, `owner: sdd`, `surface`, `consumers: [governance]`) is **unchanged** — this is a consumer-side coherence verification, not a contract surface bump (FR-006).

## Coordination item / cross-repo issue #28

| Field | Value / rule |
|---|---|
| Issue | `FS-GG/FS.GG.Governance#28` (labels `cross-repo`, `cross-repo:request`) |
| Board item | Coordination Projects v2, Phase `P3 Governance`, Repo `governance` |
| Status transition | `Ready` → (work) → **`Done`** when the consumer-bearing CLI is on the feed AND the Templates probe flips (FR-009, D7) |
| Resolution | a `## Response` comment + close (ideally via the linked publish PR) |
