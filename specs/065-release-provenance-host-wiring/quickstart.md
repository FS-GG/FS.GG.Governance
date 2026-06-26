# Quickstart — Release-Provenance Host Wiring (F26 wiring)

Per-story validation scenarios proving the wiring end-to-end over the **real** F26 cores and real hosts. Build and
test from the repo root.

## Prerequisites

```bash
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln                 # full solution green, all existing goldens byte-identical
```

The pack fixtures run **real `dotnet pack`** through the F51 execution port over temp products; any synthetic pack
output is `Synthetic`-named and disclosed at the use site (Constitution V).

## Scenario 1 — Every packable project must pack at a bumped version (US1, P1)

```bash
# A temp product with several declared packable projects + version baselines, checked out standalone.
fsgg release --repo <tmp-product> --format json
```

Expected:
- Every project packs at a bumped version ⇒ pack/version preconditions `Met`, exit `0`; each pack recorded as a
  `Pack` run.
- One project's pack exits non-zero ⇒ **blocked** (exit `1`), reason names the project + pack failure, the failed
  `Pack` run is in the snapshot with its sentinel.
- One project packs unbumped/downgraded (vs its baseline) ⇒ **blocked** (exit `1`), reason names the project +
  version.
- Re-run over unchanged inputs ⇒ `release.json` v2 + `attestation.json` byte-identical (pack duration retained only
  as sensed `durationNanos`).

Covered by `ReleaseCommand.Tests` pack-boundary fixture (FR-001, FR-002, SC-001, SC-003).

## Scenario 2 — `release.json` v2 + attestation sidecar; boundary distinct from ship (US2, P1)

```bash
fsgg ship    --repo <tmp-product> --mode gate --profile standard --json   # mergeable
fsgg release --repo <tmp-product> --format json                            # releasable?
```

Expected:
- A product that passes `fsgg ship` (exit `0`) but is not releasable (unbumped version / missing publish plan /
  drifted pin) ⇒ `fsgg release` exits `1` with a release exit-code basis **distinct** from ship; `release.json` v2
  carries the failing precondition; `attestation.json` carries the compatible-shape marker.
- A fully releasable product ⇒ `fsgg release` exits `0` (`Clean`).
- `release.json` is `fsgg.release/v2`; `attestation.json` is `fsgg.attestation/v1`; both byte-identical on re-run.
- Existing `route.json` / `ship.json` goldens byte-identical to frozen baselines.

Covered by `ReleaseCommand.Tests` mergeable-but-not-releasable + determinism + byte-identity fixtures (FR-004,
FR-007, SC-002, SC-003, SC-005).

## Scenario 3 — `fsgg verify` previews release readiness advisorily (US3, P2)

```bash
fsgg verify --repo <tmp-product> --json                 # product with .fsgg/release.yml
fsgg verify --repo <tmp-no-release-decl> --json         # product without it
```

Expected:
- With a declaration ⇒ `verify.json` carries an advisory `releaseReadiness` block (`advisory: true`) with the same
  sensed evidence the release boundary would; verify's exit code is **unchanged** by the preview.
- A declared exhaustive matrix ⇒ recorded **deferred** to the scheduled boundary; verify does **not** pack.
- Without a declaration ⇒ `verify.json` byte-identical to its pre-wiring golden (no `releaseReadiness` block, no
  schema bump).

Covered by `VerifyCommand.Tests` release-preview + matrix-deferral + no-declaration byte-identity fixtures (FR-006,
FR-009, SC-004, SC-005).

## Scenario 4 — Standalone & safe failure (US4, P2)

```bash
fsgg release --repo <tmp-no-packables> --format json    # no packable projects
fsgg release --repo <tmp-broken-input> --format json    # unreadable pack / absent provenance / missing publish plan
```

Expected:
- No packable projects ⇒ pack precondition vacuously satisfied; report states "no packable projects"; no fabricated
  pack.
- Unreadable pack output / absent provenance input / missing publish plan ⇒ a clear input diagnostic naming the
  source (exit `3` `InputUnavailable`, distinct from tool defect `4`); release **blocks**; no hollow attestation,
  no fabricated pass.
- Decisions draw only on product-local sources (no monorepo path).
- Reordering the packable projects / command runs ⇒ byte-identical evidence, verdict, attestation, report.

Covered by `ReleaseCommand.Tests` standalone + safe-failure + reorder fixtures (FR-011, FR-013, FR-014, SC-006).

## Scenario 5 — Shared declaration leaf parses for both hosts

```bash
dotnet test tests/FS.GG.Governance.ReleaseDeclaration.Tests
```

Expected: the combined parse yields `Rules`/`Expectations`/`Layout` + `PackableProjects` + `Matrix`; a
`release.yml` without the additive sections parses with `PackableProjects = []` / `Matrix = None`
(backward-compatible); a malformed packable/matrix entry ⇒ `Error DeclError`.

## Constitution gate checks

- Surface baselines: `ReleaseDeclaration` added; `ReleaseCommand` / `VerifyCommand` re-blessed; the seven F26
  baselines unchanged. `BLESS_SURFACE=1 dotnet test …` then re-run drift green.
- Dependency boundary: no new external/NuGet dependency (`Directory.Packages.props` unchanged); no pure core gains
  a filesystem/process reference (FR-010).
- Full solution green with every existing golden byte-identical and the new outputs deterministic (SC-007).
