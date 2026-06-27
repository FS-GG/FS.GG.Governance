# Contract: VerifyJson projection seam modules (4 new public seams + unchanged entry)

The NEW additively-public modules split out of `VerifyJson.fs`, each `.fsi`-curated and
compiled **before** `VerifyJson.fs`. All writers walk a caller-owned `Utf8JsonWriter`
and append in the same order as today ⇒ byte-identical JSON. Each seam's `.fsi` exposes
ONLY the entry the composing module (or a sibling seam) calls; token helpers, `rr*`,
and per-writer plumbing stay absent (⇒ private).

> `w: Utf8JsonWriter` throughout. Domain types as in `VerifyJson.fsi` today.

## `FS.GG.Governance.VerifyJson.Core`

Owns the verdict core: `verdictToken`, `dispositionToken` (the local hyphenated
`not-executed` divergence — stays here, NOT re-unified with `JsonTokens`, FR-009),
`writeCauseValue`, `writeEnforcement`, `writeCache`, `writeExecution`, `writeItem`,
`writeSection`, `gateItemIds`, `writeCurrency`, `writeCore`. Curated `.fsi` exposes the
orchestrator the entry calls (and any writer a sibling seam reuses):

```fsharp
/// The shared verdict body the four entry points all begin with — schemaVersion, verdict,
/// exitCodeBasis, blockers/warnings/passing, currency — appended in the fixed wire order.
val writeCore:
    w: Utf8JsonWriter ->
    decision: ShipDecision ->
    cache: CacheEligibilityReport option ->
    execution: (GateId * GateOutcome) list ->
        unit
```

## `FS.GG.Governance.VerifyJson.SurfaceChecks`

```fsharp
val writeSurfaceFinding: w: Utf8JsonWriter -> f: SurfaceFinding -> unit
```

## `FS.GG.Governance.VerifyJson.ReleaseReadiness`

Owns `rr*` + `writePackProject`/`writePackageEvidence`/`writeVersionPolicy`/
`writeAttestationRef` (all private) and the one public entry:

```fsharp
val writeReleaseReadiness: w: Utf8JsonWriter -> preview: VerifyReleasePreview -> unit
```

## `FS.GG.Governance.VerifyJson.GeneratedViews`

```fsharp
val writeGeneratedViews:
    w: Utf8JsonWriter ->
    views: (CurrencyFinding * EnforcementDecision) list ->
        unit
```

## `FS.GG.Governance.VerifyJson` (thin composing entry — UNCHANGED `.fsi`)

`VerifyJson.fsi` is **byte-identical** to baseline (FR-003/FR-004): `schemaVersion` +
the four entry points with identical names and signatures. Bodies become thin
compositions, e.g.:

```fsharp
let ofVerifyDecisionWithGeneratedViews decision cache execution findings preview generatedViews =
    JsonText.writeToString (fun w ->
        w.WriteStartObject()
        Core.writeCore w decision cache execution
        if not (List.isEmpty findings) then (* surfaceChecks array via SurfaceChecks.writeSurfaceFinding *) ...
        match preview with Some p -> ReleaseReadiness.writeReleaseReadiness w p | None -> ()
        if not (List.isEmpty generatedViews) then GeneratedViews.writeGeneratedViews w generatedViews
        w.WriteEndObject())
```

The exact start/end-object placement and additive-field guards are copied verbatim from
today's bodies — the composition reproduces the current byte stream.

## Invariants

- `VerifyJson.fsi` byte-identical; the four entry points keep identical signatures
  (FR-003) — surface baseline's existing `VerifyJsonModule` block unchanged.
- The four seams add exactly four `…Module` types to the re-blessed
  `surface/FS.GG.Governance.VerifyJson.surface.txt`; no existing line changes.
- Every `VerifyJson.Tests` golden + determinism fixture stays byte-identical (FR-005,
  SC-002).
- No new `ProjectReference`; library stays System.*/FSharp.Core-only (FR-007).
