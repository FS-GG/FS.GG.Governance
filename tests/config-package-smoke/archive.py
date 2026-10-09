#!/usr/bin/env python3
"""Fail-closed Config archive/closure inspection; never builds, packs or pushes."""
import argparse
import base64
import hashlib
from datetime import datetime
import json
import io
import shutil
import stat
import os
from pathlib import Path
import re
import subprocess
import urllib.error
import urllib.request
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile

PACKAGE = 'FS.GG.Governance.Config'
ALLOWED = {'FS.GG.Contracts', 'YamlDotNet', 'FSharp.Core'}
CONTRACTS_ARCHIVE_SHA256 = 'b1df3ebd6251f5b18aaece4dd0c5449a7825dc7056f925cf2516febc35f9dfc5'
CONTRACTS_ASSEMBLY_SHA256 = '91f484d28416c5d860a375a91ed70cdda1d3b6d85d504c15ea21e08a9af727ee'

def require(value, message):
    if not value:
        raise ValueError(message)

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def inspect(path, version, sha, revision):
    path = Path(path).resolve(strict=True)
    require(re.fullmatch(r'[0-9a-f]{64}', sha), 'expected SHA-256 must be lowercase hex')
    require(digest(path) == sha, 'archive SHA-256 mismatch')
    candidates = list(path.parent.glob(PACKAGE + '.*.nupkg'))
    require(candidates == [path], 'expected exactly one ordinary Config archive in input directory')
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), 'duplicate archive entries')
        require(all(not n.startswith('/') and '..' not in Path(n).parts for n in names), 'unsafe archive path')
        specs = [n for n in names if n.endswith('.nuspec')]
        require(len(specs) == 1, 'expected one nuspec')
        root = ET.fromstring(archive.read(specs[0]))
        ns = {'n': root.tag.split('}')[0][1:]} if root.tag.startswith('{') else {}
        prefix = 'n:' if ns else ''
        metadata = root.find(prefix + 'metadata', ns)
        def find(name):
            return metadata.find(prefix + name, ns)
        require(find('id').text == PACKAGE and find('version').text == version, 'wrong package ID/version')
        repository = find('repository')
        require(repository is not None and repository.get('commit') == revision, 'package source revision mismatch')
        types = find('packageTypes')
        require(types is None or all(t.get('name') == 'Dependency' for t in types), 'tool/template/content package type')
        require(not any(n.startswith(('tools/', 'content/', 'contentFiles/', 'build/', 'buildTransitive/')) for n in names), 'unexpected tool/template/content payload')
        libraries = [n for n in names if n.startswith('lib/') and n.endswith('.dll')]
        require(libraries == ['lib/net10.0/' + PACKAGE + '.dll'], 'wrong TFM/assembly payload')
        for name in [libraries[0], 'lib/net10.0/' + PACKAGE + '.xml']:
            require(name in names and len(archive.read(name)) > 0, 'missing/empty assembly or XML documentation')
        xml = archive.read('lib/net10.0/' + PACKAGE + '.xml')
        require(b'CapabilityBindings' in xml and b'resolve' in xml, 'resolver API documentation missing')
        groups = metadata.findall('.//' + prefix + 'group', ns)
        require(len(groups) == 1 and groups[0].get('targetFramework') == 'net10.0', 'wrong dependency TFM')
        dependencies = {d.get('id'): d.get('version') for d in groups[0]}
        require(len(dependencies) == len(groups[0]), 'duplicate nuspec dependency')
        require(set(dependencies) == ALLOWED, 'dependency boundary drift')
        return {'package': PACKAGE, 'version': version, 'sha256': sha, 'sourceRevision': revision,
                'path': str(path), 'dependencies': dependencies,
                'entries': {n: hashlib.sha256(archive.read(n)).hexdigest() for n in names if not n.endswith('/')}}

def prepare(args):
    manifest = inspect(args.package, args.version, args.sha256, args.revision)
    closure = json.loads(Path(args.lock).read_text())['dependencies']['net10.0']
    require(set(closure) == ALLOWED, 'producer dependency closure changed; review source mapping')
    for name, entry in closure.items():
        # Nuspec minimum may be older for centrally pinned transitive dependencies.
        require(entry['resolved'], 'missing resolved dependency version')
        def minimum(value):
            return value.strip('[]() ').split(',')[0].strip()
        require(minimum(manifest['dependencies'][name]) == minimum(entry['requested']),
                'nuspec dependency version drift: ' + name)
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    manifest['closure'] = closure
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    # Exact pins are independently taken from the committed producer lock, never guessed.
    (output / 'versions.json').write_text(json.dumps({n: e['resolved'] for n, e in closure.items()}))
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources')
    ET.SubElement(sources, 'clear')
    packed_source = str(Path(args.package).resolve().parent)
    require(packed_source not in [args.contracts_source, args.public_source],
            'dependency feed must remain separate from the exact packed Config source')
    # NuGet deduplicates equal source endpoints. Keep one key per endpoint and
    # union its exact package mappings, so public Contracts can share the public
    # endpoint without dropping FSharp.Core/YamlDotNet behind a duplicate alias.
    endpoints = {}
    for key, value, packages in [('packed-config', packed_source, [PACKAGE]),
                                 ('contracts', args.contracts_source, ['FS.GG.Contracts']),
                                 ('public', args.public_source, ['FSharp.Core', 'YamlDotNet'])]:
        if value not in endpoints:
            endpoints[value] = (key, [])
        endpoints[value][1].extend(packages)
    mapping = ET.SubElement(config, 'packageSourceMapping')
    for value, (key, packages) in endpoints.items():
        ET.SubElement(sources, 'add', key=key, value=value)
        source = ET.SubElement(mapping, 'packageSource', key=key)
        for name in packages:
            ET.SubElement(source, 'package', pattern=name)
    ET.ElementTree(config).write(output / 'NuGet.Config', encoding='utf-8', xml_declaration=True)

def resolved(args):
    manifest = json.loads(Path(args.manifest).read_text())
    assets = json.loads(Path(args.assets).read_text())
    expected = {PACKAGE: manifest['version'], **{n: e['resolved'] for n, e in manifest['closure'].items()}}
    libraries = assets['libraries']
    require(set(libraries) == {n + '/' + v for n, v in expected.items()}, 'resolved package closure drift')
    require(all(e['type'] == 'package' for e in libraries.values()), 'project dependency detected')
    require(set(assets['project']['restore']['sources']) == set(args.source), 'restore source drift')
    lock = json.loads(Path(args.lock).read_text())['dependencies']['net10.0']
    require(set(lock) == set(expected), 'consumer lock closure drift')
    for name, version in expected.items():
        require(lock[name]['resolved'] == version, 'resolved version drift: ' + name)
        if name in manifest['closure']:
            require(lock[name]['contentHash'] == manifest['closure'][name]['contentHash'], 'dependency bytes drift: ' + name)
    targets = assets['targets']
    require(len(targets) == 1, 'ambiguous consumer target')
    target = next(iter(targets.values()))
    contract = target['FS.GG.Contracts/' + expected['FS.GG.Contracts']]
    dlls = [n for n in contract['compile'] if n.endswith('/FS.GG.Contracts.dll')]
    require(len(dlls) == 1, 'not one resolved Contracts assembly')
    require((Path(args.cache) / 'fs.gg.contracts' / expected['FS.GG.Contracts'] / dlls[0]).is_file(),
            'resolved Contracts assembly missing')
    require(digest(manifest['path']) == manifest['sha256'], 'archive changed during consumption')


# Native Actions observations authenticate qualification; local receipts alone cannot.
QUALIFICATION_STEPS = ['Restore (locked, cold)', 'Test complete Config suite',
                       'Resolve Config package version', 'Pack Config once',
                       'Identify exact Config archive', 'Config package consumer smoke',
                       'Validate complete Config retention']

def actions_read(repository, endpoint, archive=False):
    require(repository == 'FS-GG/FS.GG.Governance', 'wrong recovery repository')
    token = os.environ.get('GH_TOKEN')
    require(token, 'Actions read credential unavailable')
    request = urllib.request.Request('https://api.github.com/repos/' + repository + '/' + endpoint,
        headers={'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
                 'X-GitHub-Api-Version': '2022-11-28'})
    class ArtifactRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, request, *redirect):
            redirected = super().redirect_request(request, *redirect)
            if redirected is not None:
                redirected.remove_header('Authorization')
            return redirected
    with urllib.request.build_opener(ArtifactRedirect()).open(request, timeout=30) as response:
        limit = 64 * 1024 * 1024 if archive else 4 * 1024 * 1024
        raw = response.read(limit + 1)
        require(len(raw) <= limit, 'Actions observation exceeds bounded input')
    return raw if archive else json.loads(raw)

def native_qualification(repository, revision, run_id, version, run, workflow, jobs, recovery):
    require(repository == 'FS-GG/FS.GG.Governance', 'wrong recovery repository')
    require(str(run['id']) == str(run_id) and run['repository']['full_name'] == repository,
            'original run/repository mismatch')
    require(run['head_repository']['full_name'] == repository and run['head_sha'] == revision,
            'original source mismatch')
    require(run['run_attempt'] == 1, 'original attempt must be one; reruns cannot promote repacked bytes')
    require(workflow['id'] == run['workflow_id'] and workflow['path'] == '.github/workflows/publish.yml',
            'wrong original owning workflow')
    require(run['event'] == 'workflow_dispatch' if recovery else run['event'] in ['workflow_dispatch', 'push', 'release'],
            'wrong original event')
    if recovery:
        require(run['status'] == 'completed', 'original operation still active/unknown')
        titles = ['publish workflow_dispatch scope=config version=' + v + ' recovery=none'
                  for v in [version, 'v' + version]]
        require(run['display_title'] in titles, 'original Config scope/version/first-run selection mismatch')
    require(jobs['total_count'] == len(jobs['jobs']) and len(jobs['jobs']) <= 100,
            'ambiguous/incomplete native job population')
    matches = [j for j in jobs['jobs'] if j['name'] == 'Pack + publish FS.GG.Governance.Config']
    require(len(matches) == 1, 'missing/ambiguous original Config job')
    job = matches[0]
    require(job['run_id'] == run['id'] and job['run_attempt'] == 1 and job['head_sha'] == revision,
            'original Config job identity mismatch')
    names = QUALIFICATION_STEPS if recovery else QUALIFICATION_STEPS[:-1]
    if recovery:
        names = names + ['Retain Config archive and manifest']
        resolver = [j for j in jobs['jobs'] if j['name'] == 'Resolve selected scope and project version']
        require(len(resolver) == 1 and resolver[0]['conclusion'] == 'success', 'original scope resolver failed')
        require(all(j['name'] in [job['name'], resolver[0]['name']] or j['conclusion'] == 'skipped'
                    for j in jobs['jobs']), 'original scope includes unrelated work')
    numbers = []
    for name in names:
        found = [step for step in job['steps'] if step['name'] == name]
        require(len(found) == 1 and found[0]['status'] == 'completed' and found[0]['conclusion'] == 'success',
                'original native qualification/retention stage missing or failed: ' + name)
        numbers.append(found[0]['number'])
    require(numbers == sorted(set(numbers)), 'original qualification ordering drift')
    return job

def observed_run(args):
    require(args.repository == 'FS-GG/FS.GG.Governance' and re.fullmatch(r'[0-9a-f]{40}', args.revision), 'invalid repository/source selection')
    require(re.fullmatch(r'[1-9][0-9]*', str(args.run_id)), 'invalid original run ID')
    run = actions_read(args.repository, 'actions/runs/' + str(args.run_id))
    workflow = actions_read(args.repository, 'actions/workflows/' + str(run['workflow_id']))
    jobs = actions_read(args.repository, 'actions/runs/' + str(args.run_id) + '/attempts/1/jobs?per_page=100')
    return run, workflow, jobs

def retained_inputs(package, version, sha, revision, lock, evidence):
    evidence = Path(evidence)
    inspected = inspect(package, version, sha, revision)
    manifest = json.loads((evidence / 'manifest.json').read_text())
    for field in ['package', 'version', 'sha256', 'sourceRevision', 'dependencies', 'entries']:
        require(manifest[field] == inspected[field], 'retained manifest identity drift: ' + field)
    producer = json.loads(Path(lock).read_text())['dependencies']['net10.0']
    require(set(producer) == ALLOWED and manifest['closure'] == producer, 'retained producer dependency closure drift')
    for name, dependency in producer.items():
        require(inspected['dependencies'][name].strip('[]() ').split(',')[0].strip() ==
                dependency['requested'].strip('[]() ').split(',')[0].strip(), 'retained nuspec dependency drift')
    require(json.loads((evidence / 'versions.json').read_text()) == {n: e['resolved'] for n, e in producer.items()}, 'retained dependency pins changed')
    source_config = ET.parse(evidence / 'NuGet.Config').getroot()
    mappings = [package.get('pattern') for package in source_config.findall('./packageSourceMapping/packageSource/package')]
    require(sorted(mappings) == sorted([PACKAGE, *ALLOWED]), 'retained source mapping drift')
    expected = {PACKAGE: version, **{n: e['resolved'] for n, e in producer.items()}}
    consumer = json.loads((evidence / 'packages.lock.json').read_text())['dependencies']['net10.0']
    require(set(consumer) == set(expected), 'retained consumer lock closure drift')
    for name, selected in expected.items():
        require(consumer[name]['resolved'] == selected, 'retained consumer version drift: ' + name)
        if name in producer:
            require(consumer[name]['contentHash'] == producer[name]['contentHash'], 'retained dependency bytes drift')
    assets = json.loads((evidence / 'project.assets.json').read_text())
    require(set(assets['libraries']) == {n + '/' + v for n, v in expected.items()} and
            all(item['type'] == 'package' for item in assets['libraries'].values()), 'retained assets closure drift')
    receipt = json.loads((evidence / 'default-consumer-result.json').read_text())
    require(receipt['result'] == 'passed' and receipt['configSha256'] == sha and receipt['sourceRevision'] == revision,
            'retained actual consumer receipt mismatch')
    require(producer['FS.GG.Contracts']['resolved'] == '7.6.0', 'selected Contracts compatibility not qualified')
    contracts = json.loads((evidence / 'contracts-selection.json').read_text())
    require(contracts['version'] == '7.6.0' and contracts['archiveFile'] == 'FS.GG.Contracts.7.6.0.nupkg' and
            contracts['archiveSha256'] == CONTRACTS_ARCHIVE_SHA256 and
            contracts['assemblySha256'] == CONTRACTS_ASSEMBLY_SHA256 and
            contracts['contentHash'] == producer['FS.GG.Contracts']['contentHash'], 'selected published Contracts identity mismatch')
    require(digest(evidence / contracts['archiveFile']) == contracts['archiveSha256'], 'retained Contracts archive changed')
    with zipfile.ZipFile(evidence / contracts['archiveFile']) as archive:
        require(hashlib.sha256(archive.read('lib/net10.0/FS.GG.Contracts.dll')).hexdigest() == contracts['assemblySha256'],
                'retained Contracts payload changed')
    require(receipt['loadedConfigCount'] == 1 and receipt['loadedContractsCount'] == 1 and
            receipt['contractsAssemblySha256'] == contracts['assemblySha256'], 'actual selected Contracts assembly mismatch')
    require(receipt['configAssemblySha256'] == inspected['entries']['lib/net10.0/' + PACKAGE + '.dll'],
            'actual Config assembly differs from selected archive')
    report = evidence / 'config-tests/config-tests.trx'
    require(report.is_file() and report.stat().st_size > 0, 'complete Config test report absent')
    return manifest

def retain(args):
    require(args.attempt == '1', 'only original first attempt can retain a release candidate')
    retained_inputs(args.package, args.version, args.sha256, args.revision, args.lock, args.evidence)
    run, workflow, jobs = observed_run(args)
    native_qualification(args.repository, args.revision, args.run_id, args.version, run, workflow, jobs, False)
    evidence = Path(args.evidence)
    shutil.copyfile(args.lock, evidence / 'producer.packages.lock.json')
    (evidence / 'qualification-native.json').write_text(json.dumps(
        {'run': run, 'workflow': workflow, 'jobs': jobs}, indent=2) + '\n')

def recover(args):
    require(re.fullmatch(r'[1-9][0-9]*', str(args.artifact_id)) and re.fullmatch(r'[0-9a-f]{64}', args.sha256), 'invalid original artifact/hash selection')
    run, workflow, jobs = observed_run(args)
    original_job = native_qualification(args.repository, args.revision, args.run_id, args.version, run, workflow, jobs, True)
    artifact = actions_read(args.repository, 'actions/artifacts/' + str(args.artifact_id))
    require(str(artifact['id']) == str(args.artifact_id) and not artifact['expired'] and
            artifact['name'] == 'config-package-' + args.revision, 'original artifact missing/expired/wrong identity')
    link = artifact['workflow_run']
    require(link['id'] == run['id'] and link['head_sha'] == args.revision and
            link['repository_id'] == run['repository']['id'] and
            link['head_repository_id'] == run['head_repository']['id'], 'artifact original run/source/repository mismatch')
    retention = next(step for step in original_job['steps'] if step['name'] == 'Retain Config archive and manifest')
    timestamp = lambda value: datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(timestamp(retention['started_at']) <= timestamp(artifact['created_at']) <= timestamp(retention['completed_at']),
            'artifact does not belong to original successful retention stage')
    raw = actions_read(args.repository, 'actions/artifacts/' + str(args.artifact_id) + '/zip', True)
    require(artifact['digest'] == 'sha256:' + hashlib.sha256(raw).hexdigest(), 'Actions artifact archive digest mismatch')
    root = Path(args.output)
    require(not (root / 'config-packages').exists() and not (root / 'config-evidence').exists(), 'recovery output already occupied')
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)) and len(names) <= 2000, 'ambiguous/oversized retained artifact')
        require(sum(item.file_size for item in archive.infolist()) <= 64 * 1024 * 1024, 'retained artifact expansion exceeds bound')
        for item in archive.infolist():
            path = Path(item.filename)
            require(not path.is_absolute() and '..' not in path.parts and '\\' not in item.filename and
                    path.parts and path.as_posix() == item.filename.rstrip('/') and path.parts[0] in ['config-packages', 'config-evidence'] and
                    not stat.S_ISLNK(item.external_attr >> 16), 'unsafe/unexpected retained artifact path')
        archive.extractall(root)
    package = root / 'config-packages' / (PACKAGE + '.' + args.version + '.nupkg')
    evidence = root / 'config-evidence'
    manifest = retained_inputs(package, args.version, args.sha256, args.revision, args.lock, evidence)
    require(json.loads((evidence / 'producer.packages.lock.json').read_text()) == json.loads(Path(args.lock).read_text()),
            'original retained producer lock mismatch')
    native = json.loads((evidence / 'qualification-native.json').read_text())
    native_qualification(args.repository, args.revision, args.run_id, args.version,
                         native['run'], native['workflow'], native['jobs'], False)
    # Resolve the recovered physical location after verifying all original identities.
    manifest['path'] = str(package.resolve())
    (evidence / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    (evidence / 'recovery-observations.json').write_text(json.dumps(
        {'run': run, 'workflow': workflow, 'jobs': jobs, 'artifact': artifact,
         'archiveSha256': hashlib.sha256(raw).hexdigest()}, indent=2) + '\n')
    with Path(args.outputs).open('a') as output:
        output.write('path=' + str(package.resolve()) + '\nsha256=' + args.sha256 + '\n')


def org_absence(version):
    """Authenticated scoped enumeration establishes absence, never a bare 404.

    This uses existing GitHub package read permission. Unreadable populations
    remain unknown; no grant, retry authority or alternative publisher is added.
    """
    observations = []
    def pages(endpoint):
        found = []
        for page in range(1, 101):
            separator = '&' if '?' in endpoint else '?'
            url = endpoint + separator + 'per_page=100&page=' + str(page)
            headers = {'Authorization': 'Bearer ' + os.environ['FSGG_PACKAGES_READ_TOKEN'],
                       'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
                    raw = response.read()
            except urllib.error.HTTPError as error:
                raise ValueError('authenticated org enumeration unreadable: HTTP ' + str(error.code)) from None
            values = json.loads(raw)
            require(isinstance(values, list), 'org enumeration did not return a package population')
            observations.append({'endpoint': endpoint, 'page': page, 'count': len(values),
                                 'responseSha256': hashlib.sha256(raw).hexdigest()})
            found.extend(values)
            if len(values) < 100:
                return found
        raise ValueError('org enumeration exceeded bounded population; root observation required')
    packages = pages('https://api.github.com/orgs/FS-GG/packages?package_type=nuget')
    matches = [p for p in packages if p['name'].casefold() == PACKAGE.casefold()]
    require(len(matches) <= 1, 'ambiguous org package identity')
    if matches:
        versions = pages('https://api.github.com/orgs/FS-GG/packages/nuget/' +
                         urllib.parse.quote(matches[0]['name'], safe='') + '/versions')
        require(not any(v['name'].casefold() == version.casefold() for v in versions),
                'existing org version is not readable as an archive; equality unknown')
    return {'outcome': 'absent-by-authenticated-enumeration', 'observations': observations}


def collision(args):
    """Observe both feeds before any effect; unreadable observations refuse.

    Equal versions compare payload entries. Repository signatures are retained separately,
    and excluded only when identified by NuGet's reserved .signature.p7s entry.
    """
    manifest = json.loads(Path(args.manifest).read_text())
    require(digest(manifest['path']) == manifest['sha256'], 'archive changed before collision observation')
    evidence = []
    for feed in [args.org_source, args.public_source]:
        def fetch(url, absent=False):
            headers = {}
            if feed == args.org_source:
                actor, token = os.environ['FSGG_PACKAGES_ACTOR'], os.environ['FSGG_PACKAGES_READ_TOKEN']
                headers['Authorization'] = 'Basic ' + base64.b64encode((actor + ':' + token).encode()).decode()
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
                    return response.read()
            except urllib.error.HTTPError as error:
                if absent and error.code == 404:
                    return None
                raise ValueError('feed observation unreadable: HTTP ' + str(error.code)) from None
        index = json.loads(fetch(feed))
        bases = [r['@id'] for r in index['resources'] if r['@type'].startswith('PackageBaseAddress/')]
        require(len(bases) == 1, 'feed has no unambiguous archive endpoint; root observation required')
        package_id, version = PACKAGE.lower(), manifest['version'].lower()
        url = bases[0].rstrip('/') + '/' + package_id + '/' + version + '/' + package_id + '.' + version + '.nupkg'
        raw = fetch(url, absent=True)
        if raw is None:
            if feed == args.org_source:
                absence = org_absence(manifest['version'])
                evidence.append({'feed': feed, 'archiveUrl': url, **absence})
            else:
                evidence.append({'feed': feed, 'outcome': 'absent', 'archiveUrl': url})
            (Path(args.manifest).parent / 'feed-observations.json').write_text(json.dumps(evidence, indent=2) + '\n')
            continue
        name = 'org-existing.nupkg' if feed == args.org_source else 'public-existing.nupkg'
        target = Path(args.manifest).parent / name
        target.write_bytes(raw)
        with zipfile.ZipFile(target) as archive:
            names = archive.namelist()
            require(len(names) == len(set(names)), 'existing archive duplicate entries')
            entries = {n: hashlib.sha256(archive.read(n)).hexdigest() for n in names if not n.endswith('/')}
        selected = dict(manifest['entries'])
        signature = entries.get('.signature.p7s')
        selected_signature = selected.get('.signature.p7s')
        if signature != selected_signature:
            # Naming alone is not signature identification. NuGet must authenticate and
            # identify a repository signature before it can be excluded from payload comparison.
            verification = subprocess.run(['dotnet', 'nuget', 'verify', str(target), '--all'],
                                          capture_output=True, text=True, timeout=60)
            (target.parent / (name + '.verification.txt')).write_text(verification.stdout + verification.stderr)
            require(verification.returncode == 0 and
                    re.search(r'Signature type:\s*Repository', verification.stdout, re.IGNORECASE),
                    'differing signature is not independently verified as repository signing')
            require(selected_signature is None, 'selected archive already signed; root signature reconciliation required')
            entries.pop('.signature.p7s')
        else:
            entries.pop('.signature.p7s', None)
        selected.pop('.signature.p7s', None)
        require(entries == selected, 'pre-existing version payload conflict')
        evidence.append({'feed': feed, 'outcome': 'payload-equal', 'rawSha256': digest(target),
                         'repositorySignatureSha256': signature, 'selectedSignatureSha256': selected_signature})
    (Path(args.manifest).parent / 'feed-observations.json').write_text(json.dumps(evidence, indent=2) + '\n')
    if args.outputs:
        with Path(args.outputs).open('a') as output:
            for name, observation in zip(['org', 'public'], evidence):
                output.write(name + '=' + ('equal' if observation['outcome'] == 'payload-equal' else 'absent') + '\n')

parser = argparse.ArgumentParser(description=__doc__)
sub = parser.add_subparsers(dest='mode', required=True)
p = sub.add_parser('prepare')
for name in ['package', 'version', 'sha256', 'revision', 'lock', 'output', 'contracts-source', 'public-source']:
    p.add_argument('--' + name, required=True)
p.set_defaults(action=prepare)
p = sub.add_parser('resolved')
for name in ['manifest', 'assets', 'lock', 'cache']:
    p.add_argument('--' + name, required=True)
p.add_argument('--source', action='append', required=True)
p.set_defaults(action=resolved)
p = sub.add_parser('collision')
for name in ['manifest', 'org-source', 'public-source']:
    p.add_argument('--' + name, required=True)
p.add_argument('--outputs')
p.set_defaults(action=collision)
for mode, names in [('retain', ['package', 'sha256', 'version', 'revision', 'repository', 'run-id', 'attempt', 'lock', 'evidence']),
                    ('recover', ['run-id', 'artifact-id', 'sha256', 'version', 'revision', 'repository', 'lock', 'output', 'outputs'])]:
    p = sub.add_parser(mode)
    for name in names:
        p.add_argument('--' + name, required=True)
    p.set_defaults(action=retain if mode == 'retain' else recover)
if __name__ == '__main__':
    try:
        args = parser.parse_args()
        args.action(args)
    except (ValueError, KeyError, OSError, zipfile.BadZipFile, ET.ParseError) as error:
        parser.exit(1, 'Config package refused: ' + str(error) + '\n')
