# Phase 0 Research: `.fsgg` Project, Policy, Capability, and Tooling Schemas

All Technical-Context unknowns are resolved below. Each entry records the decision, why it
was chosen, and the alternatives rejected.

## D1 — Where the schemas live (project home)

**Decision**: A new optional class library `FS.GG.Governance.Config`, sibling to
Kernel/Host/adapters, depending only on YamlDotNet + FSharp.Core.

**Rationale**: The spec's Layering assumption names "Host or a light configuration library"
and requires the Kernel to receive only typed facts, never YAML or product vocabulary. A
dedicated library keeps the new YAML dependency isolated, keeps Host BCL-only, and lets the
typed facts be produced and tested without any kernel coupling. Later Phase-2 features (a
git/CI adapter, the router, the gate registry) bridge these config facts into kernel facts;
nothing in this feature needs the kernel.

**Alternatives rejected**:
- *Add to Host* — would pull YamlDotNet into Host (currently Kernel-only) and mix config
  parsing with the sense/plan/act loop; the spec explicitly offers the lighter library
  option.
- *Add to Kernel* — forbidden by the constitution (kernel is BCL-only; receives only typed
  facts).

## D2 — YAML parsing approach

**Decision**: Use **YamlDotNet** in **parse-to-node** mode only (`YamlStream` /
`YamlDocument` / `YamlNode`); hand-write the typed model and every strictness rule over the
node tree.

**Rationale**: The design doc explicitly recommends "YAML: YamlDotNet with strict FS.GG-owned
schemas." The user confirmed this approach during planning. Strictness is the whole point of
the feature (FR-006), and YamlDotNet's object-graph deserializer is lenient — it silently
ignores unknown fields and cannot give per-node locations for our diagnostics. Reading into a
generic node tree gives us full control: we walk the mapping nodes ourselves, reject unknown
keys, detect duplicate keys/ids, validate `schemaVersion`, normalize and bounds-check paths,
and resolve cross-references — each producing a located diagnostic.

**Alternatives rejected**:
- *YamlDotNet object binding (`Deserializer`)* — lenient by default; unknown fields and
  duplicate keys pass silently, defeating FR-006; poor location info for diagnostics.
- *Hand-rolled YAML parser* — keeps the repo zero-dependency but re-implements a fiddly spec
  (anchors, block/flow, quoting, comments) with real edge-case risk; disproportionate to the
  task. (Considered and declined by the user during planning.)

**Pin**: `YamlDotNet 16.3.0`, centrally in `Directory.Packages.props`; owner is the
`FS.GG.Governance.Config` maintainer.

## D3 — MVU/I-O boundary placement (Constitution Principle IV)

**Decision**: Keep the validation core a **pure total function**
(`Schema.validate : RawSource -> Validation`). Isolate the only I/O — reading the four
`.fsgg` files and distinguishing absent from present — at the edge in `Loader`, which takes an
injected `FileReader` port (a function value) and an interpreter that binds the real
filesystem. No full Elmish `Program`.

**Rationale**: Principle IV requires stateful/I-O workflows to expose a Model/Msg/Effect
boundary, but explicitly allows "for libraries, CLIs, and small tools … a local MVU/effect
algebra … when it preserves the same separation: `update` is pure, I/O is represented as data
or `Cmd<Msg>`, and interpretation happens only at the edge." Reading a fixed set of four files
has no multi-step state machine, no retries, no convergence loop; a full `Model`/`Msg`/
`update` would be ceremony that Principle III discourages. Representing the I/O as an injected
`FileReader` function and keeping all decision logic in the pure `validate` achieves the same
separation: every validation rule is testable as a pure function over fixture strings, and the
loader is testable over real directories. This mirrors the repo's existing `Loop`/`Interpreter`
split at a smaller scale.

**Alternatives rejected**:
- *Full Elmish `Program` with `Cmd`* — over-engineered for a four-file read; no package is
  warranted (the repo already avoids the Elmish package per Host research D2).
- *`validate` reads files itself* — would make the entire validation contract I/O-bound and
  un-pure, violating Principle IV and making determinism tests filesystem-dependent.

## D4 — Required vs optional files

**Decision**: `project.yml` and `capabilities.yml` are **required**; `policy.yml` and
`tooling.yml` are **optional but fully validated when present**.

**Rationale**: The spec states this assumption and flags it confirmable; the user confirmed it
during planning. `project.yml` carries identity and the governed root; `capabilities.yml`
carries the catalog that gives the rest of Phase 2 something to route over — neither is
meaningfully optional. Profiles (`policy.yml`) and tooling are declared-but-not-yet-enforced
in this feature, so a product can omit them and still produce useful typed facts. FR-015
requires the result to distinguish an *absent optional* file (fine) from a *present-but-
invalid* one (a diagnostic), so the loader records presence per file.

**Alternatives rejected**:
- *All four required* — forces every product to author policy/tooling even when defaults
  suffice; heavier than the MVP needs.
- *Only `project.yml` required* — lets a product omit the capability catalog, leaving the
  later router with nothing; weakens the MVP's "what is governed here?" answer.

## D5 — Path normalization rules

**Decision**: Normalize every declared path to a canonical relative-POSIX form before it
enters a typed fact: split on both `/` and `\`, drop `.` segments, resolve `..` segments,
collapse repeated separators, and treat the governed root as the boundary. A path whose `..`
segments escape the governed root is rejected with a path-normalization diagnostic. Case is
preserved but compared case-sensitively for determinism (paths are stored as authored after
separator/`.`/`..` normalization).

**Rationale**: FR-008 and the spec's "Path shape portability" edge case require the same
logical path to produce the same typed fact regardless of separator style or leading `./`,
and require rejection of paths that resolve outside the governed root. Normalizing to a single
canonical form makes typed facts byte-stable (SC-002) and makes glob precedence (a later
feature) well-defined. Pure string normalization (no `Path.GetFullPath`, which would leak the
host's absolute filesystem layout into the result) keeps the function deterministic and
host-independent.

**Alternatives rejected**:
- *`System.IO.Path.GetFullPath`* — resolves against the process CWD and host root, leaking
  absolute, non-deterministic, machine-specific paths into typed facts; forbidden by SC-002/
  SC-005.
- *Lowercasing for case-insensitive compare* — would make two genuinely distinct paths on a
  case-sensitive filesystem collide; we preserve case and document case-sensitive comparison.

## D6 — Surface classification (the five MVP classes)

**Decision**: Classify each declared surface into exactly one of five typed categories named
by the design: `Routine` (unmanaged/undeclared), `GovernedRoot`, `ProtectedSurface`
(package/API), `GeneratedView`, and `ReleaseSurface`. Classification is driven by an explicit
`kind` field on each surface declaration plus the governed-root declaration in `project.yml`;
it is not inferred from path heuristics. Routine is the absence of any declared surface match,
so undeclared files yield no protected-surface or governed-root fact (FR-011, SC-004, US3
scenario 3).

**Rationale**: FR-011 and SC-004 require each MVP surface class to be its own typed category
with a fixture that classifies into it. Driving classification from an explicit `kind`
(rather than guessing from extensions/paths) keeps the typed facts unambiguous and keeps
"light-by-default" intact — nothing becomes a protected surface without an explicit
declaration, matching the design's routing-safety policy.

**Alternatives rejected**:
- *Infer class from path/extension* — ambiguous, fragile, and would silently promote routine
  files; contradicts light-by-default.

## D7 — Diagnostic id taxonomy

**Decision**: A closed set of stable diagnostic ids, one per malformed class named in the spec
(SC-003): `unknownField`, `missingRequiredField`, `malformedValue`, `duplicateId`,
`missingSchemaVersion`, `malformedSchemaVersion`, `unsupportedSchemaVersion`,
`pathEscapesRoot`, `danglingReference`, `emptyFile`, and `missingRequiredFile`. Each
`Diagnostic` carries `Id`, `File`, a `Locator` (field path / id / line where available), and a
human-readable `Message` with a fix hint (FR-013). Diagnostics are emitted in a deterministic
order (by file, then locator, then id).

**Rationale**: FR-013 and SC-003 require every malformed-input class to map to its own
distinct, stable id with a locating reference and fix hint, and the set to be closed so tests
can assert one fixture per id. Splitting `schemaVersion` into missing/malformed/unsupported
matches the spec's three distinct acceptance scenarios (US2 scenario 3, "upgrade the tool"
edge case).

**Alternatives rejected**:
- *Free-text error strings* — not stable, not assertable, no machine id; violates FR-013.
- *One generic `parseError`* — collapses distinct, separately-testable failure classes;
  violates SC-003.

## D8 — Determinism & ordering strategy

**Decision**: Every emitted list (domains, path-map entries, surfaces, checks, commands,
environment classes, diagnostics) is sorted by a stable key (its declared id or normalized
path, ordinal comparison) before entering the typed facts, so re-ordering authored entries
cannot change the result (FR-012, SC-002). No wall-clock, environment, random, or
host-filesystem value enters the typed facts.

**Rationale**: FR-012/SC-002 require byte-identical results for identical trees and
order-independence under re-ordering. Sorting at emission time is the simplest guarantee and
is property-testable with FsCheck permutations. This mirrors the kernel's existing
ordinal-sorted JSON emission (`Json.ofEffective`).

**Alternatives rejected**:
- *Preserve authored order* — makes re-ordering observable, violating FR-012.
- *Sort only at JSON time* — leaves the in-memory typed facts order-sensitive; the spec's
  contract is the typed facts themselves, so they must be ordered.
