# Contract: Template Provider Seam (in-process port)

**Feature**: `071-runtime-project-templates` · **Contract version**:
`fsgg.template-provider/v1` → `ProviderContractVersion { Major = 1; Minor = 0 }`.

This is the single seam across which **all** delegation happens (FR-004). The tool
knows a provider *only* through this contract; it hardcodes no provider name,
package id, target name, toolchain, or layout (FR-003).

## C1. The provider port

A provider is a resolved in-process value:

```fsharp
type TemplateProvider =
    { Id: ProviderId
      ContractVersion: ProviderContractVersion
      Emit: ScaffoldRequest -> Result<ProviderEmission, ProviderError> }
```

**Provider obligations**

1. **Describe, do not write.** `Emit` returns a `ProviderEmission` (target-relative
   paths + contents) or a `ProviderError`. It MUST NOT touch the filesystem,
   network, environment, or clock. The **tool** performs every write (D1).
2. **Stay in-bounds.** Every `RelativePath` MUST be relative, free of any `..`
   segment that escapes the target, and not rooted. Out-of-bounds paths are
   rejected by the tool (`Refusal.OutOfTarget`), not honoured (FR-009, D5).
3. **Own only runtime files.** A provider MUST NOT emit paths under the
   `ReservedPaths` (the host-owned lifecycle skeleton); doing so is treated as a
   collision (FR-007, D3).
4. **Fail cleanly.** On internal failure, return `ProviderError` rather than
   throwing or partially writing. (The tool still guards by catching exceptions at
   the edge and reifying them — C4.)

## C2. Version negotiation (tool-owned, pre-invoke)

The tool supports the inclusive range for the current major:

```
compatible(declared)  ⇔  declared.Major = 1  ∧  declared.Minor ≤ 0
```

An incompatible provider is refused **before** `Emit` is called, with an actionable
diagnostic naming the declared vs supported version (`Refusal.ContractMismatch`),
and **no** scaffolding occurs (FR-009, US2 AS3).

## C3. Identical treatment of every provider (FR-004)

The tool applies the **same** safety, recording, and reporting rules to every
provider — built-in, fake, or third-party. Delegation behaviour differs *only* in
what the provider emits (US2 AS2). There is no provider-specific branch anywhere in
the tool: selection resolves to a `TemplateProvider` value and the same
`Loop`/`Interpreter` path runs.

## C4. The edge interpreter is total and safe (FR-008, SC-005)

`Interpreter.step`/`run` execute the pure core's `Effect`s against injected
`Ports`:

```fsharp
type Ports =
    { Invoke : TemplateProvider -> ScaffoldRequest -> Result<ProviderEmission, ProviderError>
      Probe  : string list -> Result<string list, string>   // returns the subset that already exists
      Write  : (string * string) list -> Result<unit, string>  // atomic, all-or-nothing (temp+rename)
      Out    : string -> unit }

val realPorts : target: string -> Ports
val step      : ports: Ports -> effect: Loop.Effect -> Loop.Msg
val run       : ports: Ports -> request: Loop.RunRequest -> Loop.Model
```

Guarantees:

- **Never throws.** Every port `Error` and every thrown exception is caught and
  reified to the matching `Msg` (a failed invoke ⇒ refusal; a probe/write failure ⇒
  a recoverable refusal). Mirrors `RouteCommand.Interpreter`.
- **No overwrite.** `Probe` runs before `Write`; any existing/reserved path refuses
  the whole batch — the tool never overwrites operator or prior-run content (FR-007).
- **No partial tree.** `Write` is all-or-nothing (temp + atomic rename); a failure
  leaves zero new files (SC-005). The host-owned lifecycle skeleton is never written
  by this seam, so it is always left valid (FR-008).
- **Deterministic re-run.** Re-running over an already-scaffolded target reports the
  existing files as collisions and writes nothing (edge case "re-run after a prior
  scaffold").

## C5. Selection & ownership boundaries (out of scope here)

- **Provider resolution** (registry, assembly discovery/loading) is a **host**
  concern; this seam receives an already-resolved `TemplateProvider` value (D1, D0).
- **The lifecycle skeleton** (`.fsgg/`/`work/`/`readiness/`) is authored by the host
  *before* the seam runs and is never created, mutated, or owned by a provider
  (spec assumption; the seam only adds runtime files and records them).
- **Trust**: selecting a provider is an explicit operator decision; this contract
  enforces path-boundary and contract-version safety only — content sandboxing is
  out of scope (spec Assumptions).
