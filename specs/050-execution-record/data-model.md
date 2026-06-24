# Phase 1 Data Model: Digest Captured Output And Assemble A Command Record From An Execution Outcome

This row introduces **no new type**. It reuses F032 vocabulary verbatim and adds two pure functions. The
"entities" below are therefore the existing types the two operations consume and produce, plus the byte input.

## Vocabulary (all reused; nothing new)

| Value | Origin | Role here |
|-------|--------|-----------|
| `byte[]` (captured output bytes) | BCL | **Input** to `digestOf`, and the two output positions of `recordOf`. Raw bytes a gate wrote to stdout/stderr; carries no clock/path/product vocabulary. No new wrapper type (research D3). |
| `OutputDigest = OutputDigest of string` | F032 `CommandRecord.Model` | **Output** of `digestOf`; an input field of the assembled record. Opaque string; F032 does no hashing itself (D3) — this row supplies the value. |
| `Executable`, `Argument list`, `WorkingDirectory`, `EnvironmentDelta`, `ExitCode`, `CapturedOutput` | F032 `CommandRecord.Model` | The reproducible run facts `recordOf` carries verbatim into the record via `build`. |
| `TimeoutLimit` | F014 `Config.Model` | The reproducible timeout fact; reused verbatim (arrives transitively through F032). |
| `SensedDuration = SensedDuration of nanoseconds: int64` | F032 `CommandRecord.Model` | The one sensed fact; carried into `record.Duration`, **never** read by the digest or `canonicalId`. |
| `CommandRecord` | F032 `CommandRecord.Model` | **Output** of `recordOf`; an ordinary complete F032 record, indistinguishable from one `build` produced. |

## Operation 1 — `digestOf : byte[] -> OutputDigest`

The content-addressing primitive — the **first and only place in the codebase that hashes output bytes** (the
gap F032 left open at D3).

**Definition** (research D1):

```
digestOf bytes = OutputDigest (lowercase-hex (SHA-256 bytes))
```

i.e. `OutputDigest (System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData bytes).ToLowerInvariant())`.

**Semantics**:

| Property | Guarantee | Source |
|----------|-----------|--------|
| Content-only | Depends on byte **content** alone — never on duration, clock, GUID, path, locale, env, or array identity. | FR-001, FR-009 |
| Agreement | Byte-identical content → byte-identical `OutputDigest`. | FR-002, SC-002 |
| Sensitivity | Any byte changed, added, removed, or reordered → a different `OutputDigest` (cryptographic, overwhelming probability). | FR-002, SC-003 |
| Totality | Defined for **empty** bytes (`[||]` → the fixed empty-SHA-256 digest), binary/non-textual bytes (hashed as raw bytes, no decoding/locale/normalization), and arbitrarily large bytes (fixed-form result, no truncation). Never throws. | FR-003, FR-008, Edge cases |
| Empty distinctness | The empty-input digest is distinct from every non-empty digest. | FR-003, Edge "Empty captured output" |
| Identical streams | Equal stdout and stderr bytes → equal digests (a function of content); positional distinction is `canonicalId`'s job. | Edge "Identical stdout and stderr bytes" |
| Determinism | Identical bytes → byte-identical digest on every run/process/machine. | FR-009, SC-005 |
| Fixed/internal | Not a policy knob, configurable algorithm, or caller-varied value — supply bytes, receive an opaque digest. | FR-011 |

## Operation 2 — `recordOf : … -> CommandRecord`

Assembles a complete F032 record from a captured execution outcome by digesting the two output streams and
**delegating everything else to `CommandRecord.build` verbatim** (research D2).

**Signature** — `CommandRecord.build`'s ten curried arguments in the same order, with the two `OutputDigest`
positions replaced by two `byte[]`:

```
recordOf
    (executable: Executable)
    (arguments: Argument list)
    (workingDirectory: WorkingDirectory)
    (environment: EnvironmentDelta)
    (timeout: TimeoutLimit)
    (exitCode: ExitCode)
    (stdout: byte[])
    (stderr: byte[])
    (capturedOutput: CapturedOutput)
    (duration: SensedDuration)
    : CommandRecord
```

**Definition**:

```
recordOf executable arguments workingDirectory environment timeout exitCode stdout stderr capturedOutput duration =
    CommandRecord.build
        executable arguments workingDirectory environment timeout exitCode
        (digestOf stdout) (digestOf stderr) capturedOutput duration
```

**Semantics**:

| Property | Guarantee | Source |
|----------|-----------|--------|
| Verbatim delegation | Equals `CommandRecord.build` of the same facts with `digestOf stdout` / `digestOf stderr` in the two digest positions — no new record shape, no normalization, no policy. | FR-004, US3 |
| Correct positions | stdout's digest lands in `StdoutDigest`, stderr's in `StderrDigest` — never swapped. | FR-005 |
| Carriage | Every other fact carried unchanged: arguments in supplied order; the env delta's three classes preserved (a `Changed` entry never split into `Added`+`Removed`); duration only in `record.Duration`, nowhere in `record.Reproducible`. | FR-005 |
| Duration-invariance | Two outcomes identical in all reproducible facts (incl. output bytes) but differing only in duration assemble to records with byte-identical `canonicalId` (and therefore byte-identical F049 `EvidenceRef`) — neither `digestOf` nor `canonicalId` reads `SensedDuration`. | FR-006, SC-004 |
| Identity sensitivity | Any single reproducible-fact perturbation — executable, an argument or its order, working directory, the env delta as a set, timeout, exit code, **a byte of either output**, or the captured-output outcome — changes `canonicalId` (and the F049 reference). | FR-007, SC-003 |
| Failed run recorded | A non-zero `ExitCode` or an applied timeout assembles to an ordinary complete record (inherited from `build`, F032 FR-003); no success/exit-code gating. | US3 AC2, FR-004 |
| Totality | Defined for empty output bytes, a non-zero exit code, an applied timeout, an empty env delta, and every captured-output outcome; never throws. | FR-008, SC-006 |
| Determinism | Identical bytes + facts → byte-identical record on every run/process/machine. | FR-009, SC-005 |

## Close-the-loop (verified by reuse, no new code — research D8)

The chain this row unblocks, every step already merged except `recordOf`:

```
byte[] stdout/stderr + run facts + duration
   │  recordOf            (this row)
   ▼
CommandRecord
   │  EvidenceCapture.referenceOf  (F049)
   ▼
EvidenceRef = EvidenceRef (CommandRecord.identityValue (CommandRecord.canonicalId record))
   │  EvidenceCapture.capture inputs record store   (F049 = EvidenceReuse.record inputs (referenceOf record) store)
   ▼
ReuseStore   ── EvidenceReuse.decide inputs store ──▶  Reuse (referenceOf record)
   │  EvidenceReuseStore.serialise / persist  (F047/F048)
   ▼
durable store entry
```

For any captured outcome assembled by `recordOf`:

- `CommandRecord.canonicalId (recordOf …)` is **defined and reproducible** (FR-007, SC-001).
- `EvidenceCapture.referenceOf (recordOf …)` is a **reproducible reference** (FR-007).
- `EvidenceCapture.capture inputs (recordOf …) store`, then `EvidenceReuse.decide inputs (…)`, returns
  `Reuse (referenceOf (recordOf …))` — the gate's world is now reusable for exactly the derived reference
  (FR-007, SC-001) — and any single-output-byte change flips the reference, so a changed output is never served
  as fresh (SC-003).
