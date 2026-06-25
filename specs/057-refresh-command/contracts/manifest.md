# Contract: `.fsgg/refresh.yml` Generation Manifest

**Status**: Tier 1 contract (new authored surface) | **Parser**:
`FS.GG.Governance.RefreshCommand.Declaration` | **Schema**: row-local (does **not** edit the frozen F014
`.fsgg` four-file schema)

`.fsgg/refresh.yml` is the **authored** declaration of the Governance-owned generated views: which views
exist, what sources each derives from, which generator produces it, and how the generator version is sensed.
It is parsed by an in-project `Declaration` adapter via YamlDotNet parse-to-node (the `ReleaseCommand`
`release.yml` precedent — no new package). It is **never mutated by refresh** (research D4); the recorded
provenance lives in a separate generated lock (see below).

## Schema

```yaml
views:
  - id: gate-metadata                     # stable view identity (selector target — FR-015); unique
    kind: gate-metadata                   # structural ViewKind (product-neutral — FR-011)
    output: docs/generated/gates.json     # repo-relative path of the generated view
    sources:                              # declared source path(s), in declared order (FR-002)
      - .fsgg/gates.yml
    generator: ["fsgg", "route", "--json"] # the declared generator command (argv) — run at the edge (D3)
    generatorBasis: command-version        # how the generator version is sensed (token | command)

  - id: api-surface
    kind: api-surface-doc
    output: surface/FS.GG.Governance.Kernel.surface.txt
    sources:
      - src/FS.GG.Governance.Kernel/
    generator: ["dotnet", "test", "tests/FS.GG.Governance.Kernel.Tests", "-e", "BLESS_SURFACE=1"]
    generatorBasis: command-version
```

## Field rules

| Field | Rule | FR |
|---|---|---|
| `views` | a (possibly empty) list; **empty is valid** ⇒ "nothing to refresh" | FR-012 |
| `id` | non-empty, unique across views; duplicate ⇒ `DeclError` | FR-015 |
| `kind` | a recognized structural `ViewKind` token (kebab/camel/underscore-tolerant, the release-rule recognizer precedent); unknown ⇒ `Other` | FR-011 |
| `output` | non-empty repo-relative path | FR-005 |
| `sources` | list of repo-relative paths, declared order significant for the source-digest set | FR-002 |
| `generator` | non-empty command argv run at the edge to regenerate the view | FR-011/D3 |
| `generatorBasis` | how the generator version is sensed (product-neutral) | FR-002 |

**Parsing is pure and total** (`Declaration.parse : string list -> Result<GenerationManifest, DeclError>`):
a malformed/unreadable document ⇒ `Error { Reason }`, never an exception, with a message naming the offense
(FR-010, FR-016). No product identity, path, version, or renderer is baked into the parser — every value is
read from the file (FR-011, SC-006).

## Recorded-provenance lock (generated companion — research D4)

A **generated** record (e.g. `.fsgg/refresh.lock.json`) holds, per view, the source digests + generator
version + output digest captured at the last successful generation. Refresh writes it atomically on
regeneration and reads it to reconstruct the `recorded` `FreshnessInputs`; it is **not authored by hand**
and is itself governed by the currency rules. Reuses the F048 `EvidenceReuseStore` serialization if the
record shape fits, else a minimal deterministic row-local lock. Staleness is decided by comparing recorded
digests + generator version against freshly-sensed values — **by digest, never by file presence** (FR-002,
SC-002).
