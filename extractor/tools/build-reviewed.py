"""Publish all four projects without touching the original release/evidence."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess

root = Path(__file__).resolve().parents[1]
out = root / 'release-reviewed'
evidence = root / 'artifacts' / 'reviewed'
out.mkdir(exist_ok=True)
evidence.mkdir(parents=True, exist_ok=True)
summary = []
for project in ['UI', 'Service', 'Worker', 'Tests']:
    command = ['dotnet', 'publish', str(root / project / (project + '.csproj')),
               '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
               '-p:PublishSingleFile=false', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', str(out)]
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (evidence / ('publish-' + project + '.log')).write_bytes(result.stdout)
    print(result.stdout.decode('utf-8', errors='replace'))
    summary.append({'project': project, 'exit': result.returncode})
    if result.returncode:
        raise SystemExit(result.returncode)
for name in ['README.md', 'config.example.json']:
    shutil.copy2(root / name, out / name)
shutil.copytree(root / 'docs', out / 'docs', dirs_exist_ok=True)
(evidence / 'publish-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
files = [{'name': str(p.relative_to(out)), 'bytes': p.stat().st_size,
          'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
         for p in out.rglob('*') if p.is_file() and 'selftest-data' not in p.parts]
(evidence / 'published-files.json').write_text(json.dumps(files, indent=2), encoding='utf-8')
print('Published reviewed executables:', out)
