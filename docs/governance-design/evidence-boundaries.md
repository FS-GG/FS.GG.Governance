---
title: Evidence boundaries
category: Governance design
categoryindex: 7
index: 12
description: Executable evidence classes, outcome proof, producer inventories, and safe failure.
---

# Evidence boundaries

Evidence is only as strong as the boundary it observes.  The governed shape is
implemented by `FS.GG.Governance.DesignChecks.EvidenceBoundary` and exercised in
`EvidenceBoundaryTests.fs`; this page is guidance, not a substitute for its
fixtures.

## Required receipt facts

Each changed behavior carries semantic-regression, boundary-fixture, and
golden/schema evidence.  A reachable behavior also carries a production journey.
Each record names whether it is real or synthetic, its represented seam, command,
exit code, source/head digest, and freshness.  Synthetic evidence is useful but
cannot silently satisfy a real-boundary obligation.

An emitted request proves dispatch only.  It does not prove the native host,
filesystem, process, renderer, or other boundary observed the requested outcome.
Use an `ObservedOutcome` fixture for that claim.  A malformed or unknown input,
partial write, or optional integration must produce its explicit diagnostic or
`Degraded` outcome; it may never become an empty successful answer.

## Generated and mitigation claims

A generated or tool-facing artifact records its source, deterministic
regeneration, named consumer, and golden/schema compatibility check.  A file with
no consumer is not evidence of a contract.

For “X is never requested”, inventory every constructor/producer class of X and
run a mutation for each class that reintroduces it.  A generic test seam or a
mutation of an unrelated input does not establish the mitigation.

## Render evidence

A render script is a gated product.  Its fixtures compile and run in the normal
gate.  It declares either byte reproducibility or a stable semantic/render receipt
when bytes are intentionally not reproducible.  The test suite includes the latter
shape by invoking a real local process and persisting a deterministic receipt.

## Runnable controls

`dotnet test tests/FS.GG.Governance.DesignChecks.Tests/FS.GG.Governance.DesignChecks.Tests.fsproj`
executes the positive case and red controls for dispatch-only evidence, malformed,
unknown, stale and partial states, a consumerless artifact, incomplete producer
mutation, and the render receipt.  Add new evidence rules beside a corresponding
positive fixture and a control that changes the exact fact asserted.
