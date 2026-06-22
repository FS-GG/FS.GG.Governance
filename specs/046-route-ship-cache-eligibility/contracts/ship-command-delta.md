# Contract delta — `FS.GG.Governance.ShipCommand` (F046)

The `fsgg ship` command now senses → resolves → evaluates and passes a **real** `CacheEligibilityReport`
to the F045 embed. **Tier 1** surface change (mirrors RouteCommand). `audit.json` keeps schema
`fsgg.audit/v2` — only its cache section flips from *not-evaluated* to *evaluated*. **The ship verdict, the
blockers/warnings/passing partition, every enforcement field, the `ExitCodeBasis`, and the numeric exit code
are unchanged (the safety-critical invariant, SC-003).**

## `Loop.fsi` additions

Identical to RouteCommand (see [route-command-delta.md](./route-command-delta.md)): `RunRequest.StorePath`;
`Effect` `SenseFreshness`/`LoadStore`; `Msg` `FreshnessSensed`/`StoreLoaded`; `Model`
`Snapshot`/`SelectedGates`/`Sensed`/`Store`/`CacheNotes`; a pre-projection `Phase` (`Selected`) between
`Loaded'` and `Rolled`.

`exitCode` is **unchanged**: `Success 0 | Blocked 1 | UsageError' 2 | InputUnavailable 3 | ToolError 4`. The
`Emitted` → `exitFromBasis decision.ExitCodeBasis` mapping is **untouched** — cache never participates in the
exit decision (FR-009).

## `Loop.fs` behavior

- `Sensed (Ok snapshot)` — also store `Snapshot = Some snapshot`.
- `Loaded (Valid facts)` — compute `result`/`decision`/`selectedGates` as today (`decision = Ship.rollup
  result mode profile`), store them, set `Phase = Selected`, emit `[ SenseFreshness(selectedGates,
  baseHeadOf model); LoadStore request.StorePath ]`. **No write here anymore.**
- `FreshnessSensed`/`StoreLoaded` Ok/Error — same degrade-not-fail transitions as RouteCommand (D2).
- `tryProject` (both inputs `Some`): build `cacheReport` (data-model §3), `auditDoc =
  AuditJson.ofShipDecision decision (Some cacheReport)`, set `Phase = Rolled`, emit the **single**
  `WriteArtifact(AuditArtifact, …, auditDoc)`.
- `Wrote (_, Ok())` → `Persisted` + `EmitSummary` (single write — unchanged).
- `Emitted` → exit from `decision.ExitCodeBasis` (**unchanged**).
- `render` — `renderText` adds the cache summary + `CacheNotes`; `renderJson` still returns the `auditDoc`
  verbatim (`--json` stdout == the persisted file byte-for-byte), which now carries the evaluated section.

## `Interpreter.fsi` / `.fs`

`Ports` gains `Freshness: FreshnessSensing.FreshnessSensor` and `Store: FreshnessSensing.StoreReader`; `step`
handles the two new effects via `FreshnessSensing.senseFreshness` / `loadStore`; `realPorts` wires
`FreshnessSensing.realSensor` / `realStoreReader`.

## `.fsproj`

Add `ProjectReference`s: `FreshnessSensing`, `CacheEligibility`, `FreshnessResolution`, `EvidenceReuse`,
`FreshnessKey`. No third-party `PackageReference`. `IsPackable=false` unchanged.

## Tests — including the SC-003 invariant

`Support.fs:265` and `LoopTests.fs:50` recompute the expected `audit.json` with `Some expectedReport`.
Add the **SC-003 ship-invariant** test: project the same `ShipDecision` with `Some report` and assert the
verdict, the three-way partition, every per-item enforcement field, the `ExitCodeBasis`, and the resulting
numeric exit are value-identical to the `None` (pre-wiring) projection of the same input — i.e. `Some report`
adds only the cache section and changes nothing the merge decision depends on. Add the US3 degrade tests
(unsensed fact ⇒ `notEvaluated` + note, exit unchanged; malformed store ⇒ degrade-to-empty + note, exit
unchanged) and re-bless `surface/FS.GG.Governance.ShipCommand.surface.txt`.

## Untouched (SC-008)

`tests/FS.GG.Governance.AuditJson.Tests` and `tests/FS.GG.Governance.EnforcementFixtures.Tests` (the F028
golden `audit.json` snapshots) stay on the `None` path — neither is edited or re-blessed.
