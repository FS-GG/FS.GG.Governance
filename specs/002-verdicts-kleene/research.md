# Phase 0 Research: Verdicts — Kleene Composition (F02 · `002-verdicts-kleene`)

All Technical Context unknowns are resolved below. The behavioural model is fixed by
[spec.md](./spec.md), `docs/governance-design/kernel.md` ("Verdicts"), and the
Hazard-2 reason mitigation in `docs/governance-design/theory-and-composition.md`; the
roadmap (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`, §F02)
fixes the public surface (`Verdict = Pass | Fail of string | Uncertain of string` +
Kleene `and`/`or`/`negate`). No `NEEDS CLARIFICATION` markers remain. The decisions
here are the *engineering* choices the spec deliberately left to planning.

## D1 — Where the verdict algebra lives

- **Decision**: Add `Verdict` to the **existing `FS.GG.Governance.Kernel` assembly**
  as a new `Verdict.fsi`/`Verdict.fs` pair in `src/FS.GG.Governance.Kernel/`,
  compiled **before** `Kernel.*` (it has zero dependency on the F01 fact/rule types).
  No new project.
- **Rationale**: The roadmap (§3) keeps the four `Check` interpreters and the verdict
  algebra *in the kernel* precisely so adapters and F03 reuse them with **zero new
  dependencies** — the algebra is pure and domain-neutral (FR-010), exactly the
  kernel's contract. A separate assembly would add a project and a reference for a
  three-case union and three functions, against Principle III and SC-005.
- **Alternatives considered**: a standalone `FS.GG.Governance.Verdict` project —
  rejected (premature split; the roadmap explicitly folds the algebra into the
  kernel, and F02 "may even fold into F03"). Defining it inside `Kernel.fs` next to
  `FixedPoint` — rejected; a dedicated `.fsi`/`.fs` pair keeps the curated surface
  per-module (Principle II) and signals the algebra's independence from derivation.

## D2 — Collection reductions as the primary surface (`all` / `any`)

- **Decision**: The combination surface is two **reductions over `Verdict list`** —
  `Verdict.all` (conjunction) and `Verdict.any` (disjunction) — plus unary
  `Verdict.negate`. No binary `&&`/`||`-style operators are exposed.
- **Rationale**:
  - The empty-list **identities** (FR-009: `all [] = Pass`, `any [] = Fail ""`) and
    **associativity** (FR-005, US2 AS3) are most naturally and totally expressed as a
    fold over a list with an identity, rather than as a binary op a caller must nest.
  - It matches how F03 consumes them: `Check.All cs`/`Any cs` carry **lists** of
    sub-checks, so `eval` maps straight onto `Verdict.all`/`Verdict.any` of a list —
    no adaptor, no re-association. (Design `rule-edsl.md` `eval` does exactly this.)
  - Custom operators would need Principle III justification for no real gain; a single
    list reduction is the plainest shape.
- **Alternatives considered**: binary `Verdict.and`/`Verdict.or` + `fold` — more
  surface, and `and`/`or` are awkward as F# identifiers; rejected. A general
  `combine : bool -> Verdict list -> Verdict` toggling conjunction/disjunction —
  rejected as a less legible surface than two named functions.

## D3 — Kleene "strong" truth tables (the outcome)

- **Decision**: Adopt the standard **Kleene strong** three-valued logic with
  `pass`=true, `fail`=false, `uncertain`=unknown, evaluated as:
  - `all`: any `Fail` ⇒ `Fail`; else any `Uncertain` ⇒ `Uncertain`; else `Pass`.
  - `any`: any `Pass` ⇒ `Pass`; else any `Uncertain` ⇒ `Uncertain`; else `Fail`.
  - `negate`: swaps the pass/fail *tag* — `negate Pass = Fail ""`, `negate (Fail _) =
    Pass` (the reason is **not** preserved across the flip), `Uncertain` unchanged. An
    involution on tags; a full involution (`negate (negate v) = v`) only for `Uncertain`
    and `""`-reasoned values (see data-model INV-7).
- **Rationale**: This is the **commutative and associative** truth table
  (`theory-and-composition.md` "confluent by construction"), so the *outcome* is
  order- and nesting-independent by construction (FR-005, SC-001) and `Uncertain` is
  only ever overridden by a genuinely dominating result, never silently coerced
  (FR-007, SC-002). It is the documented, deliberate semantics the spec pins
  (Assumptions); no alternative truth table is in scope.
- **Alternatives considered**: Łukasiewicz / Bochvar three-valued logics (differ on
  implication/`unknown` propagation) — rejected; the spec and design name Kleene
  strong explicitly, and only `negate` (not a connective `implies`) is in F02.

## D4 — Reason aggregation: reserved-separator component normalisation (the headline)

- **Decision**: A combined `Fail`/`Uncertain` reason is computed from the contributing
  reasons **of the dominating kind only** by:
  1. **splitting** each contributing reason on the reserved separator `"; "` into
     components (`StringSplitOptions.RemoveEmptyEntries`),
  2. **de-duplicating** the components (`List.distinct`),
  3. **ordinal-sorting** them (`String.CompareOrdinal`, culture-invariant),
  4. **re-joining** with `"; "`.
  `Pass` contributes no reason. The empty `any []` identity is `Fail ""` (no
  components → empty reason), absorbed by the same normalisation.
- **Rationale**: This is the Hazard-2 mitigation from `theory-and-composition.md`
  ("collect *every* failing reason … sorted … order-free"), strengthened to be fully
  **associative under re-nesting**, which a naïve "treat each sub-verdict's whole
  reason as one atom, sort, join" approach is **not**: e.g.
  `all [all [Fail "a"; Fail "z"]; Fail "m"]` would yield `"a; z; m"` but
  `all [Fail "a"; all [Fail "z"; Fail "m"]]` would yield `"a; m; z"` — different
  strings for the same multiset, violating FR-006 / US2 AS3. Splitting each
  contributing reason back into components before re-sorting makes the combined reason
  a function of the **set of reason components**, so it is byte-for-byte identical
  under reordering, re-nesting, **and** duplication (FR-006, SC-001, the "reason
  determinism under duplication" edge case). The `"; "` separator is therefore
  **reserved** in reason text: a caller whose individual reason embeds `"; "` has it
  treated as multiple components — a documented rendering rule that affects only the
  joined text, never the pass/fail/uncertain outcome, and never inspects reason
  *meaning* (FR-010 preserved; the kernel still treats reasons as opaque, normalising
  only on a structural separator).
- **Ordinal sort, specifically**: `String.CompareOrdinal` (not the current-culture
  comparer) so the ordering is identical on every machine and locale — required for
  the "100% byte-for-byte identical" guarantee (SC-001). A culture-sensitive sort
  could reorder reasons differently under, e.g., a Turkish locale.
- **Alternatives considered**:
  - *Whole-reason atoms* (no split) — simpler, commutative, but **fails associativity**
    of the reason under re-nesting (counterexample above) and so fails US2 AS3 /
    SC-001; rejected.
  - *Carry a reason `Set`/list in the `Verdict` type, render only at the edge* — would
    make composition trivially associative, but changes the public type away from the
    design's `Fail of string` / `Uncertain of string` and pushes rendering onto every
    consumer; rejected (out of scope, heavier surface).
  - *Keep only the first failing reason* — the original Hazard-2 defect (order-
    dependent message); rejected outright.

## D5 — Test framework & evidence (reuse F01's, test-project only)

- **Decision**: Reuse **Expecto + FsCheck** (already pinned centrally at F01, D5) in
  the existing test project; add one `VerdictTests.fs`. Commutativity, associativity,
  and `Uncertain`-preservation are natural **properties** (for any list / any
  permutation / any re-nesting, the result is identical) — FsCheck generates and
  shrinks. The existing reflective surface-drift test (V11) extends to the `Verdict`
  surface for free once the baseline is re-blessed; **no new drift test** is needed.
- **Rationale**: These are **test-project** dependencies only; the kernel assembly
  stays BCL+FSharp.Core (the existing V12 test re-confirms it — no new deps reach the
  kernel, SC-005). Real verdict values throughout (Principle V); no synthetic fixtures
  required, so no `// SYNTHETIC:` disclosures are expected in F02.
- **Alternatives considered**: hand-rolled shuffle loops instead of FsCheck —
  clumsier for the headline order-independence property the spec is built around
  (SC-001); rejected.

## Deferred / out of scope (confirmed, not unknowns)

- **`Check.Not` / `Implies` and the reified algebra** — F03. F02 ships only the
  verdict values and `negate`; the connective `implies` and commutative-node hashing
  (Hazard 3) are F03 concerns.
- **Producing a verdict from facts** (probes, outcomes, `Check.eval`) — F03. F02
  operates purely on already-determined verdict values (spec Assumptions).
- **Routing `Uncertain` → review request, `Fail` → block** — F07. F02 only guarantees
  `Uncertain` survives composition so routing *can* act on it later.
- **Runtime enforcement of non-empty reasons** — out of scope; operations are total
  over all inputs including empty/blank reasons by construction (FR-008, spec
  Assumptions).
- **Structured logging** (`TODO(STRUCTURED_LOGGING)`) — no I/O in F02; the verdict
  operations emit nothing. Choice still deferred to an ADR before F08.
