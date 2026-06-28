# Contract: `FS.GG.Governance.ReferenceGateSet` content package

The external surface this feature exposes. This is the *interface* consumers (Templates#14) and the org
registry pin against. It is the Tier-1 contract surface for this feature (there is no F# API surface).

## 1. Package identity

- **Package id**: `FS.GG.Governance.ReferenceGateSet` (exact, FR-001).
- **Kind**: NuGet **content-only** package — no `lib/`, no assembly, no dependency group (FR-007/SC-005).
- **Version**: derived deterministically from the contained schema versions (§3).

## 2. Content layout (what a consumer gets)

Inside the `.nupkg`, exactly the four reference files, byte-identical to
`samples/sdd-reference-gate-set/.fsgg/` (FR-002/FR-003/SC-002):

```
contentFiles/any/any/.fsgg/governance.yml
contentFiles/any/any/.fsgg/capabilities.yml
contentFiles/any/any/.fsgg/policy.yml
contentFiles/any/any/.fsgg/tooling.yml
```

**Stability guarantee (FR-005/FR-009)**: after restore, the four files materialize at the
**version-stable, predictable** path

```
<nuget-global-packages>/fs.gg.governance.referencegateset/<version>/contentFiles/any/any/.fsgg/
```

The relative tail `contentFiles/any/any/.fsgg/<file>` does **not** change across versions, so a consumer
drift gate's comparison path is stable on upgrade.

**Content-only guarantee (FR-007/SC-005)**: the package carries no `lib/<tfm>/` folder and declares no
package dependencies; reading the files requires no governance runtime/assembly reference.

## 3. Version-derivation rule (FR-006/SC-003)

```
Version = "{governance}.{capabilities}.{policy}.{tooling}"   # schemaVersion of each file, fixed order
```

- Current value: **`1.2.1.1`** (governance=1, capabilities=2, policy=1, tooling=1).
- **Deterministic**: identical schema versions always yield the identical string (no clock/env input).
- **Distinguishable**: a bump to any one file's `schemaVersion` changes exactly one segment (e.g. a
  `policy.yml` bump to 2 → `1.2.2.1`).
- Consumers SHOULD pin **exact** (`[1.2.1.1]`) to lock a coherent reference set (US3).
- The numbering is recorded as an ADR in `FS-GG/.github` (FR-008) — the rule is itself a contract.

## 4. Production guarantees (producer-side, FR-004/SC-004)

- The package is produced **only** when the G1–G7 reference-set guard passes; a red guard aborts pack
  with a non-zero exit before any `.nupkg` is written. The shipped artifact is therefore provably the
  validated artifact.
- The packed bytes are verified byte-identical to the on-disk samples by an automated guard (SC-002).
- Pack output location: `~/.local/share/nuget-local/` (constitution).

## 5. Distribution status

- **In scope now**: a consumable `.nupkg` via local/CI `dotnet pack`, and a registry contract entry
  (`FS-GG/.github` `registry/dependencies.yml` + regenerated `docs/registry/compatibility.md`).
- **Deferred**: push to the org GitHub Packages feed (admin-blocked, `.github#21`). The registry entry
  records the deferred-feed status and links its PR.

## 6. Consumer expectation (Templates#14 — informative, out of scope to build here)

A consumer overlay drift gate restores this package, then runs a `git diff --exit-code`-style comparison
of its `fs-gg-governance/.fsgg/*.yml` overlay against the materialized
`…/contentFiles/any/any/.fsgg/*.yml`. Byte-identical → exit 0; any divergence → non-zero (a stale overlay
fails the build instead of shipping). This feature delivers the source of truth that gate consumes; the
gate itself is built in the Templates repo.

## 7. Acceptance evidence map

| Contract clause | Verified by |
|-----------------|-------------|
| §2 four files, byte-identical | `.nupkg` unzip vs on-disk byte compare (SC-002) |
| §2 content-only (no `lib/`, no deps) | `.nupkg` has no `lib/` entry, empty dependency group (SC-005) |
| §3 version rule + distinguishability | `--print-version` assert + temp-dir `schemaVersion` bump (SC-003) |
| §4 gated production | pack aborts when G1–G7 red (FR-004) |
| §3/§5 registered contract | `registry/dependencies.yml` entry + compatibility projection (SC-006) |
