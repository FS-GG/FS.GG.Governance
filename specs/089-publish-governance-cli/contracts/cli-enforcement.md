# Contract: published-CLI enforcement behavior (consumer-side coherence of `governance-handoff@1.0.0`)

The behavioral contract the published `FS.GG.Governance.Cli` MUST satisfy. This is what makes the publish a real consumer-side verification of `governance-handoff@1.0.0`, and exactly what the FS.GG.Templates#25 probe asserts once the CLI is on PATH.

## Exit-code contract (source of truth: `src/FS.GG.Governance.Cli/Cli.fs:338-344`)

| Decision | Exit code |
|---|---|
| `Success` | `0` |
| `GovernedBlocking` | `2` |
| `UsageError` | `64` |
| `InputUnavailable` | `66` |
| `ToolError` | `70` |

The route exit is `GovernedBlocking` iff a **blocking** route entry has a non-`Pass` outcome (`Cli.fs:355-366` `hasBlockingFailure` / `exitFor`).

## Required behavior over a produced `governance-handoff.json`

The consumer (`FS.GG.Governance.Adapters.SddHandoff`, wired through `RouteCommand/Interpreter.fs` — locates every `readiness/<id>/governance-handoff.json` under the repo root) MUST make a produced handoff drive the verdict:

| Scenario | Command | Required result |
|---|---|---|
| Product with a **failing** handoff, strict gate | `fsgg-governance route --root <product> --mode gate` | **blocks** — exit `2` (`GovernedBlocking`); the verdict is attributable to the consumed handoff |
| Same failing handoff, light/non-strict mode | (light route) | does **not** block — exit `0` |
| Product with a **passing** handoff, strict gate | `fsgg-governance route --root <product> --mode gate` | passes — exit `0` (`Success`) |

## Anti-contract (the failure this feature exists to prevent)

A predecessor build lacking `Adapters.SddHandoff` exits `0` against a failing handoff (the handoff is silently ignored — "green by omission"). A package exhibiting this MUST NOT be publishable under the consumer-bearing version (enforced by the `enforcement-smoke` job, `publish-workflow.md`).

## Downstream acceptance

The FS.GG.Templates#25 composition stage probes the installed `fsgg-governance` and SKIPs while it cannot enforce a failing handoff (never green-by-omission, never false-fail). A published CLI that satisfies the table above flips that probe from **SKIP** to asserting the strict-blocks / light-passes matrix (SC-003). That flip — not the push alone — is the acceptance signal for issue #28 (research D7).
