# `generated-product-scaffold` — a captured real `fsgg-sdd init` workspace

**Provenance: REAL, captured — not synthetic.** These bytes are the verbatim `.fsgg/` output of

```bash
mkdir GeneratedProduct && cd GeneratedProduct
fsgg-sdd init --root . --json      # exit 0
```

run with **`fs.gg.sdd.cli` 1.0.0** (`fsgg-sdd --version` → `1.0.0`) on 2026-08-04, for
FS-GG/FS.GG.Governance#385. Nothing was edited, trimmed, or hand-written afterwards. The workspace
`id` (`generatedproduct`) is derived by the tool from the directory leaf, which is why the capture
directory has a fixed name; there is no timestamp or other nondeterministic field in the output.

## Why it exists

`#385` AC3 requires a **generated product** to consume the composed F# constitution profile, proven
from a clean clone. The generated-product route's scaffolding half is owned by `FS-GG/FS.GG.SDD`
(`fsgg-sdd init`), not by this repository, and that tool is a **global dotnet tool published from
another repo**: it is not in this repo's `.config/dotnet-tools.json`, and restoring it into the
Governance gate would couple this repo's CI to the org's credentialed feed — which the resolution
tests deliberately blank (`FSGG_PACKAGES_ACTOR`/`FSGG_PACKAGES_READ_TOKEN`) precisely so they prove
hermeticity.

So `ReferenceGateSetResolution` R7 runs the real tool **when it is on `PATH`** — which is how the
end-to-end proof was executed and recorded on the item — and falls back to these captured bytes when
it is not. The fallback is **disclosed at runtime**, not silent: the test prints which source it
used, and it never skips. Both paths assert the same thing, because the subject under test is the
*resolution into an already-populated `.fsgg/`*, not the scaffolder.

## What it shows, and what it does not

`fsgg-sdd init` writes **six** files here, and **none of them is a Governance file**: there is no
`governance.yml`, `capabilities.yml`, `policy.yml`, or `tooling.yml`. That is the measured gap
`FS-GG/FS.GG.SDD#845` owns, and it is why the generated-product route is a genuinely *different*
resolution path from the ordinary-repository one: the destination `.fsgg/` already exists and
already has an owner, so the profile must merge into it without disturbing what the generator wrote.
R7 asserts exactly that — every byte of these six files survives the resolve.

This fixture is **not** evidence about `fsgg-sdd`'s behaviour beyond the capture above, and #385
changes nothing in `FS-GG/FS.GG.SDD`. Re-capture it with the command at the top if the scaffolder's
output changes.
