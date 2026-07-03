# Contract: Shared CommandHost leaves + surface deltas (#49)

These are the signatures this feature adds or changes. Exact types are firmed against the actual host code during implementation (the informal shapes below reflect the surveyed local copies). Following Principle I, each is a signature-first sketch to be exercised through the host/packed surface before the `.fs` bodies are rewired.

## Contract A — new `CommandHost.fsi` `val`s (Phase A)

```fsharp
// Atomic file write — replaces 7 byte-identical host copies.
val writeAtomic: path: string -> content: string -> Result<unit, string>

// Readiness handoff discovery, ordinal-sorted by directory name — replaces the 3 host
// copies AND the divergent Array.sortBy mirror in Cli/ArtifactReading (D3).
// (<HandoffDir> = the element type the host copies already return.)
val realHandoffs: readinessDir: string -> <HandoffDir>[]

// Environment / builder sensing — replaces 3 copies each. Signatures mirror the existing
// host lets; the shared realization keeps EnvironmentClass unqualified where Release needed
// it to avoid the Snapshot.Model.CiEnvironment name clash (D1 watch-out).
val senseEnvironmentReal: unit -> EnvironmentClass
val senseBuilderReal: unit -> BuilderIdentity

// Shared handler for the snapshot/catalog step arms (SenseScope + LoadCatalog) — a function
// `step` CALLS. This MUST NOT change any host `step` signature (ports -> effect -> msg).
// Shape TBD against the effect/msg types; likely:
val stepSnapshotArms: ports: <Ports> -> effect: <Effect> -> <Msg> option   // None = not one of the shared arms
```

**Baseline**: `CommandHost` surface-drift baseline updated to include these `val`s. The 9 host `step` baselines stay **unchanged** (the invariant to verify).

## Contract B — shared argv value-guard (Phase A, M-CLI-3 / D2)

A single value-consuming helper that rejects a `--`-prefixed token where a value is expected. Parameterized over how each host signals "missing", so hosts keep their existing error DUs.

```fsharp
// Returns the value + remaining tokens, or signals missing (a --prefixed next token counts as missing).
// onMissing lets each host map to its own MissingValue / MissingOptionValue / string-record error.
val requireValue:
    option: string ->
    onMissing: (string -> 'err) ->
    rest: string list ->
        Result<string * string list, 'err>
```

Call-site shape at each option arm (replacing `"--repo" :: v :: more -> … Some v`):
```fsharp
| "--repo" :: rest ->
    match requireValue "--repo" MissingValue rest with
    | Ok (v, more) -> go { acc with Repo = Some v } more
    | Error e -> Error e
```
Equivalent guard already exists for `--paths`: `t :: more when not (t.StartsWith "--")`.

**Behavior contract**: `--repo --json` → `Error (MissingValue "--repo")` (was: `Repo = "--json"`, JSON dropped). A legitimate value that starts with `--` is out of scope (no host option documents one).

## Contract C — `ArtifactReading.fsi` minimal widening (Phase B, D6)

`EvidenceCommand` must compose these instead of copying internals. Current public surface:
```fsharp
val optionsFor: request: RunRequest -> ProjectOptions
val readArtifact: root: string -> artifact: ArtifactRef -> Result<string, string>
val loadSnapshot: request: RunRequest -> Result<ProjectSnapshot, string>
```
Add **only** what Evidence needs to drop its copy — candidates (finalize against Evidence's call sites):
```fsharp
// If Evidence needs the unit/raw-root shapes it currently uses locally, expose adapters
// rather than re-copying, e.g.:
val optionsForRoot: root: string -> ProjectOptions          // or have Evidence build a RunRequest
val loadSnapshotFromRoot: root: string -> Result<ProjectSnapshot, string>
// Plus any fact-derivation helper (specKitFacts/designFacts) Evidence reads, if not reachable
// via loadSnapshot's result.
```
Keep the widening as small as possible; prefer having Evidence construct a `RunRequest` and call the existing three over adding new surface. The dead `"present"` check divergence is resolved by using Cli's implementation.

**Baseline**: `Cli`/`ArtifactReading` surface-drift baseline updated for whatever `val`s are added.

## Contract D — `ExitDecision` consolidation (Phase C, D5)

No new signature; a **removal**. Each host's `Loop.fsi` drops its local:
```fsharp
type ExitDecision = Success | Blocked | UsageError' | InputUnavailable | ToolError
val exitCode: ExitDecision -> int
```
and references `CommandHost.ExitDecision` / `CommandHost.exitCode` instead (identical DU). **Baseline**: each host surface baseline updated to reflect the removed type/val. Fallback if adoption is noisier than the value: delete the dead canonical from `CommandHost.fsi` instead and update that one baseline.

## Verification hooks (all contracts)

- SurfaceDrift tests (in `Tests.Common`) are the enforcement: Phase A baselines gain the `CommandHost` `val`s and are otherwise **unchanged**; Phase B/C baselines change deliberately and in-commit.
- Behavior contracts (B, and the F13/F15/M-CLI-7 fixes) each get a RED→GREEN Expecto test driving the real parsed surface (`Loop` parse / `update`) — see [quickstart.md](../quickstart.md).
