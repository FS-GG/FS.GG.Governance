# Contract: Skill checks — path contracts, task lists, mirrors (F24, P2)

**Library**: `FS.GG.Governance.SkillChecks` | **Story**: US3 | **FR**: 005, 009, 010, 012

Checks each declared skill: its path contract holds, its task-skill list is internally consistent, and any
declared mirror is present and in sync.

## C1 — Path contract (FR-005, SC-003)

- `PathHolds` ⇒ no finding.
- `PathUnresolved claimed` ⇒ one **Blocking** `skill.path-contract` finding naming the skill and the claimed
  path that does not resolve (acceptance 3.2).
- `PathEscapesBounds claimed` ⇒ one **Blocking** `skill.path-contract` finding naming the skill and the path
  that resolves but escapes the skill's declared bounds.

## C2 — Task-skill-list consistency (FR-005)

- `TaskListConsistent` ⇒ no finding.
- `TaskListInconsistent detail` ⇒ one **Blocking** `skill.task-list` finding naming the skill and the
  inconsistency.

## C3 — Mirror (FR-005, SC-003)

- `NoMirrorDeclared` ⇒ **no finding** — a skill that declares no mirror is **not** an error (acceptance 3.3,
  explicit in FR-005).
- `MirrorInSync` ⇒ no finding.
- `MirrorMissing mirror` ⇒ Blocking `skill.mirror` finding naming the absent mirror.
- `MirrorDrifted (mirror, detail)` ⇒ Blocking `skill.mirror` finding naming the drift.

## C4 — Clean pass, determinism, input/seam (SC-003, SC-005, FR-010, FR-012, FR-007)

- A conformant skill (path holds, list consistent, mirror in sync **or** no mirror) ⇒ zero findings
  (acceptance 3.1).
- `evaluate` sorts by `(skill id, locus)`; identical `SkillFacts` ⇒ byte-identical.
- `Unreadable` manifest/mirror ⇒ `IsInputState` findings naming the source (FR-012). The only filesystem seam
  is `Interpreter.SkillPort`; `evaluate` is pure/no-I/O.

## Acceptance (maps to spec US3 scenarios)

1. Path holds + list consistent + mirror in sync ⇒ pass + evidence.
2. Unresolved/escaping claimed path ⇒ `skill.path-contract` naming skill + path.
3. Missing/drifted mirror ⇒ `skill.mirror`; no mirror declared ⇒ not an error.
