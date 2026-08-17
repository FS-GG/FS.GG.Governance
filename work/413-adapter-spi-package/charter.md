---
schemaVersion: 1
workId: 413-adapter-spi-package
title: Publish package-consumable adapter SPI
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# Publish package-consumable adapter SPI Charter

## Identity
- Publish `FS.GG.Governance.Adapters.Spi` as a supported package-only authoring surface so product repositories can compile domain adapters without consuming Governance source or tool internals.

## Principles
- Preserve the existing curated `.fsi` contract and keep the SPI pure: only `FSharp.Core`, the BCL, and the published Kernel package may be runtime dependencies.
- Prove the bytes consumers install, in locked mode and from an isolated package feed, rather than treating a source-project build as package evidence.
- Declare public API and package metadata together, with a negative fixture and subject-mutation evidence for every new gate.

## Scope Boundaries
- In: SPI package metadata/version, Kernel dependency, package/API compatibility gates, clean external F# consumer, negative absence/transitivity control, documentation, and release evidence.
- Out: new adapter semantics, changes to the Kernel contract, command-host or I/O dependencies, CLI tool installation as an SDK mechanism, and adoption changes in S.I.R.#198.

## Policy Pointers
- Honor `.fsgg/constitution.md` principles I, III, VI, and VIII: specify first, declare the public surface, provide fail-before/pass-after evidence, and fail closed on unreadable package facts.
- Follow `.fsgg/sdd.yml` and `.fsgg/agents.yml`; generated views remain projections rather than authority.
- Preserve the repository's pack/API-validation and coherent-release conventions before any downstream consumer pin changes.

## Lifecycle Notes
- Tier 1 cross-repository package contract. S.I.R.#198 remains blocked until this producer package is merged, released as required, and publicly consumable.
- Next lifecycle action: `fsgg-sdd specify --work 413-adapter-spi-package`.
