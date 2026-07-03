# ADR 0007 — `FS.GG.Governance.CommandHost` is the home for the shared host-edge impure leaves

**Status**: Accepted · **Date**: 2026-07-03 · **Feature**: Epic FS-GG/FS.GG.Governance#44 (2026-07-02 code review)

**Resolves**: issue FS-GG/FS.GG.Governance#74 ("consolidate the impure leaves `writeAtomic`/`realHandoffs`/`sense*` — deferred from #49"). Follows [ADR-0003](0003-gaterunhost-unification.md) (host unification) and [ADR-0006](0006-cli-format-flag-vocabularies.md) (the #49 F9 decision).

## Context

The 2026-07-02 code-quality review ([report](../reports/2026-07-02-141008-code-quality-architecture-review.md), §"second extraction pass on the command hosts") measured four impure host-edge helpers hand-copied across the seven command hosts:

| Helper | Pre-consolidation copies | Does |
|---|---|---|
| `writeAtomic` | 7 (Route/Ship/Verify/Release/Evidence/Refresh/CacheEligibility) | temp-file + atomic rename write |
| `realHandoffs` | 4 (3 hosts + a Cli mirror; **one copy had drifted** on sort order) | scans `readiness/*/governance-handoff.json` |
| `senseEnvironmentReal` | 3 (Ship/Verify/Release) | reads the `CI` env var → `EnvironmentClass` |
| `senseBuilderReal` | 3 (Ship/Verify/Release) | constant `BuilderIdentity "fsgg"` |

Issue #49 landed the correctness fixes and deferred this consolidation because it had a *placement* obstacle (`CommandHost` is charted pure; the impure leaves "can't live there"), not a lack of value. #74 posed three options: **(A)** own them in `CommandHost` and formalize the exception; **(B)** push each down to its type-owner project (`realHandoffs → Adapters.SddHandoff`, `sense* → Provenance`, `writeAtomic → some shared IO util`); **(C)** extract a net-new `FS.GG.Governance.CommandHost.Io` sibling.

**State at decision time:** commit `2fcb1ba` ("Phase A leaf pass") had already **consolidated all four helpers into a single copy each** in `src/FS.GG.Governance.CommandHost/CommandHost.fs` (declared in `CommandHost.fsi`). The de-duplication is therefore already banked — including the correction of the drifted `realHandoffs` sort to a single `String.CompareOrdinal` ordering. #74 reduced to a pure *placement* question: is `CommandHost` the permanent home, or should the leaves move?

## Decision

**Ratify Option A: `FS.GG.Governance.CommandHost` permanently owns the four host-edge impure leaves as a deliberate, enumerated exception to its otherwise-pure charter.**

`CommandHost` is a **host-layer** leaf — it sits BELOW the seven command hosts and ABOVE the domain-type owners. The impure host edges belong "in the host layer, never the domain core," and `CommandHost` *is* that host layer. The original "pure and total; no I/O" charter was simply too strict for a project at this altitude; the honest fix is to amend the charter (this ADR + the `.fsproj`/`.fs`/`.fsi` header notes), not to relocate the code.

**Option B (push-down) is rejected on evidence.** Both proposed type-owner homes are chartered *pure and total*, and each helper would violate the target's own written contract:

- `Adapters.SddHandoff` — `Reader.fsi` states *"LOCATING and READING the file is the host's port… this layer sees only the already-read (path, json) text."* `realHandoffs` scans directories and reads files — the exact I/O the module exists to keep out.
- `Provenance` — `Provenance.fsi` states *"reading no clock, filesystem, git, environment… performs NO sensing."* `senseEnvironmentReal` reads the `CI` environment variable — a direct violation.
- `writeAtomic` carries no governance type at all, so it has **no** natural type-owner to push down to.
- Push-down would also trip the `referencesOnly` surface guards (new transitive references) and force re-blessing multiple `surface/*.surface.txt` baselines — cost with no coherence gain.

**Option C (new `.Io` sibling) is deferred, not rejected.** It is architecturally clean (mirrors the 073 `JsonWriters` extraction precedent) and would restore `CommandHost`'s purity, but its benefit over A is marginal (both keep the I/O out of the domain core), and it carries a real cost: a net-new project needs a committed `packages.lock.json` generated in an authenticated feed environment — the exact offline-verification obstacle that caused #49 to defer this in the first place — plus a new surface baseline and re-pointing all seven host `.fsproj`s. If a future change gives the `.Io` project independent reason to exist (e.g. absorbing the ad-hoc `Scaffold`/`Cli` atomic writers), this ADR is the place to revisit C.

## Consequences

- `CommandHost` intentionally owns a **small, enumerated impure surface**: `writeAtomic`, `realHandoffs`, `senseEnvironmentReal`, `senseBuilderReal`. The `.fs`/`.fsi` "host edge I/O leaves" section already documents this; the `.fsproj` header comment is corrected from "no I/O" to point here.
- The four helpers stay type-honest (each returns its domain type: `HandoffRead list`, `EnvironmentClass`, `BuilderIdentity`) while the *type owners* keep their pure charters intact.
- No new project, no new lockfile, no baseline churn, no host re-pointing — the code is already in place; this ADR makes the placement a ratified decision rather than an accidental charter violation.
- The dedup value (7/4/3/3 copies → 1 each, drifted `realHandoffs` sort corrected) is preserved; #74's acceptance ("one implementation per helper reachable by all hosts") is met by the already-landed `2fcb1ba` plus this placement ratification.
- **Guardrail for future readers:** a well-meaning "restore purity" refactor that pushes these leaves down into `Adapters.SddHandoff`/`Provenance` is explicitly rejected here — it would break those projects' pure charters and their round-trip contracts. Extract to a `.Io` sibling (Option C) instead if purity of `CommandHost` is ever required.
