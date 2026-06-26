# Contract: `scaffold-manifest.json`

**Feature**: `071-runtime-project-templates` · **Schema version**:
`fsgg.scaffold-manifest/v1` · **Producer**:
`FS.GG.Governance.ScaffoldManifestJson.ofManifest : ScaffoldManifest -> string`.

The deterministic, machine-readable provenance record of one scaffold run (FR-005,
FR-010, FR-012). A pure, total projection over the typed `ScaffoldManifest`
(data-model §6): no I/O, no clock, no git, never throws, **byte-identical** for
identical input. It carries **no** absolute target path, wall-clock timestamp, or
environment value, so the same provider over the same empty target yields an
identical manifest on any machine (SC-004, D6).

## Field order & shape

One top-level JSON object, fields emitted in this fixed order:

| Field | Type | Notes |
|-------|------|-------|
| `schemaVersion` | string | always `"fsgg.scaffold-manifest/v1"` |
| `outcome` | string | `"noProvider"` \| `"scaffolded"` \| `"refused"` |
| `refusal` | object \| null | present (non-null) **iff** `outcome = "refused"`; otherwise `null` |
| `provider` | object \| null | `{ "id": string, "contractVersion": "M.m" }`; `null` only when `outcome = "noProvider"` |
| `generated` | array | provider-owned paths written; `[]` unless `outcome = "scaffolded"` |
| `collisions` | array of string | pre-existing/reserved paths that forced a refusal; `[]` otherwise |

### `refusal` object (closed `reason` token, no wildcard)

```json
{ "reason": "contractMismatch", "declaredVersion": "2.0" }
{ "reason": "providerUnavailable", "detail": "…" }
{ "reason": "outOfTarget",  "paths": ["…"] }
{ "reason": "collision",    "paths": ["…"] }
{ "reason": "providerErrored", "detail": "…" }
```

### `generated[]` element

```json
{ "path": "src/App/Program.fs", "ownership": "providerOwned" }
```

Every generated path is target-**relative** and tagged `providerOwned` so later
lifecycle/Governance steps never mistake it for a lifecycle-authored source
(FR-005, FR-006).

## Ordering & determinism rules

- `generated` is sorted **ascending by `path`**; `collisions` and any `paths` array
  are sorted **ascending**. No element is dropped, added, or truncated.
- `contractVersion` renders as `"<Major>.<Minor>"`.
- Closed-DU tokens (`outcome`, `refusal.reason`, `ownership`) are exhaustive and
  wildcard-free — a new case is a compile error, never a mistoken field.

## Worked examples

**Success** (`outcome = scaffolded`):

```json
{
  "schemaVersion": "fsgg.scaffold-manifest/v1",
  "outcome": "scaffolded",
  "refusal": null,
  "provider": { "id": "acme.fsharp-lib", "contractVersion": "1.0" },
  "generated": [
    { "path": "src/App/App.fsproj", "ownership": "providerOwned" },
    { "path": "src/App/Program.fs", "ownership": "providerOwned" }
  ],
  "collisions": []
}
```

**Collision refusal** (`outcome = refused`, nothing written):

```json
{
  "schemaVersion": "fsgg.scaffold-manifest/v1",
  "outcome": "refused",
  "refusal": { "reason": "collision", "paths": ["src/App/Program.fs"] },
  "provider": { "id": "acme.fsharp-lib", "contractVersion": "1.0" },
  "generated": [],
  "collisions": ["src/App/Program.fs"]
}
```

**No provider selected** (the seam was a no-op; in practice the host writes no
manifest at all on this path — FR-002 — but the projection is total over the value):

```json
{
  "schemaVersion": "fsgg.scaffold-manifest/v1",
  "outcome": "noProvider",
  "refusal": null,
  "provider": null,
  "generated": [],
  "collisions": []
}
```

## Determinism test obligations (SC-004, SC-006)

- Same provider over the same empty target ⇒ byte-identical manifest text.
- Field-exclusion sweep: no input value injects an absolute path, clock, or
  environment value into the output.
- 100% of `generated[]` paths are attributable to the `provider` id + contract
  version from the manifest alone, without re-inspecting the provider.
