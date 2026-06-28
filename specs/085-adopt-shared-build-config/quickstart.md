# Quickstart: validate the shared-build-config adoption

Runnable validation that the adoption (a) conforms to the contract and (b) changes no behavior. Details of the file partition live in [data-model.md](./data-model.md); conformance obligations in [contracts/build-config-contract.md](./contracts/build-config-contract.md).

## Prerequisites

- .NET SDK `10.0.x`, F# / `dotnet fsi`.
- A local checkout of `FS-GG/.github` (public) for the sync script — clone if needed:
  ```sh
  git clone https://github.com/FS-GG/.github /tmp/fsgg-dotgithub
  ```
- Repo root: `FS.GG.Governance/`.

## 0. Baseline (before the change)

Capture the pre-change signal so "no behavior change" is provable.

```sh
dotnet fsi build.fsx test            # record project + test counts (all green)
```

## 1. Apply the adoption (manual split — see research.md D3)

1. Write `Directory.Build.props` and `Directory.Packages.props` **verbatim** from `/tmp/fsgg-dotgithub/dist/dotnet/`.
2. Create `Directory.Build.local.props` with the repo-specific properties (data-model.md table).
3. Create `Directory.Packages.local.props` with the repo-specific pins, **dropping** `FSharp.Core`.
4. Copy `/tmp/fsgg-dotgithub/dist/dotnet/.config/dotnet-tools.json` to `.config/dotnet-tools.json`.

## 2. Conformance — drift check is clean (C1, SC-002)

```sh
/tmp/fsgg-dotgithub/scripts/sync-build-config.sh --check .
# expect: "ok: Directory.Build.props", "ok: Directory.Packages.props",
#         "ok: .config/dotnet-tools.json", exit 0
```

## 3. No-behavior-change — build, test, locked restore (C4, C6, SC-001/003/004)

```sh
dotnet restore FS.GG.Governance.sln --locked-mode    # green; lockfiles still valid (D4)
dotnet fsi build.fsx test                            # same counts as step 0 (all green)
```

- If locked restore fails: `dotnet restore FS.GG.Governance.sln --force-evaluate`, commit the updated `packages.lock.json`, and note it (D4 contingency).
- Confirm `FSharp.Core` resolves to `10.1.301` and there is no `NU1504`/`NU1011` (C3).

## 4. No surface drift (C6, SC-007)

```sh
git status --porcelain src tests samples docs build.fsx '*.sln'
git diff --quiet -- src tests samples docs build.fsx '**/*.fsi' '**/*.fs' && echo "OK: no src/surface drift"
# expect: no changes to src/, tests/, *.fsi, *.fs, *.sln, samples/, docs/, build.fsx, goldens, baselines
```

## 5. Drift gate fails on a hand-edit, passes on revert (C5, SC-005)

```sh
printf '\n<!-- tamper -->\n' >> Directory.Build.props
/tmp/fsgg-dotgithub/scripts/sync-build-config.sh --check .   # expect: DRIFT (differs), exit 1
git checkout -- Directory.Build.props
/tmp/fsgg-dotgithub/scripts/sync-build-config.sh --check .   # expect: ok, exit 0
```

## 6. CI gate (push a branch)

Push and confirm `gate.yml` runs **two** jobs green: the existing locked-restore+build job and the new `build-config-drift` job (which checks out `FS-GG/.github` and runs `--check`). Tamper with a managed file on a branch → the drift job goes red; revert → green.

## Done when

- Steps 2–5 pass locally; CI shows both gate jobs green (step 6). All of SC-001…SC-007 are then satisfied.
