# F# public-surface migration

`fsharp-public-surface/v1` is the reusable Governance check for compiled F# projects. It is not an SDD-only
rule: `fsgg verify` senses every declared `Compile` item in every repository `.fsproj`, including executables.

The sensor reads project order, not a filesystem glob. A non-test implementation module requires a curated
`.fsi` immediately before it unless it is an entry point, generated source, explicitly internal, or has a
governed exemption. Signature declarations need XML documentation. Package or tool-facing baseline checks are
opt-in, so an executable does not acquire a package baseline merely by using this profile.

The receipt is deterministic JSON with fixed property order:

```json
{"schemaVersion":1,"kind":"fsharp-public-surface","applicable":true,"project":"src/App.fsproj","compiledSources":["Domain.fs"],"findings":["fsharp.signature-missing"],"freshnessDigest":"…","malformed":null}
```

`freshnessDigest` covers the project file and every compiled source/signature. An unreadable or malformed
project emits `malformed` and no digest; callers must treat that as an input failure, never as a clean result.

For a standalone, persisted-artifact workflow use the dedicated command and redirect its only stdout value:

```bash
dotnet run --project src/FS.GG.Governance.FSharpSurfaceCommand -- \
  --root . --project src/MyProject/MyProject.fsproj > readiness/fsharp-public-surface.json
```

It exits `3` for malformed input and `2` for invalid command syntax. Findings are carried in the receipt;
they are advisory during migration and become enforcement input through `fsgg verify`.

The current policy posture is advisory through **2026-10-01**. Promotion to blocking requires a fresh fixture
run covering libraries, executables, exemptions, documentation, signature order, malformed project input, and
the stable receipt contract.
