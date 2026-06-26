<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the roadmap at
docs/initial-implementation-plan.md. The most recently delivered feature is
specs/073-kernel-json-consolidation/ (Phase A of the architecture/quality/
de-duplication roadmap — ✅ DELIVERED). It consolidated the duplicated JSON
emit into three new pure leaves placed BELOW everything (NOT in `Kernel`,
whose tested firewalls forbid a projection→Kernel edge):
`FS.GG.Governance.JsonText` (the canonical `writeToString`),
`FS.GG.Governance.JsonTokens` (the seven closed-enum token helpers), and
`FS.GG.Governance.JsonWriters` (`writeCause`/`verdictByGate`/`outcomeByGate`/
`writeExecution`). Divergent copies (VerifyJson `dispositionToken`/
`writeCauseValue`/`writeExecution`, the `Verdict` token, and the per-projection
`writeEnforcement`) stay local. Acceptance was byte-identical `*Json` goldens.
Past feature plans live under specs/ — e.g.
specs/072-sdd-first-class-integration/plan.md.
<!-- SPECKIT END -->
