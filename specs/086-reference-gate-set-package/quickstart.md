# Quickstart: produce, verify, and consume the Reference Gate Set package

Runnable validation that the feature works end-to-end. Assumes the repo builds (`dotnet fsi build.fsx`)
and the G1–G7 guard is green. See [contracts/reference-gate-set-package.contract.md](./contracts/reference-gate-set-package.contract.md)
and [data-model.md](./data-model.md) for the details these steps validate.

## Prerequisites

- .NET `net10.0` SDK; repo restored (`dotnet restore FS.GG.Governance.sln`).
- The four reference files present at `samples/sdd-reference-gate-set/.fsgg/`.

## 1. Confirm the version the rule derives (FR-006/SC-003)

```bash
dotnet fsi pack-reference-gate-set.fsx --print-version
# expected: 1.2.1.1   (governance=1, capabilities=2, policy=1, tooling=1)
```

Validates: the deterministic rule reads the four `schemaVersion:` lines and composes the package version
without packing. (Maps to contract §3.)

## 2. Produce the package — gated on G1–G7 (FR-001/FR-004)

```bash
dotnet fsi pack-reference-gate-set.fsx
# runs the reference-set guard first; if G1–G7 are red it aborts non-zero BEFORE packing.
# on green: writes ~/.local/share/nuget-local/FS.GG.Governance.ReferenceGateSet.1.2.1.1.nupkg
ls ~/.local/share/nuget-local/FS.GG.Governance.ReferenceGateSet.*.nupkg
```

Validates: the artifact is only produced when its frozen invariants hold (contract §4). To see the gate
fire, temporarily break an invariant (e.g. blank a check in `capabilities.yml`) and re-run — pack must
fail and write no `.nupkg`; then restore the file.

## 3. Verify content-only + byte-identity (FR-003/FR-007/SC-002/SC-005)

```bash
PKG=$(ls ~/.local/share/nuget-local/FS.GG.Governance.ReferenceGateSet.*.nupkg | head -1)
unzip -l "$PKG" | grep -E 'contentFiles/any/any/.fsgg/|lib/'
# expected: the four .fsgg/*.yml entries; NO lib/ entry

# byte-identity of each file:
TMP=$(mktemp -d); unzip -q "$PKG" -d "$TMP"
for f in governance capabilities policy tooling; do
  diff "$TMP/contentFiles/any/any/.fsgg/$f.yml" "samples/sdd-reference-gate-set/.fsgg/$f.yml" \
    && echo "$f.yml: identical"
done
```

Validates: exactly the four files, byte-identical to source, with no assembly/`lib/` (contract §2/§6).

## 4. Run the automated guard (the real-evidence test, Principle V)

```bash
dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests/FS.GG.Governance.ReferenceGateSet.Tests.fsproj
```

Validates (asserted, not eyeballed):
- existing **G1–G7** still freeze the bundle invariants;
- **packed `.nupkg` bytes == on-disk samples** (SC-002);
- **no `lib/` / empty dependency group** (SC-005);
- **derived version** matches the rule, and a **simulated `schemaVersion` bump** (temp-dir copy) yields a
  distinguishable version (SC-003).

## 5. Simulate a schema bump (SC-003, manual cross-check of step 4's automated case)

```bash
TMP=$(mktemp -d); cp -r samples/sdd-reference-gate-set/.fsgg "$TMP/.fsgg"
sed -i 's/^schemaVersion: 1/schemaVersion: 2/' "$TMP/.fsgg/policy.yml"
dotnet fsi pack-reference-gate-set.fsx --print-version --source "$TMP"
# expected: 1.2.2.1  (the policy segment changed; distinguishable from 1.2.1.1)
```

## 6. Consume it (smoke test of the consumer path, FR-005/FR-009)

```bash
# from a throwaway consumer project that references the package from the local feed:
dotnet add package FS.GG.Governance.ReferenceGateSet --version 1.2.1.1 \
  --source ~/.local/share/nuget-local
dotnet restore
ls ~/.nuget/packages/fs.gg.governance.referencegateset/1.2.1.1/contentFiles/any/any/.fsgg/
# expected: governance.yml capabilities.yml policy.yml tooling.yml
```

Validates: the documented, version-stable materialization location holds — the location a Templates#14
`git diff --exit-code` overlay gate compares against (contract §6). Building this consumer requires **no**
governance assembly reference (content-only).

## 7. Registry (cross-repo, FR-008/SC-006)

Register the package as a versioned contract via the **cross-repo-coordination** protocol: a PR to
`FS-GG/.github` adding `FS.GG.Governance.ReferenceGateSet` to `registry/dependencies.yml` (consumer:
`FS.GG.Templates`), regenerating `docs/registry/compatibility.md`, and adding the version-rule ADR. The
registry entry links its PR and notes the deferred org-feed status (`.github#21`).

## Done when

- Steps 1–6 pass locally; step 4's guard is green in CI (the `reference-gate-set-pack` job).
- The registry PR (step 7) is open/merged in `FS-GG/.github`.
- Templates#14 has an authoritative, versioned source of truth to point its overlay drift gate at (SC-001).
