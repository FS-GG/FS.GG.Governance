# Tasks: F# Simplicity Architecture Gate

- [X] T001 Record the issue boundary, research decisions, data model, API contract, and validation guide.
- [X] T002 Draft the public `.fsi` model and analyzer surface in `src/FS.GG.Governance.CodeChecks/`.
- [X] T003 [US1] Add failing semantic fixtures for idiomatic and unjustified compiler-backed structures.
- [X] T004 [US2] Add failing freshness fixtures for exact and stale structured justifications.
- [X] T005 [US3] Add failing opt-in threshold and approved-primitive duplicate fixtures.
- [X] T006 [US4] Add the rename mutation fixture and parse-failure/false-positive controls.
- [X] T007 Implement compiler-backed analysis and deterministic typed diagnostics.
- [X] T008 Add public surface baseline, solution wiring, and generated guidance.
- [X] T009 Run focused tests, packed-library smoke, locked restore, solution build/test, and path checks.

## Dependencies and evidence

T002 precedes tests; T003–T006 precede T007; T008 follows the stable implementation; T009 completes all.
Principle IV is not applicable because this is a single request/response computation with no I/O workflow.
Planted source strings are synthetic and must be named `Synthetic`; the packed-library smoke exercises the
production public surface.

Evidence: focused suite 11/11; locked solution restore; bounded full solution build; full solution test
exit 0 in 171111 ms; Release pack `FS.GG.Governance.CodeChecks.0.1.0.nupkg`; packed DLL public-route
analysis returned zero findings/diagnostics for an idiomatic DU/module input; `git diff --check` clean.
