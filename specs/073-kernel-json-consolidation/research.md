# Phase 0 Research — Kernel JSON consolidation

All NEEDS CLARIFICATION items from Technical Context are resolved here. Each decision
records what was chosen, why, and what was rejected. Counts were verified against the
working tree at branch `docs/architecture-quality-analysis` (tip `7dcf149`), not estimated.

## D1 — Where do the shared helpers live? (`Kernel` vs a new layer)

**Decision.**
- `writeToString` → **exported from `FS.GG.Governance.Kernel/Json.fsi`** (stays in `Kernel`).
- The token + writer helpers → **two NEW pure leaves placed above the domain-enum owners
  and below the projections**, named `FS.GG.Governance.JsonTokens` and
  `FS.GG.Governance.JsonWriters` (not `Kernel.*`).

**Rationale.** `writeToString` depends only on `System.Text.Json` (`MemoryStream` +
`Utf8JsonWriter` + UTF-8 decode) — no domain types — so it correctly belongs in `Kernel`,
which is `System.*`-only. It is already defined at `Json.fs:23`; only the `.fsi` line is
missing. By contrast every token/writer helper serializes a **domain enum or record** —
`Cost`, `Maturity`, `Severity`, `EnvironmentClass`, `GateDisposition`, `ExitCodeBasis`,
`Profile`, `RecomputeCause`, `CacheEligibilityVerdict`, `GateOutcome`, `EnforcementDecision`
— all defined in projects that themselves reference `Kernel`. A leaf physically *under*
`Kernel` cannot see those types, so `Kernel.JsonTokens`/`Kernel.JsonWriters` as drawn in the
report do not compile. The report's `Kernel.` prefix was namespace shorthand for "a shared
pure leaf," confirmed by its own design rule that the leaves "depend only on the already-
shared domain types" — which places them *above* those types.

**Alternatives rejected.**
- *Put the token/writer helpers in `Kernel`.* Rejected — would force `Kernel` to reference
  `Gates`/`Enforcement`/`CacheEligibility`/…, inverting the dependency graph and destroying
  `Kernel`'s `System.*`-only purity (a constitution constraint).
- *Re-home each token in its owning domain project's `.fsi`* (e.g. reuse
  `Enforcement.maturityToken`, `FreshnessKey.environmentToken`). Rejected as the primary
  strategy — see D4; the domain copies may emit different strings for non-JSON consumers,
  and byte-identity is the hard gate. Kept as a *possible* future cleanup, not this feature.

## D2 — One combined leaf, or two (`JsonTokens` + `JsonWriters`)?

**Decision.** Two leaves. `JsonWriters` references `JsonTokens`.

**Rationale.** Distinct concerns with distinct dependency fan-out:
- `JsonTokens` (enum→string) needs only the **enum-owning** projects: `Config`/`Gates`
  (`Cost`, `Maturity`, `EnvironmentClass`, `GateDisposition`), `Findings`/`Enforcement`
  (`Severity`, `Profile`, `ExitCodeBasis`).
- `JsonWriters` (sub-object/map emission) additionally needs `CacheEligibility`,
  `EvidenceReuse`, `GateRun`, `CommandRecord`, and `Enforcement` (`RecomputeCause`,
  `CacheEligibilityVerdict`, `GateOutcome`, `ExitCode`, `EnforcementDecision`), and it calls
  token helpers — so it sits one layer above `JsonTokens`.

Combining them would push the wider `JsonWriters` dependency set onto consumers that only
want a token. The repo's one-concern-per-project norm (75 micro-libraries) favors the split.

**Alternative rejected.** A single `FS.GG.Governance.JsonShared` leaf — simpler project
count, but couples two concerns and over-broadens the dependency surface for token-only
users.

## D3 — Which token helpers are in scope?

**Decision.** Only the **seven helpers that are actually duplicated across projections**:
`costToken`, `maturityToken`, `severityToken`, `environmentToken`, `dispositionToken`,
`basisToken`, `profileToken`. Verified duplication sites (working tree):

| Helper | Duplicated in | Canonical-elsewhere? |
|---|---|---|
| `costToken` | RouteJson, GatesJson, CostBudgetJson | — |
| `maturityToken` | RouteJson, GatesJson, AuditJson, VerifyJson | also `Enforcement.maturityToken` (left untouched, D4) |
| `severityToken` (`Severity`→advisory/blocking) | VerifyJson, AuditJson, ReleaseJson, CostBudgetJson | distinct from `Kernel.Json.severityToken` (same strings, different home) and `SurfaceChecks.Model.severityToken` |
| `environmentToken` | RouteJson, GatesJson, ProvenanceJson, AttestationJson | also `FreshnessKey.environmentToken`, `Provenance.environmentToken`, `EvidenceReuseStore.environmentToken` (left untouched, D4) |
| `dispositionToken` | RouteJson, AuditJson, VerifyJson | — |
| `basisToken` | VerifyJson, AuditJson, ReleaseJson | — |
| `profileToken` | VerifyJson, AuditJson | also `Enforcement.profileToken` (left untouched, D4) |

**Out of scope (NOT moved):**
- Single-use tokens that appear in exactly one projection — e.g. `ReleaseJson.factStateToken`,
  `ReleaseJson.outcomeToken`. De-duplicating a one-definition helper adds a dependency edge
  for no reduction; leave them local.
- The `Verdict` token (`verdictToken` in ReleaseJson + VerifyJson, plus VerifyJson's
  `rr`-prefixed `rrVerdictToken`). It is *copied*, but the copies are **not byte-identical**:
  `VerifyJson.verdictToken` emits `Fail` → `blocked`, whereas `ReleaseJson.verdictToken` and
  `rrVerdictToken` emit `Fail` → `fail`. `Verdict` is also not one of the seven closed enums.
  Unifying would change goldens, so it is surfaced and left local (D4, spec Edge Cases).

Only move what is copied **and** byte-identical.

## D4 — Byte-identity strategy (the hard gate)

**Decision.** Build each shared helper from the **exact strings the projections currently
emit**, and confirm byte-identity per slice by running the relevant `*Json.Tests` golden/
snapshot suites *before deletion of the last copy*. Do **not** re-point projections at the
pre-existing **domain-owned** token helpers (`Enforcement.maturityToken`,
`FreshnessKey.environmentToken`, `Provenance.environmentToken`, `Kernel.Json.severityToken`,
`SurfaceChecks.Model.severityToken`) in this feature.

**Rationale.** The domain-owned copies serve non-JSON consumers (human text, sensing) and
may legitimately differ in casing or vocabulary; adopting them risks a silent golden shift.
The duplicated projection copies are believed identical (the report's premise), but the
acceptance gate proves it rather than assuming it: extract → reference → **run goldens** →
only then delete. A copy that proves to differ is reported and left local, not silently
unified (spec Edge Cases). The `Verdict` token is the confirmed instance: `VerifyJson`'s
`rr`-prefixed `rrVerdictToken` matches `ReleaseJson.verdictToken` (`Fail` → `fail`) but
diverges from `VerifyJson.verdictToken` (`Fail` → `blocked`), so it cannot be unified and
is out of scope (D3).

**Verification mechanism.** Each `*Json.Tests` project holds golden fixtures and snapshot
assertions; a green run with no fixture file modified in `git status` is the proof. The
per-project `SurfaceDriftTests.fs` proves the projection's *public* surface is unchanged
(the moved helpers are hidden, so projection surfaces should not move at all).

## D5 — Dependency edges, sequencing, and name-collision risk

**Decision / findings.**
- **`writeToString` reachability.** Most projections already reach `Kernel` transitively
  (e.g. `AuditJson → Ship → … → Kernel`), and .NET SDK transitive project references flow at
  compile time, so `Json.writeToString` is callable without a *new* edge in many cases. Add
  an explicit `Kernel` `ProjectReference` only where `Kernel` is not already on the graph.
  The two non-projection sites (`EvidenceReuseStore`, `RefreshCommand/Interpreter`) and
  `AttestationJson` are included in Slice 1.
- **Sequencing.** Slice 1 (`writeToString`) first — it is the largest, lowest-risk
  reduction and establishes the `Kernel` edge. Slice 2 (`JsonTokens`) next. Slice 3
  (`JsonWriters`) last — it depends on `JsonTokens` and carries the most shape. One concern
  per commit so each test run isolates any golden drift (report design rule).
- **Open-ambiguity risk.** A projection that `open`s both `Enforcement` (which exposes
  `maturityToken`/`profileToken`) and the new `JsonTokens` would hit an ambiguous-name
  error. Mitigation: call the shared helpers **module-qualified** (`JsonTokens.maturityToken`)
  or via a local module abbreviation (`module T = FS.GG.Governance.JsonTokens`), rather than
  `open`-ing the leaf. This keeps call sites unambiguous and self-documenting.
- **Surface baselines.** New leaves each get a `surface/<Project>.surface.txt` baseline and a
  `SurfaceDriftTests.fs`; `Kernel`'s baseline gains the `writeToString` member. Projection
  baselines should be unchanged (helpers were already hidden).
- **Solution registration.** The two new `src` projects and their two `*.Tests` projects are
  added to `FS.GG.Governance.sln`.

## Resolved unknowns

| Unknown | Resolution |
|---|---|
| Can the shared token/writer leaves sit under `Kernel`? | No — D1. Placed above domain projects, below projections. |
| One leaf or two? | Two — D2. |
| Reuse pre-existing domain token helpers? | No, not in this feature — D4. |
| How is byte-identity proven? | Existing `*Json.Tests` goldens unchanged + `SurfaceDriftTests` — D4. |
| Do all projections need a new `Kernel` edge? | Only those not already reaching `Kernel` transitively — D5. |
| Exact number of `writeToString` copies | 13–14 non-Kernel copies (incl. `AttestationJson`'s `private` one); exact set enumerated during `/speckit-tasks`. SC-001 ("one definition remains") is the invariant. |
