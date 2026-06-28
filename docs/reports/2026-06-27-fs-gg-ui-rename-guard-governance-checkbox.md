# Governance-side fs-gg-ui rename: checkbox closed (2026-06-27)

**Cross-repo item:** Coordination board **P5** — the org-wide rename of the UI version machinery
(`fs-skia-ui` → `fs-gg-ui`, ADR-0003).

**Governance-side checkbox:** *verify no straggling reference to the legacy version machinery remains
in FS.GG.Governance, and keep it that way.*

## Result: closed with durable evidence

A full-tree scan of `main` confirms **zero** legacy version-machinery identifiers in
FS.GG.Governance. The only references to the predecessor name are legitimate historical-repository
**provenance prose** (the `EHotwagner/FS-Skia-UI` repo that preceded this one) in four documentary
files, which are deliberately preserved byte-for-byte.

The verification is made **durable** by a self-contained regression guard:

- **Project:** [`tests/FS.GG.Governance.RenameGuard.Tests`](../../tests/FS.GG.Governance.RenameGuard.Tests/)
  (Expecto; no production `ProjectReference`; Tier 2 — no `src/`/`.fsi`/baseline change).
- **What it freezes (R1–R7):** the production scan of the git-tracked tree carries no
  legacy version-machinery identifier (R1); the four provenance files are present, allowlisted, and
  untouched (R2); any reintroduced legacy identifier — including case/separator variants — turns the
  guard red with a diagnostic naming the file, the line, and the canonical `fs-gg-ui` replacement
  (R3–R5, R7); the canonical root and the guard's own scaffolding stay green (R6).
- **How it tells machinery from the repo name:** the version machinery always carries a
  `-version` / `-bom` / `Version` / `/v<n>` suffix; the bare predecessor repo name never does, so a
  suffix-anchored match spares lineage prose by construction. The four provenance files are
  additionally allowlisted as defense-in-depth.

## Evidence

- `dotnet test tests/FS.GG.Governance.RenameGuard.Tests` → **7/7 green** on `main` (R1–R7).
- The guard runs in every checkout/CI that already runs the suite (it only reads files and shells
  `git ls-files`), so the checkbox stays closed automatically: a future straggler fails the build.

See [`specs/083-fs-gg-ui-rename-guard/`](../../specs/083-fs-gg-ui-rename-guard/) for the spec,
contract (R1–R7), and design notes.
