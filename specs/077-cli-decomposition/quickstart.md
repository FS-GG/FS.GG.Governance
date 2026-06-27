# Quickstart / Validation: CLI render / IO decomposition (Phase E)

The binding acceptance test is **byte-identity** of every CLI text/JSON output plus a
green full suite, at **every** commit (FR-009). This guide is the runnable validation;
implementation detail lives in `tasks.md` and the modules themselves.

## Prerequisites

- .NET `net10.0` SDK; repo restores/builds with standard tooling.
- Run from repo root: `/home/developer/projects/FS.GG.Governance`.

## 0. Capture the pre-change baseline (once, before commit 1)

Capture a representative matrix — each command × text/json × success/usage-error/
blocking — and the existing goldens/snapshots, so any drift is detectable:

```bash
dotnet build src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release
# Representative invocations (extend with the project's own transcript fixtures):
for cmd in route explain contract evidence; do
  dotnet run --project src/FS.GG.Governance.Cli -- $cmd --root . \
    > /tmp/077-before-$cmd.txt 2>&1
  dotnet run --project src/FS.GG.Governance.Cli -- $cmd --root . --json \
    > /tmp/077-before-$cmd.json 2>&1
done
dotnet run --project src/FS.GG.Governance.Cli -- bogus \
  > /tmp/077-before-usage.txt 2>&1          # usage error → text, exit 64
```

## 1. After each extraction commit — prove byte-identity

```bash
dotnet build src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj -c Release
for cmd in route explain contract evidence; do
  dotnet run --project src/FS.GG.Governance.Cli -- $cmd --root . > /tmp/077-after-$cmd.txt 2>&1
  dotnet run --project src/FS.GG.Governance.Cli -- $cmd --root . --json > /tmp/077-after-$cmd.json 2>&1
  diff /tmp/077-before-$cmd.txt  /tmp/077-after-$cmd.txt  && echo "OK text $cmd"
  diff /tmp/077-before-$cmd.json /tmp/077-after-$cmd.json && echo "OK json $cmd"
done
dotnet run --project src/FS.GG.Governance.Cli -- bogus > /tmp/077-after-usage.txt 2>&1
diff /tmp/077-before-usage.txt /tmp/077-after-usage.txt && echo "OK usage"
```

**Expected**: every `diff` is empty (SC-001). Any non-empty diff means the extraction
changed behavior — revert/fix that commit rather than re-blessing a golden.

## 2. Full suite green at every commit (SC-002)

```bash
dotnet test FS.GG.Governance.sln          # or the repo's documented test entry
```

**Expected**: green, with per-project counts at/above the pre-change baseline. The CLI
suites that gate this feature specifically:

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj
```

covering parser, MVU, snapshot, output (the relocated `CliRender.render*` call sites),
read-only watch/tui, and surface-drift. (A known pre-existing `dotnet pack` timeout
flake in the CLI packaging test is unrelated to this change.)

## 3. Surface-drift is additive (FR-011)

The surface-drift test compares the in-test `generatedSurface` literal to
`surface/FS.GG.Governance.Cli.surface.txt`. After all three commits both list, after
`module Cli`:

```
module CliRender
module ArtifactReading
module ReviewStore
```

**Expected**: surface-drift test green; baseline grew by exactly three lines; no line
removed/renamed. The "CLI remains optional" + "expected runtime references" guards stay
green unchanged (no new ProjectReference, FR-008/FR-010).

## 4. Structural separation checks (SC-003 / SC-004)

Verifiable by inspection / grep:

```bash
# SC-003: the parser/MVU module no longer contains the render* functions
grep -nE "let +render(Text|Json|ParseError)? " src/FS.GG.Governance.Cli/Cli.fs ; echo "exit=$?"   # expect: no matches

# SC-004: runHost/main hold no inline artifact-path or review-store file I/O
sed -n '/let runHost/,/let writeOutput/p' src/FS.GG.Governance.Cli/Program.fs \
  | grep -nE "File\.|Directory\.|reviewStoreRoot|specKitArtifactPath" ; echo "exit=$?"  # expect: no matches
```

**Expected**: render lives only in `CliRender`; artifact-path/review-store file I/O
lives only in `ArtifactReading`/`ReviewStore`; `runHost`/`main` delegate.

## 5. Read-only watch/tui untouched (edge case)

```bash
git diff --stat HEAD~3 -- src/FS.GG.Governance.Cli/Program.fs   # after the 3 commits
```

**Expected**: the watch/tui block (`readOnlyRoutePorts`/`composeRouteView`/`runWatch`/
`runTui`/…) shows no behavioral change — only the `Cli.render` → `CliRender.render`
repoint in `writeOutput`/`main` and the delegated reads/persistence calls in `runHost`.
