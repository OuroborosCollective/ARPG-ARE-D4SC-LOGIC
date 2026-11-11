"""Compile C# proposals in isolation; never execute their generated assemblies."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile


def run_bounded(command, timeout):
    """Collect a real subprocess outcome, distinguishing timeout from compiler rejection."""
    try:
        result = subprocess.run(command, capture_output=True, text=True,
                                encoding='utf-8', errors='replace', timeout=timeout)
        return {'status': 'completed', 'exit_code': result.returncode,
                'log': result.stdout + result.stderr}
    except subprocess.TimeoutExpired as error:
        def decode(value):
            """TimeoutExpired may return bytes even when text mode was requested."""
            return value.decode('utf-8', errors='replace') if isinstance(value, bytes) else (value or '')
        return {'status': 'timeout', 'exit_code': None,
                'log': decode(error.stdout) + decode(error.stderr) + '\nCOMPILER_TIMEOUT\n'}


def main():
    """Audit each source independently and persist all completed/timeout outcomes as UTF-8."""
    parser = argparse.ArgumentParser()
    parser.add_argument('--audit', required=True, type=Path)
    parser.add_argument('--dotnet-root', required=True, type=Path)
    args = parser.parse_args()
    audit = args.audit.resolve()
    manifest = json.loads((audit/'manifest.json').read_text(encoding='utf-8'))
    sdk = args.dotnet_root.resolve()
    compilers = sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
    references = sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0/*.dll'))
    if len(compilers) != 1 or not references:
        raise ValueError('Provide one pinned SDK with .NET8 reference assemblies')
    results = {}
    with tempfile.TemporaryDirectory(prefix='ashen-candidate-') as directory:
        temporary = Path(directory)
        for record in manifest['records']:
            sha = record['sha256']
            if sha in results:
                continue
            if record['filename'] not in ['csharp', 'cs', 'c#'] and not (record['filename'] or '').endswith('.cs'):
                continue
            if not re.fullmatch('[0-9a-f]{64}', sha) or record['extracted_file'] != sha + '.txt':
                raise ValueError('Invalid content-addressed source path')
            source = temporary/(sha + '.cs')
            source.write_bytes((audit/record['extracted_file']).read_bytes())
            command = [str(sdk/'dotnet'), str(compilers[0]), '/nologo', '/target:library', '/langversion:9',
                       '/out:' + str(temporary/(sha + '.dll'))]
            command += ['/reference:' + str(reference) for reference in references] + [str(source)]
            outcome = run_bounded(command, 25)
            (audit/(sha + '.compile.log')).write_text(outcome['log'], encoding='utf-8')
            results[sha] = {key: outcome[key] for key in ['status', 'exit_code']}
            results[sha].update(environment='isolated .NET8 / C#9; no Unity or dependency stubs',
                                diagnostics=outcome['log'].splitlines()[:8])
    for record in manifest['records']:
        outcome = results.get(record['sha256'])
        if outcome:
            record['compilation'] = outcome
            if outcome['status'] == 'completed':
                record['classification'] = 'ausführbarer Quellcode' if outcome['exit_code'] == 0 else 'unvollständiger Quellcode'
            # A timeout is an unverified compilation boundary, never a source rejection.
        if record['filename'] == 'json':
            try:
                json.loads((audit/record['extracted_file']).read_text(encoding='utf-8'))
                record['classification'] = 'Datenmodell'
            except ValueError:
                record['classification'] = 'Pseudocode'
    for name, value in [('manifest.json', manifest), ('compilation-summary.json', results)]:
        (audit/name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('COMPILED', len(results), 'PASS', sum(r['exit_code'] == 0 for r in results.values()),
          'TIMEOUT', sum(r['status'] == 'timeout' for r in results.values()))


if __name__ == '__main__':
    main()
