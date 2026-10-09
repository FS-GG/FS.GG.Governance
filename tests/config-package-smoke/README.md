# Config package qualification

Manual `scope=config` selects only the version resolver and Config job. The resolver evaluates
Config's own project version; omit `version` for a pack/qualification run with no collision lookup,
login or push. An explicit version must match Config. Manual omitted scope/default `all` and
release/tag runs retain CLI-led selection; Config-only selection is manual-only. All eight other
jobs require validated scope `all`, including ReferenceGateSet, so Config-only mode packs or pushes
no other artifact. Root release admission and original archive/custody requirements still apply.

The publisher qualifies the full Config test suite, packs once and passes that exact archive to:

```sh
bash tests/config-package-smoke/run.sh ARCHIVE VERSION SHA256 SOURCE_REVISION EVIDENCE_DIRECTORY
```

The committed `.fsproj.in` template becomes a project only in the temporary consumer directory,
so repository project/dependency discovery never treats the synthetic consumer as a product YAML owner.
This consumer runs outside the producer graph in a temporary directory, with an empty package and HTTP
cache. It maps only Config to the selected local archive, only Contracts to the independently selected
`CONFIG_CONTRACTS_SOURCE` (default organization feed), and YamlDotNet/FSharp.Core to
`CONFIG_PUBLIC_SOURCE` (default nuget.org). Explicit exact dependency pins and content hashes come from
the committed Config lock. It disables the SDK's implicit FSharp.Core package source and uses no user
NuGet configuration, ProjectReference or producer bin output. Credentials use the existing environment
association, never a tracked configuration. Evidence retains the manifest, consumer lock and assets;
archive SHA-256 is checked before and after consumption. The consumer calls the packaged public resolver
and legacy loader. Its synthetic declarations prove library consumption, not process execution,
cleanup, evidence freshness, installed acceptance or the SDD Contracts 7.6.0 join.

Before publication, `archive.py collision` observes both selected feeds. Existing versions must match
all payload entries; raw archives, hashes and signatures are retained separately. A differing signature
may be excluded only after successful NuGet verification identifies repository signing. Unknown endpoints,
failed authentication or ambiguous organization 404 observations refuse. An organization 404 can hide a
package: successful authenticated organization package enumeration and, when present, exact package-version
enumeration must establish absence. Unreadable enumeration refuses pending root visibility reconciliation.
No retry coordinator or publication admission is added here. A failed second feed requires observation and
use of the retained original archive, not rerunning pack. ReferenceGateSet 1.8.0 custody remains separate.

The cheap local checks, requiring only Python 3 and Bash, are:

```sh
python3 tests/config-package-smoke/publish_contract.py .github/workflows/publish.yml --mutations
python3 tests/config-package-smoke/archive_controls.py
bash -n tests/config-package-smoke/run.sh
```

The first command checks the actual job through a deliberately restricted YAML projection, refusing
unsupported step controls/conditions. It does not replace general workflow validation or expression
execution. Twenty mutations exercise each unrelated-job guard, resolver project selection and dry-run push,
plus missing consumer/retention/collision, second pack, wrong file/hash, ordering, ambiguous output
and ignored failure. Eighteen controls execute the actual extracted Bash resolver with a stub
`dotnet` returning distinct CLI/Config versions, recording exact calls and outputs. They cover
manual selection/version cases, omitted/default scope, unknown scope, invalid/empty versions and
existing tag/release matching/refusal. These synthetic controls need no SDK or feed credentials;
they do not dispatch Actions or substitute for hosted expression validation. Synthetic archive controls exercise identity/version/digest,
source revision, dependency boundary, TFM, assembly/API, payload and empty/multiple archive refusals;
their dummy assembly is explicitly not package qualification. These controls also run through
`PublishContractTests.fs` in the full Config suite. The publisher runs workflow preflight before restore.

Preflight selection: static checks for this bounded sequential addition, with no custom Quint model.
Expected reuse is each Config release and Config test run. Initial effort cap is one implementation
session; reassess at 30 minutes before any custom model. Warm target is under 60 seconds. Savings,
cold setup and native/hosted timing remain unknown until measured. Existing exact-head and coherent
qualification remain required; this check never authorizes their cancellation or publication.
