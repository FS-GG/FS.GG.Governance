# Phase 0 Research: Git/CI Snapshot Facts for the Repository Boundary

All Technical-Context unknowns are resolved below. Each entry records the decision, why it was
chosen, and the alternatives rejected.

## D1 — Where snapshot sensing lives (project home)

**Decision**: A new optional, packable class library `FS.GG.Governance.Snapshot`, sibling to
Kernel/Host/adapters/Config/Routing, referencing only `FS.GG.Governance.Config` and adding **no**
third-party dependency (read-only git via BCL `System.Diagnostics.Process`; CI context via BCL
`System.Environment`).

**Rationale**: Snapshot sensing is a distinct concern from both F014 ("YAML → typed facts") and F015
("use the facts to route paths"). It *produces* the candidate-path set F015 only consumes. A dedicated
library keeps the dependency direction one-way (Snapshot → Config), keeps the kernel and the F014 core
clean of git/process code, and lets the later `route`/`ship` commands (in Cli) reference the snapshot
without pulling in the F08 Host review-loop surface. The constitution explicitly prescribes this shape:
git and filesystem scanning are forbidden *in the core rule/evidence library* but "layer on top in
separate projects" — `FS.GG.Governance.Snapshot` is that layered project.

**Alternatives rejected**:
- *Extend `FS.GG.Governance.Host`* — Host is the F08 sense→plan→act review loop, generic over the
  kernel's `'fact` and tied to the kernel's fact/rule machinery. Snapshot sensing references Config
  (`GovernedPath`), not the kernel, and serves a different consumer (the route/ship commands). Folding
  it into Host would make Host reference Config and grow a second, unrelated boundary. The
  kernel-speckit roadmap's "Host process-runner facade" note predates the F014/F015 split into separate
  libraries; the emergent pattern (one library per concern) is the better fit.
- *Add to `FS.GG.Governance.Config`* — would mix "turn YAML into facts" with "run git," and force every
  Config consumer (including pure Routing) to carry a process-execution surface. Config's job is the
  typed facts; sensing is a separate, separately-packable use.
- *Add to the Kernel* — forbidden by the constitution (kernel is BCL-only and product-neutral; no git,
  no process, no filesystem scanning).

## D2 — Driving read-only git (no new dependency)

**Decision**: Run git through BCL `System.Diagnostics.Process` (a thin edge runner), capturing stdout
on success and reifying any nonzero exit / thrown exception as an `Error`. **No** libgit2sharp or any
git-binding package is added.

**Rationale**: The needed operations are a handful of plumbing/porcelain reads (D5). `Process` over the
system `git` is the plainest dependency-free way to get them, keeps the no-new-package posture (the
core stays BCL-only; this layered library stays third-party-free), and matches how a human or CI senses
the same facts. Using the real `git` also means the edge tests exercise the actual tool against a real
fixture repo (Principle V).

**Alternatives rejected**:
- *LibGit2Sharp* — a heavy native-bundled third-party dependency for what amounts to six read commands;
  contradicts the no-new-dependency posture and pins a native lib per RID.
- *Re-implement git plumbing over `.git`* — re-deriving diff/merge-base/status from object and index
  files is large, brittle, and exactly the kind of complexity Principle III discourages when the
  system `git` already answers correctly.

## D3 — The I/O boundary shape: injected-port edge over a pure `assemble` (not full Model/Msg/Effect)

**Decision**: Mirror the **F014 Loader** boundary, not the **F08 review loop**. A thin edge
(`Interpreter.senseSnapshot`) gathers all raw sensed inputs through injected ports
(`GitPort`/`CiPort`) into a `RawSensing` record, then a **pure, total** `Snapshot.assemble` parses,
normalizes, categorizes, orders, and diagnoses them into the `RepoSnapshot`. The only pure "planning"
of which refs to resolve is a separate pure `planResolution : SnapshotOptions -> ResolutionPlan`.

**Rationale**: Principle IV requires I/O modeled as data behind a pure core with edge interpretation,
and *explicitly permits* a local port/effect algebra in place of the Elmish `Program` / full
`Model`-`Msg`-`Effect`-`update` loop "when it preserves the same separation." Git sensing is fixed
request/response **gather** — resolve refs, compute merge base, diff, status, ls-files, branch, read CI
env — with a mild linear dependency (merge base needs resolved refs) but **no** multi-step convergence,
retry, dynamic effect emission, cache-hit short-circuiting, or user interaction. That is structurally
F014's "read a fixed set of inputs, then validate purely," for which F014's Loader deliberately chose
the lighter algebra and the constitution blessed it (Principle III/IV). Reaching for Model/Msg/Effect
here would be ceremony without payoff. The pure/impure split is still clean and fully honors Principle
IV: `planResolution`/`assemble`/parsers are pure and total and carry the bug-prone logic under literal
fixtures; the edge is a thin, real-git-tested shell that never throws.

**Alternatives rejected**:
- *Full F08-style Model/Msg/Effect/update + interpreter loop* — there is no evolving durable model
  across messages, no convergence, and no result-driven branching that would justify the extra surface;
  Principle III says prefer the lighter boundary and justify the heavier, and the heavier earns
  nothing here.
- *No boundary (call git inline in one function)* — would bury parsing/normalization behind real git,
  make the deterministic logic untestable without a repo, and violate Principle IV's pure-core /
  edge-interpretation separation.

## D4 — Pure parsing of git output (the edge returns raw text)

**Decision**: The `GitPort` returns **raw stdout text** for each command; the **pure core parses** it
(`--name-status -z` for the diff, `status --porcelain=v1 -z` for dirty+untracked, NUL-delimited). The
edge does no interpretation beyond running the command and capturing bytes.

**Rationale**: Parsing porcelain output — status letters, rename old/new pairs, the NUL framing, quoted
or non-ASCII paths — is the bug-prone part and must be unit-tested with explicit literal fixtures, with
no repo required (Principle V favors real evidence, but the *parser* is a pure string function best
tested with hand-written ugly literals). Keeping the edge a thin "run command → bytes" shell minimizes
the untested impure surface, exactly as F014 kept `Schema.validate` pure and `Loader` thin. Using the
`-z` (NUL-terminated) machine formats avoids git's path-quoting ambiguity at the source.

**Alternatives rejected**:
- *Typed port that pre-parses (e.g. returns `ChangedEntry list`)* — pushes parsing into the impure,
  repo-only-testable edge, which is precisely the logic we want pure and literal-fixture-tested.
- *Default (non-`-z`) porcelain* — git quotes "unusual" paths and uses newline framing, reintroducing
  the quoting ambiguity FR-012 wants eliminated; `-z` gives raw bytes and unambiguous framing.

## D5 — The closed, read-only git command set (read-only by construction)

**Decision**: The edge can issue only a **closed** set of read-only git subcommands, modeled as a
`GitCommand` DU so no mutating command is representable:

| `GitCommand` | git invocation (read-only) | purpose |
|---|---|---|
| `RevParse ref` | `git rev-parse --verify <ref>^{commit}` | resolve a ref to a commit id; unknown ref → `Error` → `UnknownRef` |
| `MergeBase (a,b)` | `git merge-base <a> <b>` | the three-dot base for the diff range |
| `DiffNameStatus (base,head)` | `git diff --name-status -z -M <base> <head>` | committed changed paths + status letters (rename-aware) |
| `StatusPorcelain` | `git status --porcelain=v1 -z` | tracked-modified (dirty) + untracked, in one read |
| `CurrentBranch` | `git rev-parse --abbrev-ref HEAD` | branch name; `HEAD` literal ⇒ detached ⇒ `None` |
| `RepoCheck` | `git rev-parse --is-inside-work-tree` | not-a-repo detection → `NotARepository` |

**Rationale**: FR-006 requires read-only behavior; making the command set a closed DU of read-only
invocations makes "read-only" a *type-level* guarantee, not a discipline — there is no `add`, `commit`,
`checkout`, `reset`, or `config --set` to call. `status --porcelain` yields both dirty and untracked in
one pass (untracked appear as `??`), so a separate `ls-files --others` is unnecessary. `-M` enables
rename detection so FR-012's rename rule is expressible. A before/after byte-identity test on a fixture
repo confirms read-only empirically (SC-005).

**Alternatives rejected**:
- *An open `string -> Result<string,string>` port* — would let any git command (including mutating
  ones) be issued, losing the by-construction read-only guarantee and widening the audit surface.
- *Separate `ls-files --others` for untracked* — redundant: `status --porcelain` already reports
  untracked as `??`, and one command keeps the snapshot atomic and ordering deterministic.

## D6 — Path representation: repo-relative `GovernedPath`; routing classifies in/out of root

**Decision**: Changed/dirty/untracked paths are emitted as **repo-relative** `GovernedPath`s in F014's
normalized form. The snapshot does **not** filter or classify by the governed root; the governed-root
**segment-prefix** in-root/out-of-scope decision is `Routing.route`'s job (F015). Out-of-root paths are
represented, never dropped (FR-002).

**Rationale**: F015's `Routing.route` already classifies `OutOfScope` by a segment-prefix test of each
candidate `GovernedPath` against `ProjectFacts.GovernedRoot` (see `015.../contracts/Routing.fsi`). git
emits repo-relative forward-slash paths natively, which is exactly the comparison space routing uses.
So the snapshot's one job is to normalize git's repo-relative paths into the identical `GovernedPath`
form and hand them over; routing decides scope. This keeps the two features cleanly separated, honors
"out-of-root paths are represented, not dropped," and makes the snapshot independent of the governed
root for *normalization* (it only needs the repo working directory to run git). The governed root is
carried through the snapshot for downstream convenience but is not used to drop or categorize paths in
this feature.

**Alternatives rejected**:
- *Normalize relative to the governed root (root-relative paths)* — would force out-of-root paths to
  carry `../`, which F014 normalization resolves away, and would duplicate routing's scope decision in
  the wrong layer.
- *Drop out-of-root paths in the snapshot* — violates FR-002 ("represented, not dropped") and would
  hide a real change from any downstream consumer that cares about it.

## D7 — Single-sourcing normalization: expose F014's normalizer from `Config`

**Decision**: Expose F014's path normalization as a **public `Config` function** (a `val` on
`Config.Model`, e.g. `normalizePath : raw:string -> GovernedPath`) and reuse it in Snapshot, rather
than re-implementing normalization. This is a small additive Tier-1 change to `Config`'s `.fsi` +
surface baseline.

**Rationale**: SC-001 and FR-002 require the snapshot's `GovernedPath`s to be **byte-identical** to
what routing consumes, "with no further normalization." The only way to guarantee that without drift is
a single source of truth. Today F014's normalization (separators unified, `.`/`..` resolved, leading
`./` stripped, repo-relative) lives inside the private `Schema.validate`; extracting it into a public,
pure `val` is a minor, well-scoped refactor that benefits both features and is exactly the kind of
shared primitive an `.fsi` should expose. The change is additive (no existing signature changes), so it
is low-risk; the Config surface baseline is re-blessed in the same change (Principle II).

**Alternatives rejected**:
- *Re-implement normalization inside Snapshot* — risks subtle divergence from F014 (e.g. case or
  trailing-slash handling), which would silently break routing's prefix test and glob matching; the
  spec explicitly warns against re-deciding normalization F014 already settled.
- *Leave normalization private and copy the code* — same drift risk plus duplicated logic with two
  maintenance points.

## D8 — Range resolution: pure plan + edge git, against the merge base by default

**Decision**: Split range resolution into a **pure** `planResolution : SnapshotOptions ->
ResolutionPlan` (which refs are base/head, whether to compute a merge base, the documented default when
no options are given) and an **edge** step that resolves those refs and the merge base via git. The
committed diff is computed **against the merge base** by default (three-dot semantics), so unrelated
upstream commits on a stale base branch are not reported (FR-004).

**Rationale**: The *policy* of which option form means what (US3: `--since <rev>` → base=`<rev>`,
head=working position; `--base`/`--head` → those refs; default → documented base vs current) is pure
and must be unit-tested without git. The *resolution* of a ref name to a commit and the merge-base
computation inherently need git and live at the edge. Three-dot (merge-base) diff is the standard
"what did this branch change" semantics and gives local/CI parity (SC-004); two-dot would report
upstream drift as if it were this change's work.

**Alternatives rejected**:
- *Two-dot diff (`base..head` direct)* — reports commits that landed on `base` after the branch point
  as part of the change, breaking base/head parity and the "what this branch changed" intent.
- *Resolve ranges entirely at the edge (no pure plan)* — would make the option-form contract
  (the heart of US3) testable only against a real repo, hiding a pure decision behind I/O.

## D9 — CI/PR context: runner-environment only, no network; nondeterminism kept out of facts

**Decision**: PR labels, required status-check identities, the branch (also derivable from git), and a
**closed** CI environment classification are read **only** from runner-provided environment (a
`CiPort` over `System.Environment`), returning `None`/absent fields when unavailable — never fabricated.
**No hosting-provider API is called.** Nondeterministic sensing output (raw stdout/stderr, timings,
process ids, absolute paths) never enters the deterministic facts; retained git-command provenance is a
stable `CommandRunDigest` kept separate from the path facts (FR-010).

**Rationale**: Querying a provider for live status checks would add a network dependency and a
non-reproducible test oracle, both of which the constitution and Host F08 SC-009 reject. Runner
environment (e.g. CI-provided variables) is already present, cheap, and deterministic for a given
environment, and optional-by-absence modeling keeps "unavailable" honest (FR-005). Keeping raw output
out of the facts and isolating provenance as a digest preserves byte-stable snapshots (SC-002/SC-006)
while leaving a later phase (Phase 11 freshness keys / evidence cache) room to consume the digests.
Live provider integration is a deliberate later deferral.

**Alternatives rejected**:
- *Query the hosting API for labels/status in this feature* — introduces network I/O, a non-reproducible
  oracle, and a provider coupling, contradicting the no-network stance and SC-007.
- *Embed raw command output / timing in the snapshot for "completeness"* — destroys byte-stability and
  leaks host-specific values (FR-010); the digest is the deterministic substitute.

## Resolved unknowns summary

| Technical-Context item | Resolution |
|---|---|
| Project home | New `FS.GG.Governance.Snapshot` lib → Config only; no new package (D1) |
| Git driver | BCL `System.Diagnostics.Process`; no git-binding package (D2) |
| Boundary shape | Injected-port edge + pure `assemble`; lighter F014-Loader algebra, justified (D3) |
| Output parsing | Edge returns raw `-z` text; pure core parses (D4) |
| Read-only guarantee | Closed `GitCommand` DU of read-only subcommands (D5) |
| Path form | Repo-relative `GovernedPath`; routing classifies scope (D6) |
| Normalization | Single-sourced via a new public `Config` normalization `val` (D7) |
| Range resolution | Pure `planResolution` + edge git; merge-base (three-dot) default (D8) |
| CI/PR context & determinism | Runner-env only, no network; provenance as separate digest (D9) |

No `[NEEDS CLARIFICATION]` markers remain.
