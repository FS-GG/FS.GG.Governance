# Behavioral Contract: Profile-Aware Handoff Gate at `route --mode gate`

This is the contract the published `1.2.0` CLI MUST satisfy and the FS.GG.Templates#25 Stage 6b
probe asserts. It is a **behavioral** contract (CLI exit codes), not an F# API surface — the
`governance-handoff` contract surface stays `@1.0.0` (FR-009).

## The matrix (the whole feature)

`route --mode gate` against a product that consumes one `governance-handoff.json`, varying only the
product's policy profile (`.fsgg/policy.yml defaultProfile`) and the handoff outcome:

| `defaultProfile` | handoff | expected exit | meaning |
|---|---|---|---|
| `strict` | failing | **2** (`GovernedBlocking`) | strict tightens the boundary → blocks |
| `light` | failing | **0** (`Success`) | light relaxes → failing handoff is advisory |
| `strict` | satisfied | **0** | satisfied passes (distinguishes pass from fail) |
| `light` | satisfied | **0** | satisfied passes (light never inverts a pass) |
| *absent* (no `defaultProfile` / no policy) | failing | **2** | fail-safe to strict (FR-004) |
| *absent* | satisfied | **0** | satisfied passes |
| *unrecognized* (declared, validates, but not an enforcement profile) | failing | **2** | fail-safe to strict — `recognizeProfile` not-recognized ⇒ `Strict` (D2; Invariant 2) |

Exit codes per `Cli.fs:344`: `Success → 0`, `GovernedBlocking → 2`.

## Invariants

1. **Profile governs the boundary, not the existence of the gate.** Both profiles distinguish a
   failing handoff from a satisfied one; light relaxes only the *failing* case from blocking to
   advisory — it never turns a satisfied handoff into a block, nor a failing one under strict into a
   pass.
2. **Fail-safe is one-directional.** Absent, empty, or unrecognized profile ⇒ strict ⇒ a failing
   handoff blocks. No path relaxes by omission.
3. **Mode scope.** The relaxation applies at the `--mode gate` boundary (mapped to the enforcement
   `Verify` ordinal). `--mode sandbox` / `--mode inner` remain advisory for a failing handoff under
   every profile (they map below any blocking floor), exactly as the 089 baseline.
4. **Many gates: block if any blocks.** With multiple consumed handoff gates, the run blocks iff at
   least one derives a blocking effective severity under the active profile; relaxing one gate must
   not mask another that still blocks.
5. **No handoff special-casing.** The handoff gate flows through the same
   `Enforcement.deriveEffectiveSeverity` path as every other gate; switching to `light` is a
   product-wide policy choice the handoff honors like any gate — it is not forced back to strict-only.
6. **Attribution.** A block surfaces the failing handoff's `EnforcementDecision.Reason`.

## Real-evidence checks (in-repo, gate the publish)

`tests/cli-publish-smoke/run.sh` (packs → installs → runs the real tool):

| fixture | `.fsgg/policy.yml` | assertion |
|---|---|---|
| `failing-handoff` | none (profile-less) | `--mode gate → exit 2` (unchanged, FR-006/SC-004) |
| `passing-handoff` | none (profile-less) | `--mode gate → exit 0` (unchanged) |
| `failing-handoff` | none | `--mode inner → exit 0` (unchanged) |
| **`light-failing-handoff`** (NEW) | `defaultProfile: light` | **`--mode gate → exit 0`** (the new behavior) |

Plus a pure decision-matrix test in `FS.GG.Governance.Cli.Tests` covering all rows of the matrix
above (the light+failing row fails against today's shortcut and passes after).

## Downstream acceptance signal

Against the published `1.2.0`, FS.GG.Templates#25 `tests/composition/run.sh` Stage 6b reports **all
cells passing** (SC-003) — the previously red `light + failing → exit 0` cell is green. That flip,
not the push alone, resolves FS-GG/FS.GG.Governance#34.
