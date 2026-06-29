# Quickstart / Validation: Publish the Consumer-Bearing Governance CLI

Runnable validation that the published CLI exists, installs, and actually enforces a produced handoff. Implementation details live in `tasks.md`; this is the run/verify guide.

## Prerequisites

- .NET SDK `10.0.x`.
- Org feed access: `FSGG_PACKAGES_ACTOR` + a token with `read:packages` (and `write:packages` for the real push), per `nuget.config`.
- Two committed product fixtures (see `data-model.md`): `tests/cli-publish-smoke/fixtures/{failing,passing}-handoff/readiness/<id>/governance-handoff.json`.

## 1. Confirm the gap (before)

```sh
# Expect HTTP 404 — no FS.GG.Governance.Cli on the org feed yet
gh api orgs/FS-GG/packages/nuget/FS.GG.Governance.Cli  # → 404
```

## 2. Pack the CLI locally

```sh
dotnet pack src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release -o ./_pkg
# Expect: ./_pkg/FS.GG.Governance.Cli.1.1.0.nupkg
unzip -l ./_pkg/FS.GG.Governance.Cli.1.1.0.nupkg | grep -i SddHandoff
# Expect: tools/.../FS.GG.Governance.Adapters.SddHandoff.dll present  (FR-002)
```

## 3. Enforcement smoke (the green-by-omission guard) — real evidence

```sh
TOOLDIR=$(mktemp -d)
dotnet tool install --tool-path "$TOOLDIR" --add-source ./_pkg FS.GG.Governance.Cli --version 1.1.0

# Failing handoff → must BLOCK (exit 2)
"$TOOLDIR/fsgg-governance" route --root tests/cli-publish-smoke/fixtures/failing-handoff --mode gate ; echo "exit=$?"
# Expect: exit=2

# Passing handoff → must PASS (exit 0)
"$TOOLDIR/fsgg-governance" route --root tests/cli-publish-smoke/fixtures/passing-handoff --mode gate ; echo "exit=$?"
# Expect: exit=0
```

Expected: exit `2` then `0` (per `contracts/cli-enforcement.md`). If the failing fixture returns `0`, the build is green-by-omission and MUST NOT be published.

## 4. Publish (CI)

Tag the coherent version (or publish a Release); `publish.yml` runs resolve-version → cli-tests → enforcement-smoke → push:

```sh
git tag v1.1.0 && git push origin v1.1.0     # triggers .github/workflows/publish.yml
```

## 5. Verify on the feed (after)

```sh
gh api orgs/FS-GG/packages/nuget/FS.GG.Governance.Cli   # → 200, version 1.1.0  (SC-001)
# Fresh install from the org feed only:
dotnet tool install --global FS.GG.Governance.Cli \
  --source https://nuget.pkg.github.com/FS-GG/index.json --version 1.1.0   # (SC-002)
fsgg-governance --help
```

## 6. Downstream acceptance + close the loop

- Confirm the FS.GG.Templates#25 composition stage flips from **SKIP** to asserting the strict-blocks/light-passes matrix and passes (SC-003).
- Append the coherence entry to `FS-GG/.github` `registry/dependencies.yml` (SC-005, `data-model.md`).
- Respond on + close `FS-GG/FS.GG.Governance#28`; move its Coordination board item to **Done** (SC-006).

## Done = all green

SC-001 (feed 200) · SC-002 (installs) · SC-003 (enforces + Templates flips) · SC-004 (version > predecessors) · SC-005 (registry coherence) · SC-006 (issue/board Done) · SC-007 (consumer-less build rejected by the smoke).
