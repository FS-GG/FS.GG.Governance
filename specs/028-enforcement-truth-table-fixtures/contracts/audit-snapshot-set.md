# Contract: Blocking-Altering `audit.json` Snapshot Set (`fixtures/enforcement/audit-snapshots/`)

The committed snapshots are the JSON view of the truth table's blocking-altering rows (P2). Each file is the
**verbatim** output of `AuditJson.ofShipDecision (Ship.rollup route mode profile)` for a named scenario —
no new or altered schema (FR-008); the bytes are exactly the merged `fsgg.audit/v1` document. This contract
fixes the **named scenario set** and each snapshot's source decision so the set provably covers every dial
that can flip blocking (FR-010) and demonstrates the no-hide rule (FR-009).

## File-level rules

- One file per scenario: `audit-snapshots/<name>.audit.json`, where `<name>` is the scenario slug below.
- Bytes are produced solely by `ofShipDecision` — UTF-8, the schema's own field order and newline behavior,
  no post-processing. The guard reads each committed file and asserts byte-equality with a freshly produced
  document for the same scenario (regenerate-and-compare); on mismatch it fails naming the file and the
  `BLESS_FIXTURES=1` re-bless command.
- All routes are assembled with the F025 `Support.fs`-style real builders (`mkGate`, `mkSelectedGate`,
  `mkFinding`, `mkRoute`) and rolled up by the genuine `Ship.rollup` — no hand-built `ShipDecision`, no
  mocks (Principle V).

## The named scenarios (one per blocking-altering lever)

Each scenario isolates **one dial** as the lever that flips blocking. "Item" = the gate or finding whose
partition the dial controls. The `expected partition` is asserted on the emitted document.

| `<name>` | Dial under test | Source decision (route / mode / profile) | Expected partition | Demonstrates |
|---|---|---|---|---|
| `maturity-withholds-observe` | maturity | a base-blocking-eligible item whose `Maturity = Observe` at `gate`/`standard` | **warnings** (effective advisory) | `observe` withholds blocking; warning carries base+effective severity + reason (FR-009) |
| `maturity-withholds-warn` | maturity | same item with `Maturity = Warn` at `gate`/`standard` | **warnings** | `warn` withholds blocking (no-hide) |
| `base-advisory-stays-advisory` | base severity | a base-`advisory` item under the strictest `release`/`release` | **passing** | base-advisory never escalates (FR-009 / Edge) |
| `profile-relaxes-blocker` | profile | a `block-on-release` gate at `gate` mode under a **looser** profile (`light`) that keeps it relaxed | **warnings** | a profile relaxing the floor leaves a base-blocker as a self-explaining warning |
| `profile-tightens-to-block` | profile | the same `block-on-release` gate at `gate` mode under a **stricter** profile (`release`) that pulls the floor down so it blocks | **blockers** | a stricter profile flips a relaxed finding into blocking (the inverse) |
| `mode-below-floor` | run mode | a `block-on-ship` gate under a mode **below** its boundary (e.g. `inner`) | **warnings** | run mode not reaching the boundary → relaxed |
| `mode-reaches-floor` | run mode | the same `block-on-ship` gate under a mode that **reaches** the boundary (e.g. `gate`) | **blockers** | run mode reaching the boundary → blocks |

> The exact mode/profile pairs above are chosen so the *named* dial is the sole difference between a
> blocking and a non-blocking outcome; the implementer confirms each pair against
> `deriveEffectiveSeverity` while generating, and the partition assertions (below) catch any mis-pick. The
> `profile-relaxes-blocker` / `profile-tightens-to-block` pair and the `mode-below-floor` /
> `mode-reaches-floor` pair are deliberately authored as minimal contrasts (one dial changed) so the diff
> between the two committed snapshots isolates exactly that lever's effect.

## Coverage assertion (FR-010, SC-004)

- The set of `dial under test` values across the committed files MUST include **every** dial that can flip
  a finding's blocking status: maturity, base severity, profile, and run mode. A coverage test over the
  scenario table fails if any is missing — no blocking-altering lever is left unrepresented.
- Each scenario whose item is a **relaxed** base-blocker (it appears in `warnings`) MUST, in its snapshot,
  show that item with a nested `enforcement` object carrying both `baseSeverity` and `effectiveSeverity`
  (differing) and a non-empty `reason` — the no-hide rule observed in the committed bytes (FR-009).
- Every snapshot's `verdict`/`exitCodeBasis` and three-section partition are whatever `ofShipDecision`
  emits; the tests assert the **item lands in the expected section**, never recompute the verdict (the F025
  contract already fixed it).

## Non-goals (unchanged from the merged schema)

- No new top-level fields, no field reordering, no schema-version change (FR-008): if `ofShipDecision`'s
  output shape ever changes, that is an F025 change and these snapshots re-bless to follow it — this row
  never edits the projection.
- No host paths, timestamps, environment values, numeric exit codes, or provenance — the merged projection
  already excludes them; these snapshots inherit that exclusion.
