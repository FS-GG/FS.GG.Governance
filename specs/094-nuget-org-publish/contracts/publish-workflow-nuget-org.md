# Contract: `publish.yml` — the nuget.org publish leg (extended)

Extends the existing `publish.yml` (spec 089, `contracts/publish-workflow.md`, which remains the
base contract). This contract adds the **public nuget.org** leg for both governance packages under
Trusted Publishing (ADR-0013) and adds the reference-gate-set publish path (ADR-0012 §1–§5).

## Triggers, version source, concurrency — UNCHANGED

Same as spec 089: `release: published`, `push: tags v*`, `workflow_dispatch` (optional `version`
input → dry-run when omitted). CLI version from `msbuild -getProperty:Version`; a `v<semver>` tag
must equal it. Concurrency serialized on the version/tag. A dry-run (`push=false`) packs but pushes
nothing.

## Permissions (delta)

- The **publish jobs that push to nuget.org** additionally require `id-token: write` (OIDC token
  minting for `NuGet/login`). They keep `contents: read` and `packages: write` (org-feed push).
- Non-publish jobs are unchanged (`contents: read`, `packages: read`).

## Jobs

### CLI path (extend the existing `publish` job)

Ordered gates unchanged: `resolve-version → cli-tests → enforcement-smoke → publish`.
Inside `publish`, after the existing **org-feed** push, add:

1. `NuGet/login@v1` with `user: ${{ secrets.NUGET_USER }}` (profile `Paradigma11`; may be hardcoded
   if the secret is absent) → output `NUGET_API_KEY`.
2. nuget.org push of the **same** `.nupkg` already packed (no re-pack):
   ```sh
   dotnet nuget push "artifacts/packages/FS.GG.Governance.Cli.*.nupkg" \
     --source https://api.nuget.org/v3/index.json \
     --api-key "${{ steps.login.outputs.NUGET_API_KEY }}" \
     --skip-duplicate
   ```
   Runs only when `push == 'true'` (dry-run skips it).

### Reference-gate-set path (NEW job, e.g. `publish-reference-gate-set`)

Gated and ordered like the CLI path; `id-token: write` + `packages: write`.

1. Checkout + set up .NET.
2. **Pack (self-gated)** — `dotnet fsi pack-reference-gate-set.fsx --output artifacts/packages`. The
   script runs the G1–G7 guard and refuses to pack when red (FR-004); the derived version comes from
   the four `schemaVersion` declarations. (CI may instead run the guard as a separate step and pack
   with `--no-gate`; either way the guard is a hard pre-push gate.)
3. **Assert the package was produced** — fail loudly if no
   `FS.GG.Governance.ReferenceGateSet.*.nupkg` exists (green gate + empty pack MUST NOT report
   success — FR-007).
4. **Push org feed first** — `--source https://nuget.pkg.github.com/FS-GG/index.json --api-key
   ${{ secrets.GITHUB_TOKEN }} --skip-duplicate`.
5. **`NuGet/login@v1`** → `NUGET_API_KEY`.
6. **Push nuget.org** — same `.nupkg`, `--source https://api.nuget.org/v3/index.json --api-key
   ${{ steps.login.outputs.NUGET_API_KEY }} --skip-duplicate`. Skipped on dry-run.

## Behavioral guarantees

- **Trusted Publishing only** — no `NUGET_ORG_API_KEY`; the key is the ~1 h `NuGet/login` output.
  Login + push are in **this** workflow file (never a reusable workflow — NuGet/login#6, ADR-0013 §2).
- **Byte-identical** — the nuget.org push uses the artifact already packed for the org feed; no
  second `dotnet pack` (ADR-0012 §3).
- **Org-feed-first** — org GitHub Packages push precedes the nuget.org push (ADR-0012 §4).
- **Fail-closed** — a missing/mismatched trust policy makes `NuGet/login` `401` and fails the run;
  nothing is silently skipped (FR-006; ADR-0013 §5).
- **Idempotent** — `--skip-duplicate` on both feeds; re-publishing an existing version is a no-op
  success (FR-007). A failed nuget.org push after a durable org-feed push is retry-safe.
- **Dry-run safe** — `workflow_dispatch` with no `version` packs both packages but pushes to no feed
  (FR-008).
- **Drift-safe** — no edits to org-synced `Directory.Build.props` / `Directory.Packages.props` /
  `.config/dotnet-tools.json`; any tool install is job-scoped (spec 088 D6).

## Non-goals

- Publishing the full ~70-package `FS.GG.Governance.*` set (H4/088-adjacent) — only the two
  in-scope packages.
- A reusable/org-shared publish workflow (explicitly rejected for trusted publishing — ADR-0013 §3).
- Reserving the `FS.GG.` ID prefix (a follow-on admin step — #103).
