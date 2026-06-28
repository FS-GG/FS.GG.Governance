# Contract: `fsgg-contracts` consumption by Governance Config

This feature exposes **no new** interface and changes **no existing** public surface. The
"contract" exercised here is the *inbound* consumption contract between `FS.GG.Governance.Config`
(consumer) and the org-shared `FS.GG.Contracts` package (authority, owned by FS.GG.SDD). This
document pins exactly what Governance depends on, so any future package change can be assessed
against it.

## Consumed package

- **Package id:** `FS.GG.Contracts`
- **Registry contract id:** `fsgg-contracts` (`FS-GG/.github` `registry/dependencies.yml`)
- **Pinned version:** `1.0.1` (caps=2), owner `sdd`
- **Restore source:** org GitHub Packages feed (`nuget.config`)
- **Footprint:** BCL-only — `FSharp.Core` 10.1.301 is its only dependency

## Consumed surface (the only symbols Governance binds)

| Symbol | Type | Expected value | Used by |
|---|---|---|---|
| `Fsgg.Schemas.governanceVersion` | `int` | `1` | `Schema.supportedVersionFor Project` |
| `Fsgg.Schemas.policyVersion` | `int` | `1` | `Schema.supportedVersionFor Policy` |
| `Fsgg.Schemas.capabilitiesVersion` | `int` | `2` | `Schema.supportedVersionFor Capabilities` |
| `Fsgg.Schemas.toolingVersion` | `int` | `1` | `Schema.supportedVersionFor Tooling` |

Nothing else from `Fsgg.Schemas` (record shapes, marker types, other version constants) is bound.

## Consumer-side guarantees (what this feature must hold)

- **C1 — restore parity.** The `FS.GG.Contracts` reference restores at exactly `1.0.1` from the
  org feed, in locked mode, with the regenerated `packages.lock.json`. (SC-005, FR-001, FR-010)
- **C2 — value parity.** The four consumed constants resolve to `1/1/2/1`, reproducing today's
  hard-coded supported versions. (SC-004, FR-002)
- **C3 — no local literal.** No Governance-local literal supplies the supported version for the
  four files after the change. (SC-004, FR-002)
- **C4 — behavior parity.** Consuming the constants changes no validation behavior: identical
  typed facts for valid inputs, identical diagnostic id/locator/message for invalid inputs
  (including unsupported-`capabilities` + migration pointer), preserved determinism. (FR-005,
  FR-006, SC-002, SC-003)
- **C5 — surface parity.** The public Config surface baseline is byte-identical; downstream
  projects compile unchanged. (FR-009, SC-007)
- **C6 — downstream parity.** Every Governance command/projection/gate golden and snapshot is
  byte-identical. (FR-008, SC-006)

## Forward-compatibility note (US3 / FR-001)

A future supported-version change reaches Governance by advancing the `fsgg-contracts` registry
pin and the central `PackageVersion`, then re-restoring — **not** by editing `Schema.fs`. The
mapping table above is the only edit point if SDD ever adds/removes a governed `.fsgg` file.

## Verification

This is a documentation/dependency contract, not an executable schema. It is verified by the
quickstart parity checks (build, full test suite, surface-drift, golden diff) and the SC-004
no-local-literal inspection — see [../quickstart.md](../quickstart.md).
