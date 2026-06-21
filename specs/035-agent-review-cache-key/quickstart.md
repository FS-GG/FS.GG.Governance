# Quickstart: Agent-Review Verdict Cache-Key Core (F035)

A validation/run guide for the pure `FS.GG.Governance.AgentReviewKey` core. Details live in
[data-model.md](./data-model.md), [contracts/agent-review-key-api.md](./contracts/agent-review-key-api.md), and
[contracts/agent-review-key-format.md](./contracts/agent-review-key-format.md). No host, no CLI, no clock, no
model — every input is a literally-constructible supplied token.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged sibling core `FS.GG.Governance.FreshnessKey` (provides F029's `RuleHash` and `ArtifactHash`, reused
  verbatim).
- No new third-party package is added (FR-011).

## Build

```bash
dotnet build src/FS.GG.Governance.AgentReviewKey/FS.GG.Governance.AgentReviewKey.fsproj
```

## FSI-exercise (Principle I — design-first proof)

A short F035 section in `scripts/prelude.fsx` exercises the public surface before any `.fs` body is trusted:

```fsharp
open FS.GG.Governance.FreshnessKey.Model        // RuleHash, ArtifactHash (reused verbatim)
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model

let inputs =
    { Model = ModelId "claude-opus-4"
      ModelVersion = ModelVersion "20260101"
      PromptHash = ReviewerPromptHash "p1"
      Config = ModelConfig "temp=0"
      Question = QuestionText "explains API?"
      Check = RuleHash "c1"
      ReviewedArtifacts = [ ArtifactHash "h2"; ArtifactHash "h1"; ArtifactHash "h1" ] }

// The byte-stable cache key over all seven inputs (artifacts deduped + ordinally sorted).
AgentReviewKey.compute inputs |> AgentReviewKey.value
// "mid=13:claude-opus-4\nmver=8:20260101\nprompt=2:p1\ncfg=6:temp=0\nchk=2:c1\nart=2;2:h1;2:h2\nq=13:explains API?"

// Reordering / duplicating the artifact set never changes the key.
let reordered = { inputs with ReviewedArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2" ] }
AgentReviewKey.matches inputs reordered            // true
AgentReviewKey.diff    inputs reordered            // []

// A judge / prompt / check / artifact change invalidates the prior key, and diff names exactly what changed.
let newerModel = { inputs with ModelVersion = ModelVersion "20260202" }
AgentReviewKey.matches inputs newerModel            // false
AgentReviewKey.diff    inputs newerModel            // [ ModelVersionInput ]
AgentReviewKey.diff    inputs newerModel |> List.map Model.inputToken   // [ "modelVersion" ]

// An empty review (no artifacts) is an ordinary value, not an error.
let noArtifacts = { inputs with ReviewedArtifacts = [] }
AgentReviewKey.compute noArtifacts |> AgentReviewKey.value
// "…\nart=0;\nq=13:explains API?"
```

## Test

```bash
dotnet test tests/FS.GG.Governance.AgentReviewKey.Tests/FS.GG.Governance.AgentReviewKey.Tests.fsproj
```

Validates, against the **public** surface with **real** literal tokens (Principle V — no mocks, no clock, no
model, no process):

- **Compute (SC-001):** the key incorporates all seven inputs; changing exactly one input changes the key
  (every one of the seven); worked-example byte-pin to `contracts/agent-review-key-format.md`.
- **Set semantics (SC-002):** reordering / duplicating `ReviewedArtifacts` never changes the key; the empty set
  keys to a distinct, unambiguous value.
- **Diff (SC-003):** `matches` is true IFF all seven inputs are equal; `diff` names exactly the differing inputs
  (none hidden, no equal input reported), incl. artifact-only and several-inputs-at-once cases.
- **Injectivity (SC-005):** moving the same string between two inputs changes the key; tokens containing
  `:`/`=`/`;`/`\n`, empty tokens, and a token equal to another input's text never collide or spoof a boundary.
- **Determinism (SC-004):** compute / matches / diff over the same inputs are byte-identical on repeat (FsCheck).
- **Purity (SC-006):** identical results under changed cwd / time / filesystem (FsCheck); no model invoked, no
  bytes hashed.
- **Surface drift + scope (SC-007):** the public surface equals the committed baseline and the assembly
  references only `FreshnessKey` / `Config` / BCL / `FSharp.Core`.

## Re-bless the surface baseline (intentional public-surface change only)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.AgentReviewKey.Tests/FS.GG.Governance.AgentReviewKey.Tests.fsproj
```

Rewrites `surface/FS.GG.Governance.AgentReviewKey.surface.txt`. Only run this when a public-surface change is
intended (Tier-1 discipline, Principle II).

## Expected outcomes

- `dotnet build` and `dotnet test` over the **existing** projects (incl. F029) are unchanged (SC-007); the new
  project + test project are purely additive.
- The same seven inputs always yield a byte-identical key; any single differing input yields a different key; the
  reviewed artifacts are keyed as a set; every cache miss is explainable as a named judge / prompt / check /
  artifact change.
