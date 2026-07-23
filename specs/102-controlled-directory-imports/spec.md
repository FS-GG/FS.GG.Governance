# Feature 102: Controlled file and directory imports

## Problem

The reference gate set can govern product-authored files but has no reusable contract for a pinned
upstream directory. Consumers therefore copy local hashing and exemption logic, which can grant a
broad path exemption before proving the imported bytes.

## Requirements

- **FR-001** The manifest MUST model `file` and `directory` as a closed typed import kind.
- **FR-002** Every entry MUST carry a repo-relative destination, upstream repository, revision,
  upstream path, licence, import method, and lowercase SHA-256; the revision MUST be an immutable
  40- or 64-character lowercase hexadecimal object id.
- **FR-003** Directory SHA-256 MUST use `fsgg-controlled-tree/v1`: normalized `/` paths, ordinal
  ordering, raw file bytes, and injective length-prefixed records.
- **FR-004** Missing, unreadable, escaped, symlink/reparse, or mutated content MUST fail closed with
  a named rule and path.
- **FR-005** A file or descendant exemption MUST be emitted only after its owning import verifies.
- **FR-006** Verbatim imports MUST declare the exact `-text` `.gitattributes` policy.
- **FR-007** The verifier, starter manifest, tests, and consumer example MUST ship in the
  `FS.GG.Governance.ReferenceGateSet` content package.

## Acceptance

Real temp-tree tests prove a valid directory and file, descendant mutation, missing input,
unreadable bytes, symlink escape, path traversal, checkout-policy enforcement, and exemption
ordering. The produced package contains the tested files byte-identically and remains content-only.

## Out of scope

Fetching upstream content, choosing licences, automatically updating pins, and exempting any path
not covered by a successfully verified entry.
