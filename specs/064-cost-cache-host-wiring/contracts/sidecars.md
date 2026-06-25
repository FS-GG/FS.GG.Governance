# Contract — Two Provenance Sidecars

**Cores consumed (verbatim):** `CostBudgetJson.ofReport` (`fsgg.cost-budget/v1`), `ProvenanceJson.ofSnapshot`
(`fsgg.provenance/v1`).
**Mechanism:** two new `ArtifactKind` cases written through the host's **existing** atomic `WriteArtifact` port.

## Writes
| Sidecar | Content | Default path | `ArtifactKind` | Schema |
|---|---|---|---|---|
| cost-budget | `CostBudgetJson.ofReport report findings` | `readiness/cost-budget.json` | `CostBudgetArtifact` | `fsgg.cost-budget/v1` |
| provenance | `ProvenanceJson.ofSnapshot snapshot` | `readiness/provenance.json` | `ProvenanceArtifact` | `fsgg.provenance/v1` |

Both paths are overridable via the new `RunRequest.CostBudgetOut` / `RunRequest.ProvenanceOut` fields. Writes use the
existing temp-file + rename atomic writer; no new write port.

## Guarantees (asserted by tests)
- Both sidecars are written with their declared schema versions beside the existing artifacts (FR-005, SC-003).
- **Determinism:** two runs over unchanged inputs ⇒ byte-identical sidecars; reordering candidate gates changes
  nothing; no wall-clock/username/environment/abs-path leakage (FR-006, SC-003).
- **Empty inputs:** no expensive gates / no recorded runs ⇒ both sidecars are still well-formed with empty arrays;
  existing goldens untouched (edge case, SC-003).
- The sidecars are **new artifacts**, never edits to an existing one (FR-007, SC-004).
