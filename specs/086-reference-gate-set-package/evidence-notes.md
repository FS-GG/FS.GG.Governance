# Tier-1 & Evidence Notes — 086 Reference Gate Set Package

Recorded per T015. This feature is **Tier 1** (a new published cross-repo *contract*: the
`FS.GG.Governance.ReferenceGateSet` package id + content layout + version-derivation rule).

## Principle IV (Elmish/MVU) — **N/A**

No multi-step stateful/I/O F# workflow is authored. `pack-reference-gate-set.fsx` is a linear
`read → derive → gate → pack` build step; all I/O (`Process.Start`, file reads, the `dotnet`
invocations) lives at the script edge, mirroring the existing `build.fsx`. There is no `Model`/
`Msg`/`update` surface to test, so the MVU obligations do not apply.

## `.fsi` / surface-baseline obligations — **vacuously satisfied**

The feature authors **no public F# surface**: the packaging project has no `Compile` items and
ships **no assembly** (`IncludeBuildOutput=false`); the pack logic is a script, not a packed
library; the guard test is internal (Tier 2 for the test code). The Tier-1 *contract surface* is
the **package id + content layout + version-derivation rule**, which is what gets registered in
`FS-GG/.github` (`registry/dependencies.yml` + compatibility projection + the version-rule ADR).
No `.fsi` and no `surface/*.surface.txt` baseline changes — there is no API to baseline.

## Test evidence — **real only (Principle V), no synthetic fixtures**

All guard evidence runs against real artifacts:

- The **packed `.nupkg`** is produced by the actual `pack-reference-gate-set.fsx` in the test's own
  setup (never a pre-staged file); byte-identity (SC-002) and content-only (SC-005, no `lib/`, no
  `<dependencies>`) are asserted by unzipping that real archive and comparing against the on-disk
  `samples/sdd-reference-gate-set/.fsgg/*.yml`.
- The **derived version** is asserted via the script's actual `--print-version` output — not a
  re-encoded copy of the rule. The distinguishability check (SC-003) mutates a **temp-dir copy** of
  a sample (`schemaVersion: 1 → 2`) and re-derives — real I/O, no mock.
- The **gate-fires** check (FR-004/SC-004) breaks a G1–G7 invariant on a temp-dir copy and confirms
  the script exits non-zero and writes **no** `.nupkg` — real process, real exit code.
- The **installed** copy was re-validated through the real `Config → Gates → Routing → Route →
  Enforcement` pipeline (SC-004) by pointing the 079 guard at the materialized
  `…/contentFiles/any/any/.fsgg/`.

No `// SYNTHETIC:` disclosures are required — nothing in this feature rests on synthetic evidence.

## Two small design decisions worth recording

1. **`--source` flows through the gate via `FSGG_REFERENCE_GATE_SET_DIR`.** The 079
   `ReferenceGateSetGuard` was extended to honor an optional env var naming the reference dir,
   defaulting to the canonical `samples/sdd-reference-gate-set` (behavior-preserving — unset ≡
   canonical). This lets the pack gate validate a `--source` temp copy (a broken invariant, or a
   bumped `schemaVersion`) without mutating the canonical samples, keeping a **single** guard
   implementation (no duplicated G1–G7 logic).
2. **Test hooks on the pack script: `--output <dir>` and `FSGG_PACK_GATE_NO_BUILD`.** The package
   guard packs into a temp dir (`--output`) so an automated run neither depends on nor pollutes the
   shared `~/.local/share/nuget-local/` feed, and sets `FSGG_PACK_GATE_NO_BUILD=1` so the nested
   pre-pack gate runs `dotnet test --no-build` against the already-built guard assembly instead of
   contending on a rebuild of the currently-running test DLL. Both default off, so standalone/CI
   `dotnet fsi pack-reference-gate-set.fsx` behaves exactly as documented in quickstart.
