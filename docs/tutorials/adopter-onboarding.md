# Adopter onboarding: empty directory → buildable, governed workspace

**Audience**: a newcomer adopting FS.GG.Governance who wants a runnable F#/.NET
workspace scaffolded and placed under governance. **Outcome (SC-001)**: in under 15
minutes, with no boilerplate to hand-write, you go from an empty directory to a
workspace that `dotnet build`s and is ready for the Governance lifecycle.

> **Anchored, not rotting (FR-008).** The scaffold → build → manifest steps below
> map 1:1 to assertions the worked-example test
> (`tests/FS.GG.Governance.Sample.SddReferenceProvider.Tests/WorkedExampleTests.fs`)
> runs on every build. The committed manifest this tutorial shows
> (`fixtures/sdd-reference/scaffold-manifest.golden.json`) is asserted
> byte-for-byte, so a provider/seam change that alters output fails the build
> before this page can drift.

## What this feature ships (and what it does not)

This repository ships a **reference demonstration** of the 071 template-provider
seam: a non-packable example provider under `samples/`, a layered worked example,
and these tutorials. It is **not** a change to `fsgg-sdd init`.

> **Boundary disclaimer (FR-013).** Production wiring of the seam into
> `fsgg-sdd init` — provider selection, lifecycle-skeleton-first ordering,
> exit-code mapping, manifest persistence — is owned by the sibling **`FS.GG.SDD`**
> repository. Here you drive the same seam a host would, directly.

## Layer ordering: lifecycle first, runtime second

A governed workspace has two layers:

1. **Lifecycle governance skeleton** (`.fsgg/`, `work/`, `readiness/`) — authored
   by the sibling-owned `fsgg-sdd init`. In the worked example this is a disclosed
   stand-in (`Support.lifecycleReservedPaths`); in production it is a precondition
   that **must precede** the runtime scaffold.
2. **Runtime project skeleton** (`<App>.sln`, `src/`, `tests/`) — emitted by the
   template provider and written by the seam.

The lifecycle paths are passed to the seam as `ScaffoldRequest.ReservedPaths`, so
the runtime layer can never collide with or overwrite the governance layer.

## Step 1 — Scaffold the runtime layer

Select the reference provider and run the seam against an empty target whose leaf
directory name becomes your app name (`MyApp` here):

```fsharp
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Sample.SddReferenceProvider

let req =
    { Request = { Target = target            // …/MyApp  (leaf ⇒ <App>)
                  ReservedPaths = [ ".fsgg/policy.fsgg"; "work/0001/spec.md"; "readiness/0001/state.json" ] }
      Provider = Some SddReferenceProvider.provider }

let model = Interpreter.run (Interpreter.realPorts target) req
```

**Asserted** by `WorkedExampleTests` ("empty dir → seam scaffolds…"): the terminal
outcome is `Scaffolded`, `Collisions = []`, and these provider-owned files exist:

```
MyApp.sln
src/MyApp/MyApp.fsproj
src/MyApp/Program.fs
tests/MyApp.Tests/MyApp.Tests.fsproj
tests/MyApp.Tests/Tests.fs
README.md
```

The dependency closure is **FSharp.Core only**, so the result builds offline.

## Step 2 — Build it (no hand-editing)

```bash
dotnet build MyApp.sln
```

**Asserted** by `WorkedExampleTests` ("emitted skeleton `dotnet build`s
first-attempt"): exit code 0, first attempt, no edits (FR-004, SC-002). If the
.NET SDK is not installed, that test **skips with a named prerequisite rationale**
rather than failing — the SDK is the documented toolchain prerequisite.

## Step 3 — Confirm the deterministic manifest

The seam folds a deterministic provenance record; projecting it with
`ScaffoldManifestJson.ofManifest` yields, byte-for-byte:

```json
{"schemaVersion":"fsgg.scaffold-manifest/v1","outcome":"scaffolded","refusal":null,"provider":{"id":"fsgg.sample.sdd-reference","contractVersion":"1.0"},"generated":[{"path":"MyApp.sln","ownership":"providerOwned"},{"path":"README.md","ownership":"providerOwned"},{"path":"src/MyApp/MyApp.fsproj","ownership":"providerOwned"},{"path":"src/MyApp/Program.fs","ownership":"providerOwned"},{"path":"tests/MyApp.Tests/MyApp.Tests.fsproj","ownership":"providerOwned"},{"path":"tests/MyApp.Tests/Tests.fs","ownership":"providerOwned"}],"collisions":[]}
```

**Asserted** by `WorkedExampleTests` ("manifest projection matches the committed
golden…"): equal to `fixtures/sdd-reference/scaffold-manifest.golden.json`, and a
second fresh-target run is byte-identical — no absolute path, clock, or
environment leaks in (SC-003, SC-005). The manifest carries **no** absolute target
path, so it is attributable to the provider id + contract version alone.

## Step 4 — Govern, verify, ship (cross-references)

> **Not exercised by this feature's e2e check.** The steps below are existing
> Governance surfaces, shown here only to complete the adopter's mental model
> (FR-005, SC-005). They are documented by their own features, not asserted by the
> worked example.

Once the runtime app builds, the governed lifecycle proceeds with the
Governance commands shipped by prior features:

- **Govern** — author/refresh the `.fsgg` policy and routing inputs. For a curated,
  populated starting point you can copy unedited, see the reference gate set at
  [`samples/sdd-reference-gate-set/`](../../samples/sdd-reference-gate-set/README.md) —
  a full `project`/`policy`/`capabilities`/`tooling` set with `build`/`test`/`evidence`
  gates and a non-blocking-by-default (`light`) posture.
- **Verify** — `fsgg verify` runs the product-surface checks (feature 067/F24).
- **Ship** — `fsgg ship` evaluates the release rules against the gathered evidence.

For how the scaffolded workspace's SDD readiness feeds the Governance loop, see
[sdd-governance-handoff.md](./sdd-governance-handoff.md). To author your **own**
provider instead of the reference one, see
[provider-author.md](./provider-author.md).
