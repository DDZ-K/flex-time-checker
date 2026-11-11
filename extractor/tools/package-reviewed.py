"""Package reviewed source/runtime without configs, test data or old binaries."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
packages = root / 'packages'
packages.mkdir(exist_ok=True)
source = packages / 'FlexRequestExtractor-v2.0.0-reviewed-source.zip'
runtime = packages / 'FlexRequestExtractor-v2.0.0-reviewed-win-x64.zip'
release = root / 'release-reviewed'
shutil.copy2(root / 'README.md', release / 'README.md')
shutil.copytree(root / 'docs', release / 'docs', dirs_exist_ok=True)
source_dirs = ['Core', 'UI', 'Service', 'Worker', 'Tests', 'docs', 'tools']
source_files = [p for d in source_dirs for p in (root / d).rglob('*')
                if p.is_file() and not {'bin', 'obj', '__pycache__', 'selftest-data'}.intersection(p.relative_to(root).parts)]
source_files += [p for p in root.iterdir() if p.is_file() and p.name != 'config.json']
source_files += [p for p in (root / 'artifacts' / 'reviewed').rglob('*')
                 if p.is_file() and p.name != 'package-hashes.json']
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in sorted(set(source_files)):
        z.write(p, 'extractor/' + p.relative_to(root).as_posix())
with zipfile.ZipFile(runtime, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in sorted(release.rglob('*')):
        rel = p.relative_to(release)
        if (not p.is_file() or 'selftest-data' in rel.parts or p.name == 'config.json'
                or p.suffix in ('.log', '.pdb') or p.name.startswith('selftest-')):
            continue
        z.write(p, 'FlexRequestExtractor-reviewed/' + rel.as_posix())
summary = [{'path': str(p), 'bytes': p.stat().st_size,
            'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in [source, runtime]]
(root / 'artifacts' / 'reviewed' / 'package-hashes.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
for item in summary:
    print(item)
