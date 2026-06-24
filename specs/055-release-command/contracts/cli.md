# Contract: `fsgg release` command-line surface

The host exposes one command. The argv contract is parsed purely by `Loop.parse` (no I/O, no artifact on
rejection); the process result is one of five distinguishable exit codes.

## Invocation

```
fsgg release --repo <dir> [--format text|json|both] [--out <path>]
```

| Flag | Required | Default | Meaning |
|------|----------|---------|---------|
| `--repo <dir>` | yes | — | Governed repository working directory. Its `.fsgg/release.yml` declares the rules/expectations/layout. |
| `--format text\|json\|both` | no | `text` | `text` = human summary on stdout; `json` = write `release.json`; `both` = both. |
| `--out <path>` | no | `<repo>/release.json` | Destination for the `release.json` artifact (written atomically; only with `json`/`both`). |

Unknown flags, missing `--repo`, or malformed values ⇒ `UsageError'` (exit 2) with usage guidance on
stderr; **no** sensing, evaluation, or artifact occurs.

**Subcommand mapping (no central `fsgg` dispatcher).** The command ships as a standalone executable
(`FS.GG.Governance.ReleaseCommand`); no `fsgg` dispatcher exists or is introduced (see plan.md). `fsgg
release` is the documented invocation form. `Loop.parse` consumes the **flags only** and does **not**
expect or strip a leading `release` subcommand token — the executable's own argv begins at the first flag.
A leading bare `release` (or any unknown leading positional) is therefore an unknown argument ⇒ `UsageError'`
(exit 2). (When a dispatcher is later introduced in a following row, it strips the `release` token before
delegating; `Loop.parse`'s flags-only contract is unaffected.)

## Exit codes (FR-005, SC-005 — five distinguishable classes)

| Code | `ExitDecision` | Condition |
|------|----------------|-----------|
| `0` | `Success` | Release passed — `ReleaseDecision.ExitCodeBasis = Clean` (no effective-blocking rule unmet). |
| `1` | `Blocked` | Release blocked — `ExitCodeBasis = Blocked` (≥1 effective-blocking rule unmet). **Distinct from every failure-to-run code.** |
| `2` | `UsageError'` | Invalid command-line arguments. |
| `3` | `InputUnavailable` | Absent/invalid `.fsgg/release.yml`, or governing inputs the host cannot proceed past. |
| `4` | `ToolError` | Genuine tool/IO defect (e.g. unwritable `--out` path); no partial `release.json` left behind. |

Advisory-only unmet rules surface as warnings and do **not** block (exit `0`). A removed/unreadable
per-family source makes that family `Unrecoverable` ⇒ its rule unmet (never a fabricated pass) while the
run still completes with a full six-family verdict.

## stdout / stderr

- **stdout** (text/both): a human summary — overall verdict, blockers (with reason), warnings, passing
  rules. Reports the same verdict and per-rule outcomes as `release.json` for the same run (FR-009).
- **stderr**: diagnostics only, each tagged so a missing/malformed **input** is distinguishable from a
  **tool defect** (Constitution VI), mirroring `fsgg ship [<category>]: <message>`.

## `.fsgg/release.yml` declaration (row-local; F014 schema untouched)

Read through `Loader.FileReader`; parsed by `ReleaseCommand.Declaration` into `ReleaseRule list` +
`ReleaseExpectations` + `SourceLayout`. Indicative shape:

```yaml
surface: GovernancePackages          # F014 SurfaceId (typically a declared ReleaseSurface)
rules:
  - kind: version-bump
    severity: blocking
    maturity: block-on-release
  - kind: package-metadata
    severity: blocking
    maturity: block-on-release
  # ... one entry per declared family (version-bump, package-metadata, template-pins,
  #     publish-plan, trusted-publishing, provenance)
expectations:
  versionBaseline: "1.2.0"
  requiredMetadataFields: [Authors, Description, RepositoryUrl]
  expectedPins: { "FS.GG.Rendering.Templates": "3.4.0" }
  requiredPublishPosture: [nuget-org]
  requiredTrustedPublishing: [oidc]
  requiredProvenance: [slsa-provenance]
layout:
  versionPath: Directory.Build.props
  metadataPath: Directory.Build.props
  pinsPath: .config/dotnet-tools.json
  publishPlanPath: .fsgg/publish-plan.yml
  trustedPublishingPath: .github/workflows/release.yml
  provenancePath: .github/workflows/release.yml
```

An absent or malformed file ⇒ exit `3` (input-unavailable). Family `kind` tokens match
F053 `releaseRuleKindToken`; `severity`/`maturity` tokens match the F023/F014 token vocabulary. The
command never contacts the network or a registry (FR-015, SC-008).
