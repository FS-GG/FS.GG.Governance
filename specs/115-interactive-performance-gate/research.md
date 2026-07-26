# Research

- `governance-handoff` 2.0.0 is the first handoff carrying
  `PerformanceIntentDeclaration option`; its dependencies are objects with `dependent` and
  `dependency`, and governed references are flat records.
- `performance-evidence-v1` raw samples contain every binding needed for independent evaluation.
- SDD computes percentiles by nearest rank: sort, then index `ceil(p*n)-1`.
- Handoff diagnostic `staleEvidence` carries related evidence ids and is the available typed
  freshness signal at this boundary.
- `claimedBudgetPassed` and handoff `measurements` are producer projections, so Governance checks
  them against its own calculation but never trusts them as the verdict.
