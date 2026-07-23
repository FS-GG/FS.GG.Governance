# Controlled import v1 contract

## Manifest

The UTF-8 JSON document contains `schemaVersion: 1` and an `imports` array. Every entry requires
`kind`, `destinationPath`, `upstreamRepository`, `upstreamRevision`, `upstreamPath`, `license`,
`importMethod`, and `sha256`. `kind` is exactly `file` or `directory`; `upstreamRevision` is a pinned
40- or 64-character lowercase hexadecimal object id; `sha256` is 64 lowercase hex.

Destinations are non-empty repository-relative paths with no `.` or `..` segment. Resolution must
remain beneath the supplied repository root.

## File digest

`sha256 = SHA256(raw file bytes)`.

## Directory digest: `fsgg-controlled-tree/v1`

Enumerate regular files recursively without following any reparse point. Normalize each relative
path to UTF-8 with `/` separators and sort using ordinal comparison. For each file, append:

```text
BE64(len(pathBytes)) || pathBytes || BE64(len(fileBytes)) || fileBytes
```

The tree digest is SHA-256 over the concatenation. Empty trees hash the empty byte string.

## Verification and exemption

The exact checkout policy `<file> -text` or `<directory>/** -text` is part of verification.
Only a verified file itself or a verified directory and its descendants are exempt. Failure emits
no exemption.
