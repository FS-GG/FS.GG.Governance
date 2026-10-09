# Config package qualification

Manual `scope=config` selects only the version resolver and Config job. The resolver evaluates
Config's own project version; omit `version` for a pack/qualification and read-only both-feed visibility run with no login or push. An explicit version must match Config. Manual omitted scope/default `all` and
release/tag runs retain CLI-led selection; Config-only selection is manual-only. All eight other
jobs require validated scope `all`, including ReferenceGateSet, so Config-only mode packs or pushes
no other artifact. Root release admission and candidate-bound qualification still apply; historical operation holds remain reserved.

The publisher qualifies the full Config test suite, packs once and passes that exact archive to:

```sh
bash tests/config-package-smoke/run.sh ARCHIVE VERSION SHA256 SOURCE_REVISION EVIDENCE_DIRECTORY
```

The committed `.fsproj.in` template becomes a project only in the temporary consumer directory,
so repository project/dependency discovery never treats the synthetic consumer as a product YAML owner.
This consumer runs outside the producer graph in a temporary directory, with an empty package and HTTP
cache. It maps only Config to the selected local archive, only Contracts to the independently selected
`CONFIG_CONTRACTS_SOURCE` (default public nuget.org), and YamlDotNet/FSharp.Core to
`CONFIG_PUBLIC_SOURCE` (default nuget.org). Explicit exact dependency pins and content hashes come from
the committed Config lock. It disables the SDK's implicit FSharp.Core package source and uses no user
NuGet configuration, ProjectReference or producer bin output. Credentials use the existing environment
association, never a tracked configuration. Evidence retains the manifest, consumer lock and assets;
archive SHA-256 is checked before and after consumption. The consumer calls the packaged public resolver
and legacy loader. Its synthetic declarations prove library consumption, not process execution,
cleanup, evidence freshness or installed acceptance. The selected published Contracts 7.6.0 archive and actual loaded DLL must match their pinned identities; one Config and one Contracts assembly are loaded. The actual consumer receipt is retained with those archives and the producer/consumer closure.

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
execution. Static mutations exercise each unrelated-job guard, resolver project selection and dry-run push,
plus missing consumer/retention/collision, second pack, wrong file/hash, ordering, ambiguous output
and ignored failure. Resolver controls execute the actual extracted Bash resolver with a stub
`dotnet` returning distinct CLI/Config versions, recording exact calls and outputs. They cover
complete/incomplete recovery selection and manual selection/version cases, omitted/default scope, unknown scope, invalid/empty versions and
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

A new independent Config-only release remains gated on actual C1+C2/C2.3 coherent acceptance,
selected Contracts compatibility, full candidate qualification, collision admission and both-feed
readback. It does not consume or clear the historical ReferenceGateSet 1.8.0 candidate or original
operation holds; those retain their own publication/adoption/recovery prerequisites. Config
publication alone does not complete C3 or C4.

The first admitted push run tests, packs once, consumes and retains its own exact candidate before
any effect. Retention includes the complete Config TRX, original native Actions qualification,
producer lock, manifest, consumer lock/assets, `default-consumer-result.json`,
`contracts-selection.json` and `FS.GG.Contracts.7.6.0.nupkg`. Artifact upload fails when this set is
incomplete. Retain its native artifact ID/digest and original run/attempt separately from the
package SHA-256. Retain the verified candidate independently of the runner before effects;
copy it to durable private custody while the Actions artifact is available and keep that copy
through unresolved publication.
An omitted-version run makes its own no-push candidate; it is not a promotion handle.

Forward recovery selects all three inputs `config_run_id`, `config_artifact_id` and
`config_package_sha256`, manual `scope=config`, and the explicit matching Config version on the
same exact original source. Root independently observes original effects and reservations first.
The reader authenticates repository, owning workflow, completed original attempt 1, Config-only
scope/version selection, successful native qualification and retention steps, artifact ownership,
retention window, Actions archive digest and pinned package hash. It rejects rerun/recovery copies,
no-push promotion, lost archives and incomplete evidence. Recovery cannot pack or regenerate
consumer evidence. It then uses the existing collision/hash/push stages; matching feed content
needs no replacement. Unknown writes, inaccessible feeds, unequal payload or unverifiable
signatures refuse. Both-feed readback remains an independent operating check.

The existing Python controls exercise the actual extracted resolver and recovery reader using
synthetic Actions/feed responses. One retained fixture reaches verified archive selection with
zero pack/consumer calls; wrong native identities, stage failures, archive loss, byte changes,
closure drift and collision uncertainty refuse. These are source checks, not observations of
hosted retention, permissions, feed visibility or OIDC availability.
