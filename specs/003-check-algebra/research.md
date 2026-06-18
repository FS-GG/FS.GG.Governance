# Phase 0 Research: Check — The Reified Rule Algebra (F03 · `003-check-algebra`)

All Technical Context unknowns are resolved below. The behavioural model is fixed by
[spec.md](./spec.md), `docs/governance-design/rule-edsl.md` (the authoritative `Check`
shape and the four-interpreter sketch), and the Hazard-3 commutative-hash mitigation in
`docs/governance-design/theory-and-composition.md`; the roadmap
(`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`, §F03) fixes the
public surface. No `NEEDS CLARIFICATION` markers remain. The decisions here are the
*engineering* choices the spec deliberately left to planning (spec Assumptions: "the
exact textual format of `render` and the exact encoding of the `hash` are
implementation/design details fixed in the plan and `.fsi`").

## D1 — Where the Check algebra lives, and compile order

- **Decision**: Add `Check` to the **existing `FS.GG.Governance.Kernel` assembly** as a
  new `Check.fsi`/`Check.fs` pair in `src/FS.GG.Governance.Kernel/`, compiled **after**
  both `Verdict.*` (F02) and `Kernel.*` (F01). No new project.
- **Rationale**: `Check` genuinely depends on both predecessors — `eval`/`explain` fold
  to a `Verdict` (F02) and a `Probe`'s `Eval : FactSet<'fact> -> Outcome` consumes a
  `Kernel.FactSet<'fact>` (F01) — so it must compile last of the three. The roadmap (§3)
  keeps the algebra and all interpreters *in the kernel* precisely so adapters (F09–F11)
  and the F04/F06 features reuse them with **zero new dependencies**; the algebra is pure
  and domain-neutral (FR-015), exactly the kernel's contract. A separate assembly would
  add a project and reference for a value algebra against Principle III and SC-008.
- **Alternatives considered**: a standalone `FS.GG.Governance.Check` project — rejected
  (premature split; roadmap folds it into the kernel). Defining `Check` inside `Kernel.fs`
  — rejected; a dedicated `.fsi`/`.fs` pair keeps the curated surface per-module
  (Principle II) and signals the algebra's independence from the F01 fixed-point engine.

## D2 — The closed combinator union and `Outcome` mapping

- **Decision**: Adopt the design's exact shape (`rule-edsl.md`): `ArtifactRef =
  { Kind: string; Key: string }`; `Outcome = Met | Unmet of string | Unknown of string`;
  `ProbeArg = ArtifactArg of ArtifactRef | LiteralArg of string | NumberArg of double`;
  `Probe<'fact> = { Name; Reads; Args; Eval }`; and the **closed** union
  `Check<'fact> = Atom of Probe<'fact> | All of Check<'fact> list | Any of Check<'fact>
  list | Not of Check<'fact> | Implies of Check<'fact> * Check<'fact> | Opaque of name *
  (FactSet<'fact> -> Outcome)`. `Outcome` maps **one-to-one** onto the F02 `Verdict`:
  `Met → Pass`, `Unmet r → Fail r`, `Unknown r → Uncertain r` (FR-002).
- **Rationale**: The union is the keystone reification — making the check a *value* is
  what lets the six interpreters fold one source (US1/US2 "cannot drift apart"). The
  single type parameter `'fact` plus structural `ArtifactRef`s is what keeps the algebra
  and interpreters domain-vocabulary-free in the kernel (FR-015): an adapter maps its own
  closed union onto `ArtifactRef` and supplies probes; everything else is reused. The
  union is **closed by deliberate design** (the closed-coproduct trade-off recorded in
  the theory notes, accepted in spec Assumptions — third parties add probes, not
  combinators), which is what makes a single, total, exhaustively-reviewed fold possible.
- **Alternatives considered**: an open/extensible combinator set (Data Types à la Carte
  style) — rejected by the design (the closed set is reviewed in one place and folds
  totally); using `Unknown` vs spelling it `Undecided` — kept `Unknown` to match
  `rule-edsl.md` and minimise drift; the *verdict* it maps to is `Uncertain` (F02), and
  the spec's prose "undecided" is that same state.

## D3 — `eval`: reuse F02, desugar `Implies`, preserve `Uncertain`

- **Decision**: `eval` is a `let rec` fold that maps each node onto the **F02 verdict
  combinators**: `Atom p` → `outcomeToVerdict (p.Eval facts)`; `All cs` →
  `Verdict.all (List.map (eval facts) cs)`; `Any cs` → `Verdict.any (...)`; `Not c` →
  `Verdict.negate (eval facts c)`; `Implies (a, b)` → `eval facts (Any [Not a; b])`
  (desugared); `Opaque (_, f)` → `outcomeToVerdict (f facts)`.
- **Rationale**: Reusing `Verdict.all`/`any`/`negate` means the Kleene three-valued
  semantics, the empty-list identities (`All [] = Pass`, `Any [] = Fail ""`), order- and
  nesting-independence, and the deterministic reason aggregation are **inherited, not
  re-implemented** (FR-006, FR-014, SC-003) — F03 adds no new truth-table code and cannot
  diverge from F02. Desugaring `Implies (a,b)` to `Any [Not a; b]` makes "a implies b"
  behave as "either (not a) or b" exactly (FR-006, spec US1 AS5 / edge "Implication is
  desugared, not primitive"); because `negate` and `any` preserve `Uncertain`, an
  undecided antecedent or consequent yields the correct undecided implication, never a
  silent pass/fail (SC-003).
- **Alternatives considered**: a bespoke `Implies` truth table — rejected; the desugaring
  is provably equal and reuses tested F02 code. Re-deriving Kleene composition inside
  `eval` — rejected (duplicate semantics, drift risk; F02 exists precisely so F03 reuses
  it, roadmap §F02).

## D4 — `hash`: canonicalize commutative nodes, stay positional elsewhere (Hazard 3)

- **Decision**: `hash : Check<'fact> -> string` is a `let rec` fold producing a
  lowercase hex **SHA-256** digest, computed *without executing any `Eval`*:
  - `Atom p` → hash of the tag `"atom"` + `p.Name` + the **ordered** `p.Args` + the
    **ordered** `p.Reads` (positional — arguments and reads are meaningful in order).
  - `All cs` / `Any cs` → hash of the tag + the child sub-hashes **ordinal-sorted**
    (`String.CompareOrdinal`) before combining → permutation-invariant (Hazard 3).
  - `Not c` → hash of `"not"` + `hash c`.
  - `Implies (a, b)` → hash of `"implies"` + `hash a` + `hash b`, **positional**
    (`a ==> b ≠ b ==> a`).
  - `Opaque (n, _)` → hash of `"opaque"` + `n` **only** (the un-inspectable `Eval` is
    never part of the key).
  Components are combined **prefix-free**: each leaf component (name, arg, read) is itself
  hashed to a fixed-width hex string first, and a node's hash is `SHA256(tag ‖ child-hex
  ‖ …)` over UTF-8 bytes. Because every combined piece is fixed-width hex, there is no
  delimiter-injection ambiguity (two children "ab","c" can never collide with one child
  "abc").
- **Rationale**: This is the **Hazard-3 mitigation** from `theory-and-composition.md`
  ("canonicalize the commutative nodes by sorting, but keep positional hashing where
  order is meaningful") — the exact decision #4 the roadmap assigns to F03. Ordinal sort
  (culture-invariant, identical to F02's reason aggregation) gives the "100% byte-for-byte
  identical across machines/locales" guarantee (SC-002): a conjunction and any permutation
  of its members hash identically, so the agent-review cache (F04) does not miss spuriously
  and re-consult the agent (US2 "why this priority"). Keeping `Implies`/`Args`/`Reads`
  positional means meaningful reordering *does* change the key (SC-002). Hashing `Opaque`
  by name only (FR-008) keeps it cacheable despite its opacity. SHA-256 lives in
  `System.Security.Cryptography` (a `System.*` assembly), so it adds **zero package
  dependencies** and passes the V12 hygiene test (SC-008). Duplicated members in a
  commutative node hash deterministically as a consequence of canonicalization (edge
  "Duplicate sub-checks").
- **Alternatives considered**:
  - *Return the canonical structural string itself (no digest)* — simpler and
    human-debuggable, zero crypto, also collision-free; rejected as the **primary** form
    because the key becomes unbounded in length and F04 combines it with artifact hashes
    into a fixed cache key, where a compact fixed-width digest is the better fit. The
    canonical-string is retained *internally* as the pre-image SHA-256 hashes.
  - *Sort by the children's `render` text* instead of their sub-hashes — rejected; render
    is a human string that could collide or shift, whereas sub-hashes are stable and
    already computed by the fold.
  - *A non-cryptographic hash (`GetHashCode`, FNV)* — rejected; `GetHashCode` is not
    stable across runs/processes (defeats a persisted cache key), and a hand-rolled FNV
    buys nothing over BCL SHA-256, which is already a `System.*` dependency.

## D5 — `render` and the inspectable-without-execution guarantee

- **Decision**: `render : Check<'fact> -> string` is a `let rec` fold over **structure
  only** — `Atom p` → `p.Name` followed by its declared `Args` in parentheses when any
  (e.g. `contrastRatio(text, 4.5)`); `All cs` → `all of [r₁; r₂; …]`; `Any cs` →
  `any of [r₁; r₂; …]`; `Not c` → `not (r)`; `Implies (a,b)` → `(rₐ) implies (r_b)`;
  `Opaque (n,_)` → `opaque "n"`. It **never invokes** `p.Eval`, the `Opaque` function, or
  requires a `FactSet`. `render` preserves authoring order (it is the faithful sentence a
  human wrote); only `hash` canonicalizes commutative order.
- **Rationale**: Render-without-execution is the property the whole design rests on
  (FR-007, SC-001): the published contract is a *fold of the rules* (F06) that cannot
  drift from what is enforced because it *is* the enforced check, rendered. The spec fixes
  only that render is deterministic and execution-free; the exact wording is an engineering
  detail (spec Assumptions), so a plain readable pretty-print is chosen — no order
  canonicalization (unlike hash), because a contract sentence should read as the author
  wrote it, and determinism (same value → same string) is all FR-007 requires.
- **Alternatives considered**: canonicalizing commutative order in `render` too — rejected
  as unnecessary (FR-007 needs determinism, not order-independence) and as making the
  contract sentence diverge from the author's wording; an interface/`ToString` override —
  rejected (the curated interpreter is the public surface, Principle II).

## D6 — `explain`: a non-generic proof tree mirroring structure, verdict = `eval`

- **Decision**: A new **non-generic** public type `Explanation`, a proof tree that
  mirrors the check shape and carries, at each node, the rolled-up `Verdict`:
  `AtomExplained of name * Outcome * Verdict` (records the probe's met/unmet/unknown
  outcome), `AllExplained`/`AnyExplained of Explanation list * Verdict`, `NotExplained of
  Explanation * Verdict`, `ImpliesExplained of Explanation * Explanation * Verdict`,
  `OpaqueExplained of name * Outcome * Verdict`. `explain facts check` builds it so that
  the **top node's verdict is identical to `eval facts check`**, computed with the same
  F02 combinators; a small `Explanation.verdict : Explanation -> Verdict` accessor reads
  it back for the cross-fold test.
- **Rationale**: The two interpreters fold the same value and use the same F02
  combinators, so they **cannot disagree** (FR-009, SC-004) — the test asserts equality
  for any check and any facts. The tree mirrors the **surface** structure the author wrote
  (FR-009, US3 AS2): `ImpliesExplained` holds the explanations of `a` and `b` (each
  evaluated normally) while its node verdict applies the desugaring
  (`Verdict.any [Verdict.negate (verdict aExpl); verdict bExpl]`), so the proof tree reads
  as "a implies b" yet rolls up to the same verdict `eval` produces. `Explanation` is
  **non-generic** (it records names/outcomes/verdicts, never `'fact`), which keeps its F06
  JSON serialization trivial. Only the structured value ships here; its wire format and
  freshness predicates are F06 (spec Assumptions).
- **Alternatives considered**: mirroring the *desugared* `Implies` (as `AnyExplained
  [NotExplained …; …]`) — rejected; it would not mirror the author's structure (US3 AS2).
  A generic `Explanation<'fact>` — rejected; nothing in the proof tree needs `'fact`, and
  non-generic is simpler to serialize (F06). Recomputing the verdict in the cross-fold
  test by re-walking the tree — unnecessary; storing the verdict at each node makes the
  equality direct and makes the tree self-describing for F06.

## D7 — `reads` / `isReified`, smart constructors, and the test approach

- **Decision**:
  - `reads : Check<'fact> -> ArtifactRef list` collects the `Reads` of every `Atom`
    (an `Opaque` contributes none — it declares no inspectable structure), as the
    artifact half of the F04 cache key and the routing input set (FR-010). Order/dedup:
    collect in a left-to-right structural walk; de-duplication policy is deferred to the
    F04 cache-key construction (this interpreter returns the declared multiset faithfully).
  - `isReified : Check<'fact> -> bool` is `true` iff **no** `Opaque` node appears anywhere
    (FR-011); F04 uses it to refuse `Deterministic` tier for an opaque check.
  - Smart constructors/operators: `probe name reads args eval` (= `Atom { … }`),
    `allOf = All`, `anyOf = Any`, `not' = Not`, `(==>) a b = Implies (a, b)`,
    `(.&) a b = All [a; b]`, `(.|) a b = Any [a; b]` (FR-005).
  - Tests: reuse **Expecto + FsCheck** (F01 D5), one new `CheckTests.fs`. Headline
    properties: hash permutation-invariance for `All`/`Any` and positionality for
    `Implies`/args; `eval` agreement with the F02 Kleene tables; the cross-fold invariant
    `Explanation.verdict (explain f c) = eval f c`; `isReified` false iff an `Opaque` is
    present. The **never-executes** guarantee (SC-001) is proved with a real probe whose
    `Eval = fun _ -> failwith "executed"` — `render`/`hash`/`reads`/`isReified` succeed,
    `eval` throws — real evidence, no mock.
  - Compile order in `.fsproj`: `Check.fsi`/`Check.fs` **after** `Kernel.*`. The reflective
    V11 surface-drift test extends to the `Check` surface once re-blessed; V12 re-confirms
    the kernel still references only BCL + FSharp.Core after `Check.*` (SHA-256 is
    `System.*`, allowed) — **no new drift/hygiene test needed**.
- **Rationale**: These are thin structural folds other features depend on but that carry
  no logic of their own (spec US4 "why this priority"); shipping all six interpreters as
  one coherent algebra lets F04 build the tier bridge against a stable contract. The
  custom operators are the readable surface that is the eDSL's whole point (Principle III
  justification in the plan). FsCheck is the natural tool for the order-independence and
  cross-fold *properties* the spec is built around (SC-002/SC-004); these are
  **test-project** dependencies only, so the kernel stays BCL+FSharp.Core (SC-008).
- **Alternatives considered**: `reads` returning a de-duplicated `Set` — rejected here;
  dedup belongs with the F04 cache-key policy, and returning the declared multiset keeps
  this interpreter a faithful structural fold. Hand-rolled shuffle loops instead of
  FsCheck for the order-independence properties — clumsier for the headline guarantees;
  rejected (mirrors F02 D5).

## Deferred / out of scope (confirmed, not unknowns)

- **`CheckTier`, `Severity`, the `Rule` record, `toRule`, the agent-review cache key** —
  **F04**. F03 ships only the `Check` algebra and its six interpreters; F04 builds the
  tier bridge on `isReified`/`hash`/`reads` and locks the full cache-key composition
  (decision #1: `+ judge model id + version + prompt hash`).
- **`Explanation` JSON serialization, the `contract` fold, evidence-freshness** — **F06**.
  F03 produces the in-memory `Explanation` value and the `render` string; F06 serializes
  them and folds the published contract (spec Assumptions).
- **The actual agent call / review effects edge** — **F08** (MVU boundary). F03 is pure
  and applicative (FR-012); no `Cmd`/`Effect`/`update` here.
- **Structured logging** (`TODO(STRUCTURED_LOGGING)`) — no I/O in F03; the interpreters
  emit nothing. Choice still deferred to an ADR before F08.
