# Contract — Kinded-Run Recording & Provenance Snapshot

**Cores consumed (verbatim):** `CommandKind.Model.KindedCommandRun`, `CommandKind.Audit.auditSnapshot`,
`CommandKind.Audit.runIdentity`/`snapshotIdentity`.
**Insertion point:** each host's `update` on `GatesExecuted records` and the persist phase.

## Kinded-run recording
`records : (GateId * CommandRecord) list` already arrives from the edge (`senseExecution` per executed gate).
```
kindOf : Gate -> CommandKind                              // total: Build|Test|Pack|TemplateInstantiation
                                                          //        |GitDiff|PackageInspection|VisualCapture
runs = records |> List.map (fun (gid, rec) -> { Kind = kindOf (gateById gid); Record = rec })
```
**Guarantees:** `runIdentity run = CommandRecord.canonicalId run.Record` verbatim — kind does **not** participate;
two runs differing only in sensed duration share an identity (FR-004). `kindOf` is total (no silent mislabel).

## Provenance snapshot
```
snapshot =
  auditSnapshot
    (Head)                         // sourceCommit
    base head                      // from RepoSnapshot
    ruleHash genVersion digests    // from SensedFacts (F046)
    runs                           // above
    environment builder            // NEW normalized edge senses (no username/host/clock)
```
**Guarantees:** `snapshotIdentity snapshot = Provenance.canonicalId snapshot.Provenance` verbatim; the snapshot's
identity is independent of any duration or normalized-away environment detail, so `provenance.json` is byte-identical
across machines and re-runs (FR-006, SC-003).
