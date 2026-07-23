# Research decisions

## One tree pin, not one manifest row per file

A directory entry represents one controlled upstream unit. Its canonical digest detects add,
remove, rename, reorder, and byte mutation without turning an 85-file import into 85 governance
declarations.

## Binary length-prefixed records

The downstream prototype used `<path>\t<file-sha>\n`. Paths may contain delimiter bytes, so that
format is not injective without additional escaping rules. Tree-v1 instead writes big-endian
lengths followed by path and raw content bytes. The representation is unambiguous and preserves the
acceptance requirement that raw bytes, not decoded text, define identity.

## Verify, then exempt

The verifier records an import as trusted only after digest and `.gitattributes` policy both match.
The optional exemption probe consults only that verified set. A malformed manifest or failed import
therefore cannot become a broad source-field bypass.

## Symlinks fail

Following symlinks would let a declared tree hash bytes outside its repo-relative destination and
would make checkout behavior host-dependent. Every reparse point is rejected with its path.
