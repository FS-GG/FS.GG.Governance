# Phase 0 — Research & Engineering Decisions (F06 · 006-explanation-output)

All NEEDS CLARIFICATION from the Technical Context are resolved here. Each decision records
what was chosen, why, and the alternatives rejected. The spec leaves the serializer choice,
the JSON schema/field names, and the exact `.fsi` shapes to this plan (spec Assumptions); the
behaviours (mirror-shape, determinism, round-trip, drift-proof contract, inclusive freshness,
purity, domain-neutrality) are fixed by the spec.

## D1 — JSON engine: `System.Text.Json` low-level API, no `PackageReference`

**Decision.** Emit with `System.Text.Json.Utf8JsonWriter` and parse with
`System.Text.Json.JsonDocument` (DOM). Both are in the `net10.0` **shared framework**, so the
kernel adds **no** `PackageReference`. `System.Text.Json` is a `System.*` assembly, so the
existing V12 dependency-hygiene test (`name.StartsWith "System."`) passes unchanged — verified
by resolving `typeof<Utf8JsonWriter>.Assembly` (`System.Text.Json`).

**Rationale.** The spec's "JSON lives in the kernel because the runtime provides it" rationale
names `System.Text.Json` explicitly; using the shared-framework assembly keeps the kernel's
"light by default" / zero-heavy-dependency constraint (FR-012, SC-009) intact. The low-level
writer gives byte-exact control over key order and formatting (FR-003), and `JsonDocument`
gives a robust, total parser for round-trip (FR-004) without writing a tokenizer.

**Alternatives rejected.**
- *Newtonsoft.Json* — a new package dependency; violates FR-012/SC-009 outright.
- *Hand-rolled string builder + bespoke parser* — the emit side is easy, but a correct,
  total JSON *parser* (escapes, unicode, numbers) is exactly what `JsonDocument` already is;
  re-implementing it is needless risk for no dependency saving.
- *`JsonSerializer` reflective (de)serialization of the F# unions* — see D4; F# DUs do not
  round-trip cleanly under reflective STJ and key order is not guaranteed, breaking
  determinism and round-trip. Rejected in favour of explicit writer calls.

## D2 — Module decomposition: `Freshness`, `Contract`, `Json` (three `.fsi`/`.fs` pairs)

**Decision.** Three new public modules, mirroring the kernel's one-concern-per-module style
(`Verdict`, `Kernel`, `Evidence`, `Check`, `CheckRule`):
- `Freshness` — the pure freshness predicate (`Freshness` DU + `decide`/`isFresh`).
- `Contract` — the drift-proof fold (`ContractEntry` record + `ofRules`/`render`).
- `Json` — all serialization/parse pairs (explanation, contract, evidence state, effective map).

Compile order appended after `CheckRule.*`: `Freshness.*`, then `Contract.*`, then `Json.*`.

**Rationale.** Keeps serialization (the deferred concern the F03/F05 `.fsi` comments point at)
out of the value modules, exactly as those comments promised ("JSON serialization is deferred
to F06"). `Contract` must follow `CheckRule` (it references `CheckRule`/`Severity`/
`SpecSource`); `Json` must follow both `Check` (its `Explanation`), `Evidence` (its
`EvidenceState`), and `Contract` (its `ContractEntry`), so `Json` compiles last.

**Alternatives rejected.**
- *One mega `Output` module* — mixes three unrelated concerns (temporal predicate, rule fold,
  serialization) behind one surface; harder to test in isolation and read.
- *Co-locating each type's JSON in its own module* (`Check.toJson`, …) — would force F03/F05
  to take the `System.Text.Json` dependency and reverse the deliberate "defer to F06"
  decision recorded in their `.fsi`.

## D3 — Determinism strategy: fixed writer order, sorted map keys

**Decision.** Every object's members are written by **explicit ordered `Utf8JsonWriter`
calls** in a fixed code sequence; arrays preserve the value's structural order (proof-tree
`parts`, catalog order). The only place an unordered structure is serialized — the
effective-state `Map` — has its keys **ordinal-sorted on the projected id string** before
writing. `Utf8JsonWriter` is used with default (non-indented) options so output is compact and
stable. No `Dictionary`/`Map` is iterated in undefined order into the output.

**Rationale.** Byte-for-byte determinism (FR-003, SC-002) requires a single canonical
ordering. Code-fixed key order + structural array order + sorted map keys make output a pure
function of the value, diffable and archivable, and identical across any input permutation
that does not change the value's meaning (e.g. `Map` insertion order).

**Alternatives rejected.** Relying on `Map`/`Dictionary` enumeration order (not guaranteed
canonical for arbitrary `'id`); indented output (larger diffs, same determinism — no benefit).

## D4 — Round-trip representation: tag-discriminated objects

**Decision.** Each union is encoded as a JSON **object with a discriminator field plus its
payload**, and parse reconstructs the exact case:
- `Explanation` node: `{ "kind": "atom|opaque|all|any|not|implies", … }` — `atom`/`opaque`
  carry `name`, `outcome`, `verdict`; `all`/`any` carry `parts` (array) + `verdict`; `not`
  carries `part` + `verdict`; `implies` carries `antecedent`, `consequent`, `verdict`.
  `AtomExplained` vs `OpaqueExplained` stay **distinct** (different `kind`), so the round-trip
  is lossless (FR-004).
- `Outcome`: `{ "tag": "met" }` | `{ "tag": "unmet", "reason": … }` | `{ "tag": "unknown",
  "reason": … }`.
- `Verdict`: `{ "tag": "pass" }` | `{ "tag": "fail", "reason": … }` | `{ "tag": "uncertain",
  "reason": … }`.
- `EvidenceState`: a single JSON **string token** (D5).

`Json.toExplanation`/`toContract`/`toEvidenceState`/`toEffective` parse the kernel's own
emitted JSON back to a value **equal** to the original (FR-004, FR-007, FR-011, SC-003). Parse
is tolerant of object-key order (it reads fields by name) but emit is fixed (D3), so round-trip
holds at the value level for any kernel-emitted JSON.

**Rationale.** A discriminator + named payload is the standard lossless union encoding;
keeping `atom`/`opaque` distinct preserves the one piece of information a "shape-only" encoding
would drop. The nested `tag`/`reason` objects for `Outcome`/`Verdict` keep the reason string
exact (it is opaque free-text the algebra never interprets).

**Alternatives rejected.** Positional arrays (fragile, unreadable); collapsing
`atom`/`opaque` to one `kind` (loses the opacity bit — FR-002 requires the opaque node be
recognizable by name only); emitting the rolled-up verdict only at the root (the spec requires
a verdict at **every** node — FR-001).

## D5 — `EvidenceState` tokens: six distinct stable strings

**Decision.** Each of the six cases maps to a fixed lowercase token, emitted as a JSON string:
`Pending → "pending"`, `Real → "real"`, `Synthetic → "synthetic"`, `Failed → "failed"`,
`Skipped → "skipped"`, `AutoSynthetic → "autoSynthetic"`. `Json.ofEvidenceState` emits the
quoted token; `Json.toEvidenceState` maps it back; an unrecognized token fails fast. The
effective-state map serializes as a JSON object `{ "<projected-id>": "<token>", … }` with keys
ordinal-sorted (D3); `Json.toEffective` returns `Map<string, EvidenceState>` keyed by the
projected id (the projection is one-way, so parse recovers the projected map — the unit the
spec's independent test round-trips against).

**Rationale.** Distinct, stable tokens let a reader see "passed, but only on synthetic
evidence" (`autoSynthetic`) at a glance (FR-011) and round-trip each state losslessly. The
computed-only `AutoSynthetic` gets its own visible token — it is never silently merged with
`synthetic`.

**Alternatives rejected.** Numeric codes (opaque to a human reader); reusing the kernel.md
`[S*]` glyphs (not JSON-token-friendly, ambiguous).

## D6 — Freshness model: inclusive `recorded ≥ max(covered)` over comparable instants

**Decision.** `Freshness.decide (recorded: 'instant) (covered: 'instant list) : Freshness`
with `'instant : comparison`. `Fresh` iff `recorded` is **≥ every** instant in `covered`
(equivalently `recorded ≥ max covered`); `Stale` otherwise. The empty `covered` list is
`Fresh` (nothing to be stale against). The boundary is **inclusive** — `recorded = max
covered` is `Fresh` (FR-009). `isFresh` is the `bool` convenience. Instants are opaque
comparable values supplied by the caller — the kernel reads no clock (FR-010).

**Rationale.** This is the spec's simple causal model: evidence describes the artifact version
present when it was recorded; if any covered artifact changed afterward, the evidence describes
a version that no longer exists. Genericity over `'instant : comparison` keeps it
domain-neutral (a `DateTimeOffset`, a git commit time, a logical tick — all comparable) and
the kernel clock-free.

**Alternatives rejected.** An absolute max-age/TTL (`recorded` older than N ⇒ stale) — out of
scope for "simple freshness" per the spec Assumptions; a later refinement if needed. Reading
the clock or filesystem mtime — that is the F08 edge's job (FR-013).

## D7 — Surface re-bless + dependency hygiene unchanged

**Decision.** Re-bless `surface/FS.GG.Governance.Kernel.surface.txt` (V11) with
`BLESS_SURFACE=1 dotnet test` after the F06 types/modules land, so the new public surface is
captured in the committed baseline (FR-014). V12 (BCL/`System.*`-only) is **not** modified —
`System.Text.Json` already satisfies `name.StartsWith "System."`, so it passes with the new
JSON code in place and zero `PackageReference` added (SC-009).

**Rationale.** Principle II + the surface-drift discipline require the Tier-1 surface growth to
be reflected in the baseline; V12 staying green is the evidence that the "zero heavy
dependency" milestone constraint holds.

**Alternatives rejected.** Suppressing/relaxing V12 — unnecessary (STJ is already allowed) and
would weaken the very guarantee M1 advertises.

## D8 — M1 exit: pack the kernel to the local feed (tasks-level mechanics)

**Decision.** Completing F06 completes **M1**. The milestone exit action — packing
`FS.GG.Governance.Kernel` to `~/.local/share/nuget-local/` (flipping `IsPackable` for the
kernel, which is `false` by default from `Directory.Build.props`) — is a **Phase 2 task**
recorded in `tasks.md`, not part of this feature's code surface. The spec states packing is
the milestone's exit action and its mechanics are a plan/tasks concern, not a spec requirement.

**Rationale.** Keeps the feature's public-surface scope to the three modules while honouring
the milestone's exit obligation; the pack step changes packaging metadata, not behaviour, and
needs no new `.fsi`.

**Alternatives rejected.** Treating pack as a behavioural requirement of F06 (it is build
metadata, not kernel behaviour); deferring the milestone exit indefinitely (the spec ties it
to this feature).
