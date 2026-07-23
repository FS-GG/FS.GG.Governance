# Controlled upstream file and directory imports

Use the reference gate set's controlled-import contract when a repository keeps upstream bytes
verbatim and local governance rules would otherwise mistake those bytes for locally authored data.
The verifier proves provenance and byte identity before it grants an exemption; a path declaration
alone never grants one.

## Configure an import

Copy or install `samples/sdd-reference-gate-set/.fsgg/`, then add an entry to
`.fsgg/controlled-imports.json`:

```json
{
  "schemaVersion": 1,
  "imports": [
    {
      "kind": "directory",
      "destinationPath": "data/upstream/content",
      "upstreamRepository": "https://github.com/example/project",
      "upstreamRevision": "0123456789abcdef0123456789abcdef01234567",
      "upstreamPath": "content",
      "license": "MIT",
      "importMethod": "git-archive",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    }
  ]
}
```

`kind` is closed: `file` hashes one regular file's raw bytes; `directory` hashes the canonical
tree-v1 record stream documented below. Every other field is required and non-empty;
`upstreamRevision` must be a pinned 40- or 64-character lowercase hexadecimal object id.

Preserve imported bytes across checkout by adding the exact matching line to `.gitattributes`:

```gitattributes
data/upstream/content/** -text
```

For a `file` entry, use `data/upstream/NOTICE -text` instead.

## Verify before exempting

Run:

```bash
dotnet fsi .fsgg/controlled-imports.fsx -- --root .
```

The command exits non-zero and prints a named `GOV-IMPORT-*` rule for malformed declarations,
missing or unreadable content, repository escapes, symlinks/reparse points, missing text policy,
and digest drift. A successful directory emits:

```text
GOV-IMPORT-VERIFIED	directory	data/upstream/content
```

A product gate that wants to omit imported descendants from local source-field validation must ask
the same successful run:

```bash
dotnet fsi .fsgg/controlled-imports.fsx -- --root . \
  --check-exemption data/upstream/content/items/example.json
```

Only zero exit plus `GOV-IMPORT-EXEMPT` grants the exemption. If verification fails, no exemption is
printed.

## Canonical directory digest

`fsgg-controlled-tree/v1` enumerates regular files recursively, normalizes relative path separators
to `/`, and sorts paths with ordinal comparison. Each file contributes:

```text
uint64-be(path UTF-8 byte length) || path UTF-8 bytes ||
uint64-be(file byte length)       || raw file bytes
```

SHA-256 of the concatenated records is the manifest pin. The two fixed-width length prefixes make
the stream injective; raw bytes make newline or encoding conversion observable. Any symlink or
other reparse point fails before hashing, so traversal never leaves the declared tree.

When intentionally refreshing an import, obtain the upstream bytes by the recorded method, review
the diff and licence, recompute the pin with an independent implementation of this contract, update
the manifest, and rerun the gate.
