# FS.GG.Governance

Optional rule, evidence, and route-explanation tooling for the
[FS-GG](https://github.com/FS-GG) projects, built as a normal F# tool product with
standard [Spec Kit](https://github.com/github/spec-kit).

**In one sentence:** governance is a *pure inference kernel* over typed facts and
rules, where every rule declares **who is competent to decide it** (machine, agent,
or human), every rule's check is **reified data** that can be evaluated, rendered,
hashed, and explained from one source, and enforcement is **light and advisory by
default** with a loud, local-only escape hatch.

The kernel is domain-neutral: what changes between governing F# code, an essay, or a
research project is the *fact vocabulary* — the inference, arbitration, evidence, and
rule language stay the same. See the [design overview](docs/governance-design/index.md).

## Operating rule

> Governance tooling may *inspect* rendering; rendering must never *require*
> governance tooling to build, test, document, package, or release.

Generic code here must not assume any consumer's package IDs, template names, target
names, or directory layout. Rendering is one external customer, not this tool's shape.

## Architecture

```text
FS.GG.Governance.Kernel   pure, BCL-only — the inference core (M1, done)
  ├─ facts · rules · fixed-point · provenance              (F01)
  ├─ verdicts + Kleene three-valued logic                  (F02)
  ├─ reified Check algebra + eval/render/hash/explain       (F03)
  ├─ CheckTier arbitration + rule bridge + review cache key  (F04)
  ├─ evidence model + synthetic-taint over a DAG            (F05)
  ├─ JSON explanation + evidence-freshness                  (F06)
  └─ routing: Stakes / Severity / RunMode / Route           (F07)

FS.GG.Governance.Host     effects shell (I/O) — sense → plan → act (F08, done)
FS.GG.Governance.Adapters.Spi      adapter SPI + lift/compose      (F09, done)
FS.GG.Governance.Adapters.SpecKit  first concrete adapter — Spec Kit as data (F10, done)
FS.GG.Governance.Adapters.DesignSystem  second adapter — a design language as data (F11, done)
FS.GG.Governance.Cli     optional route/explain/contract/evidence tool (F12, done)
FS.GG.Governance.Adapters.*             external validation          (F13, planned)
FS.GG.Governance.Config  optional `.fsgg` schema library — strict YAML → typed facts (F14, done)
FS.GG.Governance.Routing optional routing library — paths → capability domains, deterministic glob precedence (F15, done)
FS.GG.Governance.Snapshot optional sensing library — read-only git/CI → typed repository snapshot (F16, done)
FS.GG.Governance.Findings optional classifier — unknown governed/protected-boundary path findings (F17, done)
FS.GG.Governance.Gates    optional typed gate registry — declared checks → stable GateId metadata (F18, done)

Capability platform continuation (F16-F27, planned):
  .fsgg schemas, capability catalog, git/CI facts, gate registry,
  ship/verify/release JSON contracts, native SDD bootstrap, normalized work model,
  generated-view refresh, product/package/docs/skills/design checks,
  cost/freshness cache, command provenance, and human report projections.
```

The kernel is a pure, **zero-dependency** forward-chaining (Datalog-style,
stratified-monotonic) reasoner: `FixedPoint.evaluate identify rules supplied` returns
the least fixed point of the facts under the rules, with provenance for every derived
fact. All I/O lives at the edge in `Host` (functional core / imperative shell).

> **Kernel precondition (documented, not runtime-enforced).** Rules must be
> **monotonic** (add-only); negated or aggregated facts are *supplied* from a lower
> stratum, never derived in the same fixed point. See [the kernel](docs/governance-design/kernel.md).

## Status

| Milestone | Scope | State |
|---|---|---|
| **M1** | Pure kernel + evidence + explanation (F01–F06) | ✅ Reached |
| **M2** | Light routing + effects edge (F07–F08) | ✅ Reached |
| **M3** | Adapter SPI + two domains (F09–F11) | ✅ Reached — F09 (SPI) + F10 (Spec Kit) + F11 (design system) done |
| **M4** | CLI + external validation (F12–F13) | In progress — F12 CLI done |
| **M5** | Capability catalog + protected ship skeleton (F14–F17) | Planned |
| **M6** | Policy truth tables + native SDD model (F18–F20) | Planned |
| **M7** | Readiness + generated-view currency (F21–F22) | Planned |
| **M8** | Generated-product and surface-domain checks (F23–F24) | In progress — F23 done (capability catalog expanded to the full product-surface vocabulary; `capabilities.yml` → `schemaVersion: 2` with a documented migration; routing/classification + cost-tier selection + no-hide safety surfaced through `fsgg route`); F24 deterministic check libraries landed (four independent adapter rule packs — package `.fsi`-baseline drift + FSI transcripts, docs link/reference currency, skill path/task/mirror, design token/capture/contrast/control — over a shared `SurfaceChecks` core + `Dispatch` composition, each pure pack fenced behind one host sensor; advisory checks stay advisory; the additive `surfaceChecks` section is implemented in `VerifyJson`, byte-identical when empty). **F24 host wiring landed (`067`): `fsgg verify` now classifies → senses (read-only — no baseline write, no transcript exec) → runs the four packs → folds a blocking finding into the verdict via the existing `deriveEffectiveSeverity` → emits `surfaceChecks` in `verify.json`.** |
| **M9** | Cost/cache/provenance + release gates (F25–F26) | Planned |
| **M10** | Human projections over stable reports (F27) | Planned |

F01–F12 are implemented (CLI tests included). The kernel and CLI pack to
`~/.local/share/nuget-local/`; the `Host` effects edge depends on it (zero new dependency).
F10 is the **first concrete production adapter** — it governs this repository's own Spec Kit
workflow as data, supplying only its five SPI components and reusing 100% of the kernel
(pure: no I/O, no new dependency; depends on the F09 SPI, never the reverse). F11 is the
**second** — it governs adherence to a **design language** (Ant Design as the worked example)
from a fixture token tree, adopting the kernel **by difference**: it shares none of F10's shape
(no phase, no `whenPhase`, no merge fence, no dial), proving the SPI sits at the right altitude.
Its faithful lift is proven by composing it alongside the **real** F10 adapter at one root.
F12 adds the optional `fsgg-governance` .NET tool. It exposes `route`, `explain`,
`contract`, and `evidence`, keeps command orchestration behind a CLI MVU boundary,
and inspects governed roots read-only. Fresh agent reviews are cache-only by default;
nonzero `--review-budget` records an attempted dispatch but no fake passing verdict.
The 2026-06-18 capability-design report is now incorporated into the implementation
plan as F14-F27, with checkbox progress tracking for the protected ship gate,
native SDD flow, generated views, surface checks, release gates, and provenance work.
F14 adds the optional **`FS.GG.Governance.Config`** library: the source-of-truth schemas
for the four versioned `.fsgg` files (`governance.yml`, `policy.yml`, `capabilities.yml`,
`tooling.yml`). It parses them strictly (unknown fields, duplicate ids, `schemaVersion`
range, path escapes, and dangling cross-references are all stable, located diagnostics),
normalizes paths deterministically, classifies surfaces, and emits **typed, YAML-free,
product-neutral facts** for later Phase-2 features to consume — it never routes, senses
git/CI, or enforces. YamlDotNet is an isolated internal detail (parse-to-node only); the
kernel stays BCL-only. The YAML authoring contract is
[`fsgg-schema.md`](specs/014-fsgg-project-policy-capability-schemas/contracts/fsgg-schema.md).

F15 adds the optional **`FS.GG.Governance.Routing`** library — the **first consumer of the
F014 typed facts**. Given the typed facts (governed root, declared domains, and the
`glob → capability-domain` path map) and a caller-supplied set of normalized paths, its pure
`Routing.route` answers — deterministically and explainably — which capability domain each
path belongs to. When several globs match one path, exactly one wins by a **total, reproducible
precedence order** (exact-literal › greater literal specificity › single-segment `*` over
cross-segment `**` › ordinal tiebreak), and the result records which glob won and why; genuinely
co-specific competitors still resolve to one winner but also emit an `AmbiguousRoute`
diagnostic. It references only `FS.GG.Governance.Config`, adds **no** new dependency, performs
no I/O, and senses no git/CI. The glob syntax + precedence contract is
[`glob-precedence.md`](specs/015-path-capability-routing/contracts/glob-precedence.md).

F16 adds the optional **`FS.GG.Governance.Snapshot`** library — the **sensing counterpart to
F015 routing**. It runs **read-only** `git` against a real repository and returns a typed,
deterministic **repository snapshot**: the resolved diff range (base, head, merge base), the
committed changed-path set, the working-tree dirty/untracked sets, the current branch, optional
runner-supplied CI/PR context, command-run provenance digests, and any sensing diagnostics —
every path normalized into the **same `GovernedPath` form F015 routing consumes**, so the
snapshot's changed paths feed straight into `Routing.route` with no re-normalization. Impure git
sensing is isolated behind injected ports over a pure, total `assemble`; read-only is guaranteed
by construction (a closed read-only `GitCommand` set) and proven by a before/after byte-identity
check. It references only `FS.GG.Governance.Config`, adds **no** new dependency, and reaches **no
network** (CI context comes only from the environment, never a hosting-provider API). The
read-only command set, porcelain-parse rules, and range-resolution contract are
[`git-sensing.md`](specs/016-git-ci-snapshot-facts/contracts/git-sensing.md).

F17 adds the optional **`FS.GG.Governance.Findings`** library — the **natural consumer of both
F015 routing and F014 surfaces**. It turns the decision F015 deliberately deferred (its per-path
`UnmatchedInRoot` outcome, which "carries no domain and asserts no finding/severity") into an
explicit, typed **unknown-governed-path finding** — *without* global default-deny. A single pure,
total `Findings.findUnknownGovernedPaths` classifies each candidate path by the documented
precedence **`Protected > Routine > Ordinary`** ([`precedence.md`](specs/017-unknown-governed-path-findings/contracts/precedence.md)):
an `UnmatchedInRoot` path on a declared `ProtectedSurface` escalates to a self-identifying
`UnknownProtectedBoundaryPath` finding; one inside a declared `Routine` surface is suppressed; any
other in-root miss becomes an ordinary `UnknownGovernedPath` finding; `Routed` and `OutOfScope`
paths stay silent. Output is deterministic (ordinally sorted, cross-plane deduped, byte-identical
for identical input) and carries only normalized `GovernedPath`s, declared `SurfaceId`s, a zone,
and a fix-hint message. It references only `FS.GG.Governance.Config` and
`FS.GG.Governance.Routing`, adds **no** new dependency, and closes the two Phase-2 exit criteria
F015 left open ("Routine unclassified files do not trigger global default-deny" and "Unknown paths
under declared governed roots produce explicit findings").

F18 adds the optional **`FS.GG.Governance.Gates`** library — the Phase-2 **gate-identities** row. A
single pure, total `Gates.buildRegistry : TypedFacts -> GateRegistry` projects each already-validated
F014 capability `Check` into one typed `Gate` carrying a stable, **injective** `GateId`
(`"<domain>:<checkId>"`) and the *Gate identities* field set: domain, a declared-id-only description,
declared `RequiresCommand` prerequisites, cost, a bounded command-or-default timeout (`defaultTimeout`
= five minutes when no command is referenced or `tooling.yml` is absent), owner, maturity (carried
verbatim — enforcement is Phase 5), a product-check flag (`true` iff `Check.Environment = Release`, the
MVP heuristic), and a carried `FreshnessKey` (declared inputs only — *carried, never evaluated*: no
clock, no verdict, no cache). The registry is `GateId`-ordinal sorted and byte-identical for identical
input, unchanged under input re-ordering. There is **no diagnostics layer** — F014 already proved the
facts consistent, so the registry *preserves* unique ids / resolvable prerequisites / total assembly by
construction and *proves* them with FsCheck (research D4). It references **only**
`FS.GG.Governance.Config` (no Routing edge), adds **no** new dependency, and selects, runs, and enforces
nothing — it establishes the stable gate identities the remaining Phase-2 rows (`fsgg route`/`fsgg ship`,
route/audit JSON, `.fsgg/gates.json`) and Phase 5/11 consume. Phase-10 deferrals: gate-to-gate
prerequisites + topological order, and a richer product-check derivation.

## Building & testing

Build and test the **whole solution** through the checked-in wrapper:

```bash
dotnet fsi build.fsx          # build all 162 projects of FS.GG.Governance.sln
dotnet fsi build.fsx test     # run the full test suite (the delivery gate)
```

The wrapper exists because a plain `dotnet build FS.GG.Governance.sln` over-subscribes
the build: with no shared F# compiler server, MSBuild's default one-node-per-core fan-out
launches a `dotnet fsc` process per node across 162 projects and thrashes
(`MSB6003`/`MSB6006`) instead of finishing. `build.fsx` bounds the MSBuild node count with
an explicit, hardware-derived `-m:N` on the command line — the only place the bound is
honored — turning a >20-minute failing build into ~33 s. `N` scales with the machine
(`clamp(2, ceil(cores/4), 12)`), so it is faster on bigger hosts yet never over-subscribes;
the wrapper prints the detected core count, the chosen `N`, and elapsed time on every run.
Any `dotnet` arguments after the verb pass through (e.g. `dotnet fsi build.fsx build -c Release --no-restore`).

## CLI

Build and run from source:

```bash
dotnet build src/FS.GG.Governance.Cli
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root . --json --review-budget 0
```

Install from the local feed:

```bash
dotnet pack src/FS.GG.Governance.Cli -c Release -o ~/.local/share/nuget-local
dotnet tool install FS.GG.Governance.Cli --tool-path .tmp/f12-tool --add-source ~/.local/share/nuget-local
.tmp/f12-tool/fsgg-governance route --root . --mode inner
```

### Render modes (F27)

Every command emits one of three renderings of the **same** report object; **JSON is the only contract**,
plain and rich are non-contractual presentations:

| Requested / sensed state | Mode | Output |
|---|---|---|
| `--json` / `--format json` | **JSON** | the stable, byte-identical machine contract (always wins, never colored) |
| interactive TTY, color on, no `--plain` | **Rich** | Spectre color banner + grouped tables |
| non-TTY / piped / `NO_COLOR` / `--plain` (`--no-color`) | **Plain** | the exact ANSI-free `HumanText` projection |

`--plain` (alias `--no-color`) forces ANSI-free output even on a TTY; it never changes `--json`/`--format`
meaning, and JSON always overrides it.

Two read-only interactive surfaces navigate a freshly composed route report (they write **no** artifact and
carry **no** JSON contract):

```bash
fsgg-governance watch --root .   # debounced read-only re-render on working-tree change (q to quit)
fsgg-governance tui   --root .   # navigate the route report (arrows/hjkl move/expand, q to quit)
```

The packed `fsgg` (route-only tool) also accepts `fsgg route --watch`/`--plain`. The generic spellings
`fsgg watch` / `fsgg tui` resolve to `fsgg-governance` until a future single-tool unification.

## Design lineage

The checker paradigm follows [Cedar](https://cedarpolicy.com/en) (and OPA/Rego):
**policy as analyzable data**, **forbid-trumps-permit** order-independent precedence
(the F07 routing layer), and decisions that are **explainable by construction**.
Planning and optimization are deliberately *not* native — the kernel checks a
planner's outputs at the edge rather than being one. It is **not** Cedar and does not
depend on it; Cedar is a reference for the evaluation semantics. See
[theory & composition](docs/governance-design/theory-and-composition.md) and
[scope: planning & optimization](docs/governance-design/planning-and-optimization.md).

## Design & plans

- [Design overview](docs/governance-design/index.md) — start here; the comprehensive design
  - [The theory of the rule engine](docs/governance-design/rule-engine-theory.md) — the connected, textbook story
  - [Goals & principles](docs/governance-design/principles.md) · [the kernel](docs/governance-design/kernel.md) · [the rule eDSL](docs/governance-design/rule-edsl.md)
  - [Routing, severity & run modes](docs/governance-design/routing-and-modes.md) · [domain adapters](docs/governance-design/adapters.md) · [Spec-driven development in the system](docs/governance-design/speckit-in-the-system.md)
- [Implementation plan (Spec Kit, F01–F27)](docs/2026-06-18-governance-kernel-speckit-implementation-plan.md) — the design and capability report decomposed into ordered features with progress checkboxes
- [Capability design report](docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md) — product-neutral capability envelope and protected-boundary roadmap
- [CI: GitHub Actions branch-protection guidance for `fsgg ship`](docs/ci/github-actions-branch-protection.md) — wire the merge verdict into a required status check
- [Golden enforcement fixtures](fixtures/enforcement/) — the committed, drift-guarded enforcement [truth table](fixtures/enforcement/truth-table.md) (the full base × maturity × run-mode × profile cross-product) and the blocking-altering [`audit.json` snapshots](fixtures/enforcement/audit-snapshots/). Regenerate after an intended core change with `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests`.
- [Feature specs](specs/) · [decision records](docs/decisions/)

## Workflow

Standard Spec Kit with the
[`fsharp-opinionated`](https://github.com/EHotwagner/speckit-fsharp-tooling) preset:
specify → plan → tasks → implement, via the `/speckit-*` skills. Visibility lives in
`.fsi` signatures with per-module surface-area baselines; there is no evidence-audit
or DAG-validation machinery — see the
[constitution](.specify/memory/constitution.md).

## License

[MIT](LICENSE)
