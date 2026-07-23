# Data model

```fsharp
type ImportKind =
    | RegularFile
    | DirectoryTree

type ControlledImport =
    { Kind: ImportKind
      DestinationPath: string
      UpstreamRepository: string
      UpstreamRevision: string
      UpstreamPath: string
      License: string
      ImportMethod: string
      Sha256: string }
```

`VerifiedImport` is not serialized. It exists only for entries whose filesystem bytes, path
containment, reparse-point posture, digest, and checkout policy have all passed in the current run.
Exemption is derived exclusively from this collection.

The manifest is `{ "schemaVersion": 1, "imports": ControlledImport[] }`.
