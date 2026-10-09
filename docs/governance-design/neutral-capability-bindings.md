# Neutral capability bindings (GOV-423-C1)

Tier 1: additive public Config API and generated package artifact. Authority is
[ADR-0092](https://github.com/FS-GG/.github/blob/064d27897f720709a567493d039d8379ab13395e/docs/adr/0092-descriptor-driven-polyglot-workspace-providers.md).
The [owning roadmap](../roadmaps/governance-423-neutral-capability-bindings.md)
retains the delivered 058 foundation and sequential execution/publication joins.

## Contract and acceptance

`CapabilityBindings.catalog` is the authoritative eight-entry semantic vocabulary.
`catalogJson()` derives `fsgg.neutral-capability-catalog/v1`, contract 1.0.0,
without a separately authored JSON table. Each neutral meaning requires an executable
and execution evidence when selected as required. The catalog creates no new floor.

`resolve` is a pure declaration validator. Requiredness is the union of trusted
requirements and provider-required declarations. Caller-owned semantic-only IDs,
known platforms and supported environments/evidence formats are independent policy;
provider assertions cannot replace them. Any diagnostic rejects the whole request;
no partial executable result is returned. Optional unknown IDs are explicitly
unsupported and cannot satisfy an obligation. Trusted semantic-only IDs can preserve
existing F# and gameplay floors but cannot turn a neutral ID into command-free work.
Every semantic-only declaration requires that trusted authorization, including
optional declarations; an unrecognized command-free declaration rejects the request.

The normalized API uses the already installed `Fsgg.Provider.DeclaredCommand` and
Config path/timeout/cost/environment types. SDD owns descriptor wire data and
language-specific tool-version/platform grammar. Its adapter maps explicit limits
and rejects unknown cost/environment tokens before constructing normalized input.
Missing normalized limits are representable and rejected. Known tool pins are
exact declarations, not installed observations. Evidence format support is matched
by exact ID and version. Referenced declarations must resolve uniquely; duplicate
IDs and references refuse. Required executable bindings need exact tool and evidence references.
Command executable is nonblank; literal arguments, including empty strings, spaces
and shell metacharacters, retain order without a lexer or shell roundtrip.

Working/output paths reject absolute, drive-qualified and escaping paths before
normalization; `.` is a valid working directory but not an evidence output file.
Limits need a positive timeout, explicit cost and at least one supported environment.
Bindings need at least one declared platform and must include the selected platform.
Diagnostics identify capability, field and code and sort by these values; resolved
sets and reference lists sort by identity while argv remains ordered. A declared
catalog version other than exactly 1.0.0 refuses.

Declaration resolution never observes process success, freshness, digests, actual
tool versions, provenance or evidence semantics. These remain Unknown at their
existing evaluator boundary. Release declaration grants no publication authority.
C2 must connect validated bindings to CommandHost/GateRun and preserve legitimate
command-free floors; C1 does not fix the existing NoCommand admission gap.

## Plan and tests

Specify the public signature and semantic examples before implementing the pure
resolver. Exercise public success and refusal cases: missing/unknown required IDs,
neutral SemanticOnly, duplicate identities/references, missing limits, unsupported
platform/environment/format, invalid paths and literal argv. Reverse declaration
orders to prove deterministic complete results and diagnostics.

ReferenceGateSet derivation guard compares generated catalog bytes before packing
and independently parses the catalog IDs/requirements against the canonical table.
`BLESS_REFERENCE_GATE_SET=1` deliberately regenerates that artifact. Package tests
compare actual archive bytes with canonical/source bytes. Resolution tests prove
catalog availability without copying it into `.fsgg`. Existing Config surface,
Gates/Inheritance and RouteJson controls retain compatibility coverage. Native CLR
qualification uses the root-admitted supervised route; source preparation alone
establishes no executed test result.

## Compatibility and migration

Config 0.3.0 adds a module without extending existing records or signatures.
Its project version changes require refreshing the existing lockfile project edges
from 0.2.0 to 0.3.0; external package versions and content hashes stay identical.
This expands the source touch-set only for those deterministic project references.
Capabilities YAML stays schema 2; governance/policy/tooling stay schema 1. Existing
loader/defaults, six `.fsgg` package files and four-key schema manifest remain intact.
ReferenceGateSet 1.8.0 adds `contentFiles/any/any/neutral-capabilities.json` and the
read-only `FsggNeutralCapabilityCatalog` property. Consumers explicitly read the
pinned artifact; automatic `.fsgg` resolution retains the prior file set.
Historical 1.7.0 bytes remain immutable. New adoption pins exact 1.8.0; prospective
catalog-compatible package range is [1.8.0,2.0.0). No installed migration is required
for the existing route. Publication and package-only receiver qualification remain
open; no new generated-workspace bytes or lifecycle default change in C1.

## C2 phase 1: complete-request execution planning (Tier 1)

The additive pure planner accepts the complete `Config.CapabilityBindings.ResolutionRequest`,
actual selected effective `Gate` values (after existing inheritance/profile selection), and explicit
host execution inputs. It calls `Config.resolve` before selecting any executable subset. Every
normalized required binding needs an exact selected effective gate; no empty selection, missing
required gate, unresolved selected binding or malformed declaration becomes success or `NoCommand`.
The original effective Gate records retain owner, maturity, cost and prerequisites. This function
cannot establish that the caller supplied all organization floors; the existing host remains their
selection authority.

`GateRun.Plan.ProviderCommandContext` carries governed root, observed environment class, the
independently admitted environment delta, and remaining positive host timeout. `commandForBinding`
projects a validated executable binding directly into existing `GateCommand`: literal executable and
ordered argv, normalized governed relative working directory, admitted delta, and the minimum of
binding timeout, gate timeout and remaining host timeout. The projected cost uses the greater of
binding and effective gate costs. An incompatible gate/binding environment, invalid timeout or
malformed delta refuses. These pure values do not observe tools, authenticate a root, enforce an
execution profile, renew a deadline or start a process; the real host must revalidate them before
dispatch.

`CommandHost.providerExecutionPlan` returns a separate typed plan or located Config diagnostics.
Executable entries carry current declared evidence bindings and effective cost. Over-ceiling entries
remain explicitly deferred and non-passing. Recognized command-free obligations retain a separate
semantic-only classification; they are never an executed/pass result. An effective executable floor
cannot be downgraded to semantic-only. Optional unknown capabilities stay explicitly unsupported,
with selected unknown gates classified non-passing. Provider planning has no legacy store or reuse
input and produces no legacy cache records. Existing `commandFor` and `executionPlan` are unchanged.

Verification uses synthetic normalized request/gate/profile fixtures through the public signature and
actual Config resolver: whole-request failures outside selection, required gate omission/duplicates,
empty selection, literal argv/limits/environment, floor cost/maturity preservation, deferral,
semantic-only and optional unsupported controls, plus existing legacy and surface suites. These
fixtures establish pure planning only. Native API exercise and tests require separately admitted
finite recipes; actual Verify/GateExecution observation remains phase 2/SDD C2.3.

The selected later host join uses explicit existing schema2 provenance and independently selected
policy documents, actual SDD library codecs and request conversion. Phase 1 adds no loader, flags,
SDD dependency, persisted context schema, package version or execution edge. Phase 2 waits for actual
once-packed coherent SDD library closure; C2.3 acceptance waits for both scaffold and Verify producers.

The phase-1 candidate passed the complete local GateRun suite (27 tests) and CommandHost suite
(30 tests), including 25 synthetic planning controls and the unchanged legacy tests. The public
signature was exercised first through a private FSI prototype using actual Config and domain types;
its 17 synthetic controls passed. Reflected surface changes add only the selected context/planning
APIs and their record/union members; all existing API lines remain. This validates pure planning,
not actual producer provenance, installed tool/environment enforcement, execution or evidence acceptance.

## C2 bounded direct-child component (Tier 1; local qualification)

The additive GateExecution API selects CurrentHost/direct-child execution only when the caller
explicitly accepts both uncontained, unobserved descendants and observed paths without atomic binding.
Stronger requirements refuse before launch. Captured environment plus validated deltas, initial absolute
cwd and literal argv are applied. Root bounds initial cwd selection only; refreshed path/link/metadata
checks establish observed pathname selection, never stable inode identity or atomic check-to-start
binding. The executable must be fully qualified and is passed unchanged, avoiding parent PATH search.
There is no sandbox, aggregate resource enforcement, descendant census or reaping guarantee. Existing
provider Verify has not accepted this weaker scope. Legacy execution and outcome/cache APIs stay intact.

`prepare` acquires a caller-held one-use ExecutionSession before launch. Pure workflow transitions
separate state from requested I/O. The edge retains the Process, launch/read/retirement activities,
finite buffers and registrations under the original monotonic work/cleanup ends; observation never
renews either budget. Per-stream and aggregate byte caps apply before retention, and returned prefixes
are immutable. Direct exit, EOF/closure/read failure, timeout/cancellation, first cause, distinct secondary
causes and direct settlement remain separate. A real exit124 is not timeout evidence.

`Released` requires actual direct exit and all owned activities/handles settled. Otherwise the caller
retains the same session, including after a deadline. Later settlement does not erase the original
bounded failure. Direct exit and EOF never prove descendant cleanup. Runtime signal identity remains
unqualified, so this port does not signal an ambiguous process or invent termination evidence.

Local qualification passed the public API exercise's 29 synthetic controls, actual Release compilation,
three surface/dependency/scope checks and ten synthetic workflow controls. Ten separately admitted real
controls exercised literal argv, captured environment/deltas/cwd, actual exit124, concurrent binary
streams, caps, timeout/late settlement, cancellation, one-use launch and prelaunch refusals. A separate
held-pipe test passed after an actual controlled holder was observed alive and adopted by its private
guardian. The run snapshot recorded direct exit0 with pending streams, retained custody and work/cleanup
failures; the guardian then reaped the holder and finally the runner after EOF.

The missing-executable control was accepted separately as external retirement. Its actual run recorded
LaunchOutcomeUnknown, no direct exit, Retained, launch failure and original deadline causes. The actual
CLR caller held the session until the independent guardian performed the selected SIGKILL and consumed
the exact adopted-runtime and final runner statuses. This was not a normal unit-test pass or public
`Released`, and does not qualify arbitrary ambiguous production launches. Guardian adoption/reaping is
private fixture evidence and never upgrades CurrentHost's UncontainedUnobserved descendant result.

The original two component compile failures, one later fixture-layout compile failure and the initial
held-readiness failure remain retained separately from successful successors. The initial held attempt
lacked a preassertion run snapshot; its underlying launch/expiry cause remains unknown. Corrected fixtures
construct fresh original 1-second work/3-second cleanup budgets once and record bounded run facts before
assertions; the controlled holder has a separate finite 10-second fallback within the outer window.

Native cases remain pending by default. Exact scheduling switches for the ten direct cases, held-pipe
case and unknown external control supply neither execution authority nor custody. Default/nonexact
scheduling checks skipped all ten direct cases; corrected-source default discovery skipped both remaining
cases, with zero passed native cases. Separate positive qualification used actual independently admitted
owners. Hosted full-suite CI has no such fixture owner and cannot establish positive native acceptance.
Source delivery/full Debug+Release coherent validation, actual Verify/package-backed caller joins,
stronger profile acceptance and C3 publication remain separate; GOV423-C2 stays open. Historic installed
custody/operation unknowns and the attributed native telemetry gap are unchanged.
