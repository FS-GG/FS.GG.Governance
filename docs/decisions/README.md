# Decision records — local index

Repo-local decision records for **FS.GG.Governance**. These capture design choices made *within*
this repository (why a boundary sits where it does, why a duplication is intentional, why a
unification is deferred). Each file is self-contained; this page is the index.

> **Two distinct series — do not conflate the numbers.** These local records (`0001`–`0008`, filed
> under `docs/decisions/`) are separate from the **org-level ADRs** that live in
> [`FS-GG/.github`](https://github.com/FS-GG/.github) and are indexed by
> [`docs/adr/README.md`](../adr/README.md). The two sequences reuse some numbers for *different*
> subjects (e.g. local `0007` is the CommandHost impure-leaf home; org `ADR-0007` is the
> schema-derived reference-gate-set version). Cite a local record as `docs/decisions/00NN` and an
> org ADR as `ADR-00NN`.

## Local records

| # | Subject |
|---|---|
| [0001](0001-structured-logging.md) | Structured logging: none in the kernel or the pure loop; observability via values |
| [0002](0002-sdd-governance-handoff-contract.md) | Acknowledge the SDD→Governance handoff contract v1.0.0 and confirm the evidence-state mapping |
| [0003](0003-gaterunhost-unification.md) | Defer the `GateRunHost` unification of `route → ship → verify` |
| [0004](0004-publish-governance-cli-org-feed.md) | Publish the consumer-bearing Governance CLI to the org feed |
| [0005](0005-profile-aware-handoff-gate-mode-mapping.md) | Profile-aware handoff-gate enforcement: the `Gate → Verify` mapping and the `absent → Strict` fail-safe |
| [0006](0006-cli-format-flag-vocabularies.md) | The per-command output-format flag vocabularies intentionally diverge |
| [0007](0007-commandhost-owns-host-edge-impure-leaves.md) | `FS.GG.Governance.CommandHost` is the home for the shared host-edge impure leaves |
| [0008](0008-json-writer-duplication.md) | Per-projection JSON writer duplication is intentional; byte-identity is guarded by tests, not by extraction |

## Org-level ADRs

Cross-repo decisions binding this repo (handoff contract, package-ID permanence, `.fsgg` slot
ownership, publishing) live in `FS-GG/.github`. See [`docs/adr/README.md`](../adr/README.md) for the
local pointer table (including `ADR-0012`/`ADR-0013`, which govern packaging and Trusted Publishing).
When you add a new local decision here, add its row above; when you cite a *new* org ADR from this
repo, add a row to `docs/adr/README.md`.
