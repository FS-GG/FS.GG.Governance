# Quickstart: Agent-Review Record — Auditable Review-Record Core

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.ReviewRecord` (F038). The core is
pure and total over supplied values — no model, no clock, no I/O — so every step runs offline and deterministically.

## Prerequisites

- .NET SDK `net10.0` (repo standard; `Directory.Build.props` sets `Nullable=enable`,
  `TreatWarningsAsErrors=true`).
- The merged sibling cores build already: `FS.GG.Governance.PromptIsolation` (F037) and
  `FS.GG.Governance.SensedMetadata` (F034), plus their transitive deps (`AgentReviewKey`, `FreshnessKey`,
  `CommandRecord`, `Config`).

## Build

```bash
dotnet build src/FS.GG.Governance.ReviewRecord/FS.GG.Governance.ReviewRecord.fsproj
```

Compile order is `Model.fsi → Model.fs → ReviewRecord.fsi → ReviewRecord.fs`. The project references **only**
`PromptIsolation` and `SensedMetadata` and adds no new third-party package.

## FSI proof (design-first, Principle I)

The public surface is exercised in `scripts/prelude.fsx` (a new F038 section) before any `.fs` body exists. It
loads the packed/compiled libraries and calls the public functions exactly as a consumer would — no private
helpers. Representative transcript (all values literally constructed; reuses F037's request, F035's identities,
F029's hashes, F034's marking):

```fsharp
open FS.GG.Governance.PromptIsolation.Model
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.SensedMetadata.Model
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.ReviewRecord.Model
open FS.GG.Governance.ReviewRecord

// A completed review's facts, all already-formed values:
let request =
    PromptIsolation.assemble
        (QuestionText "Does this doc explain the public API?")
        [ Excerpt (Model.excerpt (SizeBound 200) "module Foo = ...")
          DigestOnly (ArtifactHash "sha256:abc") ]

let rec038 =
    ReviewRecord.build
        request
        (ModelId "claude-opus")
        (ModelVersion "4.8")
        (ReviewerPromptHash "ph-7f3")
        [ ArtifactHash "sha256:abc" ]
        (ResponseDigest "sha256:resp")
        (RecordedVerdict "pass")
        [ SensedMetadata.markTimestamp (SensedLabel "at") (SensedTimestamp "2026-06-22T10:00:00Z") ]

printfn "[F38] all six facts read back ⇒ %A" rec038.Reproducible
printfn "[F38] identity ⇒\n%s" (ReviewRecord.identityValue (ReviewRecord.canonicalId rec038))

// Honesty boundary: drop the sensed timestamp ⇒ identity unchanged.
let recNoSensed = { rec038 with Sensed = [] }
printfn "[F38] sensed excluded from identity ⇒ %b"
    (ReviewRecord.canonicalId rec038 = ReviewRecord.canonicalId recNoSensed)

// Injectivity: flip the verdict ⇒ identity changes.
let recFail = { rec038 with Reproducible = { rec038.Reproducible with Verdict = RecordedVerdict "fail" } }
printfn "[F38] verdict changes identity ⇒ %b"
    (ReviewRecord.canonicalId rec038 <> ReviewRecord.canonicalId recFail)
```

## Test

```bash
dotnet test tests/FS.GG.Governance.ReviewRecord.Tests/FS.GG.Governance.ReviewRecord.Tests.fsproj
```

The semantic tests (Expecto + FsCheck) exercise the **public** surface only, over real literal values (no mocks,
no clock, no model, no file, no bytes hashed — Principle V; no `Synthetic` disclosure needed). They cover:

- **Faithful capture (US1, SC-001)** — all six facts read back exactly as supplied; build is deterministic;
  zero-artifact / empty-digest / empty-verdict / empty-sensed records are valid.
- **Identity (US2, SC-002)** — identical reproducible facts ⇒ byte-identical identity; any single differing
  reproducible fact (request, model id, model version, prompt hash, an artifact digest, response digest, verdict)
  ⇒ different identity (FsCheck injectivity properties + worked-example pins to
  [contracts/review-record-identity-format.md](./contracts/review-record-identity-format.md)).
- **Sensed boundary (US2 scenario 3, SC-003)** — two records differing only in `Sensed` ⇒ identical identity.
- **No raw bytes (US3, SC-004)** — the record carries only digests + the F037-bounded request; no form attaches
  raw response or unbounded artifact content.
- **Artifact set (D4)** — reordered/duplicated artifact digests ⇒ identical identity.
- **Purity (SC-005)** — identical record + identity under changed cwd / time / unrelated filesystem state.
- **Surface drift + scope hygiene (SC-006, Principle II)** — the surface baseline matches and the assembly
  references only `PromptIsolation`/`SensedMetadata` (+ allowed transitive cores).

## Re-bless the surface baseline (Tier 1)

When the public surface intentionally changes, regenerate the baseline:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.ReviewRecord.Tests/FS.GG.Governance.ReviewRecord.Tests.fsproj
```

This rewrites `surface/FS.GG.Governance.ReviewRecord.surface.txt`. Review the diff before committing — an
unexpected change is a Tier-1 surface drift (Constitution Change Classification).

## Verify the additive guarantee (SC-006)

```bash
dotnet build && dotnet test
```

No existing `src/`, `surface/`, or merged test project changes outcome; only the new project, its test project,
and the new surface baseline are added.
