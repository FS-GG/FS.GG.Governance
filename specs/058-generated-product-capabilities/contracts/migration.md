# Migration guidance: MVP capability catalog (v1) → expanded catalog (v2)

This is the **discoverable migration guidance** the `UnsupportedSchemaVersion` diagnostic points at when a
`.fsgg/capabilities.yml` is loaded at a version other than `2` (FR-011, FR-013, SC-006). `project.yml`,
`policy.yml`, and `tooling.yml` are **unchanged** — they stay on `schemaVersion: 1`. Only `capabilities.yml`
moves to `2`.

## What changed

`capabilities.yml` adds, all **additively** (existing fields keep their meaning):

1. **New surface `kind` tokens** — `package`, `docs`, `skill`, `design`, `sampleApp`, `generatedProduct`
   (alongside the MVP `routine`/`governedRoot`/`protected`/`generatedView`/`release`).
2. **Optional surface attributes** — `evidenceTag` (any kind), `templateProfile` (generatedProduct),
   `baseline` (package).
3. **Optional check `tier`** — `structuralScan`/`restoreBuild`/`focusedTests`/`fullVerify`/
   `releaseValidation`, the cost-tiered generated-product depths.
4. **New protected boundaries** — `package`, `release`, and `generatedProduct` surfaces now escalate an
   unknown in-root path to an `UnknownProtectedBoundaryPath` finding (the no-hide safety property).

## How to migrate (mechanical)

1. Change `schemaVersion: 1` → `schemaVersion: 2` in **`capabilities.yml` only**. Leave the other three
   files at `1`.
2. No other change is required: an MVP-shaped v1 body is a valid v2 body once the version is bumped — the
   new fields/kinds are optional and default to absent. The catalog validates identically (same domains,
   path map, surfaces, checks).
3. To declare product surfaces, add `kind:` values from the new set and the optional attributes as needed
   (see [capabilities-schema-v2.md](./capabilities-schema-v2.md)).

## Why a hard bump (not silent acceptance)

A `capabilities.yml` left at `schemaVersion: 1` is **rejected** with `UnsupportedSchemaVersion` naming the
version and this guidance — never best-effort mis-parsed (FR-013). The bump is a deliberate, visible signal
that the catalog vocabulary expanded, so a half-migrated catalog cannot run as if nothing changed. Because
the v1 body is a structural subset of v2, the migration itself is the one-line version bump above.

## Verification

- A v1 `capabilities.yml` ⇒ `Invalid [UnsupportedSchemaVersion]`, message contains the version `1` and a
  pointer to this file (fixture `migration-v1-capabilities`).
- The same body at `schemaVersion: 2` ⇒ `Valid` with identical typed facts to the pre-expansion MVP result
  (fixtures `valid-*`, post-migration).
- A `capabilities.yml` at `3` (or `0`) ⇒ `Invalid [UnsupportedSchemaVersion]` naming the version
  (`malformed-unsupported-schema-version`).
