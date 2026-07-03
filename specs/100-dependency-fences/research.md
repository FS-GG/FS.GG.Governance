# Phase 0 — Research & Decisions: dependency fences

All spec `[NEEDS CLARIFICATION]` / open Assumptions resolved below.

## D1 — YAML fence: document the genuine owner set (do NOT force one owner)

**Decision**: Audit the five projects that declare a direct `PackageReference Include="YamlDotNet"` (Config, CurrencySensing, RefreshCommand, ReleaseDeclaration, ReleaseCommand). Remove the reference from any project that carries the package but uses **no** YamlDotNet type (dead reference). Then **document the genuine remaining owner set** in the README (replacing the "isolated to Config" claim at README:117 and :146) with a one-line reason per owner, and add a guard asserting the direct-`YamlDotNet` set equals that documented allowlist exactly.

**Rationale**: The five projects parse *distinct* YAML domains — Config parses the four `.fsgg` files; CurrencySensing parses currency manifests; ReleaseDeclaration parses release YAML; ReleaseCommand/RefreshCommand parse their command inputs. Collapsing all YAML parsing into one owner would create a god-parser that every command must depend on, worsening coupling and violating Principle III (idiomatic simplicity). The review's own framing ("the only undocumented dependency decisions in the repo") points at the *documentation gap*, not at the existence of the references. Honest documentation + a guard is the minimal, truthful fix.

**Alternatives considered**:
- *Force all YAML through Config (single owner)* — rejected: over-coupling; forces unrelated commands to depend on Config's kernel-parser; large, risky refactor for no correctness gain.
- *Silently update only the README, no guard* — rejected: leaves the fence unguarded (it drifted precisely because nothing guarded it; FR-002/FR-006).

**Implementation note**: the audit may legitimately shrink the set (dead refs removed → fewer owners, closer to the original "Config-centric" story). Whatever the true set, the README and the guard MUST agree on it.

## D2 — Exe→exe: extract two internal application libraries

**Decision**: Break the only two exe→exe edges by relocating the shared payloads into new internal (`IsPackable=false`) libraries:

- **`FS.GG.Governance.RoutePipeline`** ← RouteCommand's `Interpreter` (`Ports`, `realPorts`, `run`) and `Loop` (`RunRequest`, `DefaultRange`, `Text`, `humanView`). Referenced by **RouteCommand** (its `main` drives it) and **Cli** (its watch/tui dispatcher in `Program.fs:141–164` reuses it). Removes `Cli → RouteCommand`.
- **`FS.GG.Governance.ProjectSensing`** ← Cli's `Project` module (`identify`, `compose`, `toLoopConfig`, `evidenceReport` + `ProjectFact`/`ProjectOptions`/`ProjectEvidenceReport`/`ProjectSnapshot`) and the `defaultJudge : JudgeId` constant. Referenced by **Cli** and **EvidenceCommand** (which today `open FS.GG.Governance.Cli` and re-uses `Project.*` verbatim, `Interpreter.fs:36–442`). Removes `EvidenceCommand → Cli`.

**Rationale**: Both payloads are already cohesive modules with `.fsi` files; moving them (module + `.fsi` together) is a mechanical relocation that preserves behavior and the MVU `Ports` boundary. Libraries may hold impure code (driven by the exe's `main`), so `realPorts`/artifact-sensing stay intact. Keeping the libraries internal (`IsPackable=false`) means **no new package-ID commitment** (ADR-0003) and no new published surface.

**Namespace/churn**: keep the moved modules' **type and member names identical** to avoid touching call sites' logic; only the owning project changes. Consumer `open` statements are updated to the new namespace (2 consumers each). The `CommandHost` leaf (the F2 pure guard/drive lib) is **not** the host — its `.fsi` charter admits only pure, no-I/O members, and these payloads are impure; hence new libraries, not CommandHost.

**Alternatives considered**:
- *Duplicate the shared code into each exe* — rejected: divergence risk; the review flagged the coupling precisely to de-duplicate.
- *Put the payloads in `CommandHost`* — rejected: violates CommandHost's documented pure-leaf charter (`CommandHost.fsi:6–15`).
- *Make Cli a library the others reference (keep it non-leaf)* — rejected: Cli is a `PackAsTool` exe; a tool must not be a library dependency of another tool (the fence being restored).

**Risk**: highest-risk slice — it moves surface between projects and touches `SurfaceDriftTests` baselines for Cli/RouteCommand. Mitigation: relocate module+`.fsi` verbatim, re-baseline the moved surface in place, and rely on the existing exe semantic tests to prove behavior is unchanged. This slice can ship as its own commit/PR after P1 if needed.

## D3 — `fsgg` owner: RouteCommand keeps it; the other two get prefixed names

**Decision**: `RouteCommand` keeps `ToolCommandName=fsgg`. `EvidenceCommand` → `fsgg-evidence`; `CacheEligibilityCommand` → `fsgg-cache-eligibility`. Guard asserts at most one project sets `<ToolCommandName>fsgg</ToolCommandName>`.

**Rationale**: RouteCommand is the flagship route tool and the natural holder of the bare `fsgg` name until the ADR-0003 multiplexer subsumes it. Prefixed names keep the other two installable and discoverable. `Cli` already uses `fsgg-governance` (unaffected). `ToolCommandName` is invocation metadata, not the permanent `PackageId`, so changing it is allowed.

**Alternatives considered**:
- *Leave `fsgg` unclaimed (all three prefixed)* — viable and even safer, but the spec allows "at most one"; keeping RouteCommand's `fsgg` preserves the current primary invocation. Either satisfies the guard; documented here as the chosen one.
- *Introduce the multiplexer now* — out of scope (ADR-0003; explicitly deferred).

## D4 — Guard host: one new `RenameGuard`-style test project

**Decision**: Add `tests/FS.GG.Governance.DependencyFences.Tests/` — a self-contained Expecto project (Tier 2, **no `.fsi`, no surface baseline**, all bindings private), modeled on `RenameGuard.Tests`. It locates the repo root, enumerates `git ls-files '*.fsproj'`, parses each project's `<OutputType>`, `<PackageReference>`, `<ProjectReference>`, and `<ToolCommandName>`, and asserts the three fences. Pure matchers (set-difference, graph-reachability, count) get red-path unit tests over literal inputs (no committed tripwire in the tracked tree).

**Rationale**: `RenameGuard.Tests` is the established, blessed precedent for a real-tree regression guard here; reusing its shape (git-tracked scan, private bindings, actionable diagnostics) is lowest-risk and idiomatic. One project for all three fences keeps the graph parse in one place.

**Alternatives considered**:
- *Assembly-reflection guard (like `DependencyBoundaryTests`)* — rejected for the YAML fence: the F# compiler prunes unused assembly references, so reflection under-reports direct package intent; scanning `.fsproj` is the truthful signal for "declares a direct `YamlDotNet` reference."
- *One guard per existing per-command test project* — rejected: scatters the graph parse and duplicates the repo-root/scan helper.

## D5 — P3 add-ons (cheap-only; may split to follow-up)

- **Centralized `VersionPrefix`**: add a single `<VersionPrefix>` to `Directory.Build.local.props` (repo-owned, drift-exempt) so the ~13 baseline-only `.fsproj` files without an explicit `<Version>` inherit one intentional value. Verify no packable tool's effective version changes unexpectedly before/after.
- **Edge-tier reference convention**: a short README/docs note declaring that command/edge-tier projects (e.g. `VerifyCommand`, 43 refs / 32 reachable) may carry broad reference lists by convention, so breadth is a documented choice.
- **Local ADR index**: a small `docs/adr/README.md` (or equivalent) pointing at the org-level ADRs this repo cites (0007/0012/0013) so readers resolve them without leaving the repo.

These do not gate P1/P2 acceptance; if any proves non-trivial it moves to a follow-up issue under epic #44.

## D1 audit result (T004) — one dead reference found

Audited the five direct-`YamlDotNet` projects for actual YamlDotNet type usage in their `.fs` sources:

| Project | Uses a YamlDotNet type? | Verdict |
|---|---|---|
| `Config` | yes — `open YamlDotNet.RepresentationModel` (Schema.fs:11) | **genuine owner** |
| `CurrencySensing` | yes — `open YamlDotNet.RepresentationModel` (CurrencySensing.fs:12) | **genuine owner** |
| `RefreshCommand` | yes — `open YamlDotNet.RepresentationModel` (Declaration.fs:12) | **genuine owner** |
| `ReleaseDeclaration` | yes — `open YamlDotNet.RepresentationModel` (Declaration.fs:12) | **genuine owner** |
| `ReleaseCommand` | **no** — zero YamlDotNet type usage; parses release YAML via its `ReleaseDeclaration` project reference | **DEAD reference → remove (T006)** |

**Final YAML-owner allowlist**: `Config`, `CurrencySensing`, `RefreshCommand`, `ReleaseDeclaration` (four owners). `ReleaseCommand`'s `.fsproj:43` `<PackageReference Include="YamlDotNet" />` (and its mis-stated comment at `.fsproj:21`) are removed; it keeps parsing release YAML through `ReleaseDeclaration`. The guard asserts exactly this four-project set.
