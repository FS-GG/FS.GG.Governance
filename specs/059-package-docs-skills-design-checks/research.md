# Phase 0 Research: Package / Docs / Skills / Design Deterministic Checks (F24)

**Branch**: `059-package-docs-skills-design-checks` | **Spec**: [spec.md](./spec.md) |
**Plan**: [plan.md](./plan.md)

This row has no open `NEEDS CLARIFICATION`: the spec fixed scope (the four deterministic domains, advisory
fence), and F23 fixed the inputs (catalog vocabulary, routing, classification, cost tiers, schema version —
all frozen here, FR-013). Research therefore resolves the *implementation-shape* decisions the spec deferred
to planning ("which existing modules are reused", "the exact project layout", "which checks are deterministic
vs advisory at the margins"). Each decision states what was chosen, why, and the alternatives rejected.

---

## D1 — Engine shape: leaf sensing pattern, not the Kernel Adapter SPI

**Decision.** Each domain check is a **leaf rule pack + host sensor** in the F046/F053/F054 lineage
(`FreshnessSensing` / `ReleaseRules` / `ReleaseFactsSensing`): a pure, total
`evaluate : SurfaceCheckRequest -> <Domain>Facts -> SurfaceFinding list` fed by an edge `Interpreter` that
reads the real source through an injected port. Findings flow into the existing F023
`Enforcement.deriveEffectiveSeverity`. The checks are **not** kernel `Adapter<'fact,'artifact,'change>`
values feeding the F04 `Kernel`.

**Rationale.** The spec names the precedent explicitly — "a pure, total fact-producing adapter (the
F014/F015/F017/F031 leaf precedent) fed by a host sensor" and the roadmap's "MVU: pure adapters plus Host
sensors." F014 (`Config`), F015 (`Routing`), F017 (`Findings`) are all pure leaves that consume typed input
and produce typed findings with no kernel coupling; F046/F053/F054 add the sensor half. This pattern keeps
the F04 `Kernel` and its `Bridge`/`CheckRule` SPI entirely out of the route/verify host, matches every
governance command shipped F053–F057, and lets each domain be tested as a pure function plus a thin real-I/O
edge.

**Alternatives rejected.**
- *Extend the kernel `Adapters.DesignSystem` (F11) / build new `Adapter<…>` packs for each domain.* The
  kernel adapter SPI (`Adapter`, `CheckRule<'fact>`, `Bridge`, `Fence`) is the F09–F11 capability-domain
  vocabulary that feeds the `Kernel`'s fact/rule evaluation. Routing a single change through the kernel just
  to produce a docs-link finding drags the kernel (and its agent-review/judge machinery) into the verify
  host — heavier, and contrary to "pure adapters plus Host sensors." It also couples four small deterministic
  checks to the kernel's evolution. Rejected.
- *A single monolithic `SurfaceChecks` engine with an internal `match` over domain.* Fails FR-008's
  independence/composability requirement (each domain must be runnable and testable in isolation, with no
  pack depending on another) and makes the P1→P2→P3 incremental delivery awkward. Rejected in favor of one
  library per domain (D2).

**Relationship to the existing `Adapters.DesignSystem`.** It stays exactly as-is and is *not* touched by this
row. It is the kernel-side design vocabulary (token-drift/contrast `CheckRule`s with facts supplied
abstractly). F24's `DesignChecks` is the *host-side deterministic check* that senses the real catalog and
emits `SurfaceFinding`s — the same division of labor as kernel release facts vs. the leaf `ReleaseRules`.
Documented so a future row can reconcile the two if desired.

---

## D2 — Project layout: one library per domain (Model + pure pack + Interpreter), plus a shared core

**Decision.** Five new `src` libraries: a shared `FS.GG.Governance.SurfaceChecks` core (pure: the
`SurfaceFinding`/`SurfaceCheckRequest` vocabulary + the `Composition` dispatcher) and four domain libraries
(`PackageChecks`, `DocsChecks`, `SkillChecks`, `DesignChecks`). Each domain library bundles three modules in
one project, the `ReleaseFactsSensing` shape: `Model.fsi/fs` (closed domain facts), `<Domain>.fsi/fs` (pure
`evaluate`), `Interpreter.fsi/fs` (impure sensor with an injected port). Each domain references only
`SurfaceChecks` + `Config` (+ `GateExecution` for `PackageChecks`); **no domain references another domain**.

**Rationale.** Bundling pure derive + impure sensor in one project with separate modules is the exact F054
precedent (`Model` / `Sensing` / `Interpreter`), and keeps the project count to one-per-concern as the repo
already does (50+ leaf projects). One library per domain gives FR-008 independence and the spec's P1/P2/P3
incremental delivery for free: `PackageChecks` (P1) can land, ship, and be tested with no other domain
present. The shared core carries the only vocabulary all four must agree on (the finding shape and the
dispatcher), so a cross-domain change is one file, not four.

**Alternatives rejected.**
- *Separate `…Sensing` projects per domain (8 libraries).* Over-fragments; F054 already proves the bundled
  shape is clean because the pure module references nothing the sensor needs. Rejected.
- *Put `Composition` in each host command.* Would duplicate the dispatcher across `verify`/`route`/`ship` and
  re-introduce a cross-domain coupling at the wrong layer. Keeping it pure in the shared core lets every host
  call one function. Rejected.

---

## D3 — Reuse F23 / F017 / F023 unchanged; add no schema, field, diagnostic, or truth-table change

**Decision.** This row reads F23's already-produced `ProductSurfaceReport` and the surface's already-declared
`EvidenceTag`; it adds **no** `SurfaceClass` case, **no** `capabilities.yml` field, **no**
`capabilities.yml` schema version, **no** `DiagnosticId`, and **no** change to the F023 enforcement truth
table. A `SurfaceFinding` carries an F023 `Severity` (`Advisory | Blocking`) and a `Maturity`, and is rolled
up through `deriveEffectiveSeverity` exactly as `ReleaseRules` rolls up `ReleaseFinding`.

**Rationale.** FR-013 and FR-014 are explicit and F23 froze the catalog. The `TierIsDeclared = false` flag
F23 produced is precisely the "declared tag, no check" signal; this row's job is to *back* it, not redefine
it. Reusing `Severity`/`Maturity`/`deriveEffectiveSeverity` means the advisory guarantee (FR-011) and the
blocking semantics (FR-014) come for free and stay consistent with every other finding source.

**Alternatives rejected.** *A new `SurfaceFindingSeverity` DU.* Would fork the enforcement vocabulary and
force a truth-table change. Reuse the F023 `Severity` verbatim. Rejected.

---

## D4 — Where the finding's location and evidence tag come from

**Decision.** `SurfaceCheckRequest` is derived from **one** F23 `ProductClassification`: it carries the
`Surface` id, the routed `Path`, the `SurfaceClass` (which selects the domain pack), and the surface's
`EvidenceTag option` (looked up from `TypedFacts.Capabilities.Surfaces` by `SurfaceId`). Each
`SurfaceFinding` records an exact `Location` (file + the precise locus: changed member, transcript example
id, docs link text + target, skill path, catalog entry id) and the bound `EvidenceTag` so produced evidence
ties back to it (FR-009).

**Rationale.** The classification already resolved *which* surface and *which* path; the check only needs to
look up that surface's `EvidenceTag` and run. Keeping the request derived from a single classification (not
the whole report) is what makes per-domain `evaluate` pure and composable — the dispatcher fans the report
out into one request per applicable classification.

**Alternatives rejected.** *Pass the whole `ProductSurfaceReport` into each `evaluate`.* Couples a domain
pack to the report shape and to other domains' classifications; the dispatcher should own the fan-out, not
the pack. Rejected.

---

## D5 — Determinism: stable ordering, normalized paths, no wall-clock (per domain)

**Decision.** Every `evaluate` sorts its output by a composite key (`Surface` id token, then a
domain-specific locus ordinal: changed-member name, transcript ordinal, link locus, catalog-entry id),
normalizes every path through the existing `Config.Model.normalizePath` (forward-slash, repo-relative), and
takes no timestamp/username/environment input. Sensors normalize paths at the edge before handing facts to
the pure pack, and the package `.fsi`-baseline comparison is a normalized token diff (not a raw text diff) so
formatting/whitespace/order noise never reports as drift.

**Rationale.** FR-010/SC-005 require byte-identical findings and evidence. The repo's byte-identical
discipline (F042–F058) is achieved by exactly this recipe; the `.fsi`-baseline normalization additionally
prevents false drift, which is the highest-stakes domain (SC-001 demands zero false positives on an unchanged
surface).

**Alternatives rejected.** *Raw text diff of `.fsi` files.* Whitespace/reordering would fire spurious drift,
violating SC-001's "unchanged surface reports zero drift." Use a normalized public-token comparison. Rejected.

---

## D6 — Advisory fence: base severity + reuse `deriveEffectiveSeverity`, do not invoke `AdvisoryPromotion`

**Decision.** A deterministic check sets `BaseSeverity = Blocking` (capable of blocking, subject to maturity/
mode/profile via the truth table); a judgement-heavy check sets `BaseSeverity = Advisory`. F024 **does not
call** `AdvisoryPromotion.decide` — advisory findings stay advisory in this row. `deriveEffectiveSeverity`
already guarantees a base-Advisory finding is never escalated to Blocking under any mode/profile, so FR-011/
SC-006 hold with no new code.

**Rationale.** The spec is explicit that judgement-heavy checks "inform but never block … until a later row
promotes them." `Enforcement` already encodes "base-advisory stays advisory"; `AdvisoryPromotion` is the
*future* promotion path (it needs deterministic backing evidence / repeated review / sign-off, none of which
this row produces for judgement checks). Not calling it keeps the safety property trivially true and testable
across the whole enforcement matrix.

**Which checks are advisory at the margins.** Deterministic (Blocking-capable): `.fsi` baseline drift; FSI
transcript compile/evaluate; docs link currency; docs reference/anchor currency; skill path contract; skill
task-list consistency; skill mirror sync; design token/capture/control resolution; design contrast-threshold.
Advisory only: docs example "reasonableness"/prose quality beyond compile-and-evaluate; design *intent* (does
the rendered surface match the design intent) — anything whose verdict requires human/agent judgement
(spec edge "judgement check mislabelled deterministic"). A docs *example freshness* check is deterministic
when it reduces to compile/evaluate (handled by the package transcript machinery) and advisory when it is a
prose/intent judgement.

**Alternatives rejected.** *Invoke `AdvisoryPromotion` now.* Premature — there is no promotion basis to feed
it this row, and it would blur the safety guarantee. Rejected.

---

## D7 — Input vs. tool-defect diagnostics; first-run baseline; standalone

**Decision.** Each sensor port returns `Result<…, string>` per source; the sensor catches every exception and
maps it to a structured *input* diagnostic naming the source (the F054 `gather` precedent), distinct from a
tool defect. Specific states:
- **Absent `.fsi` baseline (FR-002):** the package sensor *generates* the baseline (first-run) and the pack
  emits a clear, fixable "baseline generated, commit it" finding — never a tool defect, never a silent pass.
- **Unlocatable transcript / unreadable docs source / absent design catalog (FR-012):** an input diagnostic
  naming the source; the pack emits an input-state finding, never a fabricated pass.
- **Subset of domains declared (FR-015):** a domain with no declared/routed surface produces no
  `SurfaceCheckRequest`, so its sensor never runs and it emits nothing.

Standalone (FR-016): every sensor reads only the product's own declared sources under the `.fsgg` parent
(the F014 loader root), never a monorepo-only path; a monorepo-only declared path is the existing
`PathEscapesRoot`/`OutOfScope` state, not a new error.

**Rationale.** Constitution VI and FR-012 require input-vs-defect separation with no swallowed errors; F054
already demonstrates the `Result`-per-source + total `gather` shape. First-run baseline generation matches
the spec's acceptance scenario 1.2 and edge "first-run baseline absent."

**Alternatives rejected.** *Throw on a missing source.* Violates safe-failure and would crash the host on a
product that simply hasn't generated a baseline yet. Rejected.

---

## D8 — Host surfacing: additive `surfaceChecks` in `fsgg verify`, byte-identical when empty

**Decision.** The pre-PR host `fsgg verify` (`VerifyCommand`) gains an edge step: after F23 classification, it
runs the applicable domain sensors + packs via `Composition.run`, folds the resulting `SurfaceFinding`s into
its `Model` and rendered summary, and `VerifyJson` emits an additive `surfaceChecks` array **only when
non-empty** through a new overload (e.g. `ofVerifyResultWithSurfaceChecks`). Empty ⇒ byte-identical output,
so every existing `verify.json` golden is untouched and the schema version is unchanged. Enforcement, exit
codes, and the truth table are unchanged; surface findings roll up through the same
`deriveEffectiveSeverity` the command already uses.

**Rationale.** This is the exact additive precedent F23/F052 used for `productSurfaces` in `route.json` and
the reason the spec can claim "executable deterministic governance coverage" (F24 exit) without re-opening
enforcement. `fsgg verify` is the pre-PR host where these checks belong (it already senses scope, loads the
catalog, classifies, and rolls up at `RunMode.Verify`). Choosing `verify` over `route` matches the
roadmap's placement (running checks + producing evidence, not just explaining routing).

**Alternatives rejected.**
- *Bump `verify.json` schema version / change the existing `ofVerifyResult` signature.* Would break existing
  goldens and the wire contract for no benefit; the additive overload keeps both intact. Rejected (the F052
  lesson, reconciled in the F23 plan's "as-built" note).
- *Surface through `fsgg route` instead.* `route` explains classification; it does not run gates or produce
  evidence. The checks produce cost-tiered evidence and belong in the verify/ship execution host. Rejected.
- *Add a brand-new `fsgg check` command.* Unnecessary new surface; the pre-PR host already exists and the
  checks are part of verification. Rejected (could be revisited if a standalone check entry is later wanted).

---

## Resolved unknowns summary

| Spec deferral | Resolution |
|---|---|
| "which existing modules are reused" | `Config`, `ProductSurfaces`, `Enforcement`, `EvidenceCapture`/`EvidenceReuse`, `GateExecution`, `VerifyCommand`/`VerifyJson` — all unchanged in contract except the additive `verify.json` section (D1, D3, D8) |
| "exact project layout" | 1 shared core + 4 domain libraries (Model + pure pack + Interpreter each) + extended `VerifyCommand`/`VerifyJson` (D2) |
| "deterministic vs advisory at the margins" | Deterministic: baseline drift, transcript, link/reference currency, skill path/task/mirror, design token/capture/contrast/control. Advisory: prose quality, design intent, example "reasonableness" beyond compile/evaluate (D6) |
| transcript execution mechanism | shell FSI through the existing `GateExecution.ExecutionPort`; no `FSharp.Compiler.Service` dependency (Technical Context, D1) |
| first-run baseline / missing source | first-run generation + structured input diagnostics, never a fabricated pass (D7) |
| host wiring | additive `surfaceChecks` in `fsgg verify`, byte-identical when empty (D8) |
