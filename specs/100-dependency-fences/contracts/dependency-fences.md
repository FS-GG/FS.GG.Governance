# Contract: the three dependency-fence allowlists

This file is the **authoritative documented state** each guard test asserts. When a fence
legitimately changes, this file, the README, and the guard change together — that is the
whole point of the fence.

---

## Fence 1 — YAML owner set (INV-1)

**Claim to fix**: README:117 (`YamlDotNet isolated here`) and README:146 (`YamlDotNet is an
isolated internal detail`) currently under-state reality.

**Contract**: exactly the following projects MAY declare a direct
`PackageReference Include="YamlDotNet"`. The set is finalized during implementation after
the dead-reference audit (D1); the guard asserts the direct-`YamlDotNet` set equals this
list with no undocumented member.

| Owner project | Why it parses YAML |
|---|---|
| `FS.GG.Governance.Config` | strict YAML → typed facts for the four `.fsgg` files |
| `FS.GG.Governance.CurrencySensing` | parses currency manifests *(confirm still used; else remove)* |
| `FS.GG.Governance.ReleaseDeclaration` | parses release-declaration YAML *(confirm; else remove)* |
| `FS.GG.Governance.ReleaseCommand` | reads release YAML input *(confirm; else remove)* |
| `FS.GG.Governance.RefreshCommand` | reads refresh YAML input *(confirm; else remove)* |

**Guard**: fail if any project *outside* this list declares a direct `YamlDotNet` reference,
or if any listed project *drops* it (keeps list honest in both directions). Diagnostic names
the offending project.

---

## Fence 2 — every executable is a leaf (INV-2)

**Contract**: no `<OutputType>Exe</OutputType>` project may reference another `Exe` project,
directly or transitively.

The eight executables — `Cli`, `RouteCommand`, `EvidenceCommand`, `CacheEligibilityCommand`,
`VerifyCommand`, `ShipCommand`, `ReleaseCommand`, `RefreshCommand` — must each have an
exe-free `ProjectReference` closure.

**Current violations to remove**:
- `Cli → RouteCommand` → break by extracting `FS.GG.Governance.RoutePipeline`.
- `EvidenceCommand → Cli` (→ RouteCommand transitively) → break by extracting
  `FS.GG.Governance.ProjectSensing`.

**Guard**: fail listing every offending `Exe → Exe` edge (direct or transitive).

---

## Fence 3 — a single `fsgg` owner (INV-3)

**Contract**: at most one project sets `<ToolCommandName>fsgg</ToolCommandName>`.

| Project | `ToolCommandName` after this change |
|---|---|
| `FS.GG.Governance.RouteCommand` | `fsgg` (sole owner) |
| `FS.GG.Governance.EvidenceCommand` | `fsgg-evidence` |
| `FS.GG.Governance.CacheEligibilityCommand` | `fsgg-cache-eligibility` |
| `FS.GG.Governance.Cli` | `fsgg-governance` (unchanged) |

`PackageId` values are unchanged for all four (ADR-0003).

**Guard**: fail if two or more projects claim `fsgg`, naming the second claimant.

---

## Guard-test contract

- Host: `tests/FS.GG.Governance.DependencyFences.Tests` — Tier 2, no `.fsi`, all bindings
  private (per `RenameGuard.Tests`).
- Input: the **real** tracked tree via `git ls-files '*.fsproj'` (Principle V: real evidence).
- Output on failure: a loud, file-level, actionable diagnostic per `Violation` (Principle VI).
- Red-path unit tests exercise the pure matchers over literal inputs (no committed tripwire).
