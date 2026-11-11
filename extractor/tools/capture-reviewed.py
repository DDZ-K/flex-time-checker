"""Capture only the reviewed GUI PID, without global mouse input."""
from pathlib import Path
import importlib.util
import subprocess
import time

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('owned_capture', root / 'tools' / 'capture-ui.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
p = subprocess.Popen([str(root / 'release-reviewed' / 'FlexRequestExtractor.exe')])
try:
    main = m.find(p.pid, 'Flex 报文提取')
    time.sleep(1)
    m.capture(main, 'reviewed/ui-main-light.png')
    m.click(p.pid, main, '深色模式')
    m.capture(main, 'reviewed/ui-main-dark.png')
    m.u.PostMessageW(main, 0x10, 0, 0)
    p.wait(timeout=8)
finally:
    if p.poll() is None:
        p.kill()
        p.wait()
