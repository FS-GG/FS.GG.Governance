# Reference `.fsgg` gate set

A single curated, **populated** reference gate set under [`.fsgg/`](./.fsgg/), shaped to
govern the SDD reference worked-example skeleton (`<App>.sln`, `src/<App>/`,
`tests/<App>.Tests/`). Copy this directory into a product root and it loads, routes, and
enforces through the FS.GG.Governance pipeline with **zero edits** — the only adopter
substitution is the `<App>` placeholder in the `tooling.yml` command strings.

> **Frozen, not rotting.** Every invariant below is pinned by the regression guard
> `tests/FS.GG.Governance.ReferenceGateSet.Tests/` (assertions G1–G7), which loads this
> on-disk artifact through the real `Config → Gates → Routing → Route → Enforcement`
> pipeline on every build. Empty the checks, break a command reference, or flip
> `defaultProfile` off `light` and the build goes red before this page can drift.

## The four files

| File | Schema | Purpose |
|------|--------|---------|
| `governance.yml` | v1 | Project identity, the three governed `domains`, the `src` package surface, and refs to the sibling policy/capabilities files. |
| `capabilities.yml` | v2 | The `domains`, the `pathMap` (glob → domain), the `public-api` package surface, and the **three checks**. |
| `policy.yml` | v1 | The profiles and the load-bearing `defaultProfile: light`, plus declared branch-policy / review-budget placeholders. |
| `tooling.yml` | v1 | The three allow-listed commands each check binds to, the environment classes, and the `dotnet` external tool. |

## The three gates

Each `check` becomes one gate `"<domain>:<checkId>"`, bound to a declared `tooling.yml`
command and reached by a path-map glob:

| Gate | Routed by | Command | Maturity | Blocks? |
|------|-----------|---------|----------|---------|
| `build:build` | `src/**`, `*.sln` | `dotnet-build` | `block-on-ship` | yes, at the ship/release ratchet |
| `test:test` | `tests/**` | `dotnet-test` | `block-on-ship` | yes, at the ship/release ratchet |
| `evidence:evidence` | `build.fsx` | `build-evidence` | `warn` | never — advisory everywhere |

`build` and `test` are the real, block-capable gates. `evidence` is a first-class
governed gate that runs the product's in-process evidence-integrity step
(`dotnet fsi build.fsx -- evidence`); its `warn` maturity keeps it **advisory on first
touch** — the "evidence not yet present" posture — even while the block-capable gates can
block.

## Non-blocking by default — and how to ratchet up

The default profile is **`light`**, a *deliberate* non-blocking posture for the everyday
inner/verify loop, not an inability to block:

- **Under `light`** (default), on a failing change at `RunMode.Verify` (ordinal 3), the
  `block-on-ship` gates derive **Advisory** — their blocking floor (4) is above Verify, so
  nothing blocks your inner loop. `evidence` (`warn`) is advisory everywhere.
- **Under `strict`**, the same failing change at the same `Verify` mode tightens the floor
  to 3, so `build`/`test` derive **Blocking**. The gates *can* block; `light` is a chosen
  default.

`Verify` is the one mode where this contrast is visible: at `Focused` and below neither
profile blocks; at `Gate`/`Release` even `light` blocks (the ship/release ratchet). To
raise strictness deliberately, switch the active profile from `light` to `strict` (or
`release`) — no rule edits required. See `specs/079-reference-gate-set/research.md` §D5 for
the floor/tighten derivation.

## Validate it yourself

```bash
# Loads Valid, assembles 3 gates, routes, stays non-blocking-by-default:
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests

# Or copy it unedited and load through the CLI from a product working tree:
fsgg route --repo . --paths src/App/Program.fs
```
