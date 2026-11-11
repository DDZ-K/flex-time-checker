"""Hold a real non-delete-shared source directory through soak's first outage."""
import ctypes as c
from ctypes import wintypes as w
from pathlib import Path
import subprocess
import sys
import time

root = Path(__file__).resolve().parents[1]
variant = sys.argv[1]
assert variant in ('baseline', 'reviewed')
permanent = '--permanent' in sys.argv[2:]
release = root / ('release' if variant == 'baseline' else 'release-reviewed')
evidence = root / 'artifacts' / 'reviewed' / ('soak-forced-sharing-' + variant + ('-permanent' if permanent else '') + '.log')
data = release / 'selftest-data'
data.mkdir(exist_ok=True)
before = set(data.glob('soak-*'))
k = c.WinDLL('kernel32', use_last_error=True)
k.CreateFileW.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, c.c_void_p, w.DWORD, w.DWORD, w.HANDLE]
k.CreateFileW.restype = w.HANDLE
k.CloseHandle.argtypes = [w.HANDLE]
with evidence.open('wb') as log:
    p = subprocess.Popen([str(release / 'FlexExtractor.Tests.exe'), '--soak', '26'], stdout=log, stderr=subprocess.STDOUT)
    handle = None
    try:
        deadline = time.monotonic() + 8
        watch = None
        while time.monotonic() < deadline:
            watch = next((d / 'watch' for d in data.glob('soak-*') if d not in before and (d / 'watch').is_dir()), None)
            if watch is not None:
                break
            time.sleep(.02)
        assert watch is not None, 'soak did not create source'
        handle = k.CreateFileW(str(watch), 0x80000000, 3, None, 3, 0x02000000, None)
        assert handle not in (None, c.c_void_p(-1).value), c.get_last_error()
        print('Held GENERIC_READ directory handle (no FILE_SHARE_DELETE):', watch)
        time.sleep(24 if permanent else 17.5)
        k.CloseHandle(handle)
        handle = None
        result = p.wait(timeout=25)
    finally:
        if handle is not None:
            k.CloseHandle(handle)
        if p.poll() is None:
            p.kill()
            p.wait()
print('variant=', variant, 'exit=', result, 'evidence=', evidence)
text = evidence.read_text(encoding='utf-8')
print('\n'.join(line for line in text.splitlines() if line.startswith(('SOAK OFFLINE', 'SOAK RESTORED', 'SOAK RECOVERED', 'SOAK RESULT', 'SOAK FAIL'))))
assert (result != 0 and 'MoveDirectory' in text and 'SOAK RESULT' not in text) if (variant == 'baseline' or permanent) else (result == 0 and 'SOAK RECOVERED' in text)
