# Quickstart: Package / Docs / Skills / Design Deterministic Checks (F24)

**Branch**: `059-package-docs-skills-design-checks` | **Spec**: [spec.md](./spec.md) |
**Plan**: [plan.md](./plan.md)

Runnable validation that proves each domain's deterministic check works end-to-end against **real on-disk
fixtures** through the real host sensor, then composes and surfaces through `fsgg verify`. Build/test commands
are the repo standard; details live in the contracts and data model, not here.

## Prerequisites

- .NET SDK `net10.0`; restore + build the solution (`dotnet build FS.GG.Governance.sln`).
- The five new projects added to `FS.GG.Governance.sln` (5 `src` + 5 `tests`).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln          # whole solution stays green (no regression)
# focused:
dotnet test tests/FS.GG.Governance.PackageChecks.Tests
dotnet test tests/FS.GG.Governance.DocsChecks.Tests
dotnet test tests/FS.GG.Governance.SkillChecks.Tests
dotnet test tests/FS.GG.Governance.DesignChecks.Tests
dotnet test tests/FS.GG.Governance.SurfaceChecks.Tests
```

## Scenario 1 — Package/API: baseline drift + transcript (US1, P1)

1. Fixture: a package surface with a committed `.fsi` baseline + one passing FSI transcript.
2. Run the sensor + `PackageChecks.evaluate`: **no finding**; baseline recorded as evidence under the
   surface's `EvidenceTag`.
3. Change the public surface (add/remove a member) ⇒ `package.baseline-drift` finding naming the change;
   revert ⇒ no drift (SC-001).
4. Delete the committed baseline ⇒ baseline regenerated + `package.baseline-absent` input-state finding
   (never a silent pass).
5. Break the transcript (won't compile) or change its stated result ⇒ the matching transcript finding naming
   the example.
6. **Expected**: drift caught 100%, unchanged ⇒ zero drift, transcript failures named; evidence tied to the
   declared tag.

## Scenario 2 — Docs/examples: link + reference currency (US2, P2)

1. Fixture: a docs source with a valid internal link, a valid reference, a valid symbol reference.
2. Run sensor + `DocsChecks.evaluate`: **zero** findings (clean pass, zero false positives — SC-002).
3. Break a link target ⇒ `docs.link-currency` naming file + link + target.
4. Remove/rename a referenced symbol/anchor ⇒ `docs.reference-currency` naming the stale reference.
5. **Expected**: each break reported with exact location; clean docs pass cleanly.

## Scenario 3 — Skills: path contract / task list / mirror (US3, P2)

1. Fixture: a skill whose path contract holds, task list is consistent, mirror is in sync.
2. Run sensor + `SkillChecks.evaluate`: **zero** findings (SC-003).
3. Introduce a claimed path that does not resolve (or escapes bounds) ⇒ `skill.path-contract` naming skill +
   path.
4. Make the task list inconsistent ⇒ `skill.task-list`.
5. Drift / remove the mirror ⇒ `skill.mirror`; a skill that declares **no** mirror ⇒ not an error.

## Scenario 4 — Design: token / capture / contrast / control (US4, P3)

1. Fixture: token/capture/contrast/control catalog files; a design surface referencing valid entries.
2. Run sensor + `DesignChecks.evaluate`: **zero** findings.
3. Reference a missing token / absent capture / unmapped control / sub-threshold contrast ⇒ the matching
   `design.*` finding naming the entry.
4. **Render fence**: inspect `FS.GG.Governance.DesignChecks` surface + `.fsproj` references and confirm
   **no** rendering/UI/registry dependency — the catalog is read only by `Interpreter.DesignPort` (SC-004).

## Scenario 5 — Advisory stays advisory (US5, P3)

1. Produce a judgement-heavy finding (`BaseSeverity = Advisory`) on a fixture.
2. Across every `(RunMode, Profile)` pair, `deriveEffectiveSeverity` returns Advisory; a run whose only
   findings are advisory **passes** the gate (SC-006).

## Scenario 6 — Composition + `fsgg verify` surfacing (FR-008, SC-008, D8)

1. Fixture: a single change touching a package surface, its docs, and its skill.
2. `Composition.run facts report bundle` ⇒ three independent finding groups (package + docs + skill), sorted,
   order-independent (shuffle inputs ⇒ byte-identical output).
3. Run `fsgg verify` against a real fixture repo:
   - No declared product surfaces ⇒ `verify.json` is **byte-identical** to the pre-F24 golden (additive
     section omitted).
   - Declared surfaces with findings ⇒ `verify.json` gains a `surfaceChecks` array; the verdict/exit code
     reflect the Blocking findings at `RunMode.Verify`; advisory entries appear but don't change the exit code.

## Done when

- All five focused test projects pass and the whole solution is green (no regression).
- Each domain: clean fixture ⇒ zero findings; each break ⇒ the named finding at the exact location;
  determinism test ⇒ byte-identical on repeat.
- Composition test ⇒ three independent groups, order-independent.
- `fsgg verify` empty case ⇒ byte-identical golden; non-empty ⇒ additive `surfaceChecks`.
- Design render-fence inspection ⇒ zero rendering/registry dependency in kernel + pure pack.
