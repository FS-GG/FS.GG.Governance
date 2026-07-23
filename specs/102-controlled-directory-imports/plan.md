# Plan

Implement the capability at the reference-set edge rather than in a product or a new runtime
assembly:

1. Add a typed F# script and empty starter manifest under the canonical sample `.fsgg/`.
2. Define and document the cross-platform tree-v1 digest and checkout byte policy.
3. Exercise the real script over temporary filesystem trees from the existing reference-set tests.
4. Pack the two files from their canonical location and bump the content contract to `1.4.0`.

The script owns filesystem I/O and diagnostics. No existing Governance library API changes.
