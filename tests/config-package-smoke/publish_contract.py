#!/usr/bin/env python3
"""Cheap static contract for the actual publish-config job's supported YAML shape.

This deliberately bounded projection is not a general YAML/expression evaluator.
Unknown step controls/conditions refuse. Independent required ordering prevents
failed qualification or ambiguous package output from reaching a publication leg.
"""
import argparse
import subprocess
import tempfile
import textwrap
from pathlib import Path
import re
import sys

UNRELATED = ['cli-tests', 'enforcement-smoke', 'publish', 'publish-fsharp-surface-command',
             'publish-kernel', 'publish-adapter-spi', 'publish-code-checks', 'publish-reference-gate-set']
REPOSITORY = "github.repository == 'FS-GG/FS.GG.Governance'"
ALL_SCOPE = REPOSITORY + " && needs.resolve-version.outputs.scope == 'all'"
GATE = "needs.resolve-version.outputs.push == 'true'"
VISIBILITY = "needs.resolve-version.outputs.scope == 'config' || " + GATE
FIRST = "needs.resolve-version.outputs.recovery == 'false'"
RECOVERY = "needs.resolve-version.outputs.recovery == 'true'"
ORDER = ['Checkout', 'Preflight Config publication contract', 'Set up .NET',
         'Restore (locked, cold)', 'Test complete Config suite', 'Authenticate original Config source', 'Checkout original Config source', 'Resolve Config package version',
         'Pack Config once', 'Identify exact Config archive', 'Config package consumer smoke',
         'Validate complete Config retention', 'Recover original Config archive',
         'Select qualified Config archive', 'Retain Config archive and manifest', 'Check Config feed collisions',
         'Retain Config feed observations', 'Push Config to org feed',
         'Trusted Publishing Config login', 'Verify same Config bytes and push public']

def require(value, message):
    if not value:
        raise ValueError(message)

def job_text(text, name="publish-config"):
    matches = list(re.finditer(r'^  ' + re.escape(name) + r':\s*$', text, re.M))
    require(len(matches) == 1, 'expected exactly one ' + name + ' job')
    start = matches[0].end()
    end = re.search(r'^  [a-z][a-z0-9-]*:\s*$', text[start:], re.M)
    return text[start:start + end.start()] if end else text[start:]

def validate(text):
    inventory = re.findall(r'^  ([a-z][a-z0-9-]*):\s*$', text.split('jobs:', 1)[1], re.M)
    require(sorted(inventory) == sorted(['resolve-version', 'publish-config'] + UNRELATED), 'unsupported job inventory')
    dispatch = text.split('  workflow_dispatch:', 1)[1].split('\n#', 1)[0]
    require(re.search(r'^      scope:\n        description: [^\n]+\n        type: choice\n        options: \[all, config\]\n        default: all\n        required: false\n', dispatch, re.M), 'closed scope input/default drift')
    for name in ['config_run_id', 'config_artifact_id', 'config_package_sha256']:
        require(re.search(r'^      ' + name + r':\n        description: [^\n]+\n        type: string\n        required: false\n', dispatch, re.M), 'recovery input drift: ' + name)
    require("run-name: publish ${{ github.event_name }} scope=${{ inputs.scope || 'all' }} version=${{ inputs.version || 'dry-run' }} recovery=${{ inputs.config_run_id || 'none' }}" in text, 'original native scope/version selection missing')
    require(re.search(r'^      config_readback:\n        description: [^\n]+\n        type: boolean\n        default: false\n        required: false\n', dispatch, re.M), 'closed readback input/default drift')
    resolver = job_text(text, 'resolve-version')
    require('readback: ${{ steps.ver.outputs.readback }}' in resolver and 'CONFIG_READBACK: ${{ inputs.config_readback }}' in resolver, 'validated readback binding missing')
    require('recovery: ${{ steps.ver.outputs.recovery }}' in resolver, 'validated recovery output missing')
    require('scope: ${{ steps.ver.outputs.scope }}' in resolver, 'validated scope output missing')
    require('INPUT_SCOPE: ${{ inputs.scope }}' in resolver and 'INPUT_VERSION: ${{ inputs.version }}' in resolver, 'resolver input binding drift')
    expected_needs = {name: ['resolve-version'] for name in UNRELATED}
    expected_needs.update({'enforcement-smoke': ['resolve-version', 'cli-tests'],
                           'publish': ['resolve-version', 'cli-tests', 'enforcement-smoke'],
                           'publish-adapter-spi': ['resolve-version', 'publish-kernel']})
    for name in UNRELATED:
        header = job_text(text, name).split('    steps:', 1)[0]
        require(re.findall(r'^    if: (.+)$', header, re.M) == [ALL_SCOPE], 'unrelated job scope guard drift: ' + name)
        needs = re.findall(r'^    needs: \[(.+)\]$', header, re.M)
        require(len(needs) == 1 and [x.strip() for x in needs[0].split(',')] == expected_needs[name], 'validated resolver dependency missing: ' + name)
    job = job_text(text)
    header = job.split('    steps:', 1)[0]
    require('    needs: [resolve-version]\n' in header, 'Config must depend only on resolve-version')
    require("    if: github.repository == 'FS-GG/FS.GG.Governance'\n" in header, 'repository guard drift')
    require('      contents: read\n      actions: read\n      packages: write\n      id-token: write\n' in header, 'permission drift')
    starts = list(re.finditer(r'^      - name: (.+)$', job, re.M))
    require([m.group(1) for m in starts] == ORDER, 'required step/order drift')
    steps = {m.group(1): job[m.end():starts[i+1].start() if i+1 < len(starts) else len(job)]
             for i, m in enumerate(starts)}
    for name, body in steps.items():
        require(not re.search(r'^        (continue-on-error|timeout-minutes):', body, re.M), 'unsupported step control: ' + name)
        condition = re.findall(r'^        if: (.+)$', body, re.M)
        expected = []
        if name in ['Restore (locked, cold)', 'Test complete Config suite', 'Pack Config once',
                    'Identify exact Config archive', 'Config package consumer smoke', 'Validate complete Config retention']:
            expected = [FIRST]
        elif name in ['Authenticate original Config source', 'Checkout original Config source']:
            expected = ["needs.resolve-version.outputs.readback == 'true'"]
        elif name == 'Recover original Config archive':
            expected = [RECOVERY]
        elif name == 'Check Config feed collisions':
            expected = [VISIBILITY]
        elif name in ['Push Config to org feed', 'Trusted Publishing Config login', 'Verify same Config bytes and push public']:
            feed = 'org' if name == 'Push Config to org feed' else 'public'
            expected = [GATE + " && steps.config-collision.outputs." + feed + " == 'absent'"]
        elif name == 'Retain Config feed observations':
            expected = ["always() && (" + VISIBILITY + ")"]
        require(condition == expected, 'unsupported/missing condition: ' + name)
    require(job.count('dotnet pack ') == 1, 'Config must pack exactly once')
    require(job.count('dotnet nuget push ') == 2 and job.count('NuGet/login@v1') == 1, 'unexpected publication effect')
    require('python3 tests/config-package-smoke/publish_contract.py .github/workflows/publish.yml --mutations' in steps['Preflight Config publication contract'], 'preflight missing before costly work')
    require('uses: ./.github/actions/locked-restore' in steps['Restore (locked, cold)'] and
            'target: tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj' in steps['Restore (locked, cold)'], 'cold locked test restore missing')
    require('run: dotnet test tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj -c Release --no-restore' in steps['Test complete Config suite'] and
            '--filter' not in steps['Test complete Config suite'], 'whole Config suite required')
    require('src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj -getProperty:Version' in steps['Resolve Config package version'], 'Config evaluated version missing')
    require(job.count('needs.resolve-version.outputs.version') == 1 and 'SELECTED_VERSION: ${{ needs.resolve-version.outputs.version }}' in steps['Resolve Config package version'] and 'if [ "$READBACK" = \'true\' ]; then' in steps['Resolve Config package version'], 'validated original version must be readback-only')
    require('src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj -c Release' in steps['Pack Config once'] and
            '-p:Version=${{ steps.config-version.outputs.version }} --no-restore -o artifacts/config-packages' in steps['Pack Config once'], 'pack project/version/output drift')
    identify = steps['Identify exact Config archive']
    for expected in ['id: config-packed', 'packages=(artifacts/config-packages/FS.GG.Governance.Config.*.nupkg)',
                     '[ "${#packages[@]}" -eq 1 ]', 'package="${packages[0]}"', 'sha256sum "$package"']:
        require(expected in identify, 'exact archive identification missing: ' + expected)
    smoke = steps['Config package consumer smoke']
    for expected in ['steps.config-version.outputs.version', 'steps.config-packed.outputs.path',
                     'steps.config-packed.outputs.sha256', 'bash tests/config-package-smoke/run.sh',
                     '"$CONFIG_PACKAGE" "$CONFIG_VERSION" "$CONFIG_SHA256" "$GITHUB_SHA" artifacts/config-evidence']:
        require(expected in smoke, 'package smoke input drift: ' + expected)
    for expected in ['archive.py retain', '--run-id "$GITHUB_RUN_ID"', '--attempt "$GITHUB_RUN_ATTEMPT"', '--lock src/FS.GG.Governance.Config/packages.lock.json']:
        require(expected in steps['Validate complete Config retention'], 'complete native retention missing: ' + expected)
    for expected in ['archive.py recover', '--run-id "$CONFIG_RUN_ID"', '--artifact-id "$CONFIG_ARTIFACT_ID"', '--sha256 "$CONFIG_SHA256"', '--revision "$EXPECTED_SOURCE"', '--repository "$GITHUB_REPOSITORY"', '--outputs "$GITHUB_OUTPUT"']:
        require(expected in steps['Recover original Config archive'], 'original recovery authentication missing: ' + expected)
    require('archive.py original-source' in steps['Authenticate original Config source'] and '--run-id "$CONFIG_RUN_ID"' in steps['Authenticate original Config source'], 'original native source authentication missing')
    for expected in ['ref: ${{ steps.config-original-source.outputs.revision }}', 'path: original-source', 'persist-credentials: false']:
        require(expected in steps['Checkout original Config source'], 'original protected source checkout drift')
    for expected in ['EXPECTED_SOURCE: ${{ steps.config-original-source.outputs.revision || github.sha }}', 'publication=(--published)', "selected_lock='original-source/src/FS.GG.Governance.Config/packages.lock.json'"]:
        require(expected in steps['Recover original Config archive'], 'published original-source readback guard drift')
    require('presence=(--require-present)' in steps['Check Config feed collisions'] and 'READBACK: ${{ needs.resolve-version.outputs.readback }}' in steps['Check Config feed collisions'], 'readback must require both feeds present')
    selection = steps['Select qualified Config archive']
    for expected in ['id: config-package', 'steps.config-recovery.outputs.path', 'steps.config-recovery.outputs.sha256', 'steps.config-packed.outputs.path', 'steps.config-packed.outputs.sha256', 'sha256sum "$package"']:
        require(expected in selection, 'verified archive selection drift: ' + expected)
    require('--logger "trx;LogFileName=config-tests.trx" --results-directory artifacts/config-evidence/config-tests' in steps['Test complete Config suite'], 'complete native Config test report retention missing')
    retain = steps['Retain Config archive and manifest']
    require('uses: actions/upload-artifact@v7' in retain and 'if-no-files-found: error' in retain and
            'artifacts/config-packages/FS.GG.Governance.Config.*.nupkg' in retain and 'artifacts/config-evidence/' in retain, 'pre-effect retention missing')
    require('archive.py collision --manifest artifacts/config-evidence/manifest.json' in steps['Check Config feed collisions'], 'both-feed collision observation missing')
    for name, source, key in [('Push Config to org feed', 'https://nuget.pkg.github.com/FS-GG/index.json', 'secrets.GITHUB_TOKEN'),
                              ('Verify same Config bytes and push public', 'https://api.nuget.org/v3/index.json', 'steps.config-nuget-login.outputs.NUGET_API_KEY')]:
        body = steps[name]
        for expected in ['package="${{ steps.config-package.outputs.path }}"',
                         'expected="${{ steps.config-package.outputs.sha256 }}"',
                         '[ "$(sha256sum "$package" | cut -d\' \' -f1)" = "$expected" ] || exit 1',
                         'dotnet nuget push "$package" --source ' + source, key]:
            require(expected in body, 'same-file publication drift: ' + name)
    require('id: config-nuget-login' in steps['Trusted Publishing Config login'], 'OIDC output identity drift')

def resolver_script(text):
    job = job_text(text, 'resolve-version')
    require(job.count('        run: |') == 1, 'one resolver script required')
    run = job.split('        run: |\n', 1)[1]
    lines = []
    for line in run.splitlines():
        if line.strip() and not line.startswith('          '):
            break
        lines.append(line)
    return textwrap.dedent('\n'.join(lines)) + '\n'

def resolver_controls(text):
    # SYNTHETIC project versions, no SDK/feed/effects. Execute the ACTUAL workflow script.
    script = resolver_script(text)
    cli = '1.1.0'
    config = '0.3.0'
    cases = [
        ('config-dry-run', 'workflow_dispatch', 'config', '', '', '', config, 'false', True, 'Config'),
        ('config-match', 'workflow_dispatch', 'config', config, '', '', config, 'true', True, 'Config'),
        ('config-prefixed-match', 'workflow_dispatch', 'config', 'v'+config, '', '', config, 'true', True, 'Config'),
        ('config-mismatch', 'workflow_dispatch', 'config', cli, '', '', config, '', False, 'Config'),
        ('config-whitespace-input', 'workflow_dispatch', 'config', ' ', '', '', config, '', False, 'Config'),
        ('config-invalid-input', 'workflow_dispatch', 'config', 'vNext', '', '', config, '', False, 'Config'),
        ('unknown-scope', 'workflow_dispatch', 'other', '', '', '', config, '', False, None),
        ('omitted-scope-dry-run', 'workflow_dispatch', '', '', '', '', cli, 'false', True, 'Cli'),
        ('all-dry-run', 'workflow_dispatch', 'all', '', '', '', cli, 'false', True, 'Cli'),
        ('all-match', 'workflow_dispatch', 'all', cli, '', '', cli, 'true', True, 'Cli'),
        ('all-mismatch', 'workflow_dispatch', 'all', config, '', '', cli, '', False, 'Cli'),
        ('push-tag-match', 'push', '', '', '', 'v'+cli, cli, 'true', True, 'Cli'),
        ('release-tag-match', 'release', '', '', 'v'+cli, '', cli, 'true', True, 'Cli'),
        ('push-tag-mismatch', 'push', '', '', '', 'v'+config, cli, '', False, 'Cli'),
        ('release-invalid-tag', 'release', '', '', 'vNext', '', cli, '', False, 'Cli'),
        ('tag-forced-config', 'push', 'config', '', '', 'v'+cli, cli, '', False, None),
        ('config-empty-project-version', 'workflow_dispatch', 'config', '', '', '', '', '', False, 'Config'),
        ('config-invalid-project-version', 'workflow_dispatch', 'config', '', '', '', 'not-a-version', '', False, 'Config'),
    ]
    with tempfile.TemporaryDirectory(prefix='config-resolver-') as directory:
        root = Path(directory)
        (root/'dotnet').write_text("""#!/bin/bash
printf '%s\\n' "$*" >> "$CALLS"
case "$2" in
  src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj) printf '%s\\n' "$CLI_VERSION" ;;
  src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj) printf '%s\\n' "$CONFIG_VERSION" ;;
  *) exit 91 ;;
esac
""")
        (root/'dotnet').chmod(0o700)
        (root/'resolver.sh').write_text(script)
        for name, event, scope, version, tag, ref, resolved, push, success, project in cases:
            output = root/'outputs'
            calls = root/'calls'
            output.write_text('')
            calls.write_text('')
            env = {'PATH': str(root)+':/usr/bin:/bin', 'LC_ALL': 'C', 'EVENT_NAME': event,
                   'INPUT_SCOPE': scope, 'INPUT_VERSION': version, 'RELEASE_TAG': tag, 'REF_NAME': ref,
                   'GITHUB_OUTPUT': str(output), 'CALLS': str(calls), 'CLI_VERSION': cli,
                   'CONFIG_VERSION': resolved if project == 'Config' else config,
                   'CONFIG_RUN_ID': '', 'CONFIG_ARTIFACT_ID': '', 'CONFIG_PACKAGE_SHA256': ''}
            result = subprocess.run(['bash', str(root/'resolver.sh')], env=env, text=True,
                                    capture_output=True, timeout=3)
            require((result.returncode == 0) == success, 'resolver outcome drift: '+name+' '+result.stdout+result.stderr)
            expected_calls = [f'msbuild src/FS.GG.Governance.{project}/FS.GG.Governance.{project}.fsproj -getProperty:Version'] if project else []
            require(calls.read_text().splitlines() == expected_calls, 'resolver evaluated wrong project: '+name)
            if success:
                require(output.read_text().splitlines() == ['scope='+('config' if scope == 'config' else 'all'),
                                                            'version='+resolved, 'push='+push, 'recovery=false', 'readback=false'], 'resolver output drift: '+name)
            else:
                require(output.read_text() == '', 'failed resolver emitted publish outputs: '+name)
        recovery_cases = [
            ('complete', 'workflow_dispatch', 'config', config, '12', '34', 'a'*64, True),
            ('missing-run', 'workflow_dispatch', 'config', config, '', '34', 'a'*64, False),
            ('missing-artifact', 'workflow_dispatch', 'config', config, '12', '', 'a'*64, False),
            ('missing-hash', 'workflow_dispatch', 'config', config, '12', '34', '', False),
            ('missing-version', 'workflow_dispatch', 'config', '', '12', '34', 'a'*64, False),
            ('wrong-scope', 'workflow_dispatch', 'all', config, '12', '34', 'a'*64, False),
            ('wrong-event', 'push', 'all', config, '12', '34', 'a'*64, False),
            ('wrong-version', 'workflow_dispatch', 'config', cli, '12', '34', 'a'*64, False),
            ('invalid-run', 'workflow_dispatch', 'config', config, '$(literal)', '34', 'a'*64, False),
            ('zero-artifact', 'workflow_dispatch', 'config', config, '12', '0', 'a'*64, False),
            ('invalid-hash', 'workflow_dispatch', 'config', config, '12', '34', 'A'*64, False),
        ]
        for name, event, scope, version, run, artifact, sha, success in recovery_cases:
            output.write_text(''); calls.write_text('')
            env = {'PATH': str(root)+':/usr/bin:/bin', 'LC_ALL': 'C', 'EVENT_NAME': event,
                   'INPUT_SCOPE': scope, 'INPUT_VERSION': version, 'RELEASE_TAG': '', 'REF_NAME': '',
                   'GITHUB_OUTPUT': str(output), 'CALLS': str(calls), 'CLI_VERSION': cli, 'CONFIG_VERSION': config,
                   'CONFIG_RUN_ID': run, 'CONFIG_ARTIFACT_ID': artifact, 'CONFIG_PACKAGE_SHA256': sha}
            actual = subprocess.run(['bash', str(root/'resolver.sh')], env=env, text=True, capture_output=True, timeout=3)
            require((actual.returncode == 0) == success, 'recovery resolver outcome drift: ' + name)
            require(output.read_text().splitlines() == (['scope=config', 'version='+config, 'push=true', 'recovery=true', 'readback=false'] if success else []),
                    'recovery resolver outputs drift: ' + name)
            expected_calls = ['msbuild src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj -getProperty:Version'] if name in ['complete', 'wrong-version'] else []
            require(calls.read_text().splitlines() == expected_calls, 'recovery resolver launched work before selection validation: ' + name)
        readback_cases = [
            ('original', 'config', config, 'true', 'a'*40, '12', True),
            ('historic-version', 'config', '0.2.0', 'true', 'a'*40, '12', True),
            ('wrong-scope', 'all', config, 'true', 'a'*40, '12', False),
            ('missing-version', 'config', '', 'true', 'a'*40, '12', False),
            ('missing-run', 'config', config, 'true', 'a'*40, '', False),
            ('invalid-mode', 'config', config, 'maybe', 'a'*40, '12', False),
        ]
        for name, scope, version, readback, source, run, success in readback_cases:
            output.write_text(''); calls.write_text('')
            env = {'PATH': str(root)+':/usr/bin:/bin', 'LC_ALL': 'C', 'EVENT_NAME': 'workflow_dispatch',
                   'INPUT_SCOPE': scope, 'INPUT_VERSION': version, 'RELEASE_TAG': '', 'REF_NAME': '',
                   'GITHUB_OUTPUT': str(output), 'CALLS': str(calls), 'CLI_VERSION': cli, 'CONFIG_VERSION': config,
                   'CONFIG_RUN_ID': run, 'CONFIG_ARTIFACT_ID': '34', 'CONFIG_PACKAGE_SHA256': 'b'*64,
                   'CONFIG_READBACK': readback}
            actual = subprocess.run(['bash', str(root/'resolver.sh')], env=env, text=True, capture_output=True, timeout=3)
            require((actual.returncode == 0) == success, 'readback resolver outcome drift: ' + name)
            require(output.read_text().splitlines() == (['scope=config', 'version='+version, 'push=false', 'recovery=true', 'readback=true'] if success else []),
                    'readback selected a publication effect: ' + name)
            require(calls.read_text() == '', 'readback invoked current project/build/pack: ' + name)
    print(f'Actual resolver controls passed: {len(cases) + len(recovery_cases) + len(readback_cases)}')



def push_byte_controls(text):
    # Execute the actual hash/effect scripts with a fake dotnet that records no network effects.
    job = job_text(text)
    def script(name):
        body = re.search(r'^      - name: ' + re.escape(name) + r'\n.*?(?=^      - name:|\Z)', job, re.M | re.S).group()
        return textwrap.dedent(body.split('        run: |\n', 1)[1])
    with tempfile.TemporaryDirectory(prefix='config-feed-hash-') as directory:
        root = Path(directory); package = root/'selected.nupkg'; calls = root/'calls'
        package.write_bytes(b'synthetic original archive'); calls.write_text('')
        import hashlib
        sha = hashlib.sha256(package.read_bytes()).hexdigest()
        (root/'dotnet').write_text('#!/bin/bash\nprintf "%s\\n" "$*" >> "$CALLS"\n')
        (root/'dotnet').chmod(0o700)
        env = {'PATH': str(root)+':/usr/bin:/bin', 'CALLS': str(calls)}
        for name, success in [('Push Config to org feed', True), ('Verify same Config bytes and push public', False)]:
            body = script(name).replace('${{ steps.config-package.outputs.path }}', str(package)).replace('${{ steps.config-package.outputs.sha256 }}', sha)
            body = body.replace('${{ secrets.GITHUB_TOKEN }}', 'synthetic').replace('${{ steps.config-nuget-login.outputs.NUGET_API_KEY }}', 'synthetic')
            result = subprocess.run(['bash', '-c', body], env=env, text=True, capture_output=True, timeout=3)
            require((result.returncode == 0) == success, 'actual between-feed hash guard drift: ' + name)
            require(len(calls.read_text().splitlines()) == 1, 'changed archive reached public effect')
            package.write_bytes(b'changed between feed effects')
    print('Actual between-feed hash control passed: public effect refused after original-file change')

def mutations(text):
    job = job_text(text)
    smoke = re.search(r'^      - name: Config package consumer smoke\n.*?(?=^      - name:)', job, re.M | re.S).group()
    packs = re.search(r'^      - name: Pack Config once\n.*?(?=^      - name:)', job, re.M | re.S).group()
    org_push = re.search(r'^      - name: Push Config to org feed\n.*?(?=^      - name:)', job, re.M | re.S).group()
    identify = re.search(r'^      - name: Identify exact Config archive\n.*?(?=^      - name:)', job, re.M | re.S).group()
    cases = {
        'remove-smoke': job.replace(smoke, ''),
        'readback-publication-proof': job.replace('publication=(--published)', 'publication=()'),
        'readback-absence-accepted': job.replace('presence=(--require-present)', 'presence=()'),
        'readback-current-source': job.replace('--revision "$EXPECTED_SOURCE"', '--revision "$GITHUB_SHA"'),
        'remove-public-hash-guard': job.replace('[ "$(sha256sum "$package" | cut -d\' \' -f1)" = "$expected" ] || exit 1', 'true', 2),
        'remove-dry-run-visibility': job.replace(VISIBILITY, GATE),
        'recovery-pack': job.replace(packs, packs.replace(FIRST, 'always()')),
        'recovery-consumer': job.replace(smoke, smoke.replace(FIRST, 'always()')),
        'remove-original-verifier': job.replace('archive.py recover', 'archive.py prepare'),
        'recovery-first-run-retention': job.replace('archive.py retain', 'true # archive.py omitted'),
        'dry-run-push': job.replace(org_push, org_push.replace(GATE, 'always()')),
        'second-pack': job.replace('      - name: Identify exact Config archive', packs + '      - name: Identify exact Config archive'),
        'swap-package-path': job.replace('package="${{ steps.config-package.outputs.path }}"', 'package="other.nupkg"', 1),
        'broken-order': job.replace(identify + smoke, smoke + identify),
        'ambiguous-output': job.replace('[ "${#packages[@]}" -eq 1 ]', '[ "${#packages[@]}" -ge 1 ]'),
        'remove-retention': job.replace(re.search(r'^      - name: Retain Config archive and manifest\n.*?(?=^      - name:)', job, re.M | re.S).group(), ''),
        'remove-collision': job.replace(re.search(r'^      - name: Check Config feed collisions\n.*?(?=^      - name:)', job, re.M | re.S).group(), ''),
        'change-file-hash': job.replace('expected="${{ steps.config-package.outputs.sha256 }}"', 'expected="replacement"', 1),
        'ignored-smoke-failure': job.replace('      - name: Config package consumer smoke\n', '      - name: Config package consumer smoke\n        continue-on-error: true\n')
    }
    whole_cases = {name: text.replace(job, mutated) for name, mutated in cases.items()}
    for name in UNRELATED:
        body = job_text(text, name)
        whole_cases['unguarded-'+name] = text.replace(body, body.replace(ALL_SCOPE, REPOSITORY, 1))
    resolver = job_text(text, 'resolve-version')
    whole_cases['config-routed-to-cli'] = text.replace(resolver, resolver.replace('project="src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj"', 'project="src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj"'))
    whole_cases['resolver-readback-push'] = text.replace(resolver, resolver.replace("if [ \"$readback\" = 'true' ]; then push=\"false\"; fi", "if [ \"$readback\" = 'true' ]; then push=\"true\"; fi"))
    whole_cases['resolver-dry-run-push'] = text.replace(resolver, resolver.replace('push="false"', 'push="true"'))
    for name, mutated in whole_cases.items():
        try:
            validate(mutated)
            resolver_controls(mutated)
        except ValueError:
            print('refused mutation: ' + name)
        else:
            raise ValueError('mutation escaped preflight: ' + name)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('workflow', type=Path)
    parser.add_argument('--mutations', action='store_true')
    args = parser.parse_args()
    try:
        text = args.workflow.read_text()
        validate(text)
        resolver_controls(text)
        push_byte_controls(text)
        if args.mutations:
            mutations(text)
        print('Config publication static contract passed')
    except (ValueError, OSError, subprocess.TimeoutExpired) as error:
        sys.exit('Config publication refused: ' + str(error))
