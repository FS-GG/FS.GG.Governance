# Contract: `release.json` deterministic audit projection

Produced by `FS.GG.Governance.ReleaseJson.ofRelease : ReleaseDecision -> SensedRelease -> string`. Pure,
total, emit-only, byte-deterministic. Compact (non-indented) UTF-8, hand-driven `Utf8JsonWriter` (the
`AuditJson`/`RouteJson` precedent — no new dependency). Identical repository state + identical
declaration ⇒ byte-identical output (FR-008, SC-003). No timestamps, no absolute/host paths, no
machine-specific content.

## Top-level shape (fixed field order)

```jsonc
{
  "schemaVersion": "fsgg.release/v1",      // fixed literal, never derived from clock/env/input
  "verdict": "pass" | "fail",              // ReleaseDecision.Verdict
  "exitCodeBasis": "clean" | "blocked",    // ReleaseDecision.ExitCodeBasis
  "rules": [ /* one entry per declared rule, F053 stable composite order */ ],
  "evidence": { /* the per-family observed-evidence snapshot */ }
}
```

## `rules[]` — one finding per declared rule (FR-007)

Order is the already-fixed F053 composite key (kind ordinal, then surface id) — never re-sorted here.

```jsonc
{
  "kind": "versionBump",                   // releaseRuleKindToken (closed enum, exhaustive)
  "surface": "GovernancePackages",         // SurfaceId
  "factState": "met" | "unmet" | "unrecoverable",   // from sensed.Facts.States[kind]
  "outcome": "satisfied" | "violated",     // ReleaseFinding.Outcome
  "baseSeverity": "advisory" | "blocking", // declared base severity
  "effectiveSeverity": "advisory" | "blocking", // F023 deriveEffectiveSeverity (from EnforcedReleaseFinding.Decision)
  "reason": "…"                            // product-neutral, actionable
}
```

`rules` contains **exactly six** entries on every successful run (one per family, FR-013/SC-006),
including all-`unrecoverable` repositories. The set of `rules` and their verdict/outcome match the human
text output for the same run (FR-009).

## `evidence` — the F054 `ReleaseSnapshot` (FR-007)

Per-family observed evidence; each per-family object is present when the evidence was recovered
(`met`/`unmet`) and `null` when `unrecoverable`. All token lists are sorted upstream (F054).

```jsonc
{
  "surface": "GovernancePackages",
  "version":  { "observed": "1.3.0", "baseline": "1.2.0" } ,            // or null
  "metadata": { "present": [...], "missing": [...] },                    // or null
  "pins":     { "resolved": [["t","v"]], "expected": [["t","v"]], "drifted": [...] }, // or null
  "publishPlan":        { "observed": [...], "required": [...], "missing": [...] },   // or null
  "trustedPublishing":  { "observed": [...], "required": [...], "missing": [...] },   // or null
  "provenance":         { "observed": [...], "required": [...], "missing": [...] },   // or null
  "diagnostics": [ { "family": "templatePins", "reason": "…" } ]         // ordinal-sorted
}
```

## Determinism & integrity guarantees

- **Byte-identical** for identical repository state and declaration (FR-008, SC-003) — verified by a
  re-run test and a committed golden baseline.
- **Emit-only**: re-derives, re-sorts, re-classifies nothing; the `ReleaseDecision` already fixed the
  verdict/basis/partition/order and the `ReleaseSnapshot` already fixed evidence ordering.
- **Exhaustive token helpers**: every enum (`verdict`, `exitCodeBasis`, `factState`, `outcome`,
  `severity`, `kind`) is matched with no wildcard, so a new case is a compile error, not a silent
  mis-token.
- **Atomic on disk**: when written to `--out`, a failed/interrupted write leaves no partial file (FR-012).
- **Validates against the committed schema/golden baseline** and never contradicts the text verdict
  (SC-007, FR-009).
