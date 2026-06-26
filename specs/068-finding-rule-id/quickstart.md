# Quickstart: Per-Finding Rule Identity

Runnable validation scenarios proving the feature end-to-end. They map to the spec's Success Criteria. See
[data-model.md](./data-model.md) for entities and [contracts/finding-rule-id.md](./contracts/finding-rule-id.md)
for the wire grammar.

## Prerequisites

```bash
dotnet build FS.GG.Governance.sln
```

## Build & unit-test the new leaf

```bash
dotnet test tests/FS.GG.Governance.RuleIdentity.Tests
```

Confirms: each constructor produces its source-prefixed token; the five prefixes are disjoint; `ruleIdToken` is
total and deterministic; `unattributed` never yields an empty id.

## SC-001 — Every emitted finding names its rule

```bash
dotnet test tests/FS.GG.Governance.AuditJson.Tests \
            tests/FS.GG.Governance.VerifyJson.Tests \
            tests/FS.GG.Governance.RouteJson.Tests
```

Over a fixture that triggers ≥1 finding on each surface, assert every per-finding/per-item object carries a
non-empty `ruleId` at the contracted position, with all pre-existing fields and the nested `enforcement` object
unchanged.

## SC-002 / SC-004 — Profile/mode invariance, no dropped findings

A single finding-bearing fixture evaluated under every `Profile` (`light`/`standard`/`strict`/`release`) and
every `RunMode`:

```bash
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests --filter RuleIdInvariance
dotnet test tests/FS.GG.Governance.ShipCommand.Tests   --filter RuleIdInvariance
```

Assert: the set of `ruleId`s is byte-identical across all profile/mode combinations; no finding is dropped; only
`enforcement.effectiveSeverity` may differ. The sensed catalog `RuleHash` is identical across the same runs
(rule-hash anchor, C4).

## SC-003 — No-findings byte-identity

```bash
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests --filter Golden
dotnet test tests/FS.GG.Governance.RouteCommand.Tests  --filter Golden
dotnet test tests/FS.GG.Governance.ShipCommand.Tests   --filter Golden
```

The frozen empty-case goldens (`verify.no-declaration.json`, the empty route/ship goldens) MUST stay
byte-identical — no `ruleId`, no schema bump, no reordering.

## SC-005 — Cross-surface id match

```bash
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests --filter CrossSurfaceRuleId
```

Produce `verify.json` and `audit.json` over identical inputs; a finding present in both carries an identical
`ruleId` on both surfaces.

## SC-006 — Honest boundary ids, no fabricated/empty ids

```bash
dotnet test tests/FS.GG.Governance.RouteJson.Tests --filter Boundary
```

Every kernel/boundary finding (unknown-governed-path) carries a non-empty `boundary:`-prefixed id, distinguishable
from `gate:` ids; no `unattributed:` token appears for the standard fixtures.

## Re-blessing finding-bearing goldens (deliberate)

After implementing the writers, re-bless the goldens that gain `ruleId` in one commit:

```bash
BLESS_GOLDEN=1 dotnet test tests/FS.GG.Governance.VerifyJson.Tests
# repeat for any other finding-bearing golden; verify the diff is exactly the additive `ruleId` field
git diff -- tests/**/goldens
```

The diff MUST show only the inserted `ruleId` field at the contracted position — no other byte changes.

## Full gate

```bash
dotnet test FS.GG.Governance.sln
```

All projection, command, and surface-drift baseline tests pass; the new `RuleIdentity` baseline is established.
