# Quickstart: validating `fsgg verify`

Runnable scenarios that prove the feature end to end. They use real temp-repository fixtures (the
`withTempRepo` helper in `tests/FS.GG.Governance.VerifyCommand.Tests/Support.fs`, mirroring ShipCommand) over
real F015–F052 cores; only the edge ports are faked. See `contracts/cli.md` and `contracts/verify.schema.md`
for the argv and document contracts, and `data-model.md` for the entities.

## Prerequisites

- .NET `net10.0` SDK (repo `Directory.Build.props`).
- `dotnet build FS.GG.Governance.sln` succeeds after the four new projects are added to the solution.

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.VerifyJson.Tests/FS.GG.Governance.VerifyJson.Tests.fsproj
dotnet test  tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj
```

## Scenario 1 — passing change exits 0 (Story 1, AC-1)

```bash
fsgg verify --repo <fixture-clean>
echo $?    # 0
```

Expect: text verdict `verify: pass`, the selected checks listed (ran or reused), the `currency` summary, and
`wrote <repo>/readiness/verify.json (fsgg.verify/v1)`. **Pass criterion**: exit `0`; verdict pass.

## Scenario 2 — blocking check unmet exits 1 (Story 1, AC-2; SC-002)

```bash
fsgg verify --repo <fixture-blocking>
echo $?    # 1
```

Expect: `verify: blocked`, the unmet blocking check named under `blockers`. **Pass criterion**: exit `1`
(distinct from 2/3/4); the blocker is named.

## Scenario 3 — advisory-only change still passes (Story 1, AC-3)

```bash
fsgg verify --repo <fixture-advisory>
echo $?    # 0
```

Expect: the advisory finding under `warnings`, verdict still `pass`. **Pass criterion**: exit `0`; warning
surfaced.

## Scenario 4 — nothing to verify (Story 1, AC-4; FR-012)

```bash
fsgg verify --paths docs/UNGOVERNED.md
echo $?    # 0
```

Expect: "nothing to verify" report. **Pass criterion**: exit `0`; empty selection is not an error.

## Scenario 5 — reuse fresh, recompute stale (Story 2; SC-003)

```bash
fsgg verify --repo <fixture> --persist-store     # run 1: populates the store
fsgg verify --repo <fixture> --persist-store     # run 2: no change
```

Expect run 2: every selected check reported under `currency.fresh` (reused); none recomputed. Then change one
check's inputs and re-run: only that check appears under `currency.recomputed` with its changed `categories`.
**Pass criterion**: 100% reuse on the unchanged re-run; exactly the changed check recomputes.

## Scenario 6 — deterministic verify.json (Story 3; SC-004)

```bash
fsgg verify --repo <fixture> --verify-out /tmp/a.json
fsgg verify --repo <fixture> --verify-out /tmp/b.json
diff /tmp/a.json /tmp/b.json          # identical
fsgg verify --repo <fixture> --json > /tmp/stdout.json
diff /tmp/stdout.json /tmp/a.json     # stdout equals the file verbatim
```

Expect: byte-identical artifacts; `--json` stdout equals the file; the document carries `schemaVersion`
`fsgg.verify/v1` and no timestamp/abs-path/username. **Pass criterion**: `diff` clean both times.

## Scenario 7 — failure modes map to distinct codes (SC-005)

| Invocation | Exit |
|------------|------|
| `fsgg verify --bogus` | `2` (UsageError) — no artifact written |
| `fsgg verify --repo <no-catalog>` | `3` (InputUnavailable) — no partial artifact |
| `fsgg verify --repo <fixture> --verify-out /proc/x/verify.json` | `4` (ToolError) — no partial artifact |

**Pass criterion**: each failure resolves to the correct distinct code; no fabricated passing verdict; no
partial `verify.json` left behind on a `3`/`4`.

## Scenario 8 — network-free own logic (SC-007)

The `ScopeGuardTests` assert no `System.Net`/`HttpClient` reference in the command's own source/IL. **Pass
criterion**: the scope-guard test is green.
