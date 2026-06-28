# Phase 0 Research: Adopt org-shared .NET build config

All Technical-Context unknowns are resolved below. No `NEEDS CLARIFICATION` remain.

---

## D1 — CI wiring mechanism for the drift gate

**Decision**: Wire the drift check **self-contained inside this repo's `.github/workflows/gate.yml`**: a job that (a) checks out this repo, (b) checks out `FS-GG/.github` into a sibling path, then (c) runs `<.github>/scripts/sync-build-config.sh --check .` against the repo root, failing the job on exit 1.

**Rationale**: The spec's preferred path — calling the org **reusable** coherence workflow — is unavailable: `FS-GG/.github#18` (the `workflow_call` `contract-coherence.yml`) is **OPEN and blocked by `FS.GG.Contracts` (H2)**, and `FS-GG/.github` currently has **zero workflows**. The self-contained checkout is the only mechanism that works today, needs no upstream, and exactly matches the adoption doc's stated hook (`sync-build-config.sh --check`). Both repos are **public**, so `actions/checkout` of `FS-GG/.github` needs **no PAT** — the default `GITHUB_TOKEN` (or even anonymous clone) suffices.

**Mechanics**: The script resolves its source as `dirname(script)/../dist/dotnet`, so the checkout must preserve `scripts/` **and** `dist/dotnet/` together. Check `FS-GG/.github` out into a subdir (e.g. `_org-build/`) and invoke `_org-build/scripts/sync-build-config.sh --check "$GITHUB_WORKSPACE"`. Run it as a **separate job** (parallel to the existing locked-restore+build job) so a drift failure is a distinct, legible signal and does not serialize behind the build.

**Ref to check against**: track `FS-GG/.github` **`main`** (the live source of truth). This is intentional coherence pressure per the adoption doc ("until a repo re-syncs … drift is flagged"): if the org later bumps the baseline (e.g. `FSharp.Core`), this repo's gate goes red until it re-syncs — exactly the desired signal.

**Alternatives considered**:
- *Call `.github#18` reusable workflow* — rejected: does not exist, blocked upstream. Recorded as a **bounded follow-up**: once `#18` lands, replace the checkout job with a `uses: FS-GG/.github/.github/workflows/contract-coherence.yml@main` call.
- *Vendor `sync-build-config.sh` + `dist/dotnet` into this repo* — rejected: that **is** forking the source of truth (the exact anti-pattern ADR-0006 forbids) and would itself drift.
- *Pin the `.github` checkout to a fixed SHA/tag* — rejected as the default (it would mask org baseline bumps and defeat the coherence signal), but noted as the escape hatch if un-pinned `main` proves too noisy for unrelated PRs; bumping the pin would then be a deliberate re-sync commit.

---

## D2 — Exact org-canonical vs repo-specific file partition

**Decision**: Split each managed file at the seam between what the org source of truth declares and what is repo-specific. Verified against the fetched canonical files.

**`Directory.Build.props`** — canonical declares only: `Deterministic`, `ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`, the `lockfile-restore-enforcement` block (`RestorePackagesWithLockFile`, `RestoreLockedMode` gated on `GITHUB_ACTIONS` + existing lockfile, `WarningsAsErrors=$(WarningsAsErrors);NU1603;NU1608`), and the `Import` of `Directory.Build.local.props` last.
→ **Moves to `Directory.Build.local.props`** (everything else currently in the repo file): `TargetFramework` (`net10.0`), `LangVersion` (`latest`), `Nullable` (`enable`), `TreatWarningsAsErrors` (`true`), `WarnOn` (`3390;1182`), `OtherFlags` (`$(OtherFlags) --nowarn:57`), `GenerateDocumentationFile` (`true`), `IsPackable` (`false`).

**`Directory.Packages.props`** — canonical declares: the CPM property group + the single org-baseline `FSharp.Core 10.1.301` `PackageVersion` + the `Import` of `Directory.Packages.local.props` last.
→ **Moves to `Directory.Packages.local.props`**: `YamlDotNet 16.3.0`, `Spectre.Console 0.57.1`, and the test pins `Expecto 10.2.3`, `Expecto.FsCheck 10.2.3`, `FsCheck 2.16.6`, `Microsoft.NET.Test.Sdk 18.6.0`, `YoloDev.Expecto.TestSdk 0.15.6`.
→ **Dropped entirely**: the local `FSharp.Core` `PackageVersion` (org baseline owns it; re-declaring it raises `NU1504`/`NU1011`). The local file carries **only** `<ItemGroup>`s of `PackageVersion`s — **not** the CPM property group (canonical provides it).

**Rationale**: The issue body's enumerated list (`WarnOn`, `--nowarn:57`, `IsPackable`, `GenerateDocumentationFile`) is **non-exhaustive** — the canonical Build.props omits `TargetFramework`/`LangVersion`/`Nullable`/`TreatWarningsAsErrors`, so those **must** move to local or the build regresses (e.g. framework defaults away from `net10.0`). The rule "everything not in the canonical file moves to local" is what guarantees no behavior change.

**Note**: `--adopt` mode of `sync-build-config.sh` automates the *move* (renames the existing hand-authored `*.props` → `*.local.props`, then writes canonical), but it renames **whole files**. Because this repo's hand-authored files mix org-canonical and repo-specific content, a verbatim rename would leave org-canonical duplicates (e.g. CPM property group, the lockfile block) in the local file. Therefore: run the canonical write, but author the two `*.local.props` **by hand** to contain only the repo-specific subset above. (See D3.)

---

## D3 — Adopt by `--adopt` script vs hand-authored split

**Decision**: Write the two managed files **verbatim from the source of truth** (copy the fetched canonical content), and **hand-author** the two `*.local.props` with exactly the repo-specific subset from D2. After writing, prove byte-identity with `sync-build-config.sh --check .` (must report `ok` for all three managed files).

**Rationale**: The `--adopt` flag's whole-file rename does not produce a clean org/repo split for a file that already mixes both (D2 note). Hand-authoring the local files is a few lines and yields exactly the intended partition; the `--check` run is the objective proof that the managed halves are canonical. This keeps the human edit confined to the genuinely repo-owned files.

**Alternatives considered**: *Run `--adopt` then prune the local files* — rejected: more steps, same end state, and leaves a window where the local file holds org-canonical duplicates that would error under CPM (`NU1504`).

---

## D4 — Lockfile validity under the restructure

**Decision**: Expect the 165 committed `packages.lock.json` to **stay valid** (no regeneration). Verify by running locked restore (`dotnet restore --locked-mode`) after the change; only if it fails, regenerate with `--force-evaluate` and commit, and call it out.

**Rationale**: A `packages.lock.json` records the **resolved dependency graph + content hashes**, derived from each project's package references and the **effective** central versions — not from the physical file layout. Moving `PackageVersion` items from `Directory.Packages.props` into an imported `Directory.Packages.local.props` does not change the *effective* set (MSBuild merges the import), and every version (incl. `FSharp.Core 10.1.301`) is unchanged. Adding `.config/dotnet-tools.json` affects the **tool** manifest, not project restore. So the resolved graph — and thus every lockfile — is identical. The locked-restore CI step is the guard if this reasoning is wrong.

---

## D5 — The unused `fake-cli` tool manifest

**Decision**: Adopt `.config/dotnet-tools.json` **verbatim** (pins `fake-cli 6.1.4`), even though this repo's `build.fsx` is a plain `dotnet fsi` script that never invokes `fake`.

**Rationale**: The drift check covers **all three** managed files; omitting the manifest would fail `--check`. The pinned tool is dormant (nothing restores or runs it in this repo's build path), so it is harmless. This is recorded as an accepted deviation from "dependency minimization": the dependency is on the **shared-build-config contract**, which requires the file verbatim, not on `fake` functionally.

---

## D6 — Behavior-equivalence guarantees

**Decision**: The adopted config is behavior-identical because: (1) the canonical `lockfile-restore-enforcement` block is **textually equivalent** to the repo's existing `Restore (211)` block (same `GITHUB_ACTIONS` gate, same `Exists(lockfile)` bootstrap guard, same `NU1603`/`NU1608` promotion) — the issue confirms "Gate condition is unchanged — already `GITHUB_ACTIONS`"; (2) all compiler properties survive in the local override; (3) all package versions survive (relocated, not changed); (4) the only **new** org default is `Deterministic=true`, an intended org behavior, not a regression.

**Evidence plan**: `dotnet fsi build.fsx test` green with the same project/test counts as `main` before the change; `dotnet restore --locked-mode` green; `--check` exit 0; `git diff` empty over `src/`/`tests/`/`.fsi`/goldens/baselines.

---

## Summary of decisions

| # | Decision |
|---|---|
| D1 | Self-contained CI drift gate: checkout `FS-GG/.github` (public, no PAT), run `--check` as a separate `gate.yml` job tracking `main`; migrate to reusable `#18` later (follow-up). |
| D2 | Partition: canonical = determinism/CPM/lockfile-gate (+ FSharp.Core baseline); local = all compiler props + non-FSharp.Core pins; drop local FSharp.Core. |
| D3 | Write managed files verbatim from source of truth; hand-author the two `*.local.props`; prove with `--check`. |
| D4 | Lockfiles expected valid (graph unchanged); verify via locked restore, regenerate only if it fails. |
| D5 | Adopt `fake-cli` manifest verbatim (drift-gate parity); dormant/harmless. |
| D6 | No behavior change; evidence = green build/test + locked restore + `--check` 0 + empty src diff. |
