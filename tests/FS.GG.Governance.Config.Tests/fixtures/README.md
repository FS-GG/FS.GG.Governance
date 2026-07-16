# F014 fixtures

Each fixture is a product tree containing a `.fsgg/` directory. `valid-*` fixtures
validate to typed facts; `malformed-*` carry exactly one defect; `surface-*` exercise one
MVP surface class.

## valid

| Fixture | Asserts |
|---|---|
| `valid-complete` | all four files valid → full typed facts (US1) |
| `valid-reordered` | same content, lists reordered → identical typed facts (FR-012) |
| `valid-no-policy` | optional `policy.yml` absent → `Valid`, `Policy = None` (FR-015) |
| `valid-no-tooling` | optional `tooling.yml` absent → `Valid`, `Tooling = None` (FR-015) |

## malformed → DiagnosticId

| Fixture | DiagnosticId | Acceptance scenario |
|---|---|---|
| `malformed-duplicate-id` | `DuplicateId` | US2 AS1 (two capability/domain ids share an id) |
| `malformed-unknown-field` | `UnknownField` | strictness (FR-006) |
| `malformed-missing-required-field` | `MissingRequiredField` | strictness (FR-006) |
| `malformed-malformed-value` | `MalformedValue` | closed-enum out-of-set (`cost`) |
| `malformed-nonpositive-timeout` | `MalformedValue` | non-positive command `timeout` (CORE-2) |
| `malformed-missing-schema-version` | `MissingSchemaVersion` | US2 AS3 |
| `malformed-malformed-schema-version` | `MalformedSchemaVersion` | US2 AS3 |
| `malformed-unsupported-schema-version` | `UnsupportedSchemaVersion` | "upgrade the tool" edge case |
| `malformed-path-escapes-root` | `PathEscapesRoot` | path-escape edge case (FR-008) |
| `malformed-dangling-domain-ref` | `DanglingReference` | US2 AS5 (check → undeclared domain) |
| `malformed-dangling-command-ref` | `DanglingReference` | cross-file (check → command, tooling absent) |
| `malformed-dangling-default-profile` | `DanglingReference` | defaultProfile not a declared profile |
| `malformed-empty-file` | `EmptyFile` | present-but-empty `governance.yml` |
| `malformed-missing-required-file` | `MissingRequiredFile` | required `capabilities.yml` absent |

## surface-* → SurfaceClass

| Fixture | SurfaceClass |
|---|---|
| `surface-routine` | `Routine` |
| `surface-governed-root` | `GovernedRoot` |
| `surface-protected` | `ProtectedSurface` |
| `surface-generated-view` | `GeneratedView` |
| `surface-release` | `ReleaseSurface` |
| `surface-undeclared-only` | (none — no surface facts; US3 scenario 3) |
