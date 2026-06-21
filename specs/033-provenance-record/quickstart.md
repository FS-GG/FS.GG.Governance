# Quickstart: Provenance Core (F033)

A validation/run guide for the pure provenance core. It proves the feature end-to-end through the **public**
surface (`Provenance.build` / `canonicalId` / `identityValue`) — no host, no process spawn, no I/O. For the
type vocabulary see [data-model.md](./data-model.md); for the function laws and identity bytes see
[contracts/provenance-api.md](./contracts/provenance-api.md) and
[contracts/provenance-identity-format.md](./contracts/provenance-identity-format.md).

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged cores build (`FS.GG.Governance.FreshnessKey`, `FS.GG.Governance.CommandRecord`,
  `FS.GG.Governance.Config`) — this core references all three (research D1).

## Build

```bash
dotnet build src/FS.GG.Governance.Provenance
```

Expected: clean build under `TreatWarningsAsErrors=true`. The project references `../FS.GG.Governance.FreshnessKey`,
`../FS.GG.Governance.CommandRecord`, and `../FS.GG.Governance.Config` and adds **no** new third-party package.

## FSI design-first proof (Principle I)

The public shape is exercised in `scripts/prelude.fsx` (the new **F033 section**) before any `.fs` body is
trusted. Run:

```bash
dotnet fsi scripts/prelude.fsx
```

The F033 section demonstrates (each prints its expected result):

1. **Carriage (SC-001/SC-002).** Build a provenance from the eight facts (a real F032 `CommandRecord` made via
   `CommandRecord.build`) and read each fact back: the three revisions, rule hash, generator version, the
   artifact digests, the command records (whole), environment class, and builder identity.
2. **Duration excluded, reachable (SC-003).** Build two provenances identical except the embedded record's
   `SensedDuration`; print that their `canonicalId`s are **equal** and that `provenance.CommandRecords.[0].Duration`
   still differs — the sensed metadata is reachable but not in the identity.
3. **Per-field sensitivity (SC-004).** Change one reproducible fact (e.g. `Head`, an extra `ArtifactHash`, or a
   command record's argument) and print that the `canonicalId` **differs**.
4. **Artifact-digest set vs command-record order (SC-005).** Reorder/duplicate the artifact digests ⇒
   **unchanged** identity; reorder the command records ⇒ **changed** identity.
5. **Worked-example identity.** Print `identityValue (canonicalId p)` for the
   [identity-format](./contracts/provenance-identity-format.md) worked example and confirm it equals the
   contract block byte-for-byte.

## Test

```bash
dotnet test tests/FS.GG.Governance.Provenance.Tests
```

Expected: all green. The suite (Expecto + FsCheck, public surface only, real literally-constructed facts — no
mocks, Principle V) covers:

| File | Proves |
|---|---|
| `ProvenanceTests.fs` | all eight facts carried verbatim; records carried whole; artifact digests reported as a set; no-records / no-artifacts / equal-base-head / failed-or-timed-out-record edge cases (SC-001, SC-002) |
| `IdentityTests.fs` | durations excluded from + reachable beside identity; duration-only ⇒ equal id; any reproducible change ⇒ different id; same string in two fields ⇒ different id; worked-example byte match (SC-003, SC-004) |
| `DeterminismTests.fs` | build/identity twice ⇒ equal; artifact-digest reorder/dup ⇒ unchanged id; command-record reorder ⇒ changed id (SC-005) |
| `PurityTests.fs` | provenance + identity identical across changed cwd / time / filesystem (SC-006) |
| `SurfaceDriftTests.fs` | surface baseline equality + scope hygiene: references only FreshnessKey/CommandRecord/Config/BCL/FSharp.Core (SC-007) |

## Full-solution no-regression (SC-007)

```bash
dotnet test FS.GG.Governance.sln
```

Expected: full solution green with **no** diff to any existing `src/**` or `surface/**` — only the new
`Provenance` core, its tests, and `surface/FS.GG.Governance.Provenance.surface.txt` are added.

## Re-bless the surface baseline (when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Provenance.Tests --filter SurfaceDrift
```

Then re-run **without** `BLESS_SURFACE` to confirm the committed baseline matches (the F029–F032 pattern). The
baseline must list exactly the two modules (`Model`, `Provenance`), three vals (`build`, `canonicalId`,
`identityValue`), and the declared new types (`BuilderIdentity`, `ProvenanceIdentity`, `Provenance`).
