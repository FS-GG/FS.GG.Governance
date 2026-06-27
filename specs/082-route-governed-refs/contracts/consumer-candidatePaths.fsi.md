# Contract: `Consumer.candidatePaths` (the new adapter surface)

**Module**: `FS.GG.Governance.Adapters.SddHandoff.Consumer`
**Classification**: Tier 1 — additive public surface (one `val` + one baseline line).

## FSI signature (to add to `Consumer.fsi`)

```fsharp
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Consumer =

    type ConsumeResult =
        { Gates: Gate list
          Selected: SelectedGate list
          Diagnostics: Diagnostic list }

    val consume: reads: Reader.HandoffRead list -> ConsumeResult

    /// The de-duplicated declared `governedReferences` paths from every CONSUMABLE document,
    /// projected as first-class routing candidates (F082). A host merges these into the candidate
    /// set fed to `Routing.route` BEFORE `Route.select`, so the surface a work item declares it
    /// governs drives gate selection (FR-001/FR-002).
    ///
    /// • A document `Reader.parse` REFUSES (malformed / missing-required / unsupported major /
    ///   declared-`autoSynthetic`) contributes NOTHING — consistent with `consume`'s bad-document
    ///   rule; the document's blocking integrity gate is produced by `consume`, not here (FR-008).
    /// • Paths are already normalized by `Reader.parse`, so they de-duplicate value-equally against
    ///   the sensed change set (FR-006).
    /// • Deterministic ordinal order; empty input — or no consumable `governedReferences` — ⇒ `[]`
    ///   (the no-op path that keeps every existing golden byte-identical — FR-005).
    ///
    /// PURE and TOTAL — never throws (Constitution VI).
    val candidatePaths: reads: Reader.HandoffRead list -> GovernedPath list
```

`open FS.GG.Governance.Config.Model` is already present in `Consumer.fsi` (it supplies
`GovernedPath` via the `Model` open chain); confirm `GovernedPath` is in scope and add the
`open` only if the surface check requires it.

## Reference implementation (for `Consumer.fs`)

```fsharp
let candidatePaths (reads: Reader.HandoffRead list) : GovernedPath list =
    reads
    |> List.choose (fun r ->
        match Reader.parse r with
        | Ok handoff -> Some handoff
        | Error _ -> None)                       // bad document ⇒ no candidates (FR-008)
    |> List.collect (fun h -> h.GovernedReferences |> List.collect (fun g -> g.Paths))
    |> List.distinct                              // dedup across work items / docs
    |> List.sortBy (fun (GovernedPath p) -> p)    // deterministic (route re-sorts anyway)
```

## Behavioral contract (test obligations)

| ID | Given | Expect |
|----|-------|--------|
| C1 | `[]` | `[]` |
| C2 | one consumable doc, no `governedReferences` | `[]` |
| C3 | one consumable doc declaring `src/A/x`, `tests/A/y` | `[GovernedPath "src/A/x"; GovernedPath "tests/A/y"]` (normalized, sorted) |
| C4 | two consumable docs declaring overlapping paths | union, de-duplicated (each path once) |
| C5 | one consumable + one malformed doc | only the consumable doc's paths (the malformed doc contributes none) |
| C6 | a single bad (version-mismatch) doc | `[]` |
| C7 | same path declared twice (one doc, two work items) | one entry |
| C8 | declared raw path `src\A\x` (back-slashes / un-normalized) | normalized to `src/A/x` (via `Reader.parse`) |

All cases are pure unit tests over hand-built `HandoffRead` JSON fixtures — no I/O, no mocks.
