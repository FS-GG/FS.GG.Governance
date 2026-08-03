# F# simplicity architecture gate

The default is deliberately boring F#: functions and modules, records and discriminated unions, local
state, and approved BCL/FSharp.Core/organization primitives. The gate uses compiler parse/type-check,
entity, symbol-use, mutability, base-type, source-range, and compiler-token facts. Identifier spelling is
never used as a negative structural invariant.

Findings are either:

- `complexity-requires-justification`: the construct may be correct, but the simpler default was not
  used. Supply one structured justification bound to normalized path, compiler symbol key, reviewed head,
  and `CodeChecks.sourceDigest` of the complete source. State the simpler alternative and either measured
  evidence or an interoperability constraint. Any binding mismatch leaves the finding active as stale.
- `prohibited-structure`: the declared policy already identifies a conflicting approved primitive, or
  compiler analysis failed. A complexity justification cannot suppress this category.

Size and dependency fan-out values are review triggers, never correctness limits. Every threshold is
optional and disabled unless the repository supplies it. Duplicate detection is also explicit: callers
declare the capability, approved symbols, and candidate symbols because similar names do not establish
semantic equivalence.

Generated sources are excluded only through `SourceDocument.IsGenerated`; no filename is silently trusted.
For structural tests, assert compiler kind/relationship/case membership or behavior. A rename of a
non-contractual identifier must not require editing the invariant; a change from a DU to inheritance must.
