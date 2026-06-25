# Contract: `.fsgg/capabilities.yml` schema v2 (F23)

The authored shape of the **expanded** capability catalog. Only `capabilities.yml` moves to
`schemaVersion: 2`; `project.yml` / `policy.yml` / `tooling.yml` stay at `1` (untouched). Strict validation
(`FS.GG.Governance.Config.Schema.validate`) is unchanged in spirit: unknown field → `UnknownField`, bad
enum/scalar → `MalformedValue`, repeated id → `DuplicateId`, escaping path → `PathEscapesRoot`, dangling
domain/command → `DanglingReference`, wrong version → `UnsupportedSchemaVersion` (+ migration pointer). No
new diagnostic id (the closed set is preserved).

## Top-level shape

```yaml
schemaVersion: 2            # REQUIRED; exactly 2 for capabilities.yml (else UnsupportedSchemaVersion)
domains:                   # unchanged (sorted, de-duped, dangling-checked targets)
  - workflow
  - package-api
pathMap:                   # unchanged (glob -> declared domain)
  - glob: "src/**"
    capability: package-api
surfaces:                  # EXTENDED: new kinds + optional product attributes
  - id: <surface-id>
    kind: <surfaceKind>    # routine|governedRoot|protected|generatedView|release
                           # |package|docs|skill|design|sampleApp|generatedProduct
    paths: ["..."]
    owner: <owner>
    maturity: <maturity>
    evidenceTag: <tag>        # OPTIONAL — any kind (F24 produces the evidence)
    templateProfile: <name>   # OPTIONAL — meaningful on generatedProduct
    baseline: <pin>           # OPTIONAL — meaningful on package
checks:                    # EXTENDED: optional tier
  - id: <check-id>
    domain: <declared-domain>
    command: <declared-command>   # optional, cross-file dangling-checked (unchanged)
    owner: <owner>
    cost: cheap|medium|high|exhaustive   # unchanged generic cost
    environment: local|ci|local-or-ci|release
    maturity: <maturity>
    tier: <generatedProductTier>  # OPTIONAL — structuralScan|restoreBuild|focusedTests|fullVerify|releaseValidation
```

## `kind` tokens (closed)

| token              | `SurfaceClass`          | protected boundary? (FR-003) |
|--------------------|-------------------------|------------------------------|
| `routine`          | `Routine`               | no (suppresses unknowns)     |
| `governedRoot`     | `GovernedRoot`          | no                           |
| `protected`        | `ProtectedSurface`      | **yes**                      |
| `generatedView`    | `GeneratedView`         | no                           |
| `release`          | `ReleaseSurface`        | **yes**                      |
| `package`          | `PackageSurface`        | **yes**                      |
| `docs`             | `DocsSurface`           | no                           |
| `skill`            | `SkillSurface`          | no                           |
| `design`           | `DesignSurface`         | no                           |
| `sampleApp`        | `SampleAppSurface`      | no                           |
| `generatedProduct` | `GeneratedProductRoot`  | **yes**                      |

Any other token ⇒ `MalformedValue` naming the field.

## `tier` tokens (closed, ordered)

`structuralScan` < `restoreBuild` < `focusedTests` < `fullVerify` < `releaseValidation`. Any other ⇒
`MalformedValue`. Absent ⇒ an ordinary (non-tiered) check.

## Worked example (a generated product, abridged)

```yaml
schemaVersion: 2
domains: [package-api, docs, skills, design, release]
pathMap:
  - { glob: "src/**",      capability: package-api }
  - { glob: "docs/**",     capability: docs }
  - { glob: ".claude/skills/**", capability: skills }
  - { glob: "design/**",   capability: design }
  - { glob: "release/**",  capability: release }
surfaces:
  - { id: public-api, kind: package, paths: ["src/**/*.fsi"], owner: platform,
      maturity: block-on-ship, baseline: "src/public-api.baseline.txt", evidenceTag: api-surface }
  - { id: product-root, kind: generatedProduct, paths: ["."], owner: platform,
      maturity: block-on-pr, templateProfile: fsharp-lib }
  - { id: guide-docs, kind: docs, paths: ["docs/**"], owner: docs, maturity: warn, evidenceTag: docs-links }
  - { id: ship-skill, kind: skill, paths: [".claude/skills/**"], owner: platform, maturity: warn }
  - { id: tokens, kind: design, paths: ["design/**"], owner: design, maturity: warn, evidenceTag: design-tokens }
  - { id: sample, kind: sampleApp, paths: ["samples/**"], owner: platform, maturity: observe }
  - { id: rel, kind: release, paths: ["release/**"], owner: platform, maturity: block-on-release }
checks:
  - { id: scan,    domain: package-api, owner: platform, cost: cheap,      environment: local-or-ci, maturity: warn,            tier: structuralScan }
  - { id: build,   domain: package-api, owner: platform, cost: medium,     environment: local-or-ci, maturity: block-on-ship,  tier: restoreBuild }
  - { id: test,    domain: package-api, owner: platform, cost: high,       environment: ci,          maturity: block-on-ship,  tier: focusedTests }
  - { id: verify,  domain: release,     owner: platform, cost: high,       environment: ci,          maturity: block-on-release, tier: fullVerify }
  - { id: relgate, domain: release,     owner: platform, cost: exhaustive, environment: release,     maturity: block-on-release, tier: releaseValidation }
```

A declared `evidenceTag` whose F24 check does not exist yet is a known, non-error state (FR-016).
