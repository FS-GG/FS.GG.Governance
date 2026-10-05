#!/usr/bin/env python3
"""Synthetic archive controls only; not real package/native qualification."""
import hashlib
import json
import io
import os
import urllib.error
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
    with mock.patch('urllib.request.urlopen', return_value=io.BytesIO(b'[]')):
        assert org_absence(VERSION)['outcome'] == 'absent-by-authenticated-enumeration'
    def missing_version(request, timeout):
        values = [{'name': 'FS.GG.Governance.Config'}] if 'package_type=nuget' in request.full_url else [{'name': '0.2.0'}]
        return io.BytesIO(json.dumps(values).encode())
    with mock.patch('urllib.request.urlopen', side_effect=missing_version):
        assert org_absence(VERSION)['outcome'] == 'absent-by-authenticated-enumeration'
    def existing_version(request, timeout):
        values = [{'name': 'FS.GG.Governance.Config'}] if 'package_type=nuget' in request.full_url else [{'name': VERSION}]
        return io.BytesIO(json.dumps(values).encode())
    with mock.patch('urllib.request.urlopen', side_effect=existing_version):
        refused('existing-unreadable-org-version', lambda: org_absence(VERSION))
    with mock.patch('urllib.request.urlopen', side_effect=urllib.error.HTTPError('https://synthetic.invalid', 404, 'synthetic unknown', {}, None)):
        refused('bare-org-404', lambda: org_absence(VERSION))
print('Authenticated enumeration controls passed; live feed observation remains unrun.')
