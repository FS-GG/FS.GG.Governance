# Contract: `.fsgg/release.yml` — additive `packableProjects` + `matrix` declaration

`ReleaseCommand.Declaration.parse` is extended **additively** to read two new optional sections from
`.fsgg/release.yml`; the existing F55 release-declaration sections (`rules`, `expectations`, `layout`) are
unchanged. Product-neutral (FR-014): every value — project ids, pack commands, baselines, matrix axes — comes
from the file; the adapter hardcodes none. Fail-safe (FR-011): a malformed new section is an `Error DeclError`
(input-unavailable, exit 3), never partial facts.

## Added declaration fields (additive)

```fsharp
    type ReleaseDeclaration =
        { Rules: ReleaseRule list                         // F55 — unchanged
          Expectations: ReleaseExpectations               // F55 — unchanged
          Layout: SourceLayout                            // F55 — unchanged
          // ── F26 additive ──
          PackableProjects: (SurfaceId * GateCommand * string option) list
          Matrix: ExhaustiveMatrix option }
```

## YAML shape (new sections)

```yaml
# … existing rules / expectations / layout sections (F55, unchanged) …

packableProjects:
  - surface: "FS.GG.Governance.Kernel"          # the project's governed surface id (product-neutral)
    pack:                                        # the pack command-to-run (an F051 GateCommand)
      executable: "dotnet"
      arguments: ["pack", "src/FS.GG.Governance.Kernel", "-c", "Release", "-o", "<out>"]
      workingDirectory: "."
      timeoutSeconds: 600
    baseline: "1.2.0"                            # released version baseline; omit for first release

matrix:                                          # optional exhaustive validation matrix (P3)
  name: "pack-all-targets"
  cost: "exhaustive"
  dimensions: ["packableProjects", "targetFrameworks"]
```

## Rules (tested)

- Both new sections are OPTIONAL: an absent `packableProjects` ⇒ `NoPackableProjects = true` downstream (no
  project blocks on packing, D6); an absent `matrix` ⇒ `MatrixPlan.NotDeclared` (never invented, FR-009).
- `PackableProjects` is normalized to `SurfaceId` order so identical file content yields a structurally
  identical declaration (determinism, D7).
- A malformed pack command / unknown cost token / non-mapping section ⇒ `Error DeclError`, never a partial or
  fabricated `Met` (FR-011).
- No new dependency: parsed in YamlDotNet parse-to-node mode (the existing F14/F55 precedent).
