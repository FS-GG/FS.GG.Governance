# Readiness: F# Simplicity Architecture Gate

Date: 2026-08-03

## Verification

- Locked solution restore: passed with the repository package-read credential.
- Bounded full solution build (`dotnet fsi build.fsx build --no-restore`): passed.
- Full solution test (`dotnet fsi build.fsx test --no-restore`): exit 0 in 173188 ms after the
  reviewed dependency graph was regenerated for the new packable boundary.
- Focused CodeChecks suite: 13 passed, 0 failed, including surface/dependency checks and the
  independent-review reflection regressions.
- Release pack: `FS.GG.Governance.CodeChecks.0.1.0.nupkg` created successfully.
- Diff hygiene: `git diff --check` passed.

## Packed public-route evidence

The package was extracted to a disposable directory and FSI loaded
`lib/net10.0/FS.GG.Governance.CodeChecks.dll` plus its declared compiler-service dependency. It called
the public `CodeChecks.analyze` entry point for both sides of the reflection semantic boundary required
by independent review round 1.

```text
packed-artifact=/tmp/fsgg-governance-368-r1-final-pack.xHwwnf/extracted/lib/net10.0/FS.GG.Governance.CodeChecks.dll
unused-open-reflection=0
actual-call-symbols=["System.Type.GetMethods"]
```

The planted architecture source documents in the semantic suite are synthetic by design and are marked
`SYNTHETIC` at the use site and in every test name. They isolate compiler fact shapes; the packed smoke
above is the production public-route exercise.
