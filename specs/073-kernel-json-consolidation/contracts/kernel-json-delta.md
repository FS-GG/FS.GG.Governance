# Contract delta — `FS.GG.Governance.Kernel/Json.fsi`

The **only** change to `Kernel` is promoting the already-defined `writeToString` to public.
Add the following `val` to `module Json` in `Json.fsi` (placed in the existing
"writer plumbing" region, before `ofExplanation`):

```fsharp
/// Emit compact (non-indented) UTF-8 JSON through a callback and return it as a string.
/// Default Utf8JsonWriter options ⇒ no indentation ⇒ deterministic, compact output. This is
/// the canonical deterministic-emit helper every projection shares; the 13–14 hand-copied
/// definitions across `src` are deleted in favour of this one (feature 073).
val writeToString: emit: (System.Text.Json.Utf8JsonWriter -> unit) -> string
```

`Json.fs` is unchanged — the body already exists at `Json.fs:23`.

## Surface-baseline impact

`surface/FS.GG.Governance.Kernel.surface.txt` gains one member line, e.g.:

```text
  [Method] System.String writeToString(Microsoft.FSharp.Core.FSharpFunc`2[System.Text.Json.Utf8JsonWriter,Microsoft.FSharp.Core.Unit])
```

(exact rendered form taken from the `SurfaceDriftTests` regeneration). No other Kernel
member changes.
