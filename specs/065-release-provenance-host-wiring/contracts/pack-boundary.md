# Contract — Pack-and-Version Boundary (`fsgg release`)

**Scope**: how `fsgg release` turns declared packable projects into the package evidence the existing F53 release
rules are evaluated against, and how a failed/unbumped pack blocks. Pure work in `update`; the pack runs and
output reads are interpreter-edge effects.

## Inputs

- `decl.PackableProjects : (Surface, PackCommand: GateCommand, Baseline: string option) list` (from the shared
  `ReleaseDeclaration` leaf).
- The F54 `SensedRelease` for the product (sensed verbatim, unchanged).

## Edge sequence

1. `update` (on `DeclarationLoaded(Ok decl)`) emits `PackProjects [(surface, packCommand); …]` and
   `SenseRelease(decl.Layout, decl.Expectations)`.
2. Interpreter, per project: `record = GateExecution.Interpreter.senseExecution ports.Execute packCommand`;
   `run = { Kind = Pack; Record = record }`; `outcome = ports.PackRead surface executionOutcome`:
   - non-zero exit ⇒ `PackFailed (surface, sentinel, run)`
   - zero exit, no/unreadable artifact ⇒ `PackedNoArtifact (surface, reason, run)`
   - zero exit, artifact read ⇒ `Packed ({ Surface; ArtifactPath; PackedVersion; Digest }, run)`
   The recorded run is carried in **every** case (never dropped, FR-001). Result fed back as `PacksRun outcomes`
   (request order preserved).

## Pure composition (in `update`, once `Sensed` + `PacksRun` + `ProvenanceSensed` land)

```
pack        = Pack.evaluatePack (baselines decl) outcomes
mergedFacts = overlay (Pack.factContributions pack) onto sensed.Facts   // packed wins on the 3 pack families (D1)
decision    = Release.evaluateRelease decl.Rules mergedFacts            // VERBATIM — no re-derivation
```

- `Pack.versionPolicy baseline packed` decides `Bumped | Unbumped | Downgraded | NoBaseline | NotPackable` against
  the **packed** version (D1). `Baseline = None ⇒ NoBaseline` (first release, not a downgrade).
- `factContributions`: `Packed`+`Bumped`/`NoBaseline` keeps the three families `Met`; `Unbumped`/`Downgraded`
  marks `VersionBump` `Unmet`; `PackedNoArtifact`/`PackFailed` marks all three `Unmet`; `NoPackableProjects ⇒
  Map.empty` (vacuously satisfied — no family blocked on packing).

## Guarantees

- **GP-1** Every declared packable project is packed before the verdict may pass (FR-001).
- **GP-2** A failed pack ⇒ `evaluateRelease` blocks with a named reason; the failed `Pack` run is in the snapshot
  with its sentinel exit (FR-001).
- **GP-3** A pack at an unbumped/downgraded **packed** version ⇒ blocked, naming project + version (FR-002).
- **GP-4** No new release-rule family; `evaluateRelease` unchanged; the `ReleaseDecision`/`ExitCodeBasis` carried
  verbatim into the report (FR-003, FR-012).
- **GP-5** `NoPackableProjects = true` ⇒ pack precondition vacuously satisfied, reported "no packable projects",
  no fabricated pack (FR-013/edge).
- **GP-6** Deterministic & order-independent: reordering `decl.PackableProjects` changes no evidence/verdict
  (FR-011); `evaluatePack` sorts verdicts by `(SurfaceId, ArtifactPath)`.
