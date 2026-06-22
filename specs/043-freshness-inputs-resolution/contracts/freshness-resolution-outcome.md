# Contract: the freshness-resolution report value

**Feature**: `043-freshness-inputs-resolution`

This is the **observable value contract** of `FreshnessResolution.resolve` — what a `FreshnessResolutionReport`
contains for a given routed change, independent of the F# surface. The report is an in-value typed result; this
core renders **no JSON** (that is F042 and the later projection rows). The worked examples below are pinned and
exercised by the semantic tests.

## Shape

A report is an ordered list of entries, one per supplied gate:

```
report   = entry*                         (sorted by GateId ordinal, structural tiebreak; duplicates kept)
entry    = { Gate: GateId ; Outcome }
Outcome  = Resolved  FreshnessInputs       (the complete F029 ten-field value)
         | Unresolved [MissingFact ...]    (non-empty; every gap named, enum order)
```

## Field provenance for a `Resolved` outcome

| FreshnessInputs field | comes from | notes |
|---|---|---|
| `Check`, `Domain`, `Environment`, `Command` | the gate's carried `FreshnessKey` | verbatim; `Command` keeps its `option` |
| `RuleHash`, `GeneratorVersion`, `Base`, `Head` | `SensedFacts` repo-wide options | verbatim |
| `CoveredArtifacts` | `SensedFacts.CoveredArtifacts.[gateId]` | verbatim, incl. an explicitly-empty list |
| `CommandVersion` | `SensedFacts.CommandVersions.[c]` when `Command = Some c`; else `None` | absent command ⇒ absent version |
| ~~`Cost`~~ | — | **dropped** (not a freshness input, FR-002) |

## Missing-fact tokens (no-hide vocabulary)

| `MissingFact` | token | raised when |
|---|---|---|
| `MissingRuleHash` | `ruleHash` | `SensedFacts.RuleHash = None` |
| `MissingCoveredArtifacts` | `coveredArtifacts` | gate id absent from `SensedFacts.CoveredArtifacts` |
| `MissingCommandVersion` | `commandVersion` | gate declares `Command = Some c` and `c` absent from `CommandVersions` |
| `MissingGeneratorVersion` | `generatorVersion` | `SensedFacts.GeneratorVersion = None` |
| `MissingBaseRevision` | `baseRevision` | `SensedFacts.Base = None` |
| `MissingHeadRevision` | `headRevision` | `SensedFacts.Head = None` |

## Worked examples

### A — fully sensed, command-bearing gate ⇒ `Resolved`

Gate `build:tests` with `FreshnessKey = { Check; Domain; Cost = Medium; Environment = Ci; Command = Some "dotnet" }`,
and `SensedFacts` carrying every repo-wide fact, `CoveredArtifacts = map ["build:tests", [artA; artB]]`,
`CommandVersions = map ["dotnet", v]`:

```
entry { Gate = "build:tests"
        Outcome = Resolved { Check=…; Domain=…; Command=Some "dotnet"; Environment=Ci
                             RuleHash=…; CoveredArtifacts=[artA; artB]; CommandVersion=Some v
                             GeneratorVersion=…; Base=…; Head=… } }
```

`candidate entry = Some { Gate = "build:tests"; Inputs = <those FreshnessInputs> }` → fed to F041 unchanged.

### B — command-less gate, fully sensed ⇒ `Resolved`, no command/version

Gate with `Command = None`, covered artifacts sensed (possibly `[]`), all repo-wide facts present:

```
entry { Gate = …; Outcome = Resolved { … Command = None; CommandVersion = None; … } }
```

A `None` command is a **consistent absence**, never `MissingCommandVersion` (FR-005).

### C — several facts unsensed ⇒ `Unresolved`, every gap named

Gate `lint:style` declaring `Command = Some "eslint"`; `SensedFacts.RuleHash = None`, `Base = None`, the gate id
absent from `CoveredArtifacts`, `"eslint"` absent from `CommandVersions`; generator + head present:

```
entry { Gate = "lint:style"
        Outcome = Unresolved [ MissingRuleHash          // "ruleHash"
                               MissingCoveredArtifacts   // "coveredArtifacts"
                               MissingCommandVersion     // "commandVersion"
                               MissingBaseRevision ] }   // "baseRevision"
```

Ordered by enum order; **no** `FreshnessInputs` produced; `candidate entry = None` (recompute-safe).

### D — sensed-empty vs unsensed covered artifacts

`CoveredArtifacts = map ["g", []]` for gate `g` ⇒ `Resolved { … CoveredArtifacts = []; … }` (a legitimate
resolved empty set). The same gate **absent** from the map ⇒ `Unresolved [MissingCoveredArtifacts; …]`. The two
are never conflated (FR-003, Edge Cases).

### E — empty / single / duplicate inputs

- `resolve [] sensed = FreshnessResolutionReport []` — total, valid, not an error.
- one gate ⇒ a one-entry report; the single-gate path equals the many-gate path.
- two input gates with the **same** `GateId` ⇒ **two** entries, ordered by the structural tiebreak; neither
  merged nor dropped.

## Determinism

For value-equal inputs supplied in any order, the report is **byte-identical**: entries sorted by `GateId`
ordinal with a total structural tiebreak, each `Unresolved` list in enum order. No working-directory, clock, or
filesystem state can change the result — `resolve` performs no I/O.
