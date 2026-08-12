# Feature Specification: Package F# Surface Producer

**Feature Branch**: `item/396-package-fsharp-surface-producer`
**Created**: 2026-08-12
**Status**: In progress
**Input**: FS-GG/FS.GG.Governance#396

## User Scenarios & Testing

### User Story 1 - Produce a receipt from a released tool (Priority: P1)

A package-only consumer installs a released Governance tool and produces the v1 receipt for any F# project without a Governance project reference or source checkout.

**Independent Test**: Install the packed tool into an empty tool path, run it against a temporary F# project, and compare stdout to the persisted receipt.

### User Story 2 - Retain the v1 failure contract (Priority: P1)

An adopter receives the existing malformed/no-verdict outcome rather than a forged clean receipt for malformed policy or project input.

**Independent Test**: Run the packaged command against malformed policy and an unreadable project and assert exit 3 plus receipt semantics.

### Edge Cases

- Applicable projects select zero, one, and many signatures deterministically.
- Explicit test-project non-applicability remains a valid receipt.
- The output file is atomically replaced and does not expose partial JSON.

## Requirements

### Functional Requirements

- **FR-001**: Publish a supported installable Governance command accepting the existing root, project, test-project, and baseline options and writing the v1 receipt.
- **FR-002**: Persist atomically replaced deterministic JSON whose bytes equal stdout apart from the console terminator.
- **FR-003**: Preserve zero/one/many, explicit non-applicable, stale/baseline, and malformed-input semantics from #371/#375.
- **FR-004**: Pack the executable and all required runtime dependencies, and document package ID, command, exit codes, and minimum version.
- **FR-005**: Prove a packed artifact operates from a clean consumer fixture without source-project invocation, copied source, project references, or local command DLL.

## Success Criteria

- **SC-001**: A clean tool installation creates a parseable v1 receipt and stdout matches the result file.
- **SC-002**: The packed route exits 3 and emits no clean verdict for malformed policy and invalid project input.
- **SC-003**: Package inspection proves the command and runtime closure exist.

## Change Classification

Tier 1: public package/tool contract.

## Assumptions

The existing dedicated executable becomes the supported global tool and retains its source-project form for maintainers.
