# ADR 0006 — The per-command output-format flag vocabularies intentionally diverge

**Status**: Accepted · **Date**: 2026-07-03 · **Feature**: `specs/103-command-host-consolidation`

**Resolves**: review finding **F9** (issue FS-GG/FS.GG.Governance#49, Epic #44) — "four divergent format-flag vocabularies; converge them or document why they differ."

## Context

The seven command hosts do not share one output-format flag grammar. The 2026-07-02 review measured four distinct vocabularies:

| Vocabulary | Hosts | Flags | Non-JSON value | Combined mode |
|---|---|---|---|---|
| **A — boolean `--json`** | Route, Ship, Verify | bare `--json` (+ `--plain`) | `Text` | — |
| **B — `--format text\|json\|both`** | Release | `--format <v>` | `text` | `both` |
| **C — boolean triple** | Refresh | `--text` / `--json` / `--text-and-json` | `text` | `text-and-json` |
| **D — `--format human\|json`** | Evidence, CacheEligibility | `--format <v>` (+ `--plain`) | `human` | — |

The central `Cli.fs` additionally accepts *both* bare `--json` and `--format`, straddling A and B. Secondary axes of divergence: the non-JSON value is named `text`/`Text` in A/B/C but `human`/`Human` in D; a combined "text **and** json" mode exists only in Release (`both`) and Refresh (`text-and-json`), under two different spellings.

The review asked whether to converge these to one grammar.

## Decision

**Do not converge. Document the divergence (this ADR) and leave each host's vocabulary as-is.**

The command hosts consolidated their *impure/pure internals* under #49 (shared `guard`/`drive`, the reused `Cli.ArtifactReading`), but the **output-format flag grammar is a published CLI contract**, not an internal detail. Converging it would:

- **Break existing invocations and CI.** Renaming or removing an accepted flag (e.g. dropping Refresh's `--text-and-json`, or Release's `--format both`, or unifying `human` ↔ `text`) is a backward-incompatible change to every script, workflow, and downstream product that calls these commands. That is a Tier-1 CLI break with cross-repo blast radius, disproportionate to a cosmetic consistency win.
- **Erase meaningful distinctions.** The `human` vs `text` naming is not accidental: Evidence/CacheEligibility emit a *human-readable* report with no ANSI/rich path, whereas Route/Ship/Verify emit a `Text` summary that composes with `--plain`. The combined `both`/`text-and-json` modes exist only where a host genuinely writes a text summary *and* a JSON artifact.

The internal-consolidation goal of #49 is served without touching the surface: the shared parsing *hazard* (M-CLI-3 value-swallowing) was already fixed uniformly across all vocabularies in Phase A. What remains is nomenclature, and nomenclature on a published CLI is a contract, not drift.

## Consequences

- The four vocabularies stand. Contributors should expect per-host flag grammars and consult each host's `Loop.fs` parser + `usage`.
- If a future major (SemVer) CLI revision is undertaken, this ADR is the place to record a converged grammar (candidate target: `--format text|json|both` everywhere, with `--plain` additive, retiring the bare-boolean and `human` spellings behind a deprecation window). Until then, convergence is explicitly deferred as a breaking change not worth its cost.
- No code changes accompany this decision; the M-CLI-3 guard (Phase A, #69) already made the *parsing* of every vocabulary safe and uniform.

## Update — 2026-07-15 (CLI-3): `text` accepted as an additive synonym for `human`

The 2026-07-15 review re-raised the `human` vs `text` split (finding **CLI-3**): `fsgg release --format text` succeeds while `fsgg evidence --format text` errors with `BadFormat "text"`, a footgun for anyone reusing the plain-token spelling across hosts.

This ADR's "do not converge" decision stands for the **breaking** direction — nothing is renamed or removed, so no existing invocation changes behavior. What landed is the strictly **additive** half the finding also offered: Evidence and CacheEligibility now accept `text` as a synonym for the canonical, still-default `human` token (`Loop.parse` in both hosts; `BadFormat` messages now read `expected human|text|json`). `human` remains canonical and the default; `text` is simply also accepted, so `--format text` now works on all seven hosts.

This is backward-compatible (a pure superset of previously-accepted input) and moves one non-breaking step toward the deferred convergence target recorded above (`--format text|json|both` everywhere). The reverse — teaching route/ship/verify/release/dispatcher to accept `human` — was **not** done: the convergence direction is toward `text` (README documents the shared spelling as `--format {text|json}`), and adding `human` everywhere would broaden surface against that direction. Full convergence (retiring the bare-`--json` boolean, the `both`/`text-and-json` spellings, and the `human` default) remains a breaking change deferred to a future major SemVer CLI revision, exactly as decided above.
