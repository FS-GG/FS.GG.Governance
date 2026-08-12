# F# public-surface migration

`fsharp-public-surface/v1` is the reusable Governance check for compiled F# projects. It is not an
SDD-only rule: `fsgg verify` senses every declared `Compile` item in every repository `.fsproj`,
including executables.

The sensor reads project order rather than discovering implementations from the filesystem. A
non-test implementation module requires a curated `.fsi` immediately before it unless it is an entry
point, generated source, explicitly internal, or has a governed exemption. Signature declarations
need XML documentation. Signature compatibility is checked by building the owning project, so earlier
compiled files, project references, defines, and project compiler options are part of the check.
Package or tool-facing baseline checks are opt-in; an executable does not acquire a package baseline
merely by using this profile.

## Policy

The optional `.fsgg/fsharp-surface.json` file has this typed shape:

```json
{
  "declaredGlob": "src/**/*.fsi",
  "maturity": "block-on-ship",
  "projects": {
    "src/MyProject/MyProject.fsproj": {
      "requiresBaseline": true,
      "baselineCurrent": false
    }
  },
  "exemptions": [
    {
      "module": "GeneratedBridge.fs",
      "owner": "runtime-team",
      "rationale": "generated compatibility bridge",
      "reviewBy": "2026-09-30"
    }
  ]
}
```

`maturity` is the typed declared policy for this evaluated surface: `observe`, `warn`,
`block-on-pr`, `block-on-ship`, or `block-on-release`. It defaults to `warn` only when the
policy is absent or omits the field. A present malformed value makes the whole policy malformed;
the producer emits input-state evidence and no clean receipt. `declaredGlob` is anchored at the repository root and selects only compiled `.fsi` inputs for
`matchedModules`; `**/` also matches zero nested directories. For example,
`src/**/Public*.fsi` matches both `src/PublicApi.fsi` and `src/nested/PublicModel.fsi`, but not a
root-level `Domain.fsi` or `src/Internal.fsi`. Missing policy uses `src/**/*.fsi`. Present but malformed
policy fails closed. Exemptions require all four fields and an unexpired `reviewBy` date.

## Persisted receipt

The dedicated command writes one deterministic receipt atomically to the stable path
`readiness/fsharp-public-surface.json` and also prints the same JSON to stdout. Package-only
consumers install the supported global tool (minimum version `1.12.1`) and do not need a
Governance source checkout or project reference:

```bash
dotnet tool install --tool-path ./.tools FS.GG.Governance.FSharpSurfaceCommand --version 1.12.1
./.tools/fsgg-fsharp-surface --root . --project src/MyProject/MyProject.fsproj
```

Maintainers may equivalently use the source-project form:

```bash
dotnet run --project src/FS.GG.Governance.FSharpSurfaceCommand -- \
  --root . --project src/MyProject/MyProject.fsproj

jq . readiness/fsharp-public-surface.json
```

Do not redirect stdout to the readiness path: the command owns that file and replaces it through a
temporary file. It exits `0` for a valid applicable or explicitly non-applicable receipt, `3` for malformed project/config/compiler input, and `2` for invalid command
syntax. `--test-project` emits an explicit `not-applicable` disposition and bounded reason. The
`--requires-baseline` and `--baseline-current` flags supply fallback baseline facts; per-project policy
overrides them.

The v1 projection has fixed property order. A clean project with one selected signature resembles:

```json
{
  "schemaVersion": 1,
  "kind": "fsharp-public-surface",
  "applicability": "applicable",
  "applicable": true,
  "applicabilityReason": "compiled non-test F# project",
  "project": "src/MyProject/MyProject.fsproj",
  "declaredGlob": "src/**/*.fsi",
  "compiledSources": ["PublicApi.fsi"],
  "matchedModules": ["src/MyProject/PublicApi.fsi"],
  "matchedModuleCount": 1,
  "cardinality": "one",
  "maturity": "block-on-ship",
  "findings": [],
  "freshnessDigest": "…",
  "configDigest": "…",
  "policyDigest": "…",
  "sourceDigest": "…",
  "malformed": null
}
```

`matchedModules`, `matchedModuleCount`, and `cardinality` all derive from the same configured-glob
selection; cardinality is `zero`, `one`, or `many`. Findings are objects with stable `code`, `file`,
`detail`, `isInputState`, `baseSeverity`, `effectiveSeverity`, and nullable `evidence` fields. During
migration, rule findings have blocking base severity and advisory effective severity. Malformed input
instead emits a blocking `fsharp.surface-malformed` input-state finding.

`maturity` is a v1 field whose semantics are corrected by the compatible Governance 0.2.x producer:
it is the effective configured policy, not a caller-provided value or a constant advisory placeholder.
Consumers that already parse v1 can use this value without a schema migration; producers must publish the
0.2.x Config and DesignChecks packages/tool before a consumer relies on blocking behavior. The SDD#833
rollout is therefore: publish this producer, invoke the real receipt command, then make `verify` and
`ship` enforce a `block-on-ship` zero-signature receipt.

An F# consumer should obtain this policy fact only from the producer API; it does not accept a forged
maturity argument:

```fsharp
open FS.GG.Governance.DesignChecks
open FS.GG.Governance.DesignChecks.FSharpSurface

let receipt = receipt repoRoot "src/Game/Game.fsproj" false false true request
match receipt.Malformed, receipt.Applicable, receipt.Maturity, receipt.Cardinality with
| None, true, "block-on-ship", "zero" -> failwith "public F# surface is blocking and empty"
| None, _, _, _ -> ()
| Some reason, _, _, _ -> failwith $"surface input is malformed: {reason}"
```

`sourceDigest` and `freshnessDigest` bind the project file and its sensed `.fs`/`.fsi` inputs.
`configDigest` binds `.fsgg/capabilities.yml` when present, and `policyDigest` binds
`.fsgg/fsharp-surface.json`. An unreadable project has no source/freshness digest and is never projected
as a clean empty surface. Missing, malformed, stale, or unreadable receipts are a no-verdict for an SDD
consumer that requires this control, not a pass.

The current policy posture is advisory through **2026-10-01**. Promotion to blocking requires a fresh
fixture run covering libraries, executables, exemptions, documentation, signature order, real project
compiler context, malformed input, configured-glob zero/one/many selection, and the persisted receipt
contract.
