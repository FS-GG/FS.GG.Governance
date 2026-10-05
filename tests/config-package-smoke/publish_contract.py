#!/usr/bin/env python3
"""Cheap static contract for the actual publish-config job's supported YAML shape.

This deliberately bounded projection is not a general YAML/expression evaluator.
Unknown step controls/conditions refuse. Independent required ordering prevents
failed qualification or ambiguous package output from reaching a publication leg.
"""
import argparse
from pathlib import Path
import re
import sys

GATE = "needs.resolve-version.outputs.push == 'true'"
ORDER = ['Checkout', 'Preflight Config publication contract', 'Set up .NET',
         'Restore (locked, cold)', 'Test complete Config suite', 'Resolve Config package version',
         'Pack Config once', 'Identify exact Config archive', 'Config package consumer smoke',
         'Retain Config archive and manifest', 'Check Config feed collisions',
         'Retain Config feed observations', 'Push Config to org feed',
         'Trusted Publishing Config login', 'Verify same Config bytes and push public']

def require(value, message):
    if not value:
        raise ValueError(message)

def job_text(text):
    matches = list(re.finditer(r'^  publish-config:\s*$', text, re.M))
    require(len(matches) == 1, 'expected exactly one publish-config job')
    start = matches[0].end()
    end = re.search(r'^  [a-z][a-z0-9-]*:\s*$', text[start:], re.M)
    return text[start:start + end.start()] if end else text[start:]

def validate(text):
    job = job_text(text)
    header = job.split('    steps:', 1)[0]
    require('    needs: [resolve-version]\n' in header, 'Config must depend only on resolve-version')
    require("    if: github.repository == 'FS-GG/FS.GG.Governance'\n" in header, 'repository guard drift')
    require('      contents: read\n      packages: write\n      id-token: write\n' in header, 'permission drift')
    starts = list(re.finditer(r'^      - name: (.+)$', job, re.M))
    require([m.group(1) for m in starts] == ORDER, 'required step/order drift')
    steps = {m.group(1): job[m.end():starts[i+1].start() if i+1 < len(starts) else len(job)]
             for i, m in enumerate(starts)}
    for name, body in steps.items():
        require(not re.search(r'^        (continue-on-error|timeout-minutes):', body, re.M), 'unsupported step control: ' + name)
        condition = re.findall(r'^        if: (.+)$', body, re.M)
        expected = [GATE] if name in ['Check Config feed collisions', 'Push Config to org feed',
                                      'Trusted Publishing Config login', 'Verify same Config bytes and push public'] else []
        if name == 'Retain Config feed observations':
            expected = ["always() && " + GATE]
        require(condition == expected, 'unsupported/missing condition: ' + name)
    require(job.count('dotnet pack ') == 1, 'Config must pack exactly once')
    require(job.count('dotnet nuget push ') == 2 and job.count('NuGet/login@v1') == 1, 'unexpected publication effect')
    require('python3 tests/config-package-smoke/publish_contract.py .github/workflows/publish.yml --mutations' in steps[ORDER[1]], 'preflight missing before costly work')
    require('uses: ./.github/actions/locked-restore' in steps[ORDER[3]] and
            'target: tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj' in steps[ORDER[3]], 'cold locked test restore missing')
    require('run: dotnet test tests/FS.GG.Governance.Config.Tests/FS.GG.Governance.Config.Tests.fsproj -c Release --no-restore' in steps[ORDER[4]] and
            '--filter' not in steps[ORDER[4]], 'whole Config suite required')
    require('src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj -getProperty:Version' in steps[ORDER[5]], 'Config evaluated version missing')
    require('needs.resolve-version.outputs.version' not in job, 'CLI version must not control Config package')
    require('src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj -c Release' in steps[ORDER[6]] and
            '-p:Version=${{ steps.config-version.outputs.version }} --no-restore -o artifacts/config-packages' in steps[ORDER[6]], 'pack project/version/output drift')
    identify = steps[ORDER[7]]
    for expected in ['id: config-package', 'packages=(artifacts/config-packages/FS.GG.Governance.Config.*.nupkg)',
                     '[ "${#packages[@]}" -eq 1 ]', 'package="${packages[0]}"', 'sha256sum "$package"']:
        require(expected in identify, 'exact archive identification missing: ' + expected)
    smoke = steps[ORDER[8]]
    for expected in ['steps.config-version.outputs.version', 'steps.config-package.outputs.path',
                     'steps.config-package.outputs.sha256', 'bash tests/config-package-smoke/run.sh',
                     '"$CONFIG_PACKAGE" "$CONFIG_VERSION" "$CONFIG_SHA256" "$GITHUB_SHA" artifacts/config-evidence']:
        require(expected in smoke, 'package smoke input drift: ' + expected)
    retain = steps[ORDER[9]]
    require('uses: actions/upload-artifact@v7' in retain and 'if-no-files-found: error' in retain and
            'artifacts/config-packages/FS.GG.Governance.Config.*.nupkg' in retain and 'artifacts/config-evidence/' in retain, 'pre-effect retention missing')
    require('archive.py collision --manifest artifacts/config-evidence/manifest.json' in steps[ORDER[10]], 'both-feed collision observation missing')
    for name, source, key in [('Push Config to org feed', 'https://nuget.pkg.github.com/FS-GG/index.json', 'secrets.GITHUB_TOKEN'),
                              ('Verify same Config bytes and push public', 'https://api.nuget.org/v3/index.json', 'steps.config-nuget-login.outputs.NUGET_API_KEY')]:
        body = steps[name]
        for expected in ['package="${{ steps.config-package.outputs.path }}"',
                         'expected="${{ steps.config-package.outputs.sha256 }}"',
                         '[ "$(sha256sum "$package" | cut -d\' \' -f1)" = "$expected" ] || exit 1',
                         'dotnet nuget push "$package" --source ' + source, key]:
            require(expected in body, 'same-file publication drift: ' + name)
    require('id: config-nuget-login' in steps[ORDER[13]], 'OIDC output identity drift')

def mutations(text):
    job = job_text(text)
    smoke = re.search(r'^      - name: Config package consumer smoke\n.*?(?=^      - name:)', job, re.M | re.S).group()
    packs = re.search(r'^      - name: Pack Config once\n.*?(?=^      - name:)', job, re.M | re.S).group()
    org_push = re.search(r'^      - name: Push Config to org feed\n.*?(?=^      - name:)', job, re.M | re.S).group()
    identify = re.search(r'^      - name: Identify exact Config archive\n.*?(?=^      - name:)', job, re.M | re.S).group()
    cases = {
        'remove-smoke': job.replace(smoke, ''),
        'dry-run-push': job.replace(org_push, org_push.replace('        if: ' + GATE, '        if: always()')),
        'second-pack': job.replace('      - name: Identify exact Config archive', packs + '      - name: Identify exact Config archive'),
        'swap-package-path': job.replace('package="${{ steps.config-package.outputs.path }}"', 'package="other.nupkg"', 1),
        'broken-order': job.replace(identify + smoke, smoke + identify),
        'ambiguous-output': job.replace('[ "${#packages[@]}" -eq 1 ]', '[ "${#packages[@]}" -ge 1 ]'),
        'ignored-smoke-failure': job.replace('      - name: Config package consumer smoke\n', '      - name: Config package consumer smoke\n        continue-on-error: true\n')
    }
    for name, mutated in cases.items():
        try:
            validate(text.replace(job, mutated))
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
        if args.mutations:
            mutations(text)
        print('Config publication static contract passed')
    except (ValueError, OSError) as error:
        sys.exit('Config publication refused: ' + str(error))
