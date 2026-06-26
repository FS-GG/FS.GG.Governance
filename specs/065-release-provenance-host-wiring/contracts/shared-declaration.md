# Contract — Shared Declaration Leaf (`FS.GG.Governance.ReleaseDeclaration`)

**Scope**: the thin shared adapter that parses `.fsgg/release.yml` for **both** hosts, so verify can assemble a
previewable `ReleaseReport` without referencing the release executable (research D6).

## Why it exists

`Report.preview` requires a `ReleaseReport`, which `Report.assemble` builds from a `ReleaseDecision` +
`SensedRelease`. Verify must therefore parse the same declaration the release host parses. The release host is an
**exe**, so the declaration adapter is lifted out of `ReleaseCommand.Declaration` into a library both hosts
consume. This is an **adapter relocation**, not a new pure evaluation core.

## Surface

```fsharp
namespace FS.GG.Governance.ReleaseDeclaration
module Declaration =
    type PackableProject =
        { Surface: SurfaceId
          PackCommand: GateExecution.Model.GateCommand
          Baseline: string option }                                  // None ⇒ first release

    type ReleaseDeclaration =
        { Rules: ReleaseRule list                                    // F55 — unchanged
          Expectations: ReleaseExpectations                          // F55 — unchanged
          Layout: SourceLayout                                       // F55 — unchanged
          PackableProjects: PackableProject list                     // additive
          Matrix: ValidationMatrix.Model.ExhaustiveMatrix option }   // additive

    type DeclError = { Reason: string }

    val parse: lines: string list -> Result<ReleaseDeclaration, DeclError>
```

## Behaviour

- **Re-homed semantics**: the rules/expectations/layout parse is the F55 `ReleaseCommand.Declaration` behaviour
  preserved verbatim; the release host's existing tests for that path are carried over against the new module.
- **Additive parse**: `packableProjects` (a sequence of `{ surface, packCommand, baseline? }`) and an optional
  `matrix` (`{ name, cost, dimensions }`). A malformed entry ⇒ `Error DeclError` (input-unavailable, never partial
  facts — Constitution VI).
- **PURE, TOTAL**: reads no filesystem (content arrives via the F014 `Loader.FileReader` at each host edge);
  product-neutral (every value from the file); reuses the already-pinned YamlDotNet in parse-to-node mode — **no
  new dependency**.

## Consumers

- `ReleaseCommand` — derives the pack-command list + baselines map; supplies `Rules`/`Expectations`/`Layout` to the
  existing sense/evaluate path; `Matrix` to `decideMatrix` at `ScheduledOrRelease`.
- `VerifyCommand` — supplies `Rules`/`Expectations`/`Layout` to the declaration-gated preview; `Matrix` to
  `decideMatrix` at `InnerLoop`. (Verify ignores `PackableProjects` — it does not pack.)

## Guarantees

- **GD-1** Both hosts parse the identical file through one module — a single source of truth (no duplication).
- **GD-2** No host→host (and no exe) reference; the leaf depends only on libraries.
- **GD-3** Backward-compatible: a `release.yml` with no `packableProjects`/`matrix` parses with
  `PackableProjects = []` (⇒ `NoPackableProjects`) and `Matrix = None` (⇒ `NotDeclared`).
