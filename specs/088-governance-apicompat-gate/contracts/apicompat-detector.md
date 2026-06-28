# Contract: ApiCompat Detector (the sensor)

The detector is the **I/O edge** component. It runs the external API-compatibility comparison and returns the per-package result as **data** (`ApiBreakSignal`). The pure core never invokes it; the MVU host requests it as an effect and joins the result into `ReleaseFacts` (mirrors the F065 pack-evidence join).

## Inputs

| Input | Source | Notes |
|---|---|---|
| Packed assemblies | `dotnet pack -c Release` of every `IsPackable=true` `FS.GG.Governance.*` project | produced into a temp/`~/.local/share/nuget-local/` dir |
| Baseline package(s) | latest version of each package id on the configured feed (`~/.local/share/nuget-local/`, later org GitHub Packages) | resolved via `PackageValidationBaselineVersion` or explicit `--baseline`; may be **absent** |
| Mechanism | .NET SDK Package Validation / `Microsoft.DotNet.ApiCompat` | SDK-bundled; tool installed job-scoped if used explicitly (never in `.config/dotnet-tools.json`, D6) |

## Output (per package)

`ApiBreakSignal` (see data-model.md §2):
`NoBreakingChanges` | `BreakingChanges of ApiBreak list` | `NoBaseline` | `Indeterminate of reason` | `NotPackable`.

## Behavioural contract

1. **Pure-core isolation**: the detector lives at the edge (a sensor module / `.fsx`); the pure rule/evidence libraries take no dependency on it (Constitution dependency-minimalism, Principle IV).
2. **Fail-safe**: any failure to obtain or read the baseline, or any tool error, MUST map to `Indeterminate reason` — NEVER to `NoBreakingChanges` (FR-008).
3. **No-baseline ≠ error**: a package with no prior published version MUST return `NoBaseline` (FR-009), distinct from `Indeterminate`.
4. **Determinism of classification**: for the same packed + baseline assemblies, the surfaced break set is stable and ordered (so findings are byte-identical).
5. **Does not fail the build**: the detector is invoked in a dedicated step that captures its result; it MUST NOT turn `dotnet build`/pack red on its own (D7). Enforcement is the rule's job.
6. **Coverage honesty**: a packable package the tool cannot analyze MUST surface as `Indeterminate`/`NotCovered`, never as clean (FR-007).

## Invocation shape (indicative)

```
# dedicated detector step (mirrors pack-reference-gate-set.fsx)
dotnet fsi pack-and-apicheck.fsx            # pack Release + run ApiCompat vs feed baseline
dotnet fsi pack-and-apicheck.fsx --baseline-feed <dir>
dotnet fsi pack-and-apicheck.fsx --json     # emit machine-readable signal set for the host/CI
```

The script emits the `ApiBreakSignal` set (and coverage) as data the `fsgg release`/`verify` host (or CI) consumes; it does not itself decide block/allow.
