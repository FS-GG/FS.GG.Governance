# Contract: host wiring on `route` / `ship` / `verify` (additive, Tier 1)

The handoff must affect all three verdict hosts (FR-008). Each gains the **same** additive
edge, mirroring how every other sensed input (scope, freshness, provenance, view currency,
release, surfaces) is wired: a port + effect + msg + a pure fold (research D6). The pure work
lives in `Consumer.consume`; only file location + read is impure.

Applies to `FS.GG.Governance.RouteCommand`, `FS.GG.Governance.ShipCommand`,
`FS.GG.Governance.VerifyCommand`. Each change is **additive** to the public `Loop.fsi` /
`Interpreter.fsi`, re-blessing the corresponding surface baseline additively.

## `Interpreter.Ports` — new field (the only I/O)

```fsharp
type Ports =
    { // … existing fields unchanged …
      /// Locate readiness/<id>/governance-handoff.json under `repo` in stable <id> order and
      /// read each one's raw JSON. Returns [] when none present (the no-op path). Impure edge only.
      Handoffs: string -> Reader.HandoffRead list }
```

## `Loop` — new effect + msg (additive)

```fsharp
type Effect =
    | // … existing …
    | LoadHandoffs of repo: string

type Msg =
    | // … existing …
    | HandoffsLoaded of Reader.HandoffRead list
```

## `update` — pure fold (the verdict seam)

After the existing `Route.select` step and on `HandoffsLoaded reads`:

1. `let consumed = Consumer.consume reads`
2. union `consumed.Gates` into the `GateRegistry` (so they appear in `gates.json` and are
   severity-resolvable) and `consumed.Selected` into `RouteResult.SelectedGates`;
3. carry `consumed.Diagnostics` into the summary/output.

**route**: the unioned registry + selection flow into the existing `gates.json` / `route.json`
projection — handoff gates appear as selected gates.

**ship / verify**: the augmented `RouteResult` flows into the existing `Ship.rollup route mode
profile` unchanged — handoff gates are enforced by `deriveEffectiveSeverity` and rolled into
`Blockers`/`Warnings`/`Passing` like any other gate. A blocking evidence/readiness gate ⇒
`Verdict = Fail` (SC-001, SC-005). `verify` keeps `RunMode = Verify`.

`update` stays pure: parsing/mapping are pure; the only I/O is the `Handoffs` port behind
`LoadHandoffs`.

## No-op invariant (FR-001, SC-003)

When `Handoffs repo = []`, `Consumer.consume [] = { Gates=[]; Selected=[]; Diagnostics=[] }`,
the fold is identity, and every command's text/JSON output is **byte-identical** to today. A
golden/diff test in each host's test project asserts this.

## Tests (additive to existing host test projects)

- **ShipCommand.Tests**: two fixture products differing only in handoff evidence (satisfied vs
  failing) ⇒ materially different verdicts through the real pipeline (SC-001).
- **VerifyCommand.Tests**: blocking readiness ⇒ selected blocking gate in the verdict; clean
  readiness ⇒ present non-blocking gate (SC-005); no-handoff ⇒ byte-identical (SC-003).
- **RouteCommand.Tests**: handoff gates present in `gates.json` / `route.json`; absence no-op.
