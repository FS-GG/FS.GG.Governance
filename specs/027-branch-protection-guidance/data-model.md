# Phase 1 Data Model: Branch-Protection Guidance for `fsgg ship`

This row ships no F# types and no new persisted document. Its "entities" are the **conceptual artifacts**
the guidance publishes and the **contracts it consumes** — recorded here so the contract files, the
template, and the validation script stay consistent. Where a field is *consumed*, its authority is the
merged upstream artifact (F025/F026); this row introduces no field and re-shapes none (FR-007, FR-011).

## Entity 1 — Branch-protection guidance (the published document)

The `docs/ci/github-actions-branch-protection.md` document. Required sections (each maps to FRs/US it
satisfies):

| Section | Content | Satisfies |
|---|---|---|
| Blocking model | Blocking is tied **solely** to the deterministic `fsgg ship` exit code; names no other blocking source. | FR-008, US4 |
| Exit-code → check mapping | The table from [contracts/exit-code-check-mapping.md](./contracts/exit-code-check-mapping.md): `0`→pass, `1`→fail (blocked), `2`/`3`/`4`→fail (tool failure, distinct), never a false pass. | FR-003, FR-004, US1, US2 |
| Required status check setup | Step-by-step: add the workflow, then mark the ship job a **required status check** in branch protection. | FR-002, US1 |
| Checkout requirements | `fetch-depth: 0` (or explicit base-ref fetch) for base/head sensing. | FR-009, Edge: shallow checkout |
| Fail-closed wiring | No exit-code translation; no path/event filters on the gate; gate runs on forks; surfacing never fails-open. | FR-005, Edge: forks, required-but-not-run |
| Audit surfacing | Upload `audit.json` as an artifact **and** render a job summary; the surfaced bytes are exactly what the command wrote. | FR-006, FR-007, US3 |
| Invocation (honest) | From-source `dotnet run --project …` today; packed `fsgg ship` as a clearly-marked placeholder. | FR-010, Edge: tool not packaged |
| Honesty boundary | Advisory/agent findings reported, never blocking until calibration; no provenance/attestation/compliance claims. | FR-008, FR-013, US4 |
| Substitutions | Every value an adopter must change (protected-branch name, invocation step), clearly marked. | FR-012, SC-006 |

Validation rules: the document MUST name no blocking source other than the deterministic exit code (FR-008);
MUST NOT present a `dotnet tool install`/packed install path as available (FR-010); MUST NOT claim
provenance/attestation/compliance (FR-013).

## Entity 2 — Ship CI workflow template

The copyable `docs/ci/templates/fsgg-ship.yml`, mirrored as a fenced block in the guidance (the validation
script asserts they match). Its required shape is fixed by
[contracts/ship-ci-workflow.md](./contracts/ship-ci-workflow.md):

| Element | Requirement | Satisfies |
|---|---|---|
| Trigger | `pull_request` targeting the protected branch; **no** `paths:`/`paths-ignore:` on the gate. | FR-005 |
| Checkout | `actions/checkout` with `fetch-depth: 0`. | FR-009 |
| Toolchain | `actions/setup-dotnet` (current path needs the SDK to build from source). | FR-010 |
| Gate step | Runs `… ship --mode gate --profile standard --json`; exit code is the step status, **untranslated** (no `||`, no `continue-on-error`). | FR-001, FR-003 |
| Surfacing step | `if: always()`: upload `readiness/audit.json` artifact + render `$GITHUB_STEP_SUMMARY`; best-effort, never gates. | FR-006, US3 |
| Adopter substitutions | Clearly-marked: protected-branch name; the invocation line (from-source now / packed later). | FR-012 |

Validation rules: valid GitHub Actions YAML (D3); exit code never remapped (FR-003); no filter lets a
governed change skip (FR-005); surfacing step never fails the job (fail-closed surfacing).

## Entity 3 — Exit-code → check-status mapping (consumed from F026; this row only documents it)

The contract the wiring documents. **Authority: `Loop.exitCode` in `src/FS.GG.Governance.ShipCommand`
(F026).** This row neither defines nor changes it.

| Process exit code | Meaning (F026) | GitHub check | Merge outcome | Category in run |
|---|---|---|---|---|
| `0` | `Success` — clean verdict | passing (green) | allowed | clean |
| `1` | `Blocked` — blocked merge verdict | failing (red) | blocked | **blocked verdict** |
| `2` | `UsageError'` — usage error | failing (red) | blocked | tool failure |
| `3` | `InputUnavailable` — input unavailable | failing (red) | blocked | tool failure |
| `4` | `ToolError` — tool error | failing (red) | blocked | tool failure |

Invariants the guidance preserves: `1` is the **only** code that means "blocked merge" and is distinct
from `2`/`3`/`4` (FR-004, SC-002); a tool failure (`2`/`3`/`4`) is **never** a green check (FR-005); codes
are passed through untranslated (FR-003).

## Entity 4 — `audit.json` (whole-change verdict view; consumed from F025, surfaced not re-shaped)

**Authority: the F025 contract (`specs/025-audit-json-projection/contracts/audit-json-document.md`).** The
workflow surfaces these bytes verbatim (FR-007); the guidance lists the fields only so reviewers know what
they are reading and the validation script can cross-check field names.

- Top level (field order fixed by F025): `schemaVersion` (`"fsgg.audit/v1"`), `verdict` (`pass`|`fail`),
  `exitCodeBasis` (`clean`|`blocked`), `blockers[]`, `warnings[]`, `passing[]` (each always present, empty
  array when none).
- Item (tagged by `kind`): a `gate` (`kind`,`id`,`enforcement`) or a `finding`
  (`kind`,`id`,`path`,`enforcement`).
- `enforcement` (six fields, order fixed): `baseSeverity`, `maturity`, `mode`, `profile`,
  `effectiveSeverity`, `reason`.
- No-hide: a base-`blocking` item relaxed by the profile appears in `warnings` carrying **both**
  `baseSeverity` and a differing `effectiveSeverity` — never silently dropped (FR-006/US3 AS2).

Validation rule: the field names the guidance surfaces MUST equal this set; the row re-derives, re-sorts,
and re-shapes nothing (FR-007, FR-011).

## Relationships

```text
docs/initial-implementation-plan.md  ── names ──▶  "Publish the first GitHub Actions guidance
                                                    for branch protection" (Phase-2 closing row)
                                                          │ delivered by
                                                          ▼
Entity 1 (guidance) ── embeds & references ──▶ Entity 2 (workflow template)
        │ documents                                   │ invokes
        ▼                                             ▼
Entity 3 (exit-code→check map) ◀── fixed by ── fsgg ship / Loop.exitCode (F026)
Entity 4 (audit.json view)     ◀── fixed by ── AuditJson.ofShipDecision (F025)
        ▲                                             ▲
        └──────── cross-checked by ── scripts/check-ship-ci-guidance.sh ───────┘
```

The validation script is the consistency enforcer: it proves Entity 2 is valid Actions YAML and that
Entities 3–4 as documented equal their upstream authorities — so the guidance cannot drift from the
command it wires.
