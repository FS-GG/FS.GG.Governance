# Quickstart — Validating Release-Provenance E2E Pack Evidence & Byte-Identity Goldens

Runnable validation scenarios that prove this feature's evidence holds. All work is in `tests/`; no
product code changes. See [contracts/](./contracts/) for the per-story contracts and [research.md](./research.md)
for the pre-wiring anchor (`5a0cb28`).

## Prerequisites

- .NET SDK `net10.0` with a working `dotnet pack` (the real-pack fixtures probe for this and surface a
  disclosed skip when absent — FR-008).
- The `065` wiring on `main` (already landed).
- A clone with git history reaching commit `5a0cb28` (the pre-wiring anchor for the goldens).

## Scenario 1 — Real-`dotnet pack` pack-boundary (US1 / SC-001, SC-003)

```bash
dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests \
  --filter "TestCategory=RealPack | Name~RealPack"
```

**Expected**: all four cases green — bumped ⇒ preconditions `Met`, exit 0, each project recorded as a
`Pack` run, `release.json` v2 + `attestation.json` written; failed pack ⇒ blocked naming the project,
failed run recorded with its sentinel; unbumped/downgraded ⇒ blocked naming project + version; no-baseline
⇒ first release, not a downgrade. Re-running the bumped case ⇒ byte-identical `release.json` v2 +
`attestation.json` (pack duration excluded). On a host without `dotnet pack`: a disclosed skip with a
diagnostic, never a silent pass.

## Scenario 2 — Mergeable-but-not-releasable (US2 / SC-002)

```bash
dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests \
  --filter "Name~Mergeable | Name~Releasable"
```

**Expected**: for the mergeable-but-not-releasable product, `fsgg ship` exits 0 while `fsgg release`
exits 1 with a distinct basis, and `release.json` v2 names the publish-plan / trusted-publishing /
template-pin preconditions with the failing one unmet + a named reason; for the fully-releasable product,
`fsgg release` exits 0 and all three preconditions are satisfied.

## Scenario 3 — Four frozen byte-identity goldens (US3 / SC-004)

```bash
dotnet test tests/FS.GG.Governance.RouteCommand.Tests   --filter "Name~golden | Name~ByteIdentity"
dotnet test tests/FS.GG.Governance.ShipCommand.Tests    --filter "Name~golden | Name~ByteIdentity"
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests  --filter "Name~golden | Name~ByteIdentity"
dotnet test tests/FS.GG.Governance.ReleaseCommand.Tests --filter "Name~golden | Name~ByteIdentity"
```

**Expected**: each producing command's output is byte-identical to its committed golden —
`route.json` / `ship.json` / no-declaration `verify.json` (frozen from `5a0cb28`) and the empty-additive
`release.json` v2 (the F26-blessed contract). Any drift fails loudly.

## Scenario 4 — Full sweep & deferral closure (SC-005)

```bash
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln
```

**Expected**: the full solution build + test sweep is green. Then confirm the bookkeeping:

- `specs/065-release-provenance-host-wiring/tasks.md` — T009, T018, T023, T024 marked `[X]`, citing `066`.
- `docs/initial-implementation-plan.md` — the F26 "Partial follow-ups" note rewritten to record the
  real-pack evidence + frozen goldens as **closed**.

## Constitution gate checks

- **No product surface change** — `git diff --stat` touches only `tests/`, the four golden files, the
  `065` tasks.md, and the roadmap doc. No `src/`, no `.fsi`, no `surface/*.txt`, no
  `Directory.Packages.props` change (FR-007, Tier 2).
- **Real evidence** — the pack execution is the real `GateExecution.Interpreter.realPort`; only a
  deliberately-broken project (to force a pack failure) is `Synthetic`-named and disclosed (Constitution
  V).
- **Honest goldens** — route/ship/no-decl-verify frozen from pre-wiring `5a0cb28`, not re-derived from
  `main` (non-vacuous, spec edge case).
