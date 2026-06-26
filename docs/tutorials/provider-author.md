# Provider author: clone the reference, bring your own stack

**Audience**: an author who wants to scaffold a **different** runtime stack
(a different layout, package set, or language flavor) through the same Governance
seam. **Outcome (FR-006, SC-004)**: you clone the reference provider, change only
what it *emits*, and run through the **identical** seam — with **no** change to
the tool.

> **Anchored, not rotting (FR-008).** The clone path and the version-mismatch
> refusal below map to assertions in
> `tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/FailurePathTests.fs`
> ("cloned provider runs through the identical seam…" and "contract-mismatch
> clone → Refused(ContractMismatch)…").

## The ownership boundary (FR-012)

A template provider owns **only** the files it emits. The seam owns everything
else — delegation, path-safety, recording, and reporting — and applies it
**identically** to the reference provider, your clone, and any third party. There
is no provider-specific branch anywhere in the tool. Concretely:

| Concern | Owner |
|---|---|
| *Which* files exist and their contents | **the provider** (its `Emit`) |
| Boundary checks (`..`-free, not rooted, in-target) | the seam |
| Collision detection against reserved/existing paths | the seam |
| Atomic all-or-nothing write | the seam |
| The deterministic provenance manifest | the seam |

So authoring a provider is authoring **data**: a `providerId`, a declared contract
version, and a pure `Emit`.

## Step 1 — Start from the reference

Copy `samples/FS.GG.Governance.Sample.SddReferenceProvider` as your starting point.
The whole provider is the curated `.fsi` plus a pure `.fs`:

```fsharp
module SddReferenceProvider =
    val providerId : Model.ProviderId          // ProviderId "fsgg.sample.sdd-reference"
    val provider   : Model.TemplateProvider     // ContractVersion { Major = 1; Minor = 0 }; pure Emit
```

## Step 2 — Change the id and what `Emit` describes

Pick your own stable id and emit your own target-relative file set. `Emit` is
**pure**: it returns a description, touches no filesystem/clock/env, and never
throws. The reference derives `<App>` from the target's leaf name; do the same or
anything else deterministic.

```fsharp
let provider =
    { Id = ProviderId "acme.my-stack"
      ContractVersion = { Major = 1; Minor = 0 }       // the seam's supported range
      Emit =
        fun request ->
            Ok { Files = [ { RelativePath = "hello.txt"; Contents = "from a clone\n" } ] } }
```

Honor the five contract obligations (071 C1): **describe, don't write**; stay
**in-bounds** (every path relative, `..`-free, not rooted); own **only runtime
files** (nothing under `request.ReservedPaths`); **fail cleanly** with a
`ProviderError` rather than throwing; be **deterministic**.

## Step 3 — Run it through the same seam

Select your provider exactly as the adopter selected the reference one — the
`Interpreter.run` call is unchanged:

```fsharp
Interpreter.run (Interpreter.realPorts target)
    { Request = req; Provider = Some provider }
```

**Asserted** by `FailurePathTests` ("cloned provider runs through the identical
seam — only emitted files differ"): the terminal outcome is `Scaffolded`, the
manifest records **your** id verbatim, and only **your** emitted files appear on
disk. No edit to `Scaffold` or the tool was required.

## Step 4 — Declare a compatible contract version

The seam supports contract major `1`. A provider declaring an incompatible major
is refused **before any invocation**, with no files written:

```fsharp
let broken = { provider with ContractVersion = { Major = 2; Minor = 0 } }
// Interpreter.run … ⇒ Refused (ContractMismatch { Major = 2; Minor = 0 })
```

**Asserted** by `FailurePathTests` ("contract-mismatch clone →
Refused(ContractMismatch), no files written"): the refusal names the declared
version and the target stays empty — an actionable, recoverable diagnostic
(Principle VI).

## Step 5 — Keep your provider an example, not the product surface

The reference provider is `IsPackable=false` and lives under `samples/` with its
**own** additive surface baseline — so the example is structurally separate from
the generic core (FR-002, SC-006). If you ship your provider, keep the same
posture: provider-specific knowledge lives in your provider, never in the seam.

> **Boundary disclaimer (FR-013).** Wiring provider selection into a production
> `fsgg-sdd init` invocation is owned by the sibling **`FS.GG.SDD`** repo. This
> tutorial shows the seam-level contract your provider must satisfy; the host that
> resolves and selects it is a separate concern.
