# Phase 1 Data Model: Command-Record Core (F032)

The product-neutral, YAML-free values the `CommandRecord` core defines and operates over. All types live in
`FS.GG.Governance.CommandRecord.Model` (sole public surface declared in `Model.fsi`); the operations are in
module `FS.GG.Governance.CommandRecord` (`CommandRecord.fsi`). The one verbatim reuse is F014's `TimeoutLimit`
from `FS.GG.Governance.Config.Model` (FR-009). Nothing here carries raw bytes, host paths beyond the supplied
strings, clock readings, or product vocabulary.

## Reused verbatim (not redefined — FR-009)

| Type | Origin | Used for |
|---|---|---|
| `TimeoutLimit` (`TimeoutLimit of seconds: int`) | F014 `FS.GG.Governance.Config.Model` | the *timeout* fact of a command run |

> `CommandId` / `EnvironmentClass` (also F014) were evaluated: neither maps to any of the ten declared run
> facts (the *executable* is the actual program string, distinct from a declared `CommandId`; the *environment
> delta* is concrete variable changes, not an `EnvironmentClass`). Per FR-009 ("reuse where they exist") only
> `TimeoutLimit` is reused; forcing the others in would add facts the design row does not list.

## New opaque newtypes (the minimal command-record additions)

| Type | Definition | Notes |
|---|---|---|
| `Executable` | `Executable of string` | the program that ran (path or name as sensed). Opaque, comparable. |
| `Argument` | `Argument of string` | one command-line argument. The record carries an **ordered** `Argument list`; order is significant (D6). |
| `WorkingDirectory` | `WorkingDirectory of string` | the run's working directory as sensed. Opaque (not a governed-root `GovernedPath` — an arbitrary cwd). |
| `ExitCode` | `ExitCode of int` | the process exit code. Any `int`, incl. non-zero (a failure is recorded, not rejected — FR-003). |
| `OutputDigest` | `OutputDigest of string` | a supplied, already-computed digest of stdout **or** stderr. Opaque; no hashing here (FR-010, D3). An empty string is a literal value. |
| `EnvVarName` | `EnvVarName of string` | an environment variable name. |
| `EnvVarValue` | `EnvVarValue of string` | an environment variable value. |
| `CapturedOutputPath` | `CapturedOutputPath of string` | the path to a captured-output file, when one exists. |
| `SensedDuration` | `SensedDuration of nanoseconds: int64` | the **sensed / non-deterministic** wall-clock duration of the run, as an opaque integer measure (D3). Carried, but excluded from canonical identity (FR-004/FR-005). |
| `CommandIdentity` | `CommandIdentity of string` | the byte-stable canonical identity over the reproducible facts (FR-005). Wrapped string is the canonical rendering (`contracts/command-record-identity-format.md`); equality is exact byte equality. |

## Environment delta (FR-002, D4)

```text
AddedVar   = { Name: EnvVarName; Value: EnvVarValue }              // a variable the run added (absent in baseline)
ChangedVar = { Name: EnvVarName; Old: EnvVarValue; New: EnvVarValue } // a variable the run changed (present in both, different value)
RemovedVar = { Name: EnvVarName; Old: EnvVarValue }               // a variable the run removed (present in baseline, absent after)

EnvironmentDelta =
    { Added:   AddedVar list
      Changed: ChangedVar list
      Removed: RemovedVar list }
```

- A **changed** variable appears exactly once, in `Changed`, carrying both its baseline `Old` and run `New`
  value — never decomposed into a `Removed` + `Added` pair (FR-002, SC-002).
- Any class may be empty; an entirely empty delta (`{ Added = []; Changed = []; Removed = [] }`) is an ordinary
  value, not an error (Edge cases).
- For **canonical identity**, each class is treated as a **set**: entries are rendered to a canonical
  per-entry string, deduplicated, and ordinal-sorted, so supply order and duplicates never affect the identity
  (FR-007, D6). The delta is a *partition of changes relative to a baseline*, never a full environment snapshot
  (D4).

## Captured-output outcome (FR-011, D5)

```text
CapturedOutput =
    | CapturedAt of CapturedOutputPath   // a concrete captured-output file path
    | NoCapturedOutput                   // explicit "no captured-output file" — total, locatable, never an empty string
```

`NoCapturedOutput` and `CapturedAt (CapturedOutputPath "")` encode to **distinct** identity segments (the F029
presence-digit guarantee), so absence never collides with a real (or empty) path (FR-011).

## Reproducible facts (the nine identity-bearing facts)

```text
ReproducibleFacts =
    { Executable:        Executable
      Arguments:         Argument list          // ORDERED — order is significant in the identity (D6)
      WorkingDirectory:  WorkingDirectory
      Environment:       EnvironmentDelta       // compared as a SET per class in the identity (D6)
      Timeout:           TimeoutLimit           // F014 reuse
      ExitCode:          ExitCode
      StdoutDigest:      OutputDigest
      StderrDigest:      OutputDigest
      CapturedOutput:    CapturedOutput }
```

This is the addressable "reproducible part of the run" value and the **sole input** to `canonicalId`. Every
field is reproducible from the command and its context (FR-005).

## Command record (the complete value — FR-001, D2)

```text
CommandRecord =
    { Reproducible: ReproducibleFacts   // the nine reproducible facts (above)
      Duration:     SensedDuration }    // the sensed / non-deterministic tenth fact, structurally apart
```

- Carries **all ten** declared facts; none dropped or optional-by-omission (FR-001, SC-001).
- The sensed duration is a **separate field of a distinct type**, so it is reachable as sensed metadata and
  structurally excluded from the identity (FR-004, US2 scenario 3, D2). A consumer reads `record.Duration` for
  the sensed measure and `record.Reproducible.*` for the reproducible facts.

## Operations (`CommandRecord.fsi` — see `contracts/command-record-api.md`)

| Function | Signature | Role |
|---|---|---|
| `build` | `Executable -> Argument list -> WorkingDirectory -> EnvironmentDelta -> TimeoutLimit -> ExitCode -> OutputDigest -> OutputDigest -> CapturedOutput -> SensedDuration -> CommandRecord` | the single pure, total assembly of the ten supplied facts into a complete record (FR-003); curried in the design row's field order. |
| `canonicalId` | `CommandRecord -> CommandIdentity` | the pure, total canonical identity over `record.Reproducible` only (excludes duration); byte-stable, order-independent over the env delta (FR-005/06/07). |
| `identityValue` | `CommandIdentity -> string` | unwrap the canonical identity to its string (storage, messages, tests). Total. |

### Laws (asserted by the semantic tests)

- **Carriage (SC-001).** For any ten facts, `build` yields a record from which each fact reads back verbatim
  (arguments in order; env delta partitioned into the three classes).
- **Sensed split (SC-003).** `record.Duration` is reachable and distinct from `record.Reproducible`; changing
  only the duration does not change `canonicalId record`.
- **Identity sensitivity (SC-004).** Two records equal in every reproducible fact but differing only in
  duration have **equal** `canonicalId`; two records differing in **any** reproducible fact (executable, an
  argument, working dir, env delta, timeout, exit code, either digest, or captured-output outcome) have
  **different** `canonicalId`.
- **Determinism + delta order/dup invariance (SC-005).** `build` and `canonicalId` are pure functions of the
  supplied facts; reordering or duplicating the environment-delta entries leaves `canonicalId` unchanged,
  while reordering arguments does change it.
- **Purity (SC-006).** Record and identity are identical regardless of cwd, time, or unrelated filesystem/repo
  state — no clock/filesystem/git/environment/network read, no process spawn.
