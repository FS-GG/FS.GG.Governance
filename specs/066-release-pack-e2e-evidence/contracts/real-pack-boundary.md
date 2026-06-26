# Contract — Real-`dotnet pack` Pack-Boundary Evidence (US1)

**Closes** `065` T018 · **Covers** FR-001, FR-002, FR-006, FR-008, SC-001, SC-003.

This is a **test contract**, not a product-surface contract — it pins the *evidence shape* that proves
the already-wired `065` release boundary against a real `dotnet pack`. No product code changes.

## Harness

Drive the wired host through its existing entry:

```
Interpreter.run ports request   // ports = the REAL edge, request = { Repo; Format; ReleaseOut; AttestationOut }
```

with the faked `065` edge replaced by real ports:

| Port | `065` (synthetic) | `066` (real) |
|------|-------------------|--------------|
| `Execute: ExecutionPort` | `portsWithPacks` replay stub | `GateExecution.Interpreter.realPort` (real `System.Diagnostics.Process`) |
| `PackRead` | replay from injected `PackOutcome list` | real reader: locate the produced `.nupkg`, read packed version, compute `ArtifactHash` |
| `SenseHead`/`SenseEnvironment`/`SenseBuilder` | wired `065` normalized senses | same (unchanged) |

The fixture generates a real temp tree: ≥2 packable `net10.0` projects with explicit literal
`<Version>`s and package metadata, each declared in `.fsgg/release.yml` `packableProjects` with a
`dotnet pack` `packCommand` and a `baseline`.

## Required behaviour (asserted)

| Case | Input | Verdict / Exit | Recorded runs | Artifacts |
|------|-------|----------------|---------------|-----------|
| Bumped | every project packs at a bumped version | preconditions `Met`, `Exit = Success` (0) | one `Pack` run per project | `release.json` v2 + `attestation.json` written |
| Failed pack | one project's pack fails | `Blocked`, reason names the failing project | failed pack recorded with non-zero **sentinel** (never dropped) | no fabricated pass |
| Unbumped/downgraded | one project packs at ≤ baseline | `Blocked`, reason names project **and** offending version | packs recorded | — |
| No baseline | packable project with no released-version baseline packs at a first version | **not** blocked as a downgrade (`NoBaseline`, first release) | pack recorded | — |

## Determinism (SC-003, FR-006)

- Re-run the bumped case over unchanged inputs ⇒ `release.json` v2 and `attestation.json` byte-identical.
- Pack `durationNanos` is sensed metadata only and is **excluded** from the byte-identity comparison.
- No machine path, username, wall-clock, or environment string appears in any asserted output (the F26
  cores normalize; the fixture feeds literal inputs).

## SDK gating (FR-008)

Probe for a working `dotnet pack` before the real-pack tests. Absent ⇒ a **disclosed** Expecto skip with
a clear diagnostic naming the missing SDK; never a silent green. Any literal stand-in used to *provoke* a
pack failure (e.g. a deliberately broken project) carries `Synthetic` in the test name with a use-site
disclosure; the pack execution itself is real (Constitution V).

## Anti-requirements

- MUST NOT add or change any host/core/`.fsi`/surface — the host is consumed verbatim (FR-007).
- MUST NOT drop a failed pack run or fabricate a pass.
- MUST NOT assert over wall-clock-sensitive fields.
