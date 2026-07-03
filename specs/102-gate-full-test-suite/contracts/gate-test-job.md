# Contracts: The gate runs the full test suite on every PR

Feature 102-gate-full-test-suite. Two contracts: **A** the new workflow job, **B** its binding to the pre-existing required status check. Both are authoritative shapes for the implementation and quickstart.

---

## Contract A — the `full-test-suite` job in `.github/workflows/gate.yml`

Added as a sibling of the existing `gate`, `build-config-drift`, `reference-gate-set-pack`, and `api-compatibility-gate` jobs. Mirrors the `gate` job's checkout → cached setup-dotnet → locked-restore shape, then calls `build.fsx test`.

```yaml
  # H1 (#45): the gate built but never TESTED — only 2 of 83 test projects ran anywhere in CI
  # (ReferenceGateSet.Tests here, Cli.Tests in publish.yml). Run the WHOLE Expecto suite on every
  # PR so a logic regression in any core/adapter/JSON/CLI/release project fails the merge instead of
  # merging green because it compiles. Whole-solution via build.fsx (bounded -m:N, spec 080) so the
  # 162-project graph doesn't over-subscribe the runner. SEPARATE job → a test failure is a distinct
  # signal, not serialized behind the build gate.
  #
  # NAME IS A CONTRACT: it must equal the required status-check context already registered in the
  # "main branch protection" ruleset (id 18430843). A typo → the required check never reports → every
  # PR blocks on a perpetually-pending check. Do not rename without updating the ruleset in lockstep.
  full-test-suite:
    name: Full test suite (dotnet fsi build.fsx test)
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
          # Warm the NuGet restore from the committed lockfiles. A lockfile change misses the cache
          # and re-restores; --locked-mode below still validates the graph (FR-003/FR-006).
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      # Single enforcement point: locked restore against the committed packages.lock.json. Mirrors the
      # `gate` job so a graph drift fails here too, with one clear place pointing at the regenerate cmd.
      - name: Restore (locked)
        run: |
          set -euo pipefail
          if ! dotnet restore FS.GG.Governance.sln --locked-mode; then
            echo "::error::locked restore failed — the resolved graph does not match the committed packages.lock.json (or a version was substituted, NU1603). If this is an intentional dependency change, run: dotnet restore FS.GG.Governance.sln --force-evaluate  and commit the updated packages.lock.json files."
            exit 1
          fi

      # Whole suite via the bounded entrypoint (spec 080): `test` builds then runs every test project in
      # the solution. --no-restore keeps the locked restore above as the single restore. No --no-build:
      # build.fsx test builds as part of the run. A red assertion fails the job (FR-010: no retries).
      - name: Test (full suite, bounded)
        run: dotnet fsi build.fsx test -c Debug --no-restore
```

**Invariants A**:
- `name` is byte-exact `Full test suite (dotnet fsi build.fsx test)` (Contract B).
- Exactly one restore, `--locked-mode`; the test step is `--no-restore`.
- No `continue-on-error`, no `matrix`, no retry wrapper.
- Whole-solution invocation — no enumerated project list (so new test projects are covered automatically).

**Non-goals A**: no edit to `build.fsx`, the `.sln`, any `.fsproj`, or any org-synced build-config file; no change to the other four jobs.

---

## Contract B — binding to the required status check (no ruleset edit)

The ruleset `main branch protection` (id `18430843`, enforcement `active`, target `~DEFAULT_BRANCH`) **already** requires these four contexts:

| Required context | Producing job | Status |
|---|---|---|
| `Deterministic gate (locked restore + build)` | `gate` | exists |
| `Build-config drift check (shared-build-config)` | `build-config-drift` | exists |
| `Reference gate set — pack guard (byte-identity + gated + versioned)` | `reference-gate-set-pack` | exists |
| `Full test suite (dotnet fsi build.fsx test)` | `full-test-suite` (Contract A) | **added by this feature** |

**Binding rule**: Contract A's `name` MUST equal the fourth context verbatim. On satisfying that, FR-008 (required-check present) and FR-009 (name ↔ required-check in sync) hold with **zero ruleset writes**. The `api-compatibility-gate` job is intentionally **absent** from this list (it is advisory / `continue-on-error`) and stays absent.

**Verification B** (read-only, no mutation):
```bash
gh api repos/FS-GG/FS.GG.Governance/rulesets/18430843 \
  --jq '.rules[] | select(.type=="required_status_checks")
        | .parameters.required_status_checks[].context'
# MUST include the exact string: Full test suite (dotnet fsi build.fsx test)
```

**If the required context were ever missing** (it is not, today): a maintainer with ruleset-write access adds the exact context in the same change that renames/introduces the job (FR-009). This feature does not perform that write because the context already exists.

---

## Contract C — behavioral proof obligations (for quickstart / PR evidence)

1. **RED on a hidden break**: mutate one assertion in a project ∉ {`ReferenceGateSet.Tests`, `Cli.Tests`} → `full-test-suite` fails → required check red → PR blocked. Revert → green → mergeable. (SC-001/SC-003)
2. **Total coverage**: the job's `dotnet test` output enumerates all 83 `*.Tests.fsproj` (not 2). (SC-002)
3. **Bound present**: the rendered job declares `timeout-minutes: 30`. (SC-003)
4. **Cache warm on rerun**: a second run with unchanged lockfiles reports a NuGet cache hit. (SC-005)
5. **Org config intact**: `build-config-drift` stays green; the three managed files are byte-identical. (SC-006)
