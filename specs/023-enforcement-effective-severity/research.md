# Phase 0 Research: Enforcement Levers and Effective Severity

The spec deferred a small set of plan-time reconciliations against the design's run-mode ladder and the
existing F014/kernel vocabulary. This document resolves them. Each is a **Decision / Rationale /
Alternatives** triple. The decisions are encoded into [data-model.md](./data-model.md) and
[contracts/enforcement-decision.md](./contracts/enforcement-decision.md).

Source of truth for the semantics: `docs/initial-design.md` §*Modes, profiles, and maturity* (line 501) and
its worked example (line ~514); the reused types in `src/FS.GG.Governance.Config/Model.fsi`
(`Maturity`, `ProfileId`); the kernel sublanguage in `src/FS.GG.Governance.Kernel/Route.fsi` (`RunMode`) and
`CheckRule.fsi` (`Severity`).

---

## D1 — Project home

**Decision**: A new optional, packable pure-leaf project **`FS.GG.Governance.Enforcement`** with a single
`Enforcement.fsi`/`Enforcement.fs` module pair, `IsPackable=true`, `PackageId FS.GG.Governance.Enforcement`,
taking exactly one `ProjectReference` (`FS.GG.Governance.Config`). Plus one test project
`FS.GG.Governance.Enforcement.Tests`.

**Rationale**: Continues the maintainer-confirmed one-row-one-project rhythm of F014–F021 (each pure core is
its own packable project). The feature is a pure value-to-value computation, so it is a **leaf** like
`GatesJson`/`Route`/`Gates`, not an edge like `RouteCommand`/`Host`. A dedicated project keeps the
enforcement vocabulary independently packable for the later `fsgg ship`/`audit.json` rows to reference,
exactly as those rows will reference `Gates`/`Route` today.

**Alternatives considered**:
- *Add the types into F014 `Config`* — rejected: `Config` is the YAML-validation/typed-facts layer; run
  mode, severity, and the derivation are an enforcement concern `Config` deliberately does not model (the
  spec notes F014 "did not model" this vocabulary). Layering it on top in a separate project matches the
  constitution's "heavier capabilities layer on top, not into the core."
- *Add into the kernel `Route`/`CheckRule`* — rejected: see D2; the Phase-2/5 line references the kernel
  nowhere and the kernel ladder is the wrong shape.

---

## D2 — `RunMode` and `Severity` are NEW here, not the kernel's

**Decision**: Define **new** `RunMode` (six values) and `Severity` (`Advisory|Blocking`) DUs in this
project. Do **not** reference `FS.GG.Governance.Kernel`. Reuse F014 `Config.Model.Maturity` and
`Config.Model.ProfileId` verbatim (the one project reference).

**Rationale**: The kernel already carries a `Severity` (`Advisory|Blocking`, `Kernel/CheckRule.fsi:39`) and a
**three-value** `RunMode` (`Sandbox|Inner|Gate`, `Kernel/Route.fsi:57`) — an older, separate sublanguage.
The entire Phase-2/5 line (`Config`/`Routing`/`Snapshot`/`Findings`/`Gates`/`Route`/`RouteJson`/`GatesJson`/
`RouteCommand`) references the kernel **nowhere** (F022 confirmed the distinct lineage). FR-001 requires
**six** run modes (`sandbox`/`inner`/`focused`/`verify`/`gate`/`release`); the kernel's three-value ladder
cannot express `focused`/`verify`/`release`. Taking a kernel reference solely to reuse a two-case `Severity`
would couple the clean Phase-5 leaf to the kernel lineage for no benefit. The relationship is documented in
the `.fsi` header so a reader is not surprised by the two `Severity`/`RunMode` definitions.

**Alternatives considered**:
- *Reference the kernel and reuse its `Severity`* — rejected: couples lineages; the kernel `RunMode` is still
  unusable (wrong arity), so a new `RunMode` is required regardless, and a half-reuse (kernel `Severity`, new
  `RunMode`) is more confusing than two fresh, self-contained DUs.
- *Reuse F014 for severity* — not possible: F014 models `Maturity` but not a base/effective severity value
  (the spec calls this out as vocabulary "F014 did not model").

---

## D3 — Maturity → run-mode floor mapping

**Decision**: Each maturity names the minimum **run-mode ordinal** at which a **base-blocking** finding may
block. Ordinals: `sandbox 0 < inner 1 < focused 2 < verify 3 < gate 4 < release 5`.

| Maturity | Blocks at modes ≥ | Floor ordinal |
|---|---|---|
| `observe` | never | — (∞) |
| `warn` | never | — (∞) |
| `block-on-pr` | `gate`, `release` | 4 (`gate`) |
| `block-on-ship` | `gate`, `release` | 4 (`gate`) |
| `block-on-release` | `release` | 5 (`release`) |

**Rationale**: Reproduces the design's worked example exactly (base `blocking` + `block-on-ship` + `inner` +
`light` ⇒ effective `advisory`; the same finding at `gate`/`release` ⇒ `blocking`, SC-002, acceptance
scenarios 1–2). The spec's "Maturity → boundary mapping" assumption states the boundaries verbatim:
"`block-on-pr` at the PR/gate boundary; `block-on-ship` at the ship/gate boundary; `block-on-release` only
at release." Both `block-on-pr` and `block-on-ship` name the **`gate`** mode — and in this Governance-only
slice they **deliberately coincide** at the `gate` floor: the design runs ship at `fsgg ship --mode gate`,
and there is no distinct PR vs ship *run mode* in the six-value ladder yet. The edge-case enumeration in the
spec ("below the ship boundary (`sandbox`/`inner`/`focused`) is advisory; at or beyond it (`gate`/`release`)
it blocks") confirms the ship floor is `gate` (4) and that `verify` (3), though unenumerated, sits below it
(advisory).

**Alternatives considered**:
- *Split `block-on-pr` to `verify` (3) and `block-on-ship` to `gate` (4)* for a strictly distinct ladder —
  rejected: the spec's own assumption text keys **both** to the "gate" boundary, and the design has no
  `verify`-stage PR gate concept yet; inventing one over-specifies the deferred PR/ship split. Documented as
  the future split point instead (a later feature introduces a distinct PR run-position).
- *Make `block-on-pr` block at `sandbox`/`inner`* — rejected: contradicts "promote carefully" (rules block at
  protected boundaries, not in the inner loop) and the worked example's relaxed inner-loop behavior.

---

## D4 — Profile strictness (intrinsic, four canonical profiles)

**Decision**: The four canonical profiles **tighten** (never relax below) the maturity floor by a documented
adjustment applied to the floor ordinal, clamped to `[0, 5]`:

| Profile | Floor adjustment | Effect |
|---|---|---|
| `light` | 0 | block exactly at the maturity floor |
| `standard` | 0 | block exactly at the maturity floor |
| `strict` | −1 | block one boundary earlier |
| `release` | −2 | block two boundaries earlier |

A **base-advisory** finding is **never escalated** to blocking by any profile in this core — base `advisory`
⇒ effective `advisory` always.

**Rationale**: The worked example pins `light`: base `blocking` + `block-on-ship` + `gate` must still
**block** under `light` (acceptance scenario 2 keeps the same finding and only raises the mode to
`gate`/`release`). Therefore `light` cannot raise the floor above the maturity floor — its adjustment is 0,
i.e. `light` honours the maturity floor. The relaxation that the full design gives `light` (downgrading
specific finding **classes** like `unknownPaths`/`staleEvidence` to `warn`) is a **per-class dial** read from
`.fsgg/policy.yml`, which FR-015 **defers**. With only `advisory`/`blocking` and no finding-class in this
core, the only intrinsic, monotone lever a profile has is to **lower** the maturity floor (block earlier) for
stricter profiles. `strict` and `release` do exactly that. `light` and `standard` consequently coincide on
the block/advisory bit in this slice; they remain distinguishable in the **reason text** (which names the
active profile), and the per-class layer that separates them behaviorally arrives later. This is disclosed,
not hidden.

Base-advisory non-escalation: US3 scenario 3 explicitly allows either outcome ("whether it stays advisory or
is escalated by an explicit strictness rule") and only mandates that the reason **explains** it. The "explicit
strictness rule" that would escalate is precisely the deferred per-class dial (FR-015); with no such dial in
scope, this core keeps base-advisory advisory and the reason states that no strictness rule escalates it.
This also keeps the core trivially safe (FR-009: profiles never change base severity; effective stays ≤ base
in strictness).

**Alternatives considered**:
- *Give `light` a positive (relaxing) adjustment (e.g. +1)* — rejected: it would make `block-on-ship` advisory
  at `gate`, contradicting acceptance scenario 2 and the worked example.
- *Let `strict`/`release` escalate base-advisory to blocking* — rejected for this slice: the escalation rule
  is a per-class strictness dial (FR-015 deferred); modeling it now without the class dimension would invent
  semantics the design assigns to `policy.yml`.
- *Model the full per-class dial map (`unknownPaths`/`staleEvidence`/…)* — out of scope by FR-015; it needs
  the finding-class dimension and the `policy.yml` loader, which are later layers.

---

## D5 — Input/result shape (single finding, carry, no rollup)

**Decision**: The derivation is per-finding:
`deriveEffectiveSeverity : EnforcementInput -> EnforcementDecision`, where `EnforcementInput =
{ BaseSeverity; Maturity; Mode; Profile }` and `EnforcementDecision = { BaseSeverity; Maturity; Mode;
Profile; EffectiveSeverity; Reason }`. The input base severity is **echoed** into the output unchanged. The
function computes no rollup, verdict, exit code, or blockers list; "no finding dropped" (SC-006) is the
structural property that mapping the total function over a finding list is 1:1.

**Rationale**: FR-010 lists exactly the six fields the result must carry — base severity, run mode, profile,
maturity, effective severity, reason — and FR-013 forbids any cross-finding rollup. A per-finding total
function with an echoed base severity satisfies FR-009/SC-003 (base byte-identical) and FR-012/SC-006
(reclassification, never suppression) by construction. The finding's *verdict* and *body* are not inputs to
this core (they come from the rule engine and are carried by the caller), so "carries verdict and finding
through unchanged" is satisfied trivially — the core never receives or alters them; it only assigns and
explains effective severity.

**Alternatives considered**:
- *Carry a generic `'finding` through the decision* (`deriveFor : 'finding -> EnforcementInput ->
  Decision<'finding>`) — rejected as unnecessary generics (Principle III): identity preservation and
  no-drop are provable by mapping the plain function over a list; the caller pairs the decision with its own
  finding. Kept the surface minimal.
- *Return a ship/blockers rollup* — rejected: FR-013; that is the later `fsgg ship`/`audit.json` row.

---

## D6 — Reason text and string recognition

**Decision (reason)**: The reason is a deterministic, non-empty sentence built only from the typed inputs,
with one stable form per derivation branch:
- **withhold** (`observe`/`warn`): `"maturity '<m>' withholds blocking; no run mode or profile can make it block"`.
- **base-advisory**: `"base severity is advisory; '<profile>' profile does not escalate it (per-class strictness dials deferred)"`.
- **blocking** (base blocking, mode ≥ effective floor): `"run mode '<mode>' reaches the '<floor-mode>' blocking boundary for maturity '<m>' under '<profile>' profile"`.
- **relaxed** (base blocking, mode < effective floor): `"'<profile>' profile does not block this '<m>' finding outside the '<floor-mode>' boundary (run mode '<mode>')"`.

**Decision (recognition)**: `recognizeMode : string -> Recognized<RunMode>` and `recognizeProfile : string ->
Recognized<Profile>`, where `Recognized<'T> = Recognized of 'T | Unrecognized of string`. The canonical
strings are the lower-case design tokens (`sandbox`/…/`release`; `light`/`standard`/`strict`/`release`). Any
other string yields `Unrecognized` carrying the offending value — never an exception, never a default.

**Rationale**: FR-010 requires a non-empty reason naming the responsible levers; building it from typed
inputs guarantees determinism (FR-006/SC-004) with no clock/path/environment. FR-011 requires a total
recognition with an explicit unrecognized outcome carrying the offending string; a `Recognized<'T>` DU is the
plainest total encoding (Principle III) and never throws (Principle VI). Lower-case tokens match the design
tables and the `policy.yml`/CLI strings the later loader and `fsgg ship --mode …/--profile …` edge will feed
in.

**Alternatives considered**:
- *`Result<'T,string>` for recognition* — acceptable but `Recognized`/`Unrecognized` reads more honestly at
  the call site and avoids implying an error/exception channel; FsCheck tests assert exactly the canonical
  set maps and a representative invalid set is `Unrecognized`.
- *Free-form interpolated reason* — rejected: a fixed per-branch sentence shape keeps reason text
  byte-stable and testable (SC-004) and prevents accidental clock/path leakage.

---

## Enforcement truth table (derived from D3 + D4)

Effective severity for **base = blocking** (base = advisory ⇒ always advisory). Cell = effective severity;
floor = `maturityFloor − profileTighten`, clamped. `B` = blocking, `·` = advisory. Modes left→right:
`sandbox(0) inner(1) focused(2) verify(3) gate(4) release(5)`.

| Maturity \ Profile · Mode | sb | in | fo | ve | ga | re |
|---|---|---|---|---|---|---|
| observe / warn — any profile | · | · | · | · | · | · |
| block-on-pr · light/standard (floor 4) | · | · | · | · | **B** | **B** |
| block-on-pr · strict (floor 3) | · | · | · | **B** | **B** | **B** |
| block-on-pr · release (floor 2) | · | · | **B** | **B** | **B** | **B** |
| block-on-ship · light/standard (floor 4) | · | · | · | · | **B** | **B** |
| block-on-ship · strict (floor 3) | · | · | · | **B** | **B** | **B** |
| block-on-ship · release (floor 2) | · | · | **B** | **B** | **B** | **B** |
| block-on-release · light/standard (floor 5) | · | · | · | · | · | **B** |
| block-on-release · strict (floor 4) | · | · | · | · | **B** | **B** |
| block-on-release · release (floor 3) | · | · | · | **B** | **B** | **B** |

Worked example (SC-002): base `blocking`, `block-on-ship`, `inner`(1), `light` (floor 4) → `1 < 4` → **·
advisory**, reason names `light` and the `gate` boundary. ✓
