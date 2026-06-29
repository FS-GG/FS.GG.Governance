# Quickstart — Validate Profile-Aware Handoff-Gate Enforcement

Runnable validation that proves the feature end-to-end. Details of types and the matrix live in
[data-model.md](./data-model.md) and [contracts/profile-aware-gate.md](./contracts/profile-aware-gate.md).

## Prerequisites

- .NET SDK `10.0.x` (present: `10.0.301`).
- Repo root: `/home/developer/projects/FS.GG.Governance`.

## 1. Reproduce the failing cell (before the change)

The enforcement core is already exhaustively correct; the gap is the CLI wiring. Confirm the
profile-blind shortcut at `src/FS.GG.Governance.Cli/Cli.fs:397-409` (`handoffBlocking = mode = Gate &&
List.exists gateBlocks gates`) ignores the profile, then drive the existing smoke against a
light-profile fixture (which today still exits 2):

```bash
# build the solution
dotnet build FS.GG.Governance.sln -c Release

# the new Cli.Tests matrix test (light + failing @ gate) FAILS before the change:
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release \
  --filter "FullyQualifiedName~ProfileAware"
```

Expected before: the `light + failing → exit 0` row fails (the run blocks). This is the fails-before
evidence (Principle V).

## 2. Run the unit matrix (after the change)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release \
  --filter "FullyQualifiedName~ProfileAware"
```

Expected: all rows of the contract matrix pass — `{strict,light,absent} × {failing,satisfied}` at
`--mode gate` produce the exit codes in [contracts/profile-aware-gate.md](./contracts/profile-aware-gate.md).

## 3. Run the real-evidence publish smoke

```bash
bash tests/cli-publish-smoke/run.sh
```

Expected (packs the actual tool, installs it, runs the installed `fsgg-governance`):
- `failing-handoff` (profile-less) + `--mode gate` → **exit 2** (unchanged; strict default).
- `passing-handoff` (profile-less) + `--mode gate` → **exit 0** (unchanged).
- `failing-handoff` + `--mode inner` → **exit 0** (unchanged).
- **`light-failing-handoff`** (`defaultProfile: light`) + `--mode gate` → **exit 0** (the new behavior).

## 4. Confirm the version bump

```bash
dotnet msbuild src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -getProperty:Version
# → 1.2.0   (strictly greater than the immutable 1.1.0)
```

## 5. Dry-run the publish (no push)

The `publish.yml` `workflow_dispatch` path packs and runs both gates (`cli-tests`,
`enforcement-smoke`) without pushing when `push != 'true'`. Locally, the smoke (step 3) plus the
scoped CLI tests are the same gates:

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release \
  --filter "FullyQualifiedName!~WidthResilience"
```

## 6. Downstream acceptance (after `1.2.0` is on the feed)

Install `FS.GG.Governance.Cli@1.2.0` as a dotnet tool in a downstream product and run the
FS.GG.Templates#25 composition probe:

```bash
# in FS.GG.Templates
bash tests/composition/run.sh    # Stage 6b should report all cells passing (no red cell)
```

Then close FS-GG/FS.GG.Governance#34 and move its Coordination board item to **Done**.

## Done when

- [ ] Unit matrix (step 2) green; the light+failing row that failed in step 1 now passes.
- [ ] Smoke (step 3) green, including the new light-profile no-block assertion; profile-less fixtures
      still block.
- [ ] `<Version>` is `1.2.0`.
- [ ] Templates#25 Stage 6b fully green against the published `1.2.0`; issue #34 closed, board → Done.
