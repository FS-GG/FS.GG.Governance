# Phase 1 Data Model: Provenance Core (F033)

The product-neutral, YAML-free values the `Provenance` core defines and operates over. The new types live in
`FS.GG.Governance.Provenance.Model` (sole public surface declared in `Model.fsi`); the operations are in module
`FS.GG.Governance.Provenance` (`Provenance.fsi`). The reused facts come verbatim from F029 (`FS.GG.Governance.FreshnessKey.Model`),
F032 (`FS.GG.Governance.CommandRecord.Model`), and F014 (`FS.GG.Governance.Config.Model`) — none redefined
(FR-010). Nothing here carries raw bytes, host paths beyond the supplied strings, clock readings, or product
vocabulary.

## Reused verbatim (not redefined — FR-010)

| Type | Origin module | Used for |
|---|---|---|
| `Revision` (`Revision of string`) | F029 `FS.GG.Governance.FreshnessKey.Model` | the **source commit**, the **base** revision, and the **head** revision (D2) |
| `RuleHash` (`RuleHash of string`) | F029 `…FreshnessKey.Model` | the **rule hash** |
| `GeneratorVersion` (`GeneratorVersion of string`) | F029 `…FreshnessKey.Model` | the **generator version** |
| `ArtifactHash` (`ArtifactHash of string`) | F029 `…FreshnessKey.Model` | one **artifact digest**; the value carries a SET of these (D4) |
| `CommandRecord` (`{ Reproducible; Duration }`) | F032 `FS.GG.Governance.CommandRecord.Model` | one **command record**, carried whole (FR-002); the value carries an ORDERED list of these (D4) |
| `EnvironmentClass` (`Local`/`Ci`/`LocalOrCi`/`Release`) | F014 `FS.GG.Governance.Config.Model` | the **environment class** |

> The F032 operations `CommandRecord.canonicalId : CommandRecord -> CommandIdentity` and
> `CommandRecord.identityValue : CommandIdentity -> string` (both public) are reused by `Provenance.canonicalId`
> to fold each embedded record's **reproducible** identity (never its sensed `Duration`) into the provenance
> identity (D3, D5).

## New vocabulary (the minimal provenance additions)

| Type | Definition | Notes |
|---|---|---|
| `BuilderIdentity` | `BuilderIdentity of string` | who or what produced the evidence (a CI runner, an agent, a user). Opaque, comparable — the F029 opaque-token discipline (no validation, no parsing; an empty string is a literal value). The **only** genuinely new fact type (D2). |
| `ProvenanceIdentity` | `ProvenanceIdentity of string` | the byte-stable canonical identity over the reproducible facts (FR-006). The wrapped string is the canonical rendering (`contracts/provenance-identity-format.md`); equality is exact byte equality. Mirrors F032's `CommandIdentity`. |

## Key entity: the complete provenance value (FR-001, D2/D3)

```text
Provenance =
    { SourceCommit:     Revision           // F029 reuse — the revision the evidence was built against
      Base:             Revision           // F029 reuse — base revision bounding the change
      Head:             Revision           // F029 reuse — head revision bounding the change
      RuleHash:         RuleHash           // F029 reuse — the rule digest
      GeneratorVersion: GeneratorVersion   // F029 reuse — the generator/tool version
      ArtifactDigests:  ArtifactHash list  // F029 reuse — SET in the identity (order/dup ignored — D4)
      CommandRecords:   CommandRecord list // F032 reuse — carried WHOLE, ORDERED; each carries its sensed Duration internally
      Environment:      EnvironmentClass   // F014 reuse — the environment class
      Builder:          BuilderIdentity }  // NEW — who/what built it
```

- Carries **all eight** declared facts (base and head are the two `Revision`s of one "base/head" fact); none
  dropped or optional-by-omission (FR-001, SC-001).
- **No declared fact is made optional.** A build with no command records is `CommandRecords = []`; a build
  covering no artifacts is `ArtifactDigests = []` — both ordinary complete values, not errors (FR-004, Edge
  cases). Equal base/head (`Base = Head`) is an ordinary value.
- **The sensed metadata lives inside the embedded records.** Each `CommandRecord` holds its sensed `Duration`
  in a field structurally apart from its `Reproducible` facts (F032). A consumer reads
  `provenance.CommandRecords.[i].Duration` for the sensed measure and the reproducible facts elsewhere on the
  record. There is **no** provenance-level sensed field — no wall-clock timestamp this row (D3).

## Operations (`Provenance.fsi` — see `contracts/provenance-api.md`)

| Function | Signature | Role |
|---|---|---|
| `build` | `Revision -> Revision -> Revision -> RuleHash -> GeneratorVersion -> ArtifactHash list -> CommandRecord list -> EnvironmentClass -> BuilderIdentity -> Provenance` | the single pure, total assembly of the nine supplied facts into a complete `Provenance` (FR-004); curried in the design row's field order (source commit, base, head, rule hash, generator version, artifact digests, command records, environment class, builder identity). |
| `canonicalId` | `Provenance -> ProvenanceIdentity` | the pure, total canonical identity over the reproducible facts; folds each command record via F032 `CommandRecord.canonicalId` (excludes durations); byte-stable, set-invariant over the artifact digests, order-significant over the command records (FR-006/07/08). |
| `identityValue` | `ProvenanceIdentity -> string` | unwrap the canonical identity to its string (storage, messages, tests). Total. `identityValue (ProvenanceIdentity s) = s`. |

### Laws (asserted by the semantic tests)

- **Carriage (SC-001).** For any nine facts, `build` yields a value from which each fact reads back verbatim —
  the three revisions, rule hash, generator version, the artifact digests (as a set), the command records
  (each whole, retaining all ten of its facts incl. its sensed duration), environment class, and builder
  identity.
- **Records whole; digests a set (SC-002).** Every embedded command record is carried whole (none dropped,
  flattened to a stub, or filtered by outcome); supplying the same artifact digest twice does not double-count
  it in the identity.
- **Sensed split (SC-003).** The embedded records' `Duration`s are reachable as sensed metadata and are
  **excluded** from `canonicalId` (changing only a duration does not change the identity).
- **Identity sensitivity (SC-004).** Two provenances equal in every reproducible fact but whose records differ
  **only** in duration have **equal** `canonicalId`; two provenances differing in **any** reproducible fact
  (a revision, the rule hash, the generator version, the artifact-digest set, a command record's reproducible
  facts or their order, the environment class, or the builder identity) have **different** `canonicalId`.
- **Determinism + order laws (SC-005).** `build` and `canonicalId` are pure functions of the supplied facts;
  reordering or duplicating the artifact digests leaves `canonicalId` unchanged (set), while reordering the
  command records changes it (ordered — D4).
- **Purity (SC-006).** Provenance and identity are identical regardless of cwd, time, or unrelated
  filesystem/repo state — no clock/filesystem/git/environment/network read, no process spawn, no byte hashing.
