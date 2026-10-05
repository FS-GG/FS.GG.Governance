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
