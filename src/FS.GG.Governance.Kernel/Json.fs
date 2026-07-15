namespace FS.GG.Governance.Kernel

// The kernel's JSON output layer (F06 · US1/US2/US4). Turns F03's Explanation proof tree,
// F06's ContractEntry list, and F05's EvidenceState / effective-state map into stable,
// portable JSON and parses it back without loss, using the net10.0 shared-framework
// System.Text.Json (Utf8JsonWriter to emit, JsonDocument to parse) — ZERO new dependency
// (FR-012, SC-009). Every emit is byte-for-byte deterministic (fixed key/array order;
// effective-map keys ordinal-sorted) and round-trips; serialization runs NO probe. No
// visibility modifiers — the surface is Json.fsi (Principle II).

open System
open System.IO
open System.Text
open System.Text.Json

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Json =

    // ── internal writer/reader plumbing (hidden — absent from Json.fsi) ──

    /// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string.
    /// Default Utf8JsonWriter options ⇒ no indentation ⇒ deterministic, compact output.
    let writeToString (emit: Utf8JsonWriter -> unit) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        emit writer
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    /// Fail-fast read of a required JSON string (kernel-emitted JSON never has null here);
    /// malformed/foreign JSON surfaces an explicit exception rather than a wrong value
    /// (Principle VI).
    let reqString (el: JsonElement) : string =
        match el.GetString() with
        | null -> failwith "Json: expected a JSON string but found null"
        | s -> s

    // ── Outcome / Verdict: shared tag-discriminated nested objects (data-model §3) ──

    let writeOutcome (w: Utf8JsonWriter) (o: Outcome) =
        w.WriteStartObject()

        match o with
        | Met -> w.WriteString("tag", "met")
        | Unmet r ->
            w.WriteString("tag", "unmet")
            w.WriteString("reason", r)
        | Unknown r ->
            w.WriteString("tag", "unknown")
            w.WriteString("reason", r)

        w.WriteEndObject()

    let readOutcome (el: JsonElement) : Outcome =
        match reqString (el.GetProperty "tag") with
        | "met" -> Met
        | "unmet" -> Unmet(reqString (el.GetProperty "reason"))
        | "unknown" -> Unknown(reqString (el.GetProperty "reason"))
        | t -> failwithf "Json: unknown outcome tag %A" t

    let writeVerdict (w: Utf8JsonWriter) (v: Verdict) =
        w.WriteStartObject()

        match v with
        | Pass -> w.WriteString("tag", "pass")
        | Fail r ->
            w.WriteString("tag", "fail")
            w.WriteString("reason", r)
        | Uncertain r ->
            w.WriteString("tag", "uncertain")
            w.WriteString("reason", r)

        w.WriteEndObject()

    let readVerdict (el: JsonElement) : Verdict =
        match reqString (el.GetProperty "tag") with
        | "pass" -> Pass
        | "fail" -> Fail(reqString (el.GetProperty "reason"))
        | "uncertain" -> Uncertain(reqString (el.GetProperty "reason"))
        | t -> failwithf "Json: unknown verdict tag %A" t

    // ── Explanation (F03 proof tree) — US1 ──

    let rec writeExplanation (w: Utf8JsonWriter) (e: Explanation) =
        // let rec: the idiomatic walk over the recursive Explanation tree (Principle III).
        w.WriteStartObject()

        match e with
        | AtomExplained(name, outcome, verdict) ->
            w.WriteString("kind", "atom")
            w.WriteString("name", name)
            w.WritePropertyName "outcome"
            writeOutcome w outcome
            w.WritePropertyName "verdict"
            writeVerdict w verdict
        | OpaqueExplained(name, outcome, verdict) ->
            // name + recorded outcome only — no function is ever serialized (FR-002).
            w.WriteString("kind", "opaque")
            w.WriteString("name", name)
            w.WritePropertyName "outcome"
            writeOutcome w outcome
            w.WritePropertyName "verdict"
            writeVerdict w verdict
        | AllExplained(parts, verdict) ->
            w.WriteString("kind", "all")
            w.WritePropertyName "parts"
            w.WriteStartArray()
            for p in parts do
                writeExplanation w p
            w.WriteEndArray()
            w.WritePropertyName "verdict"
            writeVerdict w verdict
        | AnyExplained(parts, verdict) ->
            w.WriteString("kind", "any")
            w.WritePropertyName "parts"
            w.WriteStartArray()
            for p in parts do
                writeExplanation w p
            w.WriteEndArray()
            w.WritePropertyName "verdict"
            writeVerdict w verdict
        | NotExplained(part, verdict) ->
            w.WriteString("kind", "not")
            w.WritePropertyName "part"
            writeExplanation w part
            w.WritePropertyName "verdict"
            writeVerdict w verdict
        | ImpliesExplained(antecedent, consequent, verdict) ->
            w.WriteString("kind", "implies")
            w.WritePropertyName "antecedent"
            writeExplanation w antecedent
            w.WritePropertyName "consequent"
            writeExplanation w consequent
            w.WritePropertyName "verdict"
            writeVerdict w verdict

        w.WriteEndObject()

    let rec readExplanation (el: JsonElement) : Explanation =
        let verdict () = readVerdict (el.GetProperty "verdict")

        match reqString (el.GetProperty "kind") with
        | "atom" -> AtomExplained(reqString (el.GetProperty "name"), readOutcome (el.GetProperty "outcome"), verdict ())
        | "opaque" -> OpaqueExplained(reqString (el.GetProperty "name"), readOutcome (el.GetProperty "outcome"), verdict ())
        | "all" -> AllExplained([ for p in el.GetProperty("parts").EnumerateArray() -> readExplanation p ], verdict ())
        | "any" -> AnyExplained([ for p in el.GetProperty("parts").EnumerateArray() -> readExplanation p ], verdict ())
        | "not" -> NotExplained(readExplanation (el.GetProperty "part"), verdict ())
        | "implies" ->
            ImpliesExplained(readExplanation (el.GetProperty "antecedent"), readExplanation (el.GetProperty "consequent"), verdict ())
        | k -> failwithf "Json: unknown explanation node kind %A" k

    let ofExplanation (explanation: Explanation) : string =
        writeToString (fun w -> writeExplanation w explanation)

    let toExplanation (json: string) : Explanation =
        use doc = JsonDocument.Parse json
        readExplanation doc.RootElement

    // ── Published contract (F06 ContractEntry list) — US2 ──

    let severityToken =
        function
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let tokenSeverity t =
        match t with
        | "advisory" -> Advisory
        | "blocking" -> Blocking
        | _ -> failwithf "Json: unknown severity token %A" t

    let writeContractEntry (w: Utf8JsonWriter) (e: ContractEntry) =
        w.WriteStartObject()
        let (RuleId id) = e.Id
        w.WriteString("id", id)
        w.WriteString("severity", severityToken e.Severity)
        w.WritePropertyName "spec"
        w.WriteStartObject()
        w.WriteString("document", e.Spec.Document)
        w.WriteString("section", e.Spec.Section)
        w.WriteEndObject()
        w.WriteString("statement", e.Statement)
        w.WriteEndObject()

    let readContractEntry (el: JsonElement) : ContractEntry =
        let spec = el.GetProperty "spec"

        { Id = RuleId(reqString (el.GetProperty "id"))
          Severity = tokenSeverity (reqString (el.GetProperty "severity"))
          Spec =
            { Document = reqString (spec.GetProperty "document")
              Section = reqString (spec.GetProperty "section") }
          Statement = reqString (el.GetProperty "statement") }

    let ofContract (contract: ContractEntry list) : string =
        writeToString (fun w ->
            w.WriteStartArray()
            for e in contract do
                writeContractEntry w e
            w.WriteEndArray())

    let toContract (json: string) : ContractEntry list =
        use doc = JsonDocument.Parse json
        [ for el in doc.RootElement.EnumerateArray() -> readContractEntry el ]

    // ── Evidence state (F05) — US4 ──

    // DIVERGENCE — DO NOT UNIFY: this kernel effective-state wire spelling is lowercase
    // (`pending`/`real`/`synthetic`/…) and deliberately DIFFERS from the Capitalized spelling
    // (`Pending`/`Real`/`Synthetic`/…) emitted by `EvidenceJson.stateToken` in the separate
    // `fsgg.evidence/v1` contract. The two are independent emit contracts, but the strings DIVERGE:
    // folding them into one shared `stateToken` would change one contract's bytes silently. This
    // lowercase spelling ALSO round-trips (`toEvidenceState`), so a casing change here breaks the
    // round-trip too. Pinned by Kernel.Tests/JsonTests; the Capitalized side by
    // EvidenceJson.Tests/ProjectionTests (cf. the `localOrCi`/`local-or-ci` divergence in JsonTokens).
    let stateToken =
        function
        | Pending -> "pending"
        | Real -> "real"
        | Synthetic -> "synthetic"
        | Failed -> "failed"
        | Skipped -> "skipped"
        | AutoSynthetic -> "autoSynthetic" // computed-only; its own visible token (FR-011)

    let tokenState t =
        match t with
        | "pending" -> Pending
        | "real" -> Real
        | "synthetic" -> Synthetic
        | "failed" -> Failed
        | "skipped" -> Skipped
        | "autoSynthetic" -> AutoSynthetic
        | _ -> failwithf "Json: unknown evidence-state token %A" t

    let ofEvidenceState (state: EvidenceState) : string =
        writeToString (fun w -> w.WriteStringValue(stateToken state))

    let toEvidenceState (json: string) : EvidenceState =
        use doc = JsonDocument.Parse json
        tokenState (reqString doc.RootElement)

    let ofEffective (project: 'id -> string) (states: Map<'id, EvidenceState>) : string =
        // Object keyed by the SUPPLIED projection of each node id (domain-neutral), keys
        // ordinal-sorted so output is byte-for-byte deterministic regardless of Map
        // ordering (FR-003/011/012, SC-002).
        let entries =
            states
            |> Map.toList
            |> List.map (fun (k, v) -> project k, v)
            |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))

        writeToString (fun w ->
            w.WriteStartObject()
            for key, state in entries do
                w.WriteString(key, stateToken state)
            w.WriteEndObject())

    let toEffective (json: string) : Map<string, EvidenceState> =
        use doc = JsonDocument.Parse json

        doc.RootElement.EnumerateObject()
        |> Seq.map (fun p -> p.Name, tokenState (reqString p.Value))
        |> Map.ofSeq
