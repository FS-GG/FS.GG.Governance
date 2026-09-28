# C3-GOVERNANCE-01 — Ordinary V2 receiver adoption

Status: installed. Dedicated custody and immutable Coordination CLI 0.1.5 are pinned; protected-main pushes run secret-free qualification before the bounded settlement job.

FS.GG.Governance is the fixed C3 source repository (`FS-GG/FS.GG.Governance`, repository ID
`1273065119`) under the code-owned `governance-v1` profile. This change adds only repository-owned
receiver source. It changes no repository setting, environment, secret, branch protection, required
check, generated workspace content, or protected effect.

## Prepared source

- The activation workflow binds protected-main pushes to a secret-free predecessor receipt and
  admits the bounded credential job only when that exact receipt selects activation.
- The source pattern comes from Net receiver commit
  `91c2a57d4e9cab553718bdd9f796a4b1e0e9aaa0`; the observer retains its repaired Audio bytes,
  and the qualifier replaces only the code-owned source profile. Their SHA-256 digests are
  `6e63f6f724fb267e77ef02f41b4c58a833e0dc5b08c003f07fd16c89e4f7fd55` and
  `b08b0335bfb9b490e4c9e603d09ed3c49c6810ece63e4ee12102a33be1db055f` respectively.
- The selected settlement checks are `Deterministic gate (locked restore + build)` and
  `contract-coherence / coherence`. The nine separate native required gates retain those two
  checks, the Debug and Release full test suites, build-config drift, reference-gate pack guard,
  `kit / coordination-kit`, `skill-view-check`, and `materialize / receiver-validate`.
  The policy records every exact check name. Every check is bound to
  GitHub Actions App `15368` and its exact workflow ID,
  path, event, PR head, run, job, suite, and attempt.
- Both selected checks come from Governance's existing gate workflow `303429298` at
  `.github/workflows/gate.yml`; the other producer workflows are coordination coherence
  `307166226`, skill-view check `321983573`, and kit materialization `316870228`.
  Governance does not produce or require
  `routine-eligibility`, and this receiver does not add that check.
- The shared policy ID remains `v2-ci-i1-ordinary-settlement-v1`; the shared Authority anchor retains
  App `5064713`, installation `164553252`, repository `FS-GG/FS.GG.Coordination.Authority`
  (`1351660651`), `contents:write`, metadata read, and the existing writer/integrity ruleset pins.
- Governance main `e4f5869837eebfb140b31bffb62ee41c3a6b2785` was independently read back with
  `ordinary-v2` environment ID `22921081283`, restricted to the single `main` branch policy ID
  `61288925`, with no reviewers and exactly the three dedicated ordinary-v2 secret names. The
  protected custody bridge run `36433647155` sealed three fixed values; the destination inventory
  was independently read back.
- Coordination CLI `0.1.5` is the required package because it contains the combined
  `governance-v1` source profile at source commit
  `1268908d2d5a38d30a764c927f3e0591e53138aa`. The immutable release tag points exactly to that
  source, and the public release package is pinned to SHA-256
  `3567a92825917a7d537f6c5c545d3a7947bc35edd666fc3a1898de3bf97267c9`. Its retained readback
  verifies matching package payloads from GitHub Packages and nuget.org and public-only install.
- Governance already pins .NET SDK `10.0.401` in the repository's tracked `global.json`. The
  credential job uses that tracked pin to install the immutable CLI and leaves the pin unchanged.

## Installation boundary

Activation requires one reviewed source change to verify all of:

1. an immutable published Coordination CLI supports the exact `governance-v1` source profile and its
   served package SHA-256 is pinned;
2. all three dedicated ordinary-v2 credentials are enrolled and independently read back without V1
   or callable-operation credential reuse; and
3. Governance identity, exact current required-check population, producer mappings, and shared
   Authority binding are freshly read back.

The activation changes policy status, installed state, package evidence, credential inventory,
observer guard, and the bounded credential job together only after that proof. It imports no V1
admission or receiver state.
