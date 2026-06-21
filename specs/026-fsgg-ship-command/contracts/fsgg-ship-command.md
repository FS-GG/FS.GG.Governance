# Wire Contract: `fsgg ship` command (F026)

The user/CI-facing contract: the flag surface, the enforcement levers, the **exit-code taxonomy** (the
row's new, load-bearing surface), and the written-artifact location. Pairs with
[Loop.fsi](./Loop.fsi) (pure surface) and [Interpreter.fsi](./Interpreter.fsi) (edge surface). Sibling of
F022's `fsgg-route-command.md`; the deltas vs `route` are flagged.

## Invocation

```
fsgg ship [--repo <dir>]
          [--mode <m>] [--profile <p>]
          [--paths <p> …] [--since <rev>]
          [--json]
          [--audit-out <path>]
```

A leading `ship` verb is tolerated and dropped. Canonical protected-branch form (the design's named
invocation): `fsgg ship --mode gate --profile standard --json`.

## Flags

| Flag | Value | Default | Meaning |
|---|---|---|---|
| `--repo` | dir | `.` | repository root |
| `--mode` | run mode | `gate` | the F023 `RunMode`; recognized by `Enforcement.recognizeMode` |
| `--profile` | profile | `standard` | the F023 `Profile`; recognized by `Enforcement.recognizeProfile` |
| `--paths` | one+ paths | — | explicit changed set; bypasses git diff. Mutually exclusive with `--since` |
| `--since` | git rev | — | base for the sensed change. Mutually exclusive with `--paths` |
| `--json` | (switch) | text | emit the `audit.json` document verbatim on stdout; suppress text |
| `--audit-out` | path | `<repo>/readiness/audit.json` | override the persisted audit location |

Omitting both `--paths` and `--since` senses the default base/head range. Omitting `--mode`/`--profile`
applies the documented defaults `gate`/`standard` (recorded per item in the audit document).

**Recognized `--mode` values**: `sandbox` `inner` `focused` `verify` `gate` `release` (F023 `RunMode`).
**Recognized `--profile` values**: `light` `standard` `strict` `release` (F023 `Profile`).
Any other value is a usage error (exit 2), naming the offending lever, and writes no artifact.

## Exit codes (research D6) — **the new-vs-`route` surface**

| Code | Category | When |
|---|---|---|
| **0** | success / clean | repo sensed, catalog valid, rollup `exitCodeBasis = clean` (`verdict = pass`); `audit.json` written |
| **1** | **blocked verdict** | rollup `exitCodeBasis = blocked` (`verdict = fail`); `audit.json` written. **Reserved for a blocked merge; used for nothing else** |
| 2 | usage error | both `--paths` and `--since`; unknown flag; missing value; **unrecognized `--mode`/`--profile`** |
| 3 | input unavailable | not a git repository / git unavailable; `--since` rev does not resolve; required `.fsgg` missing or invalid |
| 4 | tool error | output location unwritable (after a successful rollup), or any unexpected reified failure |

The blocked code **1** is distinct from every tool-failure code (2/3/4): CI can tell "the change may not
merge" (1) apart from "the tool could not run" (2/3/4), and a tool failure is never 0 or 1, so it is never
read as a pass or a blocked merge (FR-008, FR-009, SC-004).

## Written artifact

| Path (default) | Document | Owner |
|---|---|---|
| `<repo>/readiness/audit.json` | the F025 `AuditJson.ofShipDecision` projection of the F024 `Ship.rollup` | this command |

- **Exactly one** artifact (unlike `route`, which writes `gates.json` + `route.json`).
- **Byte-stable**: identical repository inputs **and levers** ⇒ byte-for-byte identical `audit.json` and
  identical exit code across runs (inherited from F025; SC-002). Carries the declared `schemaVersion`
  (`fsgg.audit/v1`); contains no wall-clock, machine-absolute path, or environment-derived value (SC-005).
- **No partial artifact**: every usage/input failure (exit 2/3) writes nothing; the real writer uses
  temp-file + atomic rename so a failed write (exit 4) never leaves a truncated file (FR-010, D10).
- **Re-run overwrites** the prior `audit.json` deterministically.

## stdout / stderr

- **Text (default)**: the verdict and exit-code basis, then the blockers / warnings / passing items each
  with identity and base + effective severity, the unknown-governed-path findings, and the written path.
- **`--json`**: the `audit.json` document text verbatim (equals the persisted file byte-for-byte).
- **Diagnostics** go to **stderr** as `fsgg ship [<category>]: <message>` (`blocked` is a verdict, not a
  diagnostic — it is reported in the summary, not as an error line).

## No-hide rule (FR-011, inherited from F024/F025)

A base-blocking item relaxed by mode/maturity/profile appears in `warnings` carrying **both** base
severity `Blocking` and effective severity `Advisory`; the verdict is `pass` and the exit is `0`, but the
relaxation is observable in both the summary and the persisted document. A profile can never silently hide
an underlying blocking finding.
