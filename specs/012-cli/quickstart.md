# Phase 1 - Quickstart (F12 - 012-cli)

Validation guide for the optional CLI tool. It exercises the public command surface described
in [`contracts/command-schema.md`](./contracts/command-schema.md), with the API shape from
[`contracts/Cli.fsi`](./contracts/Cli.fsi) and [`contracts/Project.fsi`](./contracts/Project.fsi).

## Prerequisites

- .NET SDK targeting `net10.0`.
- F01-F11 already present: Kernel, Host, Adapter SPI, Spec Kit adapter, and design-system adapter.
- Local package feed directory exists or can be created: `~/.local/share/nuget-local/`.

## Build

```bash
dotnet build src/FS.GG.Governance.Cli
dotnet test tests/FS.GG.Governance.Cli.Tests
dotnet test
```

Expected outcome: CLI tests pass through the public surface; existing kernel/host/adapter tests still pass.

## Run from the built project

```bash
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode gate --json --review-budget 0
dotnet run --project src/FS.GG.Governance.Cli -- explain --root . --format text
dotnet run --project src/FS.GG.Governance.Cli -- explain --root . --format json
dotnet run --project src/FS.GG.Governance.Cli -- contract --root . --format text
dotnet run --project src/FS.GG.Governance.Cli -- contract --root . --format json
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root . --format text
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root . --format json
```

Expected outcome:

- `route` reports light/advisory/blocking state explicitly.
- `explain` top verdicts agree with the route over the same snapshot.
- `contract` statements are derived from the active composed catalog.
- `evidence` distinguishes declared/effective states, freshness, cache hits/misses, pending reviews, budget exhaustion, disclosures, and safe failures.
- With `--review-budget 0`, no fresh review is dispatched.

## Stable JSON check

```bash
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner --json > /tmp/route-1.json
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner --json > /tmp/route-2.json
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner --json > /tmp/route-3.json
cmp /tmp/route-1.json /tmp/route-2.json
cmp /tmp/route-2.json /tmp/route-3.json
```

Expected outcome: both `cmp` commands succeed for unchanged explicit inputs.

## Budget check

```bash
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root tests/FS.GG.Governance.Cli.Tests/fixtures/review-miss --json --review-budget 0
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root tests/FS.GG.Governance.Cli.Tests/fixtures/review-miss --json --review-budget 1
```

Expected outcome: the first run reports cache-only budget exhaustion and zero fresh dispatches.
The second run attempts at most one fresh dispatch and reports the key.

## Exit code check

```bash
dotnet run --project src/FS.GG.Governance.Cli -- nope ; echo $?
dotnet run --project src/FS.GG.Governance.Cli -- route --root /path/that/does/not/exist ; echo $?
dotnet run --project src/FS.GG.Governance.Cli -- route --root tests/FS.GG.Governance.Cli.Tests/fixtures/blocking --mode gate ; echo $?
```

Expected outcome:

- Unknown command exits `64`.
- Unavailable root exits `66`.
- Failing blocking gate in `Gate` mode exits `2`.

## Read-only check

```bash
git diff --quiet
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner --json > /tmp/governance-route.json
dotnet run --project src/FS.GG.Governance.Cli -- explain --root . --mode inner --json > /tmp/governance-explain.json
dotnet run --project src/FS.GG.Governance.Cli -- contract --root . --json > /tmp/governance-contract.json
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root . --json > /tmp/governance-evidence.json
git diff --quiet -- .specify specs src tests surface
```

Expected outcome: the final `git diff --quiet` succeeds. Report files are outside the governed root.

## Package and smoke-run

```bash
mkdir -p ~/.local/share/nuget-local
dotnet pack src/FS.GG.Governance.Cli -o ~/.local/share/nuget-local
rm -rf .tmp/f12-tool
dotnet tool install FS.GG.Governance.Cli --tool-path .tmp/f12-tool --add-source ~/.local/share/nuget-local
.tmp/f12-tool/fsgg-governance route --root . --mode inner
.tmp/f12-tool/fsgg-governance explain --root . --json
.tmp/f12-tool/fsgg-governance contract --root . --json
.tmp/f12-tool/fsgg-governance evidence --root . --json
```

Expected outcome: all four installed commands run without referencing test-private helpers and without modifying the governed root.

## Surface baseline

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests --filter Surface
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Cli.Tests --filter Surface
```

Expected outcome: the first command verifies the committed `surface/FS.GG.Governance.Cli.surface.txt`.
The blessing command updates the baseline intentionally after the `.fsi` surface is reviewed.
