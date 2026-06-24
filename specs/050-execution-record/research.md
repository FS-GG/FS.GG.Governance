# Phase 0 Research: Digest Captured Output And Assemble A Command Record From An Execution Outcome

All unknowns are resolved by the spec's Assumptions section and the existing F032/F049 surfaces. No
`NEEDS CLARIFICATION` remained after loading the spec. The decisions below record *why* each shape was chosen.

## D1 — The digest is SHA-256 over the raw bytes, rendered as lowercase hex, wrapped in F032 `OutputDigest`

**Decision**: `digestOf bytes = OutputDigest (System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData
bytes).ToLowerInvariant())`. The full 64-character lowercase-hex SHA-256 digest of the raw bytes, no truncation.

**Rationale**: The spec fixes the contract (FR-001–FR-003, FR-009, FR-011) — deterministic, byte-stable, total,
content-sensitive, fixed and internal — and leaves the exact algorithm to the plan (spec Assumption: "the exact
digest algorithm is an implementation choice fixed by the plan; the spec constrains only its determinism,
byte-stability, totality, and content-sensitivity"). SHA-256 satisfies every constraint:

- **Content-addressed (FR-001/FR-002, SC-002/SC-003)**: a cryptographic hash is a pure function of byte content;
  equal bytes yield byte-identical digests and any single-byte change (added, removed, changed, reordered)
  yields a different digest with overwhelming probability — exactly the "agree on equal, diverge on different"
  contract.
- **Total over empty (FR-003, SC-006)**: `SHA256.HashData [||]` is defined — the well-known empty-input digest
  `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` — distinct from every non-empty digest,
  and never throws.
- **Byte-stable across runs/machines (FR-009, SC-005)**: SHA-256 is a fixed standard; the BCL implementation is
  deterministic and identical across platforms. No clock/GUID/path/locale/env enters.
- **Reuses existing precedent**: the F040 `Snapshot` interpreter already hashes content with
  `System.Security.Cryptography.SHA256.HashData` + `System.Convert.ToHexString(...).ToLowerInvariant()` (it
  truncates to 16 chars for a short id). This row reuses the same BCL primitives, adding **no** new third-party
  dependency (FR-010) — SHA-256 and hex rendering are in the BCL.
- **No truncation**: unlike the Snapshot short-id, this digest feeds `OutputDigest` and ultimately F032
  `canonicalId` → F049 `EvidenceRef`, where collision resistance matters (a truncated digest could let a changed
  output masquerade as unchanged — SC-003's failure mode). The full 64-char digest is kept.

The result is wrapped in the **F032 `OutputDigest` newtype verbatim** (reused, not redefined — FR-001, FR-010);
F032 treats `OutputDigest` as an opaque string, so the hex rendering is a valid, ordinary value there.

**Alternatives considered**:
- *Truncate the digest (à la Snapshot's 16-char short id).* Rejected: shortening trades collision resistance for
  brevity, and brevity buys nothing here — `OutputDigest` is opaque and never displayed to a human as the
  primary artifact. A truncated digest risks the SC-003 failure (a changed output mistaken for unchanged).
- *Prefix the digest with a scheme tag (`"sha256:…"`).* Rejected: F032 `canonicalId` already encodes each field
  with length prefixes + unique tags (injective across fields), and `OutputDigest` is opaque, so a scheme tag
  adds surface for no disambiguation benefit. The scheme is fixed and internal (FR-011); a tag would hint at a
  configurability that does not exist. The bare lowercase-hex rendering matches the Snapshot precedent.
- *A non-cryptographic hash (e.g. FNV/xxHash) or a structural digest.* Rejected: a non-crypto hash has weaker
  collision resistance for adversarial/large inputs and would need a third-party package (FR-010 forbids new
  dependencies); SHA-256 is in the BCL and is the cleanest reproducible one-way digest of content.
- *Encode the bytes themselves (e.g. base64) instead of hashing.* Rejected: that is not a *digest* (the result
  grows with input — Edge case "Large output" wants a fixed-form result regardless of size) and would bloat
  `canonicalId` and the persisted store.

## D2 — `recordOf` mirrors `CommandRecord.build` exactly, with two `byte[]` in the two `OutputDigest` positions

**Decision**: `recordOf` takes the **same ten curried arguments as F032 `CommandRecord.build`, in the same
order**, except the two `OutputDigest` parameters (`stdoutDigest`, `stderrDigest`) become two `byte[]`
parameters (`stdout`, `stderr`). Its body is:

```fsharp
CommandRecord.build
    executable arguments workingDirectory environment timeout exitCode
    (digestOf stdout) (digestOf stderr) capturedOutput duration
```

**Rationale**: US3's independent test is explicit — "assert `recordOf outcome` equals `CommandRecord.build`
applied to the same facts with those known digests substituted for the raw bytes — i.e. `recordOf` is `build`
composed with the digest on the two output fields, nothing more." Keeping `recordOf`'s argument list identical
to `build`'s (modulo the two output positions) makes that composition **structural and self-evident**: a reader
sees `recordOf` *is* `build` with `digestOf` lifted onto stdout/stderr. This guarantees verbatim delegation
(FR-004), correct digest placement — stdout's digest in `StdoutDigest`, stderr's in `StderrDigest`, never
swapped (FR-005) — and verbatim carriage of every other fact and the duration, because F032 `build` does all of
it (FR-005). It introduces no new record shape, no normalization, and no policy.

**Alternatives considered**:
- *A single `CapturedExecutionOutcome` record bundling the ten facts + two byte buffers.* Rejected: it would
  introduce a **new type** (breaking the Model-less, no-new-representation guarantee, FR-010 / D4) and obscure
  the "`recordOf` = `build` ∘ digest" equivalence US3 wants to read off the signature. The curried form keeps
  the library Model-less, exactly as F049's curried `referenceOf`/`capture` did, and makes the delegation a
  one-line composition. (The out-of-scope gate-execution port may choose to bundle an outcome record for its own
  ergonomics; this pure core does not need to.)
- *Take pre-built `ReproducibleFacts` + `SensedDuration` + two byte buffers.* Rejected: `build`'s contract is to
  *assemble* `ReproducibleFacts` from the ten flat facts; taking a pre-built `ReproducibleFacts` would bypass
  `build` and re-implement the assembly here, contradicting "delegate to `build` verbatim" (FR-004).

## D3 — `byte[]` is the captured-output input vocabulary; no new wrapper type

**Decision**: `digestOf` and the two output positions of `recordOf` take raw `byte[]`. No `CapturedBytes`
newtype is introduced.

**Rationale**: The spec's "Captured output bytes" entity is "just bytes" — "carries no clock, path, or product
vocabulary." The plainest F# representation is `byte[]` (Principle III: plainest that solves the problem). The
digest reads only byte content (FR-009: "no dependence on input collection identity (only on byte content)"),
so reference equality / mutability of the array is irrelevant — `digestOf` consumes content, not identity.
Keeping `byte[]` (a BCL type) over a newtype preserves the Model-less guarantee (D4) and matches how a real
captured-output buffer arrives from a process (`Process.StandardOutput` → bytes). A newtype would add a type the
spec's "no new record representation" (FR-004) and "reuses F032 vocabulary verbatim" (FR-010) discourage.

**Alternatives considered**:
- *`CapturedBytes of byte[]` newtype.* Rejected: a new type for no behavioural gain; `byte[]` is unambiguous in
  the two output positions and keeps the library Model-less. (If the out-of-scope executor wants a wrapper, it
  can introduce one in its own row.)
- *`ReadOnlySpan<byte>` or `Stream`.* Rejected: a `Span` cannot cross the async/closure boundaries a real
  executor uses and complicates FsCheck generation; a `Stream` is I/O-shaped and would invite the very
  process/file reading this pure core forbids (FR-008). `byte[]` is the plain in-memory buffer the spec
  describes.

## D4 — Model-less: no new type, so no `Model.fsi/fs`

**Decision**: The library carries only `ExecutionRecord.fsi` + `ExecutionRecord.fs` — no `Model` file.

**Rationale**: The row introduces **no new type** (FR-010 "reuses F032 vocabulary verbatim"; "no new record
representation"). `digestOf` returns the F032 `OutputDigest`; `recordOf` returns the F032 `CommandRecord`; both
take `byte[]` (BCL) and F032 fact types as inputs. F032 needed a `Model` file because it introduced
`OutputDigest`/`CommandRecord`/the reproducible-fact types; this row introduces nothing, so a `Model` file would
be empty — exactly F049's situation. Compile order is simply `ExecutionRecord.fsi -> ExecutionRecord.fs`.

## D5 — A new standalone library, not a function added to F032

**Decision**: New project `FS.GG.Governance.ExecutionRecord`, layered on F032 (transitively F014).

**Rationale**: FR-010 / SC-007 require zero edits to any existing core and its golden/surface baseline. Adding
`digestOf`/`recordOf` to `CommandRecord` would mutate a merged public surface and force a surface-baseline
re-bless on a frozen core — and, worse, would make F032 **start hashing bytes**, directly contradicting its
explicit design (F032 D3, FR-010: "no hashing happens here"). The whole point of this row is that a *dedicated*
row owns the digesting F032 deferred. A separate project is the established layering pattern (F042 on F041; F047
on F030; F049 on F030 + F032) and keeps the bridge referenced-by-nothing-on-landing, exactly as F047/F049 were.

**Alternatives considered**:
- *Add the two functions to `CommandRecord` (F032).* Rejected: edits a frozen merged core + its baseline
  (FR-010 violation) and forces F032 to hash bytes, reversing D3 — the deferral this row exists to honour.

## D6 — Function names and signatures match the spec's vocabulary

**Decision**: `digestOf : byte[] -> OutputDigest` and `recordOf : <build's ten args, two as byte[]> ->
CommandRecord`.

**Rationale**: The spec names `recordOf` throughout (US1, US3, FR-007). The digest operation is unnamed in the
spec ("a pure operation that derives a deterministic, byte-stable `OutputDigest`"); `digestOf` reads as the
natural sibling and parallels F049's `referenceOf` naming. Keeping `recordOf`'s argument order identical to
`build`'s lets the round-trip read as `referenceOf (recordOf … stdout stderr …) = referenceOf (build …
(digestOf stdout) (digestOf stderr) …)`.

## D7 — No reuse/success policy, no exit-code gating (mechanical assembly)

**Decision**: `recordOf` assembles whatever outcome it is handed, including a non-zero `ExitCode` and an applied
timeout.

**Rationale**: Spec Assumption "Capture is mechanical, not policy" and US3 AC2 / the "Failed run" edge case place
all gating (should a failed gate's outcome be recorded or suppressed? success/exit-code gating?) in the
**host row** that is out of scope. F032 `build` already records failures as ordinary reproducible facts (F032
FR-003), and `digestOf`/`recordOf` are total over every exit code and timeout outcome. Adding any gate here would
invent the very reuse/success policy FR-004 forbids — the same discipline F049 `capture` established.

## D8 — Close-the-loop and persistence are verified by reuse, not by new code

**Decision**: FR-007 / SC-001 are satisfied by **tests** that assemble a record with `recordOf`, derive its
reference with the already-merged F049 `EvidenceCapture.referenceOf`, fold it with F049 `capture`, and assert
F030 `EvidenceReuse.decide` returns `Reuse (referenceOf record)` for the captured world. No capture/persistence
code is added in this row.

**Rationale**: This row's only new computation is the two digests; everything downstream (`canonicalId`,
`referenceOf`, `capture`, `serialise`) is already merged and proven. Because `recordOf` returns an ordinary F032
`CommandRecord` indistinguishable from one `build` would have produced from the same (already-digested) inputs
(US3), the entire F049/F047 chain consumes it verbatim. The close-the-loop tests need only test-time
`ProjectReference`s on EvidenceCapture + EvidenceReuse + FreshnessKey (no production dependency), keeping the
production library's dependency set at exactly F032.

## Resolved unknowns

| Unknown | Resolution |
|---------|------------|
| What digest scheme? | SHA-256 over raw bytes → full lowercase-hex, wrapped in F032 `OutputDigest` (D1) |
| Truncate or tag the digest? | No — full 64-char bare lowercase hex; collision resistance matters, opacity needs no tag (D1) |
| New third-party dependency for hashing? | None — BCL `SHA256.HashData` + `Convert.ToHexString`, reused from the Snapshot precedent (D1) |
| Shape of `recordOf`? | `CommandRecord.build`'s ten curried args, two `OutputDigest` positions → two `byte[]` (D2) |
| Captured-bytes type? | Raw `byte[]`, no new wrapper — keeps the library Model-less (D3) |
| New types needed? | None — Model-less, reuses F032 vocabulary (D4) |
| New project or edit F032? | New standalone library `FS.GG.Governance.ExecutionRecord` (D5) |
| Any reuse / exit-code policy? | None — mechanical assembly; gating is the out-of-scope host row (D7) |
| How is close-the-loop proven? | Test reusing merged F049 `referenceOf`/`capture` + F030 `decide` (D8) |
