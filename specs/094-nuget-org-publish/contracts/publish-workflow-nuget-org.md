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
- **Dry-run safe** — `workflow_dispatch` with no `version` packs the selected scope but pushes to no feed
  (FR-008).
- **Drift-safe** — no edits to org-synced `Directory.Build.props` / `Directory.Packages.props` /
  `.config/dotnet-tools.json`; any tool install is job-scoped (spec 088 D6).

## Non-goals

- Publishing the full ~70-package `FS.GG.Governance.*` set (H4/088-adjacent) — only the two
  in-scope packages.
- A reusable/org-shared publish workflow (explicitly rejected for trusted publishing — ADR-0013 §3).
- Reserving the `FS.GG.` ID prefix (a follow-on admin step — #103).

## Config resolver library extension (GOV-423-C3)

`publish-config` selects one additional ordinary net10.0 library, `FS.GG.Governance.Config`, currently
source version 0.3.0. It is neither a CLI tool nor a content package. Manual `scope` is a closed
`all`/`config` choice, defaulting to `all`. Omitted scope and release/tag runs retain CLI-led version
selection; Config-only mode is manual-only and evaluates Config without evaluating CLI. An explicit
manual version must match the selected project; omission remains no-push. Unknown scope fails
before MSBuild. The resolver exports validated scope, version, push and recovery outputs.

Config-only omitted-version runs also perform and retain read-only both-feed collision/visibility observation with the same hosted identity; all login/push steps remain false. Default all-scope omitted-version behavior stays unchanged.

Config-only mode runs only `resolve-version` and `publish-config`. All eight unrelated jobs,
including CLI tests/enforcement, all other pack/push jobs and ReferenceGateSet, explicitly require
validated scope `all`. CLI tests now depend on the resolver; all other dependency edges remain.
Config independently reevaluates its own package version before packing in either mode. No package
version or trigger changes. Config alone adds `actions: read` for same-repository native qualification and retained-artifact observation.

The ordered contract is static preflight, cold locked restore of the Config test project, **all** Config
Release tests, evaluated Config version, one pack, exactly one scoped archive with captured SHA-256,
package-only consumer smoke, native qualification/closure verification and retention of original archive, test report, manifest, consumer locks/assets and selected Contracts archive, independent both-feed collision
observation, org push, OIDC login and same-file/hash public push. All login/push steps require
`needs.resolve-version.outputs.push == 'true'`; effects additionally require independently observed absence on their feed. Matching payload is retained without replacement. Omission of dispatch version performs no login/push.
The consumer verifies package ID/version/source/TFM/dependencies and public resolver/legacy loading,
with explicit source mapping, exact committed dependency closure and empty caches. See
[`tests/config-package-smoke/README.md`](../../../tests/config-package-smoke/README.md).

Retention precedes every publication effect. `--skip-duplicate` is not equality evidence. A pre-existing
version must be independently payload-matched before either push; unreadable results and ambiguous org
404s refuse pending root observation. An org 404 is resolved only by successful authenticated package/version
enumeration establishing absence, using the existing package-read authority. Raw signed archives/hashes remain distinct; only an independently
verified repository signature may be excluded when comparing payload entries. A second-feed failure
requires observation under the original operation and retained bytes, never automatic repacking.

Recovery uses only a complete manual Config selection of `config_run_id`, `config_artifact_id` and
`config_package_sha256`, with an explicit matching Config version at the exact original source SHA.
The original run must be a completed first attempt of this repository's `publish.yml`, with native
Config-only scope/version selection and successful resolver, full tests, pack, consumer, complete
retention verification and artifact upload stages. A failed overall run is eligible only when those
native stages succeeded. Reruns, no-push candidates and copies uploaded by recovery are ineligible.
The artifact ID must belong to that original run/source/repository and successful retention window;
its native Actions digest and pinned nupkg digest must both match. Missing/expired/ambiguous evidence
refuses. The existing archive reader checks original identity, producer/consumer closure and retained
selected Contracts 7.6.0 archive/DLL identities. A local receipt alone is never qualification authority.

Recovery performs no restore, tests, pack or consumer regeneration. It reuses the original physical
archive, then the existing collision and org-first/public stages, rechecking the original hash before
each effect. Recovery observations are retained with the original native evidence. Root must observe
original effects and reservations before selecting recovery; unknown writes or overlapping writers
fence the affected operation. Artifact IDs/digests, run/attempt and package hashes remain distinct.
Retain the verified candidate independently of the runner before effects through the existing
artifact upload. Copy it to durable private custody while that artifact is available and retain
the copy past unresolved publication; an expiring Actions URL is not lasting custody. Independent both-feed payload readback and normal public consumer restore
remain required before declaring distribution complete.

Source delivery does not admit publication or whole-workflow dispatch. A newly selected manual
Config-only release may proceed after actual C1+C2/C2.3 coherent qualification, selected Contracts
compatibility, complete candidate-bound Config qualification and both-feed collision admission.
Its new operation, source and archive identities are independent of the retained ReferenceGateSet
1.8.0 candidate: it neither consumes nor clears that candidate's custody or any original operation
hold. Historical recovery, ReferenceGateSet publication/adoption, whole-workflow publication and
C3 completion retain their existing applicable prerequisites. Root selects the new operation and
checks known reservations; no unknown Config writer may overlap its package/version/write-set.
Public availability, normal consumer pins, native execution, installed acceptance and C3/C4 completion
remain separate. No historical published Config API baseline is invented.
