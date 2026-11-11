"""Hold the kernel file-share lock without owning a session-local mutex."""
from pathlib import Path
import ctypes, ctypes.wintypes as w, subprocess, sys, tempfile, time
ROOT=Path(__file__).resolve().parents[1]
exe=Path(sys.argv[1]).resolve()
k=ctypes.WinDLL('kernel32',use_last_error=True)
k.CreateFileW.argtypes=[w.LPCWSTR,w.DWORD,w.DWORD,w.LPVOID,w.DWORD,w.DWORD,w.HANDLE]
k.CreateFileW.restype=w.HANDLE
k.CloseHandle.argtypes=[w.HANDLE]
with tempfile.TemporaryDirectory(prefix='file-lock-',dir=ROOT/'test-logs') as directory:
    data=Path(directory)
    handle=k.CreateFileW(str(data/'.instance.lock'),0xC0000000,0,None,4,0x80,None)
    assert handle!=w.HANDLE(-1).value, ctypes.WinError(ctypes.get_last_error())
    try:
        result=subprocess.run([str(exe),'--data-dir',str(data),'--instance-probe','--smoke-seconds','2'],timeout=20)
        assert result.returncode==10, f'shared data-dir kernel file lock must refuse independent holder, got {result.returncode}'
        assert not (data/'rework.json').exists(), 'contender wrote persistent state despite file lock'
    finally:
        k.CloseHandle(handle)
    first=subprocess.Popen([str(exe),'--data-dir',str(data),'--smoke-seconds','4'])
    try:
        time.sleep(1)
        assert first.poll() is None, 'instance did not acquire released file lock'
        second=subprocess.run([str(exe),'--data-dir',str(data/'.'/'..'/data.name),'--instance-probe','--smoke-seconds','2'],timeout=20)
        assert second.returncode==10, f'canonical same-directory contender returned {second.returncode}'
        assert first.wait(timeout=20)==0, 'graceful file-lock owner exit failed'
    finally:
        if first.poll() is None: first.kill(); first.wait()
    final=subprocess.run([str(exe),'--data-dir',str(data),'--instance-probe','--smoke-seconds','1'],timeout=20)
    assert final.returncode==0, 'file lock not released after owner exit'
    print('PASS independent kernel file lock rejects startup before state write; canonical alias contention; release/reacquire after graceful exit')
