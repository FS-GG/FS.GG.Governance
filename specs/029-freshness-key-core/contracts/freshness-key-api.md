# Contract: Freshness Key Public API

The public surface of `FS.GG.Governance.FreshnessKey` — the sole declaration is the two `.fsi` files. This
contract fixes the signatures and the laws each must satisfy; the surface-drift baseline
(`surface/FS.GG.Governance.FreshnessKey.surface.txt`) is the byte-level guard.

## Module `FS.GG.Governance.FreshnessKey.Model`

Declares the types in [data-model.md](../data-model.md): the reused `Config.Model` newtypes (open), the new
opaque newtypes (`RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision`), the
`FreshnessInputs` record, the `Key` newtype, the `InputCategory` DU, and:

```fsharp
/// Stable, human-readable wire token for an InputCategory (for `diff` output and messages).
/// Deterministic, total, and INJECTIVE over the 10 cases. This vocabulary is DISTINCT from the
/// terse encoding tags inside the key string (freshness-key-format.md) — see the table below.
val categoryToken: category: InputCategory -> string
```

**`categoryToken` table** (the committed readable vocabulary — distinct from the key's internal encoding
tags):

| `InputCategory` | `categoryToken` | (key encoding tag, for contrast) |
|---|---|---|
| `CheckIdentity` | `check` | `check` |
| `DomainIdentity` | `domain` | `domain` |
| `CommandIdentity` | `command` | `cmd` |
| `EnvironmentClassCat` | `environmentClass` | `env` |
| `RuleHashCat` | `ruleHash` | `rule` |
| `CoveredArtifactsCat` | `coveredArtifacts` | `art` |
| `CommandVersionCat` | `commandVersion` | `cmdv` |
| `GeneratorVersionCat` | `generatorVersion` | `genv` |
| `BaseRevisionCat` | `baseRevision` | `base` |
| `HeadRevisionCat` | `headRevision` | `head` |

## Module `FS.GG.Governance.FreshnessKey` (operations)

```fsharp
/// Render the freshness inputs to their canonical, deterministic, byte-stable key
/// (contracts/freshness-key-format.md). Pure and TOTAL: defined for every FreshnessInputs value;
/// reads no clock, filesystem, git, environment, or network. Covered artifacts are compared as a SET
/// (order and duplication never affect the result).
val compute: inputs: FreshnessInputs -> Key

/// The reuse predicate: true IFF the two inputs agree on EVERY input category — i.e. their keys are
/// equal. Total. `matches a b` is defined as `compute a = compute b`, so predicate and key never
/// disagree. (Foundation of the later "reuse evidence only when all freshness inputs match" row.)
val matches: a: FreshnessInputs -> b: FreshnessInputs -> bool

/// The no-hide explainer: the categories whose values differ between two inputs, in a fixed order.
/// Empty IFF `matches a b`. Covered artifacts are compared as a set. Total.
val diff: a: FreshnessInputs -> b: FreshnessInputs -> InputCategory list

/// Unwrap a Key to its canonical string (for storage, messages, tests). Total.
val value: key: Key -> string
```

## Laws (verified by the test project)

| Law | Statement | Tests / SC |
|---|---|---|
| **Determinism** | `compute x = compute x` byte-for-byte, every time, anywhere. | DeterminismTests, PurityTests / SC-001, SC-006 |
| **Set semantics** | Reordering or duplicating `CoveredArtifacts` leaves `compute` and `matches` unchanged. | DeterminismTests / SC-002 |
| **Reflexive match** | `matches x x = true` and `diff x x = []`. | InspectionTests / SC-002 |
| **Single-field distinction** | Changing exactly one input category ⇒ keys differ, `matches = false`, and `diff` = exactly that category. Holds for every category. | DistinctionTests, InspectionTests / SC-003, SC-005 |
| **Cross-category injectivity** | Moving the same opaque string from one category to another changes the key. | InjectivityTests / SC-004 |
| **Predicate/key agreement** | `matches a b ⇔ (compute a = compute b)`. | InspectionTests |
| **Diff/predicate agreement** | `diff a b = [] ⇔ matches a b`; `diff` lists every differing category and no other. | InspectionTests / SC-005 |
| **Option distinction** | `None` command/version ≠ any `Some`; `None`/`None` match. | TotalityTests / FR-011 |
| **Totality** | Every value of `FreshnessInputs` (incl. empty artifact set, empty strings, base = head) yields a `Key` with no exception. | TotalityTests / FR-011 |
| **Purity** | The key for a fixed input is identical across changed cwd, time, and unrelated filesystem state. | PurityTests / SC-006 |

## Scope guard (negative contract)

- The assembly references **only** `FSharp.Core`, `FS.GG.Governance.Config`, and the BCL
  (`System.*` / `System.Private.CoreLib` / `netstandard` / `mscorlib`). It MUST NOT reference `Gates`,
  `Snapshot`, `Route`, any `Adapters.*`, `Host`, `Cli`, or any host/edge assembly — verified by the
  `SurfaceDrift` scope-hygiene test (the AuditJson precedent).
- No new third-party `PackageReference` (FR-013).
- Exactly the two modules above are public; no token/encoding/buffer helpers leak (hidden by the `.fsi`).
