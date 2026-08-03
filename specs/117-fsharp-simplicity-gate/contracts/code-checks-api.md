# CodeChecks public contract

`CodeChecks.sourceDigest` returns the lowercase SHA-256 binding used by justifications.

`CodeChecks.analyze : AnalysisRequest -> Async<AnalysisReport>`:

1. parses and type-checks every non-excluded document with compiler services;
2. emits `CompilerAnalysisFailed` when a document cannot be analyzed;
3. derives structural candidates only from compiler facts/ranges;
4. suppresses only `ComplexityRequiresJustification` candidates carrying one exact current justification;
5. never suppresses `ProhibitedStructure` candidates;
6. returns findings ordered by path, range, stable id, and symbol key.

The contract performs no repository scanning, network access, wall-clock reads, or process exits.
