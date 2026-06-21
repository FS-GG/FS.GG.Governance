# Contract: Agent-Review Cache-Key Public API

The public surface of `FS.GG.Governance.AgentReviewKey` — the sole declaration is the two `.fsi` files. This
contract fixes the signatures and the laws each must satisfy; the surface-drift baseline
(`surface/FS.GG.Governance.AgentReviewKey.surface.txt`) is the byte-level guard.

## Module `FS.GG.Governance.AgentReviewKey.Model`

Declares the types in [data-model.md](../data-model.md): the reused `FreshnessKey.Model` newtypes `RuleHash` /
`ArtifactHash` (open), the new opaque newtypes (`ModelId`, `ModelVersion`, `ReviewerPromptHash`, `ModelConfig`,
`QuestionText`), the `AgentReviewInputs` record, the `CacheKey` newtype, the `ReviewInput` DU, and:

```fsharp
/// Stable, human-readable wire token for a ReviewInput (for `diff` output and messages).
/// Deterministic, total, and INJECTIVE over the 7 cases. This vocabulary is DISTINCT from the
/// terse encoding tags inside the key string (agent-review-key-format.md) — see the table below.
val inputToken: input: ReviewInput -> string
```

**`inputToken` table** (the committed readable vocabulary — distinct from the key's internal encoding tags):

| `ReviewInput` | `inputToken` | (key encoding tag, for contrast) |
|---|---|---|
| `ModelIdInput` | `modelId` | `mid` |
| `ModelVersionInput` | `modelVersion` | `mver` |
| `PromptHashInput` | `promptHash` | `prompt` |
| `ModelConfigInput` | `modelConfig` | `cfg` |
| `CheckHashInput` | `checkHash` | `chk` |
| `ReviewedArtifactsInput` | `reviewedArtifacts` | `art` |
| `QuestionTextInput` | `questionText` | `q` |

## Module `FS.GG.Governance.AgentReviewKey` (operations)

```fsharp
/// Render the seven agent-review inputs to their canonical, deterministic, byte-stable cache key
/// (contracts/agent-review-key-format.md). Pure and TOTAL: defined for every AgentReviewInputs value;
/// reads no clock, filesystem, git, environment, or network; invokes no model; hashes no bytes.
/// Reviewed artifacts are compared as a SET (order and duplication never affect the result). The
/// encoding is INJECTIVE across inputs: the same opaque string placed in two different inputs yields
/// different keys. BCL string building only; no hashing.
val compute: inputs: AgentReviewInputs -> CacheKey

/// The cache-hit predicate: true IFF the two input sets agree on EVERY input — i.e. their keys are
/// equal. Total. `matches a b` is defined as `compute a = compute b`, so predicate and key never
/// disagree. (Foundation of the later "invalidate cached verdicts when judge/prompt identity changes"
/// row.)
val matches: a: AgentReviewInputs -> b: AgentReviewInputs -> bool

/// The no-hide explainer: the inputs whose values differ between two input sets, in a fixed order.
/// Empty IFF `matches a b`. Reviewed artifacts are compared as a set. Total. The observable face of
/// "a judge or prompt change invalidates prior cached verdicts."
val diff: a: AgentReviewInputs -> b: AgentReviewInputs -> ReviewInput list

/// Unwrap a CacheKey to its canonical string (for storage, messages, tests). Total.
val value: key: CacheKey -> string
```

## Laws (verified by the test project)

| Law | Statement | Tests / SC |
|---|---|---|
| **Determinism** | `compute x = compute x` byte-for-byte, every time, anywhere. | DeterminismTests, PurityTests / SC-001, SC-004 |
| **Set semantics** | Reordering or duplicating `ReviewedArtifacts` leaves `compute`, `matches`, and `diff` unchanged; the empty set keys to a distinct value. | SetSemanticsTests / SC-002 |
| **Reflexive match** | `matches x x = true` and `diff x x = []`. | DiffTests / SC-001 |
| **Single-input distinction** | Changing exactly one input ⇒ keys differ, `matches = false`, and `diff` = exactly that input. Holds for every one of the seven. | ComputeTests, DiffTests / SC-001, SC-003 |
| **Cross-input injectivity** | Moving the same opaque string from one input to another changes the key; a token containing `:`/`=`/`;`/`\n` cannot spoof a field boundary. | InjectivityTests / SC-005 |
| **Predicate/key agreement** | `matches a b ⇔ (compute a = compute b)`. | DiffTests |
| **Diff/predicate agreement** | `diff a b = [] ⇔ matches a b`; `diff` lists every differing input and no other. | DiffTests / SC-003 |
| **Totality** | Every value of `AgentReviewInputs` (incl. empty artifact set, empty-string tokens, a token equal to another input's text) yields a `CacheKey` with no exception. | ComputeTests, InjectivityTests / FR-007 |
| **Purity** | The key for a fixed input is identical across changed cwd, time, and unrelated filesystem state; no model invoked, no bytes hashed. | PurityTests / SC-006 |
| **Worked-example pin** | `compute` of the documented example equals the byte-exact key in agent-review-key-format.md. | ComputeTests / SC-001 |

## Scope guard (negative contract)

- The assembly references **only** `FSharp.Core`, `FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.Config`
  (transitive, via FreshnessKey), and the BCL (`System.*` / `System.Private.CoreLib` / `netstandard` /
  `mscorlib`). It MUST NOT reference `Gates`, `Snapshot`, `Route`, any `Adapters.*`, `Host`, `Cli`, or any
  host/edge assembly — verified by the `SurfaceDrift` scope-hygiene test (the AuditJson/F029–F034 precedent).
- No new third-party `PackageReference` (FR-011).
- Exactly the two modules above are public; no token/encoding/buffer helpers leak (hidden by the `.fsi`).
- This core carries **no** cached verdict and runs **no** cache store / lookup / invalidation operation
  (FR-009); its sole outputs are `CacheKey`, `matches`, `diff`, and `value`.
