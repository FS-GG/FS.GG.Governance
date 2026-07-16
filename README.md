# FS.GG.Governance

Optional rule, evidence, and route-explanation tooling for the
[FS-GG](https://github.com/FS-GG) projects, built as a normal F# tool component with
standard [Spec Kit](https://github.com/github/spec-kit).

**In one sentence:** governance is a *pure inference kernel* over typed facts and
rules, where every rule declares **who is competent to decide it** (machine, agent,
or human), every rule's check is **reified data** that can be evaluated, rendered,
hashed, and explained from one source, and enforcement is **light and advisory by
default** with a loud, local-only escape hatch.

The kernel is domain-neutral: what changes between governing F# code, an essay, or a
research project is the *fact vocabulary* — the inference, arbitration, evidence, and
rule language stay the same. See the [design overview](docs/governance-design/index.md).

> **Platform vs. workspace.** FS-GG is a **platform** — five repositories (the
> governance kernel is one optional **component** of it). What you scaffold *with*
> the platform is a **workspace**: a generated repo with a runnable app, the
> `.fsgg/` lifecycle, skills, and optional governance. See the
> [vocabulary](https://github.com/FS-GG/.github/blob/main/docs/adr/0020-platform-workspace-component-vocabulary.md).

## The operating rule

> Governance tooling may *inspect* rendering or SDD artifacts; rendering and SDD must
> never *require* governance tooling to build, test, document, package, or release.

This is the one-directional dependency that keeps governance honest. If the kernel
ever becomes heavy, brittle, or distracting, a consuming project drops it and keeps
building. Generic code here must not assume any consumer's package IDs, template
names, target names, or directory layout — rendering is one external customer, not
this tool's shape.

## Where this sits in FS-GG

FS-GG is the split of the archived [`FS-Skia-UI`](https://github.com/EHotwagner/FS-Skia-UI)
monolith — a single self-hosting platform that bundled a UI runtime with an
experimental governance system and got too heavy to develop on — into **focused
components that each stand on their own**, each using standard Spec Kit.

| Repo | What it is |
|---|---|
| [**FS.GG.Rendering**](https://github.com/FS-GG/FS.GG.Rendering) | The UI framework — Elmish/MVU apps rendered with SkiaSharp over OpenGL. Depends on no FS-GG component; **never** depends on Governance. |
| [**FS.GG.Governance**](https://github.com/FS-GG/FS.GG.Governance) | *This repo.* Optional rule/evidence/route tooling, developed as a normal tool component. |
| [**FS.GG.SDD**](https://github.com/FS-GG/FS.GG.SDD) | Spec-driven development lifecycle tooling (`charter → specify → plan → tasks → verify → ship`) and the org-shared `FS.GG.Contracts` schema-authority package. |
| [**FS.GG.Templates**](https://github.com/FS-GG/FS.GG.Templates) | Downstream composition: `dotnet new` templates and scaffold providers wiring SDD + Rendering + Governance into a ready-to-run workspace. Depends on the others; none depend back. |

Governance fills the *enforcement / rule-engine* slot — but explicitly **optional and
one-directional**. It relates to its siblings only through explicit, versioned
contracts (see [Cross-repo contracts](#cross-repo-contracts)): it consumes SDD's
optional `governance-handoff@1` readiness document and the org-shared
`FS.GG.Contracts` package, and it publishes the policy/capabilities/tooling surfaces
and reference gate set that Templates materializes into real workspaces. Cross-repo
work is coordinated through the org's [`.github`](https://github.com/FS-GG/.github)
repo (issue-based requests, a Projects-v2 "Coordination" board, a contract &
compatibility registry, and ADRs).

## The core idea

The product is the **inference kernel** (facts, rules, fixed-point evaluation,
provenance), the **`CheckTier` arbitration model** (machine / agent / human), the
**reified rule eDSL**, and the **evidence model**. These contain no domain vocabulary
at all. Everything domain-specific lives in an **adapter**: the facts a domain
asserts, the artifacts it inspects, and the probes its rules call. The boundary test:
*generic code has zero domain vocabulary, and removing any one adapter leaves the
kernel and the other adapters intact.*

The design guarantees four properties **structurally — by construction, not by
configuration that can drift**:

1. **Light by default** — the system justifies cost, not the developer. An
   unclassified or low-stakes change incurs *no* machinery; heavy checks require a
   *positive* match against a small, named, fenced high-stakes surface (a published
   API, a release, an irreversible contract). Thinking artifacts — notes, drafts,
   experiments — live in a zero-gate zone, because thinking is not contract.
2. **Advisory by default** — a rule *reports* unless explicitly marked blocking.
   `Severity` is orthogonal to `CheckTier`: the tier says *who decides*, the severity
   says *whether failure stops you*. The full blocking set must be listable at a
   glance; a long one is a design smell.
3. **Explainable by construction** — opacity is a defect, not a tuning problem. Every
   conclusion carries provenance and every check renders to a sentence. "No reason" is
   unrepresentable, because the reason *is* the rule id plus the rendered check.
4. **Honest escape hatch** — a real off switch for the inner loop that is **loud**,
   **local-only**, and **cannot be the basis of a merge**: the merge boundary
   recomputes from scratch against the base branch and ignores any local mode. You can
   develop freely without the machinery, but you cannot *land* an un-governed state.

The kernel is a pure, **zero-dependency** forward-chaining (Datalog-style,
stratified-monotonic) reasoner: `FixedPoint.evaluate identify rules supplied` returns
the least fixed point of the facts under the rules, with provenance for every derived
fact. Verdict logic is three-valued Kleene (`Pass`/`Fail`/`Uncertain`). All I/O lives
at the edge (functional core / imperative shell). The checker paradigm follows
[Cedar](https://www.cedarpolicy.com/) and OPA/Rego — *policy as analyzable data* —
generalized in domain; planning and optimization are deliberately not native (the
kernel checks their outputs at the edge).

> **Kernel precondition (documented, not runtime-enforced).** Rules must be
> **monotonic** (add-only); negated or aggregated facts are *supplied* from a lower
> stratum, never derived in the same fixed point. See [the kernel](docs/governance-design/kernel.md).

## Architecture

The repo is one solution of ~166 F# projects (80 libraries + 84 test projects, plus a
packaging and a sample project), layered strictly from a pure core outward to I/O
edges. Every library ships a curated `.fsi` signature file as its sole public-surface
declaration.

```text
Kernel            pure, BCL-only inference core
  facts · rules · fixed-point · provenance · verdicts (Kleene)
  reified Check algebra (eval / render / hash / explain) · CheckTier arbitration
  evidence + synthetic-taint DAG · JSON explanation · routing (Stakes/Severity/RunMode/Route)

Host / CommandHost            effects shell (I/O): sense → plan → act

Adapters
  Adapters.Spi                adapter SPI + lift / compose
  Adapters.SpecKit            this repo's Spec Kit workflow, governed as data
  Adapters.DesignSystem       a design language (Ant Design worked example) as data
  Adapters.SddHandoff         consumes SDD's governance-handoff@1 readiness document

Capability platform (paths → domains → gates), each a pure core behind an I/O sensor
  Config       strict YAML → typed facts for the four .fsgg files (a YamlDotNet owner — see the YAML note)
  Routing      paths → capability domains, deterministic glob precedence
  Snapshot     read-only git/CI → typed repository snapshot
  Findings     unknown governed / protected-boundary path findings
  Gates        typed gate registry → stable, injective GateId metadata

Surface / product checks      deterministic adapter packs over a shared core
  SurfaceChecks(.Dispatch) · PackageChecks · DocsChecks · SkillChecks · DesignChecks

Evidence / cache / freshness / provenance / cost / release / currency
  EvidenceCapture · EvidenceReuse(Store) · FreshnessKey/Resolution/Sensing
  CacheEligibility · Provenance · Calibration · CostBudget · GateExecution/Run
  AgentReviewKey · ReviewRecord · PromptIsolation · AdvisoryPromotion · Attestation
  Release{Declaration,FactsSensing,Rules,Report} · Currency{Sensing,Enforcement}

JSON projections              Route/Gates/Audit/Evidence/Verify/Refresh/Release/CostBudget/Provenance/Attestation…
Human projections             HumanText (pure, ANSI-free) · HumanRender (the sole Spectre.Console owner)
Command edges (executables)   Cli · RouteCommand · ShipCommand · VerifyCommand · ReleaseCommand · RefreshCommand · EvidenceCommand · CacheEligibilityCommand
```

Each host command follows the same Elmish/MVU shape: a pure `Loop`, an injected,
fakeable-port `Interpreter`, and a thin `Program`.

> **Edge-tier reference convention.** Command/edge-tier executables are composition roots,
> so they legitimately carry broad `ProjectReference` lists — e.g. `VerifyCommand` declares
> 43 references (32 transitively reachable). That breadth is by design at the edge and is
> *not* drift; the dependency fences guarded by `FS.GG.Governance.DependencyFences.Tests`
> constrain the *kinds* of edges that matter (no executable may reference another executable;
> YAML parsing stays with its owners; Spectre.Console stays confined to `HumanRender`), not the
> raw reference count of a leaf command.

## The `.fsgg` configuration model

`FS.GG.Governance.Config` parses four versioned `.fsgg/*.yml` files **strictly**
(unknown fields, duplicate ids, `schemaVersion` range, path escapes, and dangling
cross-references are all stable, located diagnostics), normalizes paths
deterministically, and emits **typed, YAML-free, product-neutral facts** — it never
routes, senses git/CI, or enforces. YamlDotNet parsing is confined to four owners —
`Config` (the four `.fsgg` files), `CurrencySensing` (currency manifests),
`ReleaseDeclaration` (`release.yml`), and `RefreshCommand` (refresh-declaration YAML) — a
fence guarded by `FS.GG.Governance.DependencyFences.Tests` so the set cannot drift silently.

| File | Owner | Declares |
|---|---|---|
| `governance.yml` | Governance | project id, declared domains, governed root, package surfaces, optional policy/capabilities refs |
| `policy.yml` *(optional)* | Governance | enforcement profiles + default, branch policy, review budget |
| `capabilities.yml` *(schemaVersion 2)* | Governance | the routing path-map (`glob → domain`), governed surfaces (class, owner, maturity, evidence, baseline), and reified checks |
| `tooling.yml` *(optional)* | Governance | external command specs, environment classes, external-tool version requirements |

By [ADR-0005](https://github.com/FS-GG/.github), these four Governance-owned files
coexist in one `.fsgg/` directory alongside SDD-owned files (`project.yml`, `sdd.yml`,
`agents.yml`, …) with no shadowing.

## CLI / commands

There are two tool lineages today (a single-tool unification is planned):

**`fsgg-governance`** — the kernel-era CLI (`src/FS.GG.Governance.Cli`), published to
the org GitHub Packages feed. One-shot, read-only inspection of a governed root:

| Subcommand | Output |
|---|---|
| `route` | the kernel `Route` — blocking/advisory rule selection with a reason for every entry |
| `explain` | per-rule explanations (rendered reified checks) |
| `contract` | the rule catalog as a contract |
| `evidence` | a project evidence report |
| `watch` / `tui` | interactive read-only views over the composed route report |

Shared flags include `--root`, `--mode {sandbox|inner|gate}`, `--format {text|json}`,
`--domain {all|speckit|design}`, `--review-budget N`. Exit codes: `0` success, `2`
governed-blocking, `64` usage, `66` input-unavailable, `70` tool error. In `gate` mode
the CLI is profile-aware — the policy profile tightens or relaxes how a handoff gate
maps to an enforcement verdict.

**`fsgg`** — the capability-platform host commands (each its own MVU executable),
covering the full route → verify → ship → release lifecycle:

| Command | Purpose |
|---|---|
| `fsgg route` | end-to-end Phase-2 route evaluation; writes `.fsgg/gates.json` + `readiness/route.json` |
| `fsgg verify` | verify verdict + surface checks; writes `verify.json` |
| `fsgg ship` | merge-boundary ship verdict; writes `audit.json` |
| `fsgg release` | release-gate verdict + attestation |
| `fsgg refresh` | generated-view currency refresh |
| `fsgg evidence` | evidence JSON projection |
| `fsgg cache-eligibility` | cache-eligibility verdict |

`route`, `evidence`, and `cache-eligibility` are packed and installable as .NET tools;
the others currently run from source. Until the single-tool multiplexer lands, they
install under **distinct** command names so two of them never collide — `route` owns the
bare `fsgg` command, `evidence` installs as `fsgg-evidence`, and `cache-eligibility` as
`fsgg-cache-eligibility` (100/M-ARCH-3, guarded by `FS.GG.Governance.DependencyFences.Tests`).

### Exit codes

The two lineages use **two different exit-code families** — the same integer can mean
different things, so read them against the right column.

**`fsgg` verb hosts** (the MVU executables — `verify`/`ship`/`route`/`release`/
`evidence`/`cache-eligibility`). Each host declares its own exit-code DU (there is no
shared `ExitDecision` type); they agree on this `0`–`4` scheme:

| Code | Meaning |
|---|---|
| `0` | success |
| `1` | governed **blocking** verdict (the gate/verdict said no) |
| `2` | usage error (bad flags/arguments) |
| `3` | input unavailable (a required input could not be read) |
| `4` | tool error (an internal/IO failure while running) |

**`fsgg refresh`** is the one verb host with a **sixth** code: it distinguishes "nothing
needed refreshing" from "views were regenerated", so `0` is *not* the sole success code —
a successful regeneration returns `5`. A script must treat both `0` and `5` as success.

| Code | Meaning |
|---|---|
| `0` | success — nothing to refresh (all views current) |
| `1` | stale views remain unresolved (some view could not be brought current) |
| `2` | usage error (bad flags/arguments) |
| `3` | input unavailable (the refresh manifest could not be read) |
| `4` | tool error (a generator/write/provenance failure) |
| `5` | **success — views were regenerated** |

**`fsgg-governance`** (the kernel-era dispatcher, `src/FS.GG.Governance.Cli`) remaps onto
BSD **sysexits**:

| Code | Meaning |
|---|---|
| `0` | success |
| `2` | governed-blocking verdict |
| `64` | usage error (`EX_USAGE`) |
| `66` | input unavailable (`EX_NOINPUT`) |
| `70` | tool error (`EX_SOFTWARE`) |

⚠️ **`2` diverges between the families**: it is a *governed-blocking* verdict from
`fsgg-governance`, but a *usage error* from every `fsgg` verb host (whose blocking verdict
is `1`). Scripts that branch on exit codes must know which tool they invoked.

**Store / freshness read failure policy.** A store/freshness *write* failure is always a
**tool error** (`4` verb-host / `70` dispatcher), never a blocking verdict — a failed
persist must not masquerade as a governed "no" (`ShipCommand`/`VerifyCommand` FR-009). A
store/freshness *read* failure is **not** a failure exit: the store is treated as empty and
the verdict is recomputed (recompute-by-default). One known, still-unaligned divergence
(review M-CLI-6): on an *unreadable* store `route`/`ship`/`verify` degrade to `0` + a note,
whereas `cache-eligibility` currently exits `4` — documented here pending alignment.

## Cross-repo contracts

Governance participates in the org [contract & compatibility registry](https://github.com/FS-GG/.github)
(`registry/dependencies.yml`), enforced by a CI coherence gate that goes red when a
repo's reality stops matching the registry.

- **Owns / publishes:** `governance-descriptor@1` (`governance.yml`),
  `governance-policy@1`, `governance-capabilities@2`, `governance-tooling@1`, the
  published `FS.GG.Governance.Cli`, and `governance-reference-gate-set` (a content-only
  NuGet package — the four validated reference `.fsgg` files, no assembly, no runtime
  dependency, that Templates drops into scaffolded workspaces).
- **Consumes:** SDD's optional `governance-handoff@1` (`readiness/<id>/governance-handoff.json`)
  via `Adapters.SddHandoff`, and the SDD-owned `FS.GG.Contracts` package.

Recent integration ([spec 087](specs/087-retype-config-onto-contracts/)) makes
`FS.GG.Governance.Config` a **consumer** of `FS.GG.Contracts` (`Fsgg.Schemas`, pinned
`2.0.0`) rather than a second source of truth for the four `.fsgg` `schemaVersion`
constants — a future schema bump now flows in by re-pinning the package instead of
editing Governance code. The public Config surface stays byte-identical.

## Building & testing

Build and test the **whole solution** through the checked-in wrapper:

```bash
dotnet fsi build.fsx          # build all ~166 projects of FS.GG.Governance.sln
dotnet fsi build.fsx test     # run the full test suite (the delivery gate)
```

The wrapper exists because a plain `dotnet build` over-subscribes the build: with no
shared F# compiler server, MSBuild's default one-node-per-core fan-out launches a
`dotnet fsc` process per node across the solution and thrashes (`MSB6003`/`MSB6006`)
instead of finishing. `build.fsx` bounds the MSBuild node count with an explicit,
hardware-derived `-m:N` — `clamp(2, ceil(cores/4), 12)` — turning a >10-minute failing
build into ~33 s, faster on bigger hosts yet never over-subscribed. Any `dotnet`
arguments after the verb pass through (e.g. `dotnet fsi build.fsx build -c Release`).

- **Target framework:** `net10.0` · `LangVersion=latest` · `Nullable=enable` ·
  `TreatWarningsAsErrors=true`.
- **Tests:** Expecto (with FsCheck property tests); the full suite is the delivery gate.
- **Key dependencies:** `FSharp.Core 10.1.301` (org-pinned), `YamlDotNet 18.1.0`
  (isolated to `Config`), `FS.GG.Contracts 2.0.0` (consumed by `Config`),
  `Spectre.Console 0.57.2` (confined to exactly one project, `HumanRender` — a fence
  guarded by `FS.GG.Governance.DependencyFences.Tests`).
- **Build config:** `Directory.Build.props` / `Directory.Packages.props` are
  org-synced and drift-checked from [`FS-GG/.github → dist/dotnet/`](https://github.com/FS-GG/.github);
  repo-specific overrides live in `*.local.props`. Central Package Management is on
  with transitive pinning; `packages.lock.json` is committed and restored in locked
  mode under CI.

## Packaging

Most projects are packable by default (74 of 82 `src` projects; only a small explicit
set carries `IsPackable=false`). The api-compat breaking-change gate packs every packable
project against its org-feed baseline, but only the two publishable `FS.GG.Governance.*`
packages (`FS.GG.Governance.Cli` and `FS.GG.Governance.ReferenceGateSet`) are actually
published. The publishable packages pack to a local folder feed
(`~/.local/share/nuget-local/`) and publish to the org GitHub Packages feed;
`nuget.config` additively adds that feed (`https://nuget.pkg.github.com/FS-GG/index.json`)
with source mapping (`FS.GG.*` → org feed, everything else → nuget.org). Each packable
project carries a committed public-surface snapshot under `surface/*.surface.txt`,
guarded by a drift test. [Spec 088](specs/088-governance-apicompat-gate/) adds an
API-compat breaking-change gate on top of those drift guards, so a release that removes
or changes a published surface is required to carry a major version bump.

```bash
# build & run a host command from source
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo .

# inspect a governed root with the kernel CLI
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner

# install the CLI from the org feed
dotnet tool install --global FS.GG.Governance.Cli --add-source https://nuget.pkg.github.com/FS-GG/index.json
```

## Status

The pure kernel, the effects edge, the adapter SPI and three concrete adapters, the
published CLI, and the full capability platform (config, routing, snapshot, findings,
gate registry) are **implemented**. On top of them the lifecycle host commands
(`route` / `verify` / `ship` / `release` / `refresh` / `evidence` / `cache-eligibility`),
the JSON contracts, the surface/product check packs, release gates, evidence reuse /
freshness / provenance, and human projections all ship with tests.

Work is tracked as numbered Spec Kit features under [`specs/`](specs/) (001–093).
Recently landed and in flight:

- [**087**](specs/087-retype-config-onto-contracts/) — re-type `Config` onto the
  org-shared `FS.GG.Contracts` package (schema versions single-sourced).
- [**088**](specs/088-governance-apicompat-gate/) — a breaking-change (API-compat) gate
  for the published packages, advisory-first and ratcheted to required.
- [**089**](specs/089-publish-governance-cli/) — publish the consumer-bearing Governance
  CLI to the org feed.
- [**090**](specs/090-profile-aware-handoff-gate/) — profile-aware handoff-gate
  enforcement.
- [**091**](specs/091-headless-render-determinism/) — headless render-width test
  determinism; [**092**](specs/092-spectre-console-skill/) /
  [**093**](specs/093-spectre-console-reference/) — the Spectre.Console authoring skill.

## Design docs

The connected design narrative lives in [`docs/governance-design/`](docs/governance-design/index.md):

- [The theory of the rule engine](docs/governance-design/rule-engine-theory.md) — the textbook; start here.
- [Goals and principles](docs/governance-design/principles.md) · [The inference kernel](docs/governance-design/kernel.md) · [The rule eDSL](docs/governance-design/rule-edsl.md)
- [Routing, severity, and run modes](docs/governance-design/routing-and-modes.md) · [Domain adapters](docs/governance-design/adapters.md) · [Spec-driven development in the system](docs/governance-design/speckit-in-the-system.md)
- [Theory and composition](docs/governance-design/theory-and-composition.md) · [Scope: planning and optimization](docs/governance-design/planning-and-optimization.md) · [Lessons and anti-goals](docs/governance-design/lessons.md)

Decisions are recorded as ADRs under [`docs/decisions/`](docs/decisions/), and onboarding
tutorials under [`docs/tutorials/`](docs/tutorials/).

## License

See [LICENSE](LICENSE).
