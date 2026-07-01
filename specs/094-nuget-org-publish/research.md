# Phase 0 Research: Publish Governance packages to public nuget.org

All unknowns were resolvable from the governing ADRs and direct repo inspection — no `NEEDS CLARIFICATION` remained after Phase 0.

## D1 — Authentication mechanism: Trusted Publishing (OIDC), not an API key

- **Decision**: Authenticate every nuget.org push with **Trusted Publishing (OIDC)**. The publish job requests `permissions: id-token: write`, runs `NuGet/login@v1` (owner/profile `Paradigma11`) to exchange the GitHub OIDC token for a short-lived (~1 h) nuget.org key, then `dotnet nuget push … --api-key ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`.
- **Rationale**: [ADR-0013](https://github.com/FS-GG/.github/blob/main/docs/adr/0013-trusted-publishing-oidc-for-nuget-org.md) supersedes ADR-0012 §6/§4-auth. No long-lived secret to store, rotate, or leak. FS-GG/.github#103 confirms the Governance Trusted Publishing policy (Repository `FS.GG.Governance`, Workflow **`publish.yml`**) is **Active**, so `NuGet/login` will succeed.
- **Alternatives considered**: (a) `NUGET_ORG_API_KEY` long-lived secret — the original #41-body approach; **rejected**, superseded by ADR-0013 (secret-handling risk). (b) A cross-repo **reusable** `nuget-org-push.yml` (`.github#104`) — **rejected**: nuget.org matches the trust policy against the repo *and workflow file where the token is minted*; a reusable workflow 401s "No matching trust policy" (NuGet/login#6). ADR-0013 retired it.

## D2 — Everything nuget.org for Governance runs from `publish.yml`

- **Decision**: Both packages' nuget.org pushes (and their `NuGet/login`) live in **`publish.yml`**, not `gate.yml` and not a new workflow file.
- **Rationale**: The trust policy is registered against the workflow **filename** `publish.yml` (#103). OIDC exchange only succeeds from that file. Consolidating keeps the whole Governance nuget.org surface under one policy.
- **Alternatives considered**: A dedicated `release.yml` mirroring SDD/Rendering naming — **rejected**: the policy is already bound to `publish.yml`; renaming would require an admin policy change and re-provisioning (#103), unnecessary churn.

## D3 — The reference gate set has NO feed-publish path today; this feature adds one

- **Decision**: Add a `reference-gate-set` publish job to `publish.yml` that packs via `pack-reference-gate-set.fsx` and pushes the resulting `.nupkg` to **both** feeds (org-feed-first, then nuget.org via trusted publishing).
- **Rationale**: Repo inspection shows `gate.yml`'s `reference-gate-set-pack` job only packs into a **throwaway temp feed** to guard byte-identity (G1–G7) — it never pushes to a real feed. `publish.yml` is Cli-only. So `FS.GG.Governance.ReferenceGateSet` is currently unpublishable by any consumer. The spec scopes making it publicly resolvable, so a real publish path is required, not just the nuget.org leg.
- **Gating**: The publish job runs the G1–G7 reference-set guard as a hard pre-push gate (the pack script already refuses to pack when the guard is red — FR-004), so the shipped artifact is provably the tested artifact.
- **Alternatives considered**: (a) Publish the gate set from `gate.yml` — **rejected**: `gate.yml` runs per-PR and has no version-bearing release trigger, and the trust policy is bound to `publish.yml`. (b) Leave the gate set org-feed-only — **rejected**: contradicts spec US2/FR-002 (public availability) and ADR-0012's explicit "intentionally public" scoping.

## D4 — Version sources stay authoritative and unchanged

- **Decision**: CLI publishes at its evaluated fsproj `<Version>` (currently `1.2.0`); the gate set publishes at its **schema-derived** version (`{governance}.{capabilities}.{policy}.{tooling}` from the four `schemaVersion` declarations), computed by `pack-reference-gate-set.fsx`.
- **Rationale**: Preserves the existing single-source-of-truth rules (the CLI resolve-version job; the pack script's `derivedVersion`). ADR-0007 (schema-derived) and ADR-0003 (permanent IDs) are unchanged. A version-bearing tag must equal the CLI version or the run fails (already enforced).
- **Alternatives considered**: A unified release version across both packages — **rejected**: the two versioning rules are independent contracts; forcing one violates ADR-0007.

## D5 — Byte-identical artifact, org-feed-first ordering

- **Decision**: Pack **once** per package; push the same `.nupkg` file to the org feed first, then to nuget.org. No second `dotnet pack`.
- **Rationale**: ADR-0012 §3 (no re-pack — the two feeds must serve identical bytes) and §4 (org feed authoritative, pushed first; nuget.org is the additive public mirror). A failed nuget.org push after a successful org-feed push is retry-safe via `--skip-duplicate`.
- **Alternatives considered**: Re-pack per feed — **rejected**: risks divergent bytes/timestamps and breaks the "same artifact on both feeds" acceptance (SC-001/US1 scenario 3).

## D6 — Listing metadata placement (drift-safe)

- **Decision**: Put **shared** listing metadata — `RepositoryUrl` (+`RepositoryType`), `PackageLicenseExpression=MIT`, `PackageProjectUrl`, `PackageIcon`, `Authors` — in `Directory.Build.local.props` (repo-owned, drift-exempt), scoped so it only affects packable projects. Keep **package-specific** bits (`Description`, `PackageTags`, `PackageReadmeFile` target) in each `.fsproj`. Add a packed README and an icon asset.
- **Rationale**: The org-synced `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` are byte-identity drift-checked by `gate.yml` (spec 088 D6) and MUST NOT be edited. `Directory.Build.local.props` is explicitly the repo-owned, drift-exempt override file (imported last). Only `FS.GG.Governance.Cli` and `…ReferenceGateSet` set `IsPackable=true`, so the shared metadata is inert for the ~70 non-packable projects. License is **MIT** (confirmed from the repo `LICENSE`, `Copyright (c) 2026 EHotwagner`).
- **Open items for Phase 1/implementation**: which README to pack (a short packaging README vs. the root `README.md`) and the icon asset itself (none exists yet — a small PNG must be added and `Pack="true"` with `PackagePath`). Captured in `contracts/package-listing-metadata.md`.
- **Alternatives considered**: Per-`.fsproj` duplication of all metadata — **rejected**: duplicates the repo URL/license/icon across two projects and future packables; the shared props file is the single source. Editing the org-synced props — **rejected**: fails the drift check.

## D7 — Fail-closed, dry-run, and idempotency are intrinsic, not bolt-on

- **Decision**: Rely on the mechanisms already present plus the OIDC guardrail — a missing trust policy makes `NuGet/login` return `401` and fails the run (no silent skip); the existing dry-run path (`workflow_dispatch` with no `version`) packs but sets `push=false` and skips all pushes; `--skip-duplicate` makes re-publishing an existing version an idempotent no-op on both feeds.
- **Rationale**: ADR-0013 §5 ("fail-closed is intrinsic"); FR-006/FR-007/FR-008. No new secret to check means no new fail-open path.
- **Alternatives considered**: An explicit "is the key set?" guard — **rejected as unnecessary**: there is no key; the feed itself is the guardrail.

## D8 — Coherence outcome

- **Decision**: Once both packages resolve on nuget.org at their current versions, the cross-repo registry id `nuget-org-published` advances toward `coherent: true`. This is a coordination follow-up recorded via the cross-repo protocol (a note back on FS.GG.Governance#41 / the `.github` registry), not a code change in this repo.
- **Rationale**: FR-011; ADR-0013 Consequences. The `FS.GG.` prefix reservation is a separate follow-on admin step (#103) and does not block first publish.
