# Contract: `publish.yml` — the Governance CLI publish workflow

The release surface this feature adds. Modeled on `FS-GG/FS.GG.SDD/.github/workflows/release.yml`. Repo-owned (not a reusable workflow). Scoped to the `FS.GG.Governance.Cli` tool package only.

## Triggers

| Trigger | Use |
|---|---|
| `release: types: [published]` | publish on a GitHub Release |
| `push: tags: ['v*']` | publish on an annotated version tag |
| `workflow_dispatch` | manual run / dry-run (optional `version` input for validation) |

## Version source

- Read from `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` via `dotnet msbuild -getProperty:Version`.
- A `v<semver>` tag MUST equal the fsproj `<Version>` (e.g. tag `v1.1.0` ↔ `<Version>1.1.0`). Mismatch fails the run (no hardcoded version anywhere).

## Permissions

- Default job: `contents: read`, `packages: read` (locked restore from the org feed, as in `gate.yml`).
- Publish job only: `packages: write`. Run-scoped `${{ secrets.GITHUB_TOKEN }}` — no PAT.

## Jobs (ordered; push is last and gated)

1. **resolve-version** — read + echo the fsproj `<Version>`; fail if unreadable or (on a tag) mismatched.
2. **cli-tests** — locked restore (`--locked-mode`) + `dotnet test tests/FS.GG.Governance.Cli.Tests/...` (mirrors SDD's `cli-tests`).
3. **enforcement-smoke** — the green-by-omission guard (see `cli-enforcement.md`): pack → `dotnet tool install` into a temp dir → run `fsgg-governance route --mode gate` against the committed fixtures → assert exit `2`/`0` → assert `Adapters.SddHandoff.dll` present in the package. MUST pass before push.
4. **publish** (`packages: write`) — `dotnet pack -c Release` the CLI, then:
   ```sh
   dotnet nuget push <packed>.nupkg \
     --source https://nuget.pkg.github.com/FS-GG/index.json \
     --api-key ${{ secrets.GITHUB_TOKEN }} \
     --skip-duplicate
   ```

## Behavioral guarantees

- **Idempotent**: `--skip-duplicate` — re-running on an already-published version does not hard-fail (version immutability edge case).
- **Fail-safe**: auth/credential failure, version mismatch, or a failing smoke stops before any push; never a partial or mislabeled artifact (FR-007).
- **No green-by-omission**: a build whose packed tool does not enforce a failing handoff cannot reach the push job (FR-008).
- **Scoped**: only `FS.GG.Governance.Cli` is packed/pushed; the other packable `FS.GG.Governance.*` projects are not published here.
- **Drift-safe**: no edits to the org-synced `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json`; any tool install is job-scoped (D6).

## Non-goals

- Publishing the full ~70-package set (H4/088-adjacent).
- A reusable org publish workflow (none exists yet; follow-up if needed).
