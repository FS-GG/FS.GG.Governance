# Phase 1 Data Model: `fsgg verify` Surface-Checks Host Wiring

This feature adds **no new pure type** — every entity below already exists in a reused project. The only new
declarations are host-local MVU cases/fields in `FS.GG.Governance.VerifyCommand`. All shapes are reused
verbatim; this document records how they thread through the verify host.

## 1. Reused entities (no change)

- **`ProductSurfaces.Model.ProductSurfaceReport`** — the classification of declared/routed surfaces (surface
  id, `SurfaceClass`, cost tier). Produced by `ProductSurfaces.classify`. Already held by the route host.
- **`Config.Model.TypedFacts`** — the loaded capability facts, including `Capabilities.Surfaces` (declared
  surface ids + optional evidence tags). Already loaded by the verify host for its existing evaluation.
- **`SurfaceChecks.Dispatch.Composition.DomainFactBundle`** — four maps `SurfaceId → XFacts` for
  package/docs/skill/design. `emptyBundle` is the starting point; the interpreter fills only the domains whose
  surfaces were declared.
- **Per-domain facts** — `PackageChecks.Model.PackageFacts`, `DocsChecks.Model.DocsFacts`,
  `SkillChecks.Model.SkillFacts`, `DesignChecks.Model.DesignFacts`. Produced by each domain's
  `Interpreter.senseX port request`, where `port` is the domain `realPort` (package: `realPort repo exec`,
  wrapped **read-only** at verify; docs/skill: `realPort repo`; design: `realPort repo catalogLayout`) — see §3.
- **`SurfaceChecks.Model.SurfaceFinding`** — one check result: domain, surface id, code, file, detail, base
  severity, input state, optional evidence tag, message. Aggregated and deterministically sorted by
  `Composition.run`.
- **`SurfaceChecks.Model.enforcementInputOf : SurfaceFinding -> RunMode -> Profile -> EnforcementInput`** — the
  bridge into the existing enforcement path. Verdict comes from the existing `deriveEffectiveSeverity`.

## 2. New host-local MVU declarations (`VerifyCommand/Loop.fsi` + `Loop.fs`)

### 2.1 `Effect` — one new case, backed by a new `Ports` field

```fsharp
| SenseSurfaces of scope: RepoSnapshot      // emitted by `update` after the verify scope is sensed
```

- The effect carries only the **scope** to classify. The actual sensing is a **port on the verify `Ports`
  record** — `SenseSurfaces: ProductSurfaceReport -> SurfaceChecks.Model.SurfaceFinding list` — wired in
  `realPorts (repo: string)` (mirroring the existing `SenseRelease`/`Execute` ports), so the **repo root** and
  the F051/F052 `ExecutionPort` are captured at port-construction time and tests inject a synthetic port
  (research D7, resolves analyze U1). The interpreter does **not** receive the repo root on the effect.
- Inside the real `SenseSurfaces` port the **package** domain is constructed **read-only** (no baseline write,
  no transcript execution — FR-012); docs/skill/design use their read-only `realPort`s unchanged.

### 2.2 `Msg` — one new case

```fsharp
| SurfacesSensed of findings: SurfaceChecks.Model.SurfaceFinding list
```

- The deterministic, already-sorted `SurfaceFinding list` returned by `Composition.run`. `update` folds it into
  the model (and thereby into the rollup); it never re-sorts or re-runs.

### 2.3 `Model` — new fields

```fsharp
SurfaceFindings: SurfaceChecks.Model.SurfaceFinding list   // [] until SurfacesSensed; default []
SurfacesPending: bool                                      // true once SenseSurfaces is emitted, false on SurfacesSensed
```

- `SurfaceFindings` defaults to `[]` in `init`, so a host path that never declares surfaces projects
  byte-identically (FR-004).
- `SurfacesPending` joins the existing readiness gate so `verify.json` is projected only after surfaces (like
  the release preview) have arrived.

## 3. Control flow (the MVU transitions)

```
init request
  └─ DefaultRange ⇒ [ SenseProvenance; SenseScope request.Scope ]          (existing)

update (Sensed (Ok snap))
  └─ store snapshot; emit [ SenseSurfaces snap; SenseFreshness …; … ]      (NEW: add SenseSurfaces)

interpreter (SenseSurfaces snap) ⇒ calls ports.SenseSurfaces report          (NEW — edge, Constitution IV)
  // ports.SenseSurfaces is wired in `realPorts repo`, closing over `repo` + `ports.Execute`:
  ├─ facts   = load TypedFacts (capability config)         // existing loader
  ├─ report  = ProductSurfaces.classify (scope ∩ declared surfaces) facts  // REUSE
  ├─ pkgPort = { PackageChecks.Interpreter.realPort repo ports.Execute with   // READ-ONLY at verify (FR-012):
  │               WriteBaseline  = fun _ _ -> Ok ()        //   absent baseline REPORTED, never written
  │               ListTranscripts = fun _ -> Ok [] }       //   no transcript ⇒ no FSI spawned
  ├─ bundle  = emptyBundle |> fill per declared domain:                     // REUSE sensors
  │              Package ← PackageChecks.Interpreter.sensePackage pkgPort req
  │              Docs    ← DocsChecks.Interpreter.senseDocs   (realPort repo) req
  │              Skill   ← SkillChecks.Interpreter.senseSkill (realPort repo) req
  │              Design  ← DesignChecks.Interpreter.senseDesign (realPort repo catalogLayout) req
  ├─ findings = Composition.run facts report bundle                         // REUSE (pure, sorted)
  └─ SurfacesSensed findings                               // ← interpreter dispatches this Msg

update (SurfacesSensed findings)                                            (NEW — pure fold)
  ├─ model' = { model with SurfaceFindings = findings; SurfacesPending = false }
  └─ when all senses ready ⇒ project (see §4)

projection (the existing join point)                                        (EDIT — [] → findings)
  ├─ inputs = model.SurfaceFindings |> List.map (fun f ->
  │              enforcementInputOf f RunMode.Verify model.Profile)         // REUSE bridge
  ├─ decision = Ship.rollup … (gate outcomes ⊕ surface enforcement inputs) at RunMode.Verify   // REUSE
  └─ verifyDoc = VerifyJson.ofVerifyDecisionWithPreview decision cache outcomes model.SurfaceFindings preview
                                                            // was `[]`, now real findings
```

## 4. Determinism & byte-identity invariants

- **Ordering**: `Composition.run` already sorts by (surface id, domain ordinal, file, detail, code). `update`
  preserves that order; the projection emits in list order. Re-runs / input-discovery reorderings ⇒
  byte-identical output (FR-005, SC-004).
- **Empty case**: `SurfaceFindings = []` ⇒ `ofVerifyDecisionWithPreview … []` ⇒ byte-identical to the
  pre-wiring projection (FR-004, SC-002). Schema version unchanged.
- **No leakage**: each `SurfaceFinding` carries only repo-relative forward-slash paths and stable loci (enforced
  by the domain packs); the projection emits `evidenceTag` only when declared (FR-006). A test asserts the
  emitted `surfaceChecks` JSON contains no absolute path / timestamp / username / environment value.
- **Read-only ⇒ stable across runs**: the read-only package port (no baseline write, no transcript exec — FR-012)
  means an absent-baseline run does not mutate the tree, so a second run senses the identical state and emits
  byte-identical output (SC-004). The verify run is also asserted to leave the working tree unchanged.

## 5. Enforcement fold (the verdict)

| Finding base severity | `enforcementInputOf` ⇒ | At `RunMode.Verify` ⇒ | Effect on exit code |
|---|---|---|---|
| `blocking` | blocking enforcement input | `deriveEffectiveSeverity` ⇒ blocking | run **fails** (FR-007, SC-001) |
| `advisory` | advisory enforcement input | `deriveEffectiveSeverity` ⇒ advisory | exit code **unchanged** (FR-007, SC-003) |

The truth table is not re-opened; surface findings ride the same `deriveEffectiveSeverity` the gate outcomes
already use. Gate outcomes and surface findings remain distinct in the projection (`execution` vs
`surfaceChecks`).
