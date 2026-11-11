"""Real normal-startup regression: missing config with prior rework is recovery, not empty live config."""
from pathlib import Path
import subprocess, sys, tempfile
ROOT=Path(__file__).resolve().parents[1]
exe=Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix='missing-config-',dir=ROOT/'test-logs') as directory:
    data=Path(directory)
    state=data/'rework.json'
    original='{"SchemaVersion":1,"HousingsByStation":{"OLD":["H"]}}'
    state.write_text(original,encoding='utf-8')
    result=subprocess.run([str(exe),'--data-dir',str(data),'--smoke-seconds','3'],timeout=20)
    assert result.returncode==0, f'normal startup exit {result.returncode}'
    assert not (data/'jobs').exists(), 'missing configuration with rework must pause worker statistics in recovery mode'
    assert state.read_text(encoding='utf-8')==original, 'existing state changed during missing config recovery'
    assert not (data/'config.json').exists(), 'missing configuration must not be automatically written'
    print('PASS actual normal startup missing config enters recovery; no worker, state byte-preserved, graceful close')
