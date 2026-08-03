# Data Model: F# Simplicity Architecture Gate

- `SourceDocument`: normalized path plus complete source text and generated/excluded disposition.
- `Thresholds`: optional module/type/member line and dependency-fan-out triggers.
- `ComplexityJustification`: path, symbol key, head, source digest, simpler alternative, reason kind,
  and evidence. It is current only when every binding matches and required prose is non-empty.
- `ApprovedPrimitive`: capability plus approved and candidate stable symbol keys.
- `AnalysisRequest`: head, documents, pure-domain prefixes, thresholds, justifications, primitives.
- `FindingId`: closed stable ids for hierarchy, reflection/metaprogramming, shared mutation, public class,
  pure-domain imperative work, size/fan-out trigger, duplicate abstraction, and compiler failure.
- `FindingCategory`: `ProhibitedStructure` or `ComplexityRequiresJustification`.
- `ArchitectureFinding`: id/category/path/symbol/range/message/justification disposition.
- `AnalysisReport`: deterministically ordered findings and diagnostics.

State transition: a complexity candidate is unsuppressed → current justification suppresses it; changed
head/source/path/symbol or incomplete prose yields a stale/invalid disposition and retains the finding.
