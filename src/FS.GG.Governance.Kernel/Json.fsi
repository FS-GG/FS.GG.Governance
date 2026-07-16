// Curated public signature contract for the kernel's JSON output layer (F06).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Json.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Json.fs body exists (Principle I). It turns the
// kernel's in-memory values — F03's `Explanation` proof tree, F06's `ContractEntry list`,
// and F05's `EvidenceState` / effective-state map — into stable, portable JSON, and parses
// that JSON back without loss.
//
// JSON lives in the kernel because the runtime provides it: this module uses
// `System.Text.Json` (`Utf8JsonWriter` to emit, `JsonDocument` to parse) from the net10.0
// shared framework, so it adds NO `PackageReference` and keeps the kernel BCL/`System.*`-only
// (FR-012, SC-009 — V12 stays green). Every emit is DETERMINISTIC — byte-for-byte identical
// for a given value, with fixed object-key and array order (effective-map keys ordinal-sorted
// on the projected id) (FR-003, SC-002) — and ROUND-TRIPS — parsing kernel-emitted JSON
// yields a value equal to the original (FR-004, FR-007, FR-011, SC-003). Serialization runs
// NO probe and emits NO un-inspectable function: an `Opaque`/`OpaqueExplained` node
// contributes its declared name and recorded outcome only (FR-002, SC-004). All output is
// domain-neutral; node identity for the effective-state map is rendered by a SUPPLIED
// projection (FR-012). Performs no I/O — persisting/printing the JSON is the F08/F12 edge's
// job (FR-013).
//
// THROWING CONTRACT (spec 110 B8). The four parsers — `toExplanation`, `toContract`,
// `toEvidenceState`, `toEffective` — are TOTAL over the kernel's own emitted JSON but
// FAIL FAST on malformed/foreign input rather than returning a wrong value (Principle VI):
// a caller feeding externally-sourced JSON MUST expect an exception, one of
//   • `System.Text.Json.JsonException` — syntactically invalid JSON (`JsonDocument.Parse`);
//   • `System.Collections.Generic.KeyNotFoundException` — a required property is absent
//     (`GetProperty`);
//   • `System.InvalidOperationException` — a value has the wrong JSON type: a non-string
//     read as a string (`GetString`), or a non-array/non-object walked as one
//     (`EnumerateArray`/`EnumerateObject`);
//   • `System.Exception` — an unrecognized closed-enum token/tag (kind, outcome/verdict tag,
//     severity, evidence-state), raised via `failwithf` with the offending token.
// The four emitters (`ofExplanation`/`ofContract`/`ofEvidenceState`/`ofEffective`) never
// throw for any well-typed value. Each reader's `THROWS:` line below points back here.

namespace FS.GG.Governance.Kernel

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Json =

    // ── Explanation (F03 proof tree) ──

    /// Serialize an `Explanation` to deterministic JSON that MIRRORS the proof tree's
    /// surface shape: each node is an object tagged by `kind` (`atom`/`opaque`/`all`/`any`/
    /// `not`/`implies`); atomic nodes record their probe `name` and met/unmet/unknown
    /// `outcome`; EVERY node carries its rolled-up `verdict` (the root's equals `Check.eval`
    /// over the same check and facts) (FR-001). Runs NO probe; an `OpaqueExplained` node is
    /// emitted by name + recorded outcome only, never its function (FR-002). Byte-for-byte
    /// deterministic (FR-003, SC-002).
    val ofExplanation: explanation: Explanation -> string

    /// Parse JSON emitted by `ofExplanation` back to an `Explanation` EQUAL to the original
    /// — no loss of structure, outcome, or verdict (`AtomExplained` and `OpaqueExplained`
    /// stay distinct) (FR-004, SC-003). Total over kernel-emitted JSON (Principle VI).
    /// THROWS on malformed/foreign input — see the module header's THROWING CONTRACT
    /// (`JsonException` / `KeyNotFoundException` / `InvalidOperationException` / `Exception`).
    val toExplanation: json: string -> Explanation

    // ── Published contract (F06 ContractEntry list) ──

    /// Serialize a contract to deterministic JSON — an array with one object per entry
    /// carrying `id`, `severity`, `spec` (`document`/`section`), and the rendered
    /// `statement`, in catalog order (FR-007). Byte-for-byte deterministic (FR-003);
    /// total over any contract including the empty one (FR-007, SC-006).
    val ofContract: contract: ContractEntry list -> string

    /// Parse JSON emitted by `ofContract` back to a `ContractEntry list` EQUAL to the
    /// original (FR-007, SC-003). Total over kernel-emitted JSON.
    /// THROWS on malformed/foreign input — see the module header's THROWING CONTRACT
    /// (`JsonException` / `KeyNotFoundException` / `InvalidOperationException` / `Exception`).
    val toContract: json: string -> ContractEntry list

    // ── Evidence state (F05) ──

    /// Serialize a single `EvidenceState` to its distinct, stable JSON string token — one
    /// of `"pending"`/`"real"`/`"synthetic"`/`"failed"`/`"skipped"`/`"autoSynthetic"`
    /// (the computed-only `AutoSynthetic` gets its own visible token, never merged with
    /// `synthetic`) (FR-011). Deterministic; round-trips via `toEvidenceState`.
    val ofEvidenceState: state: EvidenceState -> string

    /// Parse a JSON token emitted by `ofEvidenceState` back to its `EvidenceState`
    /// (FR-011, SC-003). THROWS on malformed/foreign input — an unrecognized token raises
    /// `System.Exception` (`failwithf`); a non-string JSON value raises
    /// `System.InvalidOperationException`. See the module header's THROWING CONTRACT.
    val toEvidenceState: json: string -> EvidenceState

    /// Serialize an effective-state map (node identity → effective `EvidenceState`, as
    /// produced by `Evidence.effective`) to deterministic JSON — a JSON object keyed by the
    /// SUPPLIED `project`ion of each node id (keeping output domain-neutral), with keys
    /// ordinal-sorted so the output is byte-for-byte deterministic regardless of the map's
    /// internal ordering (FR-003, FR-011, FR-012, SC-002). Every node's effective state —
    /// including any `AutoSynthetic` — is present.
    val ofEffective: project: ('id -> string) -> states: Map<'id, EvidenceState> -> string when 'id: comparison

    /// Parse JSON emitted by `ofEffective` back to a `Map<string, EvidenceState>` keyed by
    /// the PROJECTED node id (the projection is one-way, so the recovered map is keyed by
    /// the projected strings) — equal to the projected original map (FR-011, SC-003). Total
    /// over kernel-emitted JSON.
    /// THROWS on malformed/foreign input — see the module header's THROWING CONTRACT: a
    /// non-object root or non-string value raises `System.InvalidOperationException`, an
    /// unrecognized state token raises `System.Exception`, and syntactically invalid JSON
    /// raises `System.Text.Json.JsonException`.
    val toEffective: json: string -> Map<string, EvidenceState>
