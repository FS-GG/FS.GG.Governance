# Phase 0 Research: Centralize an intentional VersionPrefix

**Feature**: 109-version-prefix-centralize · **Date**: 2026-07-03 · **Spec**: [spec.md](./spec.md)

The spec left one thing intentionally open: the centralized version *value* and whether the
version-less packable tools must be pinned to avoid regressing a published artifact. Phase 0
resolved that by reading the real tree, the publish pipeline, and the api-baseline gate. Below are
the decisions with the evidence behind each.

## Ground truth (measured, not assumed)

`git ls-files '*.fsproj'` → **171** projects. **104** carry no `<Version>`/`<VersionPrefix>` and so
fall back to MSBuild's default `VersionPrefix` of **`1.0.0`**. Of those 104, exactly **4 are
packable**; the other 100 are `IsPackable=false` (tests, adapters, internal command-libs, the
sample) and never emit a `.nupkg`.

| Project | `IsPackable` | `PackAsTool` | Effective `Version` today | Published? |
|---|---|---|---|---|
| `RouteCommand` (`fsgg`) | true | true | **1.0.0** (default) | **No** — out of `publish.yml` scope |
| `EvidenceCommand` (`fsgg-evidence`) | true | true | **1.0.0** (default) | **No** |
| `CacheEligibilityCommand` (`fsgg-cache-eligibility`) | true | true | **1.0.0** (default) | **No** |
| `ReferenceGateSet` | true | false | **1.0.0** (default) | **Yes** — but version is `-p:Version` injected |
| `Cli` (`fsgg-governance`) | true | true | **1.2.0** (explicit `<Version>`) | **Yes** — version = fsproj `<Version>` |
| ~66 internal libs | true | false | 0.1.0 (Kernel 0.1.1) explicit | packed for api-baseline only |
| ~100 tests/adapters/sample | false | — | 1.0.0 (default) | No |

## Decision D1 — Value: centralize `<VersionPrefix>0.1.0</VersionPrefix>` in `Directory.Build.local.props`

**Decision**: Declare a single `<VersionPrefix>0.1.0</VersionPrefix>` in the repo-owned, drift-exempt
`Directory.Build.local.props`. All 104 version-less projects inherit it; every project that already
pins an explicit `<Version>` keeps it (MSBuild precedence: explicit `Version` wins over `VersionPrefix`).

**Rationale**:
- `0.1.0` is the repo's established baseline — the ~66 explicitly-versioned libraries already pin
  `0.1.0` (Kernel `0.1.1`). Centralizing `0.1.0` makes the version-less projects *match* the dominant
  intentional line instead of MSBuild's unowned `1.0.0`. This is precisely the "consistent and
  intentional" outcome #63 asks for.
- Placement in `Directory.Build.local.props` (imported last, drift-exempt) is mandatory: an edit to
  the org-synced `Directory.Build.props` would be reverted by the next shared-build-config sync and
  fail the drift check (README of that file; spec 085 relocated all repo-specific props here).

**Alternatives considered**:
- *Centralize `1.0.0`* (match the current default): rejected — it would raise the ~66 libs' baseline
  intent to `1.0.0` (they're deliberately `0.1.x`), or require leaving them explicit while the
  centralized default disagrees with them. Inconsistent.
- *Strip the explicit `<Version>0.1.0</Version>` from the ~66 libs too, so the `VersionPrefix` is the
  single source for everything*: rejected as scope creep — #63 scopes to the *version-less* baseline
  projects, and a 66-file edit risks the api-baseline gate and the two projects that legitimately
  differ (Kernel `0.1.1`, Cli `1.2.0`). Kept tight: add the prefix, let the version-less inherit it.

## Decision D2 — The three unpublished tools align to `0.1.0` (ratified)

**Decision**: `fsgg` / `fsgg-evidence` / `fsgg-cache-eligibility` inherit the centralized `0.1.0`
(moving from the accidental `1.0.0`). No explicit pin is added to them.

**Rationale (why this is safe — evidence)**:
- **They are not published.** `publish.yml` publishes exactly two artifacts and states verbatim that
  "the other ~70 packable FS.GG.Governance.* projects are OUT OF SCOPE here." No consumer resolves
  these three from any feed.
- **The api-baseline gate is unaffected.** `pack-and-apicheck.fsx` selects, per package id, the
  highest feed version *strictly below* the packed version; absent ⇒ `NoBaseline`. These three were
  never pushed, so they have no baseline today and none after — `1.0.0→0.1.0` keeps them at
  `NoBaseline`. Package ids are distinct, so lowering one cannot collide with another id's baseline.
- **No test asserts their package version.** The only `1.0.0`/`1.2.0` literals in the suite are
  SddHandoff `contractVersion` fixtures and pure `Pack.versionDelta`/`versionPolicy` unit inputs —
  none reads these tools' `.nupkg` version.

This is the user-ratified option ("inherit 0.1.0 / align"). The move is recorded as intentional
(spec FR-004); it is **not** a regression of any published artifact.

## Decision D3 — The two published artifacts are provably untouched

**Decision**: Make no version edit to the CLI or ReferenceGateSet; rely on the fact that neither
derives its published version from the centralized default.

**Evidence**:
- **CLI `fsgg-governance`**: `publish.yml` `resolve-version` reads
  `dotnet msbuild …/FS.GG.Governance.Cli.fsproj -getProperty:Version` and treats the fsproj `<Version>`
  as "the sole source of truth" (a release tag must equal it). Cli pins `<Version>1.2.0</Version>`
  explicitly, so `VersionPrefix` cannot change it. Published version stays `1.2.0`.
- **ReferenceGateSet**: its `.fsproj` sets no `<Version>`; the published version is derived from the
  four contained `schemaVersion` declarations and injected by `pack-reference-gate-set.fsx -p:Version=…`.
  The command-line `-p:Version` overrides both `Version` and `VersionPrefix`, so the centralized
  default is inert for the published package. (Its *build-time* `Version` property reads `0.1.0` after
  the change, but that value is never packed.)

## Decision D4 — Verification is a real before/after effective-version map

**Decision**: The acceptance evidence is a per-project `dotnet msbuild -getProperty:Version` capture
across the whole graph, taken before and after the one-line change, plus a `git diff` proving the
three org-synced files are byte-identical, plus a green full `dotnet build`/`test`.

**Rationale**: Principle V (real evidence). The change is a single property; the risk is entirely in
its *blast radius across the graph*, which only a graph-wide effective-version diff can prove. No new
guard test is warranted (unlike the dependency fences): there is no drift-prone allowlist here — the
invariant is "explicit versions unchanged; version-less → 0.1.0," which the before/after map checks
directly and cheaply at review time. (If a standing guard is later wanted, it would assert "every
packable project has an intentional version," but that is out of scope for this chore.)

## P3 / cheap add-ons

- None required. The change is one property plus documentation. No ADR is needed (no new durable
  cross-cutting decision beyond "the repo baseline is `0.1.0`, centralized here"), though the
  declaration-site comment (FR-008/SC-005) records the rationale so a future reader does not "fix"
  the prefix into a published regression.
