"""Real first-run startup, duplicate rejection, timer close, state read-back."""
import pathlib, subprocess, tempfile, time, json, sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
exe=pathlib.Path(sys.argv[1]).resolve() if len(sys.argv)>1 else ROOT/'FlexTimeChecker/bin/Release/net8.0-windows/FlexTimeChecker.exe'
with tempfile.TemporaryDirectory(prefix='startup-',dir=ROOT/'test-logs') as directory:
    data=pathlib.Path(directory)
    p=subprocess.Popen([str(exe),'--data-dir',str(data),'--smoke-seconds','3'])
    try:
        time.sleep(1)
        assert p.poll() is None, f'normal UI exited early: {p.returncode}'
        other=subprocess.run([str(exe),'--data-dir',str(data),'--instance-probe'],timeout=10)
        assert other.returncode==10, f'single instance failed: {other.returncode}'
        assert p.wait(timeout=15)==0, 'normal UI smoke exit not zero'
        assert json.loads((data/'rework.json').read_text())['SchemaVersion']==1
        print('PASS isolated fresh normal UI startup + actual duplicate process rejection + graceful timer close + newly written state read-back')
    finally:
        if p.poll() is None: p.kill(); p.wait()

