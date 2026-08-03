# Quickstart: F# Simplicity Architecture Gate

```bash
dotnet restore tests/FS.GG.Governance.CodeChecks.Tests/FS.GG.Governance.CodeChecks.Tests.fsproj --locked-mode
dotnet test tests/FS.GG.Governance.CodeChecks.Tests/FS.GG.Governance.CodeChecks.Tests.fsproj --no-restore
dotnet pack src/FS.GG.Governance.CodeChecks/FS.GG.Governance.CodeChecks.fsproj -c Release
```

Expected: idiomatic DU/module and justified adapter/hot-loop fixtures pass; hierarchy, shared mutation,
reflection, stale justification, configured threshold, and duplicate-primitive fixtures emit the stable
typed findings documented in `contracts/code-checks-api.md`.
