# Contract: Design/rendering facts — token / capture / contrast / control (F24, P3)

**Library**: `FS.GG.Governance.DesignChecks` | **Story**: US4 | **FR**: 006, 007, 009, 010, 012

Connects design-system facts to their real catalog sources and checks them deterministically — **with the
rendering dependency kept out of the kernel and out of the pure pack** (FR-007, SC-004). This is the most
specialized domain and the one most at risk of dragging a rendering dependency inward; it is explicitly
fenced.

## C1 — Resolution checks (FR-006, SC-004)

- Token / capture / control: `Resolves` ⇒ no finding; `Absent entry` ⇒ one **Blocking** `design.token` /
  `design.capture` / `design.control` finding naming the missing entry (acceptance 4.2).
- Contrast: `Meets = true` ⇒ no finding; `Meets = false` ⇒ Blocking `design.contrast` finding naming the pair
  and reporting `Ratio` vs `Threshold` (a deterministic numeric compare).

## C2 — Render fence (FR-007, SC-004) — the load-bearing contract

- `DesignChecks.Model` and `DesignChecks.evaluate` carry **zero** rendering / UI / registry / filesystem
  dependency. They consume only plain `DesignFacts`.
- The **only** place a design catalog is read is `Interpreter.DesignPort`, using `System.IO` /
  `System.Text.Json` exclusively — **no Skia, no rendering, no UI, no registry, no network**.
- The `FS.GG.Governance.DesignChecks.fsproj` references only `SurfaceChecks` + `Config`. A test inspects the
  committed surface + project references and asserts no rendering/UI reference is present (SC-004,
  acceptance 4.3).

## C3 — All-resolve pass, determinism, input/defect (SC-004, SC-005, FR-010, FR-012)

- A design surface referencing only entries that resolve (and contrasts that meet threshold) ⇒ zero findings
  (acceptance 4.1).
- `evaluate` sorts by `(kind, entry id)`; identical `DesignFacts` ⇒ byte-identical.
- `CatalogUnavailable` (absent/unreadable catalog) ⇒ `IsInputState` `design.catalog-unavailable` finding
  naming the catalog, never a fabricated pass (FR-012).

## Acceptance (maps to spec US4 scenarios)

1. All tokens/captures/contrast/control resolve ⇒ pass + evidence.
2. Missing token/capture/control or sub-threshold contrast ⇒ the matching `design.*` finding naming it.
3. Inspection: kernel + pure adapter carry no rendering/UI/registry dependency — catalog read only by the
   host sensor.
