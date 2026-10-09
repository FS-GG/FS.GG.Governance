#!/usr/bin/env python3
"""Synthetic archive controls only; not real package/native qualification."""
import hashlib
import json
import io
import os
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from unittest import mock
from types import SimpleNamespace
from pathlib import Path
import tempfile
import zipfile
from archive import inspect, prepare, resolved, org_absence

VERSION = '0.3.0'
REVISION = 'a' * 40
NUSPEC = '''<package><metadata><id>FS.GG.Governance.Config</id><version>0.3.0</version>
<repository commit="{revision}"/><dependencies><group targetFramework="net10.0">
<dependency id="FS.GG.Contracts" version="7.5.2"/><dependency id="YamlDotNet" version="18.1.0"/>
<dependency id="FSharp.Core" version="10.1.401"/></group></dependencies></metadata></package>'''.format(revision=REVISION)
BASE = {'FS.GG.Governance.Config.nuspec': NUSPEC.encode(),
        'lib/net10.0/FS.GG.Governance.Config.dll': b'synthetic-not-an-assembly',
        'lib/net10.0/FS.GG.Governance.Config.xml': b'<doc>CapabilityBindings.resolve</doc>'}

def write(path, entries):
    with zipfile.ZipFile(path, 'w') as archive:
        for name, data in entries.items():
            archive.writestr(name, data)
    return hashlib.sha256(path.read_bytes()).hexdigest()

def refused(label, action):
    try:
        action()
    except (ValueError, FileNotFoundError):
        print('refused synthetic archive control: ' + label)
    else:
        raise AssertionError('control escaped: ' + label)

from archive import CredentialSafeRedirect, credential_safe_open
request = urllib.request.Request(
    'https://nuget.pkg.github.com/FS-GG/download/selected.nupkg',
    headers={'Authorization': 'Basic synthetic-control-only', 'Accept': 'application/octet-stream'})
redirect_args = (request, None, 302, 'Found', {}, 'https://storage.invalid/archive?sig=synthetic-signed-query')
unsafe = urllib.request.HTTPRedirectHandler().redirect_request(*redirect_args)
assert unsafe.get_header('Authorization') == 'Basic synthetic-control-only'
safe = CredentialSafeRedirect().redirect_request(*redirect_args)
assert safe.get_header('Authorization') is None and safe.get_header('Accept') == 'application/octet-stream'
with mock.patch('urllib.request.build_opener') as opener:
    credential_safe_open(request, timeout=30)
    assert isinstance(opener.call_args.args[0], CredentialSafeRedirect)
print('Faithful urllib redirect control passed: archive reader removes redirected Authorization')

with tempfile.TemporaryDirectory() as root:
    path = Path(root) / 'FS.GG.Governance.Config.0.3.0.nupkg'
    sha = write(path, BASE)
    inspect(path, VERSION, sha, REVISION)
    closure = {name: {'requested': '[' + version + ', )', 'resolved': version, 'contentHash': 'synthetic'}
               for name, version in [('FS.GG.Contracts', '7.5.2'), ('YamlDotNet', '18.1.0'), ('FSharp.Core', '10.1.401')]}
    producer_lock = Path(root) / 'producer.lock.json'
    producer_lock.write_text(json.dumps({'dependencies': {'net10.0': closure}}))
    evidence = Path(root) / 'evidence'
    prepare(SimpleNamespace(package=str(path), version=VERSION, sha256=sha, revision=REVISION,
                            lock=str(producer_lock), output=str(evidence),
                            contracts_source='https://contracts.invalid/index.json', public_source='https://public.invalid/index.json'))
    def mappings(output):
        config = ET.parse(output / 'NuGet.Config').getroot()
        endpoints = {s.get('key'): s.get('value') for s in config.findall('./packageSources/add')}
        assert len(endpoints) == len(set(endpoints.values()))
        result = {endpoints[s.get('key')]: {p.get('pattern') for p in s}
                  for s in config.findall('./packageSourceMapping/packageSource')}
        assert result[root] == {'FS.GG.Governance.Config'}
        assert set().union(*result.values()) == {'FS.GG.Governance.Config', *closure}
        assert not any('*' in p for packages in result.values() for p in packages)
        return result
    distinct = mappings(evidence)
    assert distinct['https://contracts.invalid/index.json'] == {'FS.GG.Contracts'}
    assert distinct['https://public.invalid/index.json'] == {'FSharp.Core', 'YamlDotNet'}
    shared = Path(root) / 'shared-evidence'
    shared_args = SimpleNamespace(package=str(path), version=VERSION, sha256=sha, revision=REVISION,
                                 lock=str(producer_lock), output=str(shared),
                                 contracts_source='https://public.invalid/index.json', public_source='https://public.invalid/index.json')
    prepare(shared_args)
    assert mappings(shared) == {root: {'FS.GG.Governance.Config'},
                               'https://public.invalid/index.json': set(closure)}
    refused('packed-source-mapped-as-dependency-feed', lambda: prepare(SimpleNamespace(
        **{**vars(shared_args), 'contracts_source': root})))
    expected = {'FS.GG.Governance.Config': VERSION, **{n: e['resolved'] for n, e in closure.items()}}
    sources = [root, 'https://contracts.invalid/index.json', 'https://public.invalid/index.json']
    assets = {'libraries': {n + '/' + v: {'type': 'package'} for n, v in expected.items()},
              'project': {'restore': {'sources': {s: {} for s in sources}}},
              'targets': {'net10.0': {'FS.GG.Contracts/7.5.2': {'compile': {'lib/net10.0/FS.GG.Contracts.dll': {}}}}}}
    assets_path = Path(root) / 'assets.json'
    assets_path.write_text(json.dumps(assets))
    consumer_lock = Path(root) / 'consumer.lock.json'
    consumer_lock.write_text(json.dumps({'dependencies': {'net10.0': {**closure, 'FS.GG.Governance.Config': {'resolved': VERSION}}}}))
    assembly = Path(root) / 'cache/fs.gg.contracts/7.5.2/lib/net10.0/FS.GG.Contracts.dll'
    assembly.parent.mkdir(parents=True)
    assembly.write_bytes(b'synthetic-contract-assembly')
    resolved_args = SimpleNamespace(manifest=str(evidence / 'manifest.json'), assets=str(assets_path),
                                    lock=str(consumer_lock), cache=str(Path(root) / 'cache'), source=sources)
    resolved(resolved_args)
    # NuGet's equal-endpoint source population contains only one public URL.
    assets['project']['restore']['sources'] = {root: {}, 'https://public.invalid/index.json': {}}
    assets_path.write_text(json.dumps(assets))
    resolved(SimpleNamespace(**{**vars(resolved_args), 'manifest': str(shared / 'manifest.json'),
                               'source': [root, 'https://public.invalid/index.json', 'https://public.invalid/index.json']}))
    assets['project']['restore']['sources'] = {s: {} for s in sources}
    assets_path.write_text(json.dumps(assets))
    refused('source-drift', lambda: resolved(SimpleNamespace(**{**vars(resolved_args), 'source': ['https://unexpected.invalid']})))
    assets['libraries']['FS.GG.Contracts/7.5.2']['type'] = 'project'
    assets_path.write_text(json.dumps(assets))
    refused('project-dependency', lambda: resolved(resolved_args))
    assets['libraries']['FS.GG.Contracts/7.5.2']['type'] = 'package'
    assets['libraries']['FS.GG.Contracts/7.6.0'] = {'type': 'package'}
    assets_path.write_text(json.dumps(assets))
    refused('multiple-contracts-versions', lambda: resolved(resolved_args))
    refused('wrong-digest', lambda: inspect(path, VERSION, '0' * 64, REVISION))
    refused('wrong-version', lambda: inspect(path, '0.4.0', sha, REVISION))
    refused('wrong-revision', lambda: inspect(path, VERSION, sha, 'b' * 40))
    mutations = {
        'wrong-id': {**BASE, 'FS.GG.Governance.Config.nuspec': NUSPEC.replace('FS.GG.Governance.Config', 'Other').encode()},
        'dependency-drift': {**BASE, 'FS.GG.Governance.Config.nuspec': NUSPEC.replace('YamlDotNet', 'ProductSpecific').encode()},
        'missing-assembly': {k: v for k, v in BASE.items() if not k.endswith('.dll')},
        'missing-api': {**BASE, 'lib/net10.0/FS.GG.Governance.Config.xml': b'<doc/>'},
        'tool-payload': {**BASE, 'tools/tool.dll': b'not permitted'},
        'wrong-tfm': {k.replace('net10.0', 'net9.0'): v for k, v in BASE.items()},
    }
    for name, entries in mutations.items():
        mutated_sha = write(path, entries)
        refused(name, lambda: inspect(path, VERSION, mutated_sha, REVISION))
    sha = write(path, BASE)
    second = path.with_name('FS.GG.Governance.Config.0.4.0.nupkg')
    write(second, BASE)
    refused('multiple-archives', lambda: inspect(path, VERSION, sha, REVISION))
    second.unlink()
    path.unlink()
    refused('empty-output', lambda: inspect(path, VERSION, sha, REVISION))
print('Synthetic archive controls passed; real package consumption remains separate.')

# No network or credentials: mock canonical authenticated listing responses.
with mock.patch.dict(os.environ, {'FSGG_PACKAGES_READ_TOKEN': 'synthetic-control-token'}):
    with mock.patch('archive.credential_safe_open', return_value=io.BytesIO(b'[]')):
        assert org_absence(VERSION)['outcome'] == 'absent-by-authenticated-enumeration'
    def missing_version(request, timeout):
        values = [{'name': 'FS.GG.Governance.Config'}] if 'package_type=nuget' in request.full_url else [{'name': '0.2.0'}]
        return io.BytesIO(json.dumps(values).encode())
    with mock.patch('archive.credential_safe_open', side_effect=missing_version):
        assert org_absence(VERSION)['outcome'] == 'absent-by-authenticated-enumeration'
    def existing_version(request, timeout):
        values = [{'name': 'FS.GG.Governance.Config'}] if 'package_type=nuget' in request.full_url else [{'name': VERSION}]
        return io.BytesIO(json.dumps(values).encode())
    with mock.patch('archive.credential_safe_open', side_effect=existing_version):
        refused('existing-unreadable-org-version', lambda: org_absence(VERSION))
    with mock.patch('archive.credential_safe_open', side_effect=urllib.error.HTTPError('https://synthetic.invalid', 404, 'synthetic unknown', {}, None)):
        refused('bare-org-404', lambda: org_absence(VERSION))
print('Authenticated enumeration controls passed; live feed observation remains unrun.')

# SYNTHETIC retained Actions records and archives. Exercise the actual recovery reader,
# never GitHub, dotnet, pack, dispatch or publication.
import copy
from archive import recover, collision, QUALIFICATION_STEPS
with tempfile.TemporaryDirectory(prefix='config-recovery-controls-') as directory:
    root = Path(directory)
    original = root / 'original'
    packages = original / 'config-packages'; packages.mkdir(parents=True)
    evidence = original / 'config-evidence'
    selected = packages / 'FS.GG.Governance.Config.0.3.0.nupkg'
    entries = {**BASE, 'FS.GG.Governance.Config.nuspec': NUSPEC.replace('7.5.2', '7.6.0').encode()}
    package_sha = write(selected, entries)
    closure = {name: {'requested': '['+version+', )', 'resolved': version, 'contentHash': 'synthetic-'+name}
               for name, version in [('FS.GG.Contracts', '7.6.0'), ('YamlDotNet', '18.1.0'), ('FSharp.Core', '10.1.401')]}
    producer = root / 'producer.lock.json'; producer.write_text(json.dumps({'dependencies': {'net10.0': closure}}))
    prepare(SimpleNamespace(package=str(selected), version=VERSION, sha256=package_sha, revision=REVISION,
                            lock=str(producer), output=str(evidence), contracts_source='https://public.invalid/index.json',
                            public_source='https://public.invalid/index.json'))
    (evidence / 'producer.packages.lock.json').write_bytes(producer.read_bytes())
    (evidence / 'packages.lock.json').write_text(json.dumps({'dependencies': {'net10.0': {
        **closure, 'FS.GG.Governance.Config': {'resolved': VERSION}}}}))
    libraries = {'FS.GG.Governance.Config/'+VERSION: {'type': 'package'},
                 **{n+'/'+e['resolved']: {'type': 'package'} for n,e in closure.items()}}
    (evidence / 'project.assets.json').write_text(json.dumps({'libraries': libraries}))
    contracts = evidence / 'FS.GG.Contracts.7.6.0.nupkg'
    contract_dll = b'synthetic-contracts-dll-not-qualification'
    contracts_sha = write(contracts, {'lib/net10.0/FS.GG.Contracts.dll': contract_dll})
    contracts_dll_sha = hashlib.sha256(contract_dll).hexdigest()
    (evidence / 'contracts-selection.json').write_text(json.dumps({'version': '7.6.0', 'archiveFile': contracts.name,
        'archiveSha256': contracts_sha, 'assemblySha256': contracts_dll_sha, 'contentHash': closure['FS.GG.Contracts']['contentHash']}))
    (evidence / 'default-consumer-result.json').write_text(json.dumps({'result': 'passed', 'configSha256': package_sha,
        'sourceRevision': REVISION, 'loadedConfigCount': 1, 'loadedContractsCount': 1,
        'configAssemblySha256': hashlib.sha256(entries['lib/net10.0/FS.GG.Governance.Config.dll']).hexdigest(),
        'contractsAssemblySha256': contracts_dll_sha, 'contractsAssemblyVersion': '7.6.0.0'}))
    (evidence / 'config-tests').mkdir(); (evidence / 'config-tests/config-tests.trx').write_text('synthetic native test report')
    repository = {'full_name': 'FS-GG/FS.GG.Governance', 'id': 7}
    run = {'id': 12, 'repository': repository, 'head_repository': repository, 'head_sha': REVISION,
           'run_attempt': 1, 'workflow_id': 9, 'event': 'workflow_dispatch', 'status': 'completed', 'conclusion': 'failure',
           'display_title': 'publish workflow_dispatch scope=config version=0.3.0 recovery=none'}
    workflow = {'id': 9, 'path': '.github/workflows/publish.yml'}
    steps = [{'name': name, 'number': index+1, 'status': 'completed', 'conclusion': 'success',
              'started_at': '2026-10-09T10:00:00Z', 'completed_at': '2026-10-09T10:01:00Z'}
             for index,name in enumerate(QUALIFICATION_STEPS+['Retain Config archive and manifest'])]
    job = {'name': 'Pack + publish FS.GG.Governance.Config', 'run_id': 12, 'run_attempt': 1, 'head_sha': REVISION, 'steps': steps}
    jobs = {'total_count': 2, 'jobs': [job, {'name': 'Resolve selected scope and project version', 'conclusion': 'success'}]}
    retained_native = {'run': {**run, 'status': 'in_progress'}, 'workflow': workflow, 'jobs': jobs}
    (evidence / 'qualification-native.json').write_text(json.dumps(retained_native))
    raw_buffer = io.BytesIO()
    with zipfile.ZipFile(raw_buffer, 'w') as archive:
        for path in original.rglob('*'):
            if path.is_file(): archive.write(path, path.relative_to(original))
    raw = raw_buffer.getvalue()
    artifact = {'id': 34, 'name': 'config-package-'+REVISION, 'expired': False,
                'created_at': '2026-10-09T10:00:30Z', 'digest': 'sha256:'+hashlib.sha256(raw).hexdigest(),
                'workflow_run': {'id': 12, 'head_sha': REVISION, 'repository_id': 7, 'head_repository_id': 7}}
    base_observations = {'actions/runs/12': run, 'actions/workflows/9': workflow,
                         'actions/runs/12/attempts/1/jobs?per_page=100': jobs,
                         'actions/artifacts/34': artifact, 'actions/artifacts/34/zip': raw}
    def execute(label, observations=base_observations, changes=None):
        args = SimpleNamespace(run_id='12', artifact_id='34', sha256=package_sha, version=VERSION,
                               revision=REVISION, repository=repository['full_name'], lock=str(producer),
                               output=str(root / label), outputs=str(root / (label+'.outputs')))
        if changes: args = SimpleNamespace(**{**vars(args), **changes})
        with mock.patch('archive.actions_read', side_effect=lambda repository, endpoint, archive=False: observations[endpoint]), \
             mock.patch('archive.CONTRACTS_ARCHIVE_SHA256', contracts_sha), \
             mock.patch('archive.CONTRACTS_ASSEMBLY_SHA256', contracts_dll_sha), \
             mock.patch('subprocess.run', side_effect=AssertionError('recovery must launch zero pack/consumer calls')):
            recover(args)
        return args
    positive = execute('positive')
    assert Path(positive.outputs).read_text().splitlines()[1] == 'sha256='+package_sha
    assert Path(positive.output, 'config-packages', selected.name).read_bytes() == selected.read_bytes()
    cases = []
    def altered(label, mutate):
        observations = copy.deepcopy(base_observations); mutate(observations); cases.append((label, observations))
    altered('wrong-run', lambda x: x['actions/runs/12'].update(id=13))
    altered('wrong-repository', lambda x: x['actions/runs/12']['repository'].update(full_name='Other/Repo'))
    altered('wrong-source', lambda x: x['actions/runs/12'].update(head_sha='b'*40))
    altered('wrong-workflow', lambda x: x['actions/workflows/9'].update(path='.github/workflows/other.yml'))
    altered('wrong-attempt', lambda x: x['actions/runs/12'].update(run_attempt=2))
    altered('wrong-scope', lambda x: x['actions/runs/12'].update(display_title='publish workflow_dispatch scope=all version=0.3.0 recovery=none'))
    altered('wrong-version', lambda x: x['actions/runs/12'].update(display_title='publish workflow_dispatch scope=config version=0.4.0 recovery=none'))
    altered('dry-run-promotion', lambda x: x['actions/runs/12'].update(display_title='publish workflow_dispatch scope=config version=dry-run recovery=none'))
    altered('recovery-copy-promotion', lambda x: x['actions/runs/12'].update(display_title='publish workflow_dispatch scope=config version=0.3.0 recovery=11'))
    altered('still-active-run', lambda x: x['actions/runs/12'].update(status='in_progress'))
    altered('wrong-artifact', lambda x: x['actions/artifacts/34'].update(id=35))
    altered('expired-artifact', lambda x: x['actions/artifacts/34'].update(expired=True))
    altered('wrong-artifact-run', lambda x: x['actions/artifacts/34']['workflow_run'].update(id=13))
    altered('wrong-artifact-source', lambda x: x['actions/artifacts/34']['workflow_run'].update(head_sha='b'*40))
    altered('wrong-artifact-digest', lambda x: x['actions/artifacts/34'].update(digest='sha256:'+'0'*64))
    altered('wrong-artifact-attempt-window', lambda x: x['actions/artifacts/34'].update(created_at='2026-10-09T11:00:00Z'))
    altered('incomplete-native-jobs', lambda x: x['actions/runs/12/attempts/1/jobs?per_page=100'].update(total_count=3))
    for stage in QUALIFICATION_STEPS+['Retain Config archive and manifest']:
        altered('failed-'+stage, lambda x, stage=stage: next(s for s in x['actions/runs/12/attempts/1/jobs?per_page=100']['jobs'][0]['steps'] if s['name']==stage).update(conclusion='failure'))
    for label, observations in cases:
        refused('recovery-'+label, lambda label=label, observations=observations: execute(label, observations))
    refused('recovery-wrong-package-hash', lambda: execute('wrong-package-hash', changes={'sha256': '0'*64}))
    def changed_artifact(label, modify):
        stream = io.BytesIO()
        with zipfile.ZipFile(io.BytesIO(raw)) as source, zipfile.ZipFile(stream, 'w') as target:
            for name in source.namelist():
                value = modify(name, source.read(name))
                if value is not None: target.writestr(name, value)
        observations = copy.deepcopy(base_observations); observations['actions/artifacts/34/zip'] = stream.getvalue()
        observations['actions/artifacts/34']['digest'] = 'sha256:'+hashlib.sha256(stream.getvalue()).hexdigest()
        refused(label, lambda: execute(label, observations))
    changed_artifact('lost-package', lambda name, value: None if name.endswith('.nupkg') and name.startswith('config-packages/') else value)
    changed_artifact('missing-native-evidence', lambda name, value: None if name.endswith('qualification-native.json') else value)
    changed_artifact('failed-consumer-receipt', lambda name, value: value.replace(b'"passed"', b'"failed"') if name.endswith('default-consumer-result.json') else value)
    changed_artifact('wrong-dependency-closure', lambda name, value: value.replace(b'7.6.0', b'7.7.0') if name.endswith('producer.packages.lock.json') else value)
    # Readback derives original source from native metadata, then requires original publication.
    from archive import original_source
    published = copy.deepcopy(base_observations)
    published['actions/runs/12']['conclusion'] = 'success'
    published_job = published['actions/runs/12/attempts/1/jobs?per_page=100']['jobs'][0]
    published_job['conclusion'] = 'success'
    publication_names = ['Check Config feed collisions', 'Push Config to org feed',
                         'Trusted Publishing Config login', 'Verify same Config bytes and push public']
    published_job['steps'] += [{'name': name, 'number': 20+i, 'status': 'completed', 'conclusion': 'success'}
                              for i,name in enumerate(publication_names)]
    source_args = SimpleNamespace(repository=repository['full_name'],run_id='12',outputs=str(root/'native-source.outputs'))
    with mock.patch('archive.actions_read', return_value=published['actions/runs/12']):
        original_source(source_args)
    assert Path(source_args.outputs).read_text() == 'revision='+REVISION+'\n'
    execute('published-readback', published, {'published': True})
    refused('readback-failed-original-publication', lambda: execute('failed-original-publication', changes={'published': True}))
    for name in publication_names:
        mutated = copy.deepcopy(published)
        next(stage for stage in mutated['actions/runs/12/attempts/1/jobs?per_page=100']['jobs'][0]['steps'] if stage['name']==name)['conclusion'] = 'failure'
        refused('readback-failed-'+name, lambda name=name, mutated=mutated: execute('readback-failed-'+name, mutated, {'published':True}))
    for field,value in [('status','in_progress'),('run_attempt',2),('conclusion','failure'),('head_sha','$(literal)')]:
        mutated = {**published['actions/runs/12'],field:value}
        with mock.patch('archive.actions_read', return_value=mutated):
            refused('readback-source-'+field, lambda: original_source(source_args))
    # Reuse the actual existing both-feed observer against synthetic feed bytes.
    manifest = str(Path(positive.output, 'config-evidence/manifest.json'))
    org, public = 'https://org.invalid/index.json', 'https://public.invalid/index.json'
    def feed_response(request, timeout):
        if request.full_url in [org, public]:
            return io.BytesIO(json.dumps({'resources': [{'@type':'PackageBaseAddress/3.0.0','@id':request.full_url+'/flat/'}]}).encode())
        if request.full_url.startswith(org): return io.BytesIO(selected.read_bytes())
        raise urllib.error.HTTPError(request.full_url, 404, 'synthetic absent', {}, None)
    args = SimpleNamespace(manifest=manifest, org_source=org, public_source=public, outputs=str(root/'feed.outputs'))
    with mock.patch.dict(os.environ, {'FSGG_PACKAGES_ACTOR':'synthetic', 'FSGG_PACKAGES_READ_TOKEN':'synthetic'}):
        with mock.patch('archive.credential_safe_open', side_effect=feed_response): collision(args)
        assert Path(args.outputs).read_text().splitlines() == ['org=equal', 'public=absent']
        with mock.patch('archive.credential_safe_open', side_effect=feed_response):
            refused('readback-absent-public-feed', lambda: collision(SimpleNamespace(**{**vars(args),'require_present':True})))
        def both_equal(request,timeout):
            if request.full_url in [org,public]: return feed_response(request,timeout)
            return io.BytesIO(selected.read_bytes())
        equal_outputs = root/'both-equal.outputs'
        with mock.patch('archive.credential_safe_open', side_effect=both_equal):
            collision(SimpleNamespace(**{**vars(args),'outputs':str(equal_outputs),'require_present':True}))
        assert equal_outputs.read_text().splitlines() == ['org=equal','public=equal']
        with mock.patch('archive.credential_safe_open', side_effect=urllib.error.HTTPError(org, 403, 'synthetic unknown', {}, None)):
            refused('inaccessible-feed', lambda: collision(args))
        observation = json.loads(Path(manifest).with_name('feed-observations.json').read_text())[-1]
        assert observation['stage']=='service-index' and observation['responseHost']=='org.invalid' and observation['httpStatus']==403
        def redirected_failure(request,timeout):
            if request.full_url in [org,public]: return feed_response(request,timeout)
            raise urllib.error.HTTPError('https://storage.invalid/blob?sig=synthetic-private-query',403,'synthetic unknown',{},None)
        with mock.patch('archive.credential_safe_open',side_effect=redirected_failure):
            refused('redirected-archive-access',lambda: collision(args))
        raw_observation = Path(manifest).with_name('feed-observations.json').read_text()
        observation = json.loads(raw_observation)[-1]
        assert observation['stage']=='package-archive' and observation['responseHost']=='storage.invalid'
        assert 'synthetic-private-query' not in raw_observation and 'synthetic-control-only' not in raw_observation

        def ambiguous(request, timeout):
            return io.BytesIO(json.dumps({'resources':[{'@type':'PackageBaseAddress/3.0.0','@id':'one'}, {'@type':'PackageBaseAddress/3.0.0','@id':'two'}]}).encode())
        with mock.patch('archive.credential_safe_open', side_effect=ambiguous): refused('ambiguous-feed', lambda: collision(args))
        conflict_buffer = io.BytesIO()
        with zipfile.ZipFile(conflict_buffer,'w') as archive:
            for name,value in entries.items(): archive.writestr(name, value+b'conflict' if name.endswith('.dll') else value)
        def occupied(request, timeout):
            if request.full_url in [org, public]: return feed_response(request,timeout)
            return io.BytesIO(conflict_buffer.getvalue())
        with mock.patch('archive.credential_safe_open', side_effect=occupied): refused('occupied-unequal-payload', lambda: collision(args))
        signed_buffer = io.BytesIO()
        with zipfile.ZipFile(signed_buffer,'w') as archive:
            for name,value in entries.items(): archive.writestr(name,value)
            archive.writestr('.signature.p7s',b'synthetic-unverified-signature')
        def signed(request,timeout):
            if request.full_url in [org, public]: return feed_response(request,timeout)
            return io.BytesIO(signed_buffer.getvalue())
        with mock.patch('archive.credential_safe_open', side_effect=signed), \
             mock.patch('subprocess.run', return_value=SimpleNamespace(returncode=1,stdout='',stderr='synthetic failure')):
            refused('unverifiable-signature', lambda: collision(args))
        Path(positive.output, 'config-packages', selected.name).write_bytes(b'changed after recovery')
        refused('archive-change-before-effects', lambda: collision(args))
print('Synthetic original-run recovery controls passed; zero pack/consumer/publication calls.')
