# Phase 1 Data Model: Capture A Real Evidence Reference From An Executed Gate

This row introduces **no new type**. It reuses four existing values verbatim and adds two pure functions over
them. The "model" here is therefore the *derivation* and the *fold*, not a new data shape. The committed surface
is [`contracts/EvidenceCapture.fsi`](./contracts/EvidenceCapture.fsi).

## Reused entities (all verbatim — none redefined)

| Entity | Source | Role in this row |
|--------|--------|------------------|
| `CommandRecord` | F032 `CommandRecord.Model` | **Input evidence** — an already-executed gate's reproducible facts + the one sensed `Duration`. Consumed, never produced. |
| `CommandIdentity` / `canonicalId` / `identityValue` | F032 `CommandRecord` | The byte-stable canonical rendering of `record.Reproducible` (duration excluded). The reference string is `identityValue (canonicalId record)`. |
| `EvidenceRef` | F030 `EvidenceReuse.Model` | **Derived output** — the opaque reference string the store holds. `referenceOf` wraps the F032 identity string in it. |
| `FreshnessInputs` | F029 `FreshnessKey.Model` | The resolved freshness **world** a captured reference is recorded against. Consumed, never produced. |
| `ReuseStore` / `record` / `decide` | F030 `EvidenceReuse` (+ `Model`) | The **target** store value and the verbatim fold/inverse. `capture` is `record` with the derived reference; the close-the-loop check is `decide`. |

No `Model.fsi`/`Model.fs` is added — there is nothing new to declare (research D4).

## Operation 1 — `referenceOf : CommandRecord -> EvidenceRef`

**Body (the whole implementation):**

```fsharp
let referenceOf (record: CommandRecord) : EvidenceRef =
    EvidenceRef(CommandRecord.identityValue (CommandRecord.canonicalId record))
```

**Semantics & invariants:**

- **Reproducible-identity reuse (FR-001, D1).** The reference string is byte-for-byte the F032 canonical
  identity. This row computes no new identity and hashes no bytes — it reuses F032's length-prefixed,
  uniquely-tagged BCL rendering.
- **Duration-invariance (FR-002, US2 / SC-002).** `canonicalId` projects only `record.Reproducible` and
  structurally cannot read `record.Duration`. Two records identical in every reproducible fact but differing in
  `SensedDuration` therefore yield the **byte-identical** `EvidenceRef`.
- **Injectivity over reproducible facts (FR-003, US2 / SC-003).** Inherited verbatim from F032's injective
  `canonicalId`: any single-field perturbation of a reproducible fact (executable, an argument or its order,
  working directory, env delta compared as a set, timeout, exit code, either output digest, or the
  captured-output outcome) changes the identity string and hence the reference.
- **Totality (FR-007).** Defined for every `CommandRecord`, including empty stdout/stderr digests (a literal
  value, F032 D3), a non-zero `ExitCode` (recorded, not rejected, F032 FR-003), and all three captured-output
  outcomes (`NoCapturedOutput`, `CapturedAt (CapturedOutputPath "")`, `CapturedAt (CapturedOutputPath "x")` —
  three distinct references, F032 FR-011). Never throws; needs no store.
- **Determinism / byte-stability (FR-008).** Identical record → byte-identical reference on every run and
  machine; no clock/GUID/path/locale/env input.

## Operation 2 — `capture : FreshnessInputs -> CommandRecord -> ReuseStore -> ReuseStore`

**Body (the whole implementation):**

```fsharp
let capture (inputs: FreshnessInputs) (record: CommandRecord) (store: ReuseStore) : ReuseStore =
    EvidenceReuse.record inputs (referenceOf record) store
```

**Semantics & invariants:**

- **Verbatim F030 fold (FR-004, D2).** Exactly `EvidenceReuse.record` with the derived reference substituted for
  the opaque evidence argument. No new reuse policy, store representation, or evidence representation. Inherits
  F030 `record`'s newest-first, immutable, store-in/store-out, de-duplicating-on-full-match behaviour verbatim.
- **Close-the-loop round-trip (FR-005, US1 / SC-001).** For the captured world `inputs`,
  `EvidenceReuse.decide inputs (capture inputs record store)` = `Reuse (referenceOf record)` — because `decide`
  is F030's own inverse of `record`, and `referenceOf record` is the reference just folded in.
- **Recompute-safety (FR-006, US3 / SC-004).** Relative to the input store, only the just-captured world becomes
  reusable. For every **other** candidate world, `decide` over the result equals `decide` over the input store —
  F030 `record` only de-dups an exact full-match of the captured world and never touches an entry for any other
  world. Every prior entry for a non-matching world is preserved byte-for-byte (US3 scenario 1).
- **Newest-first duplicate capture (US3 scenario 2 / Edge "Duplicate capture").** Capturing the same world again
  with a new execution makes `decide` serve the most-recently-captured reference — the F030 newest-first /
  full-match-dedup convention, no new dedup policy here. (F047 `prune` collapses superseded worlds at persist
  time; this core adds none.)
- **Mechanical, not policy (D7).** Records whatever record it is handed, including a non-zero `ExitCode`. Any
  success/exit-code gating is the out-of-scope host row.
- **Totality & determinism (FR-007, FR-008).** Defined for the empty store (`capture inputs record
  EvidenceReuse.empty` → a one-entry store) and every record edge above; identical input → byte-identical store.

## Lossless persistence round-trip (FR-010 / SC-007) — verified, not coded here

A store grown by `capture` is **indistinguishable** to persistence from any other `ReuseStore`: the reference is
an ordinary string and the world is the same F029 vocabulary F047/F046 already round-trip losslessly. The
guarantee is therefore proven by a **test** that reuses the already-merged path —

```fsharp
let grown = EvidenceCapture.capture inputs record EvidenceReuse.empty
let path = Path.GetTempFileName()
File.WriteAllText(path, EvidenceReuseStore.serialise grown)
let reread = FreshnessSensing.realStoreReader path   // string (a FILE PATH) -> Result<ReuseStore option, string>
// assert: reread = Ok (Some grown)  (world + exact derived reference preserved verbatim)
```

`realStoreReader` is a `StoreReader = string -> Result<ReuseStore option, string>` whose argument is a **file
path** (its only public load path; `Ok None` = file absent, `Error` = malformed). So the round-trip writes the
serialised text to a temp file, reads it back, and unwraps `Ok (Some _)` — mirroring F047's own `readBack`
helper. That temp file is the **only** I/O in the test; the capture core itself touches nothing. It needs no
new persistence code and no production dependency on EvidenceReuseStore / FreshnessSensing (those are test-only
`ProjectReference`s). F047 `serialise` renders the `EvidenceRef` string verbatim (F047 FR-004) and the F046
reader carries it back verbatim, so the captured reference survives byte-for-byte.

## Dependency direction (one-way, additive)

```
EvidenceCapture ─▶ EvidenceReuse (F030) ─▶ FreshnessKey (F029) ─▶ Config (F014)
              └──▶ CommandRecord (F032) ─▶ Config (F014)
```

Production references: **F030 + F032 only**. F029 and F014 arrive transitively. No FreshnessSensing /
CacheEligibility / EvidenceReuseStore / RouteJson / AuditJson / host / CLI coupling in the production library
(EvidenceReuseStore + FreshnessSensing appear only in the **test** project, for the round-trip). The library is
referenced by nothing on landing.
