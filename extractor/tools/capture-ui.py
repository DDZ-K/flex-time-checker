"""Capture only the exact published process's own windows."""
import ctypes as C
from ctypes import wintypes as W
from pathlib import Path
import subprocess
import time
from PIL import Image
ROOT = Path(__file__).resolve().parents[1]
u, g = C.windll.user32, C.windll.gdi32
u.SetProcessDPIAware()
CB = C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
u.EnumWindows.argtypes = [CB, W.LPARAM]
u.EnumChildWindows.argtypes = [W.HWND, CB, W.LPARAM]
u.GetWindowThreadProcessId.argtypes = [W.HWND, C.POINTER(W.DWORD)]
u.GetWindowTextW.argtypes = [W.HWND, W.LPWSTR, C.c_int]
u.GetWindowRect.argtypes = [W.HWND, C.POINTER(W.RECT)]
u.GetDC.argtypes = [W.HWND]; u.GetDC.restype = W.HDC
u.ReleaseDC.argtypes = [W.HWND,W.HDC]
u.PrintWindow.argtypes = [W.HWND,W.HDC,W.UINT]
u.SendMessageW.argtypes = [W.HWND,W.UINT,W.WPARAM,W.LPARAM];u.SendMessageW.restype=W.LPARAM
u.PostMessageW.argtypes=[W.HWND,W.UINT,W.WPARAM,W.LPARAM]
g.CreateCompatibleDC.argtypes=[W.HDC];g.CreateCompatibleDC.restype=W.HDC
g.CreateCompatibleBitmap.argtypes=[W.HDC,C.c_int,C.c_int];g.CreateCompatibleBitmap.restype=W.HBITMAP
g.SelectObject.argtypes=[W.HDC,W.HGDIOBJ];g.SelectObject.restype=W.HGDIOBJ
g.DeleteObject.argtypes=[W.HGDIOBJ];g.DeleteDC.argtypes=[W.HDC]
class BMI(C.Structure):
    _fields_=[('size',W.DWORD),('width',W.LONG),('height',W.LONG),('planes',W.WORD),('bits',W.WORD),('compression',W.DWORD),('imageSize',W.DWORD),('xppm',W.LONG),('yppm',W.LONG),('colors',W.DWORD),('important',W.DWORD)]
g.GetDIBits.argtypes=[W.HDC,W.HBITMAP,W.UINT,W.UINT,W.LPVOID,C.POINTER(BMI),W.UINT]
def title(h):
    b=C.create_unicode_buffer(512);u.GetWindowTextW(h,b,512);return b.value
def windows(pid,parent=None):
    result=[]
    @CB
    def visit(h,l):
        p=W.DWORD();u.GetWindowThreadProcessId(h,C.byref(p))
        if p.value==pid:result.append((h,title(h)))
        return True
    (u.EnumWindows(visit,0) if parent is None else u.EnumChildWindows(parent,visit,0))
    return result
def find(pid,text):
    for _ in range(50):
        hits=[h for h,t in windows(pid) if text in t]
        if hits:return hits[0]
        time.sleep(.1)
    raise RuntimeError('window missing '+text)
def click(pid,parent,text):
    hits=[h for h,t in windows(pid,parent) if t==text]
    if not hits:
        positions={'深色模式':(765,100),'浅色模式':(765,100),'配置':(100,100),'新增':(980,176),'时序与资源上限':(200,66),'目录与站位':(70,66)}
        if text not in positions:raise RuntimeError('control missing '+text)
        rect=W.RECT();u.GetWindowRect(parent,C.byref(rect));x,y=positions[text];sx,sy=rect.left+x,rect.top+y
        candidates=[]
        for child,_ in windows(pid,parent):
            cr=W.RECT();u.GetWindowRect(child,C.byref(cr))
            if cr.left<=sx<cr.right and cr.top<=sy<cr.bottom:candidates.append(((cr.right-cr.left)*(cr.bottom-cr.top),child,cr))
        if not candidates:raise RuntimeError('point outside child windows '+text)
        _,target,cr=min(candidates,key=lambda z:z[0]);h=target;point=((sy-cr.top)<<16)|(sx-cr.left)
    else:h=hits[0];point=(12<<16)|12
    u.PostMessageW(h,0x201,1,point);u.PostMessageW(h,0x202,0,point);time.sleep(.5)
def capture(hwnd,name):
    rect=W.RECT();u.GetWindowRect(hwnd,C.byref(rect));w,h=rect.right-rect.left,rect.bottom-rect.top
    dc=u.GetDC(hwnd);mem=g.CreateCompatibleDC(dc);bmp=g.CreateCompatibleBitmap(dc,w,h);old=g.SelectObject(mem,bmp)
    try:
        if not u.PrintWindow(hwnd,mem,2):raise RuntimeError('PrintWindow failed')
        bmi=BMI();bmi.size=C.sizeof(BMI);bmi.width=w;bmi.height=-h;bmi.planes=1;bmi.bits=32
        buf=C.create_string_buffer(w*h*4)
        if not g.GetDIBits(mem,bmp,0,h,buf,C.byref(bmi),0):raise RuntimeError('GetDIBits failed')
        path=ROOT/'artifacts'/name;Image.frombuffer('RGB',(w,h),buf,'raw','BGRX',0,1).save(path);print(path,w,h)
    finally:g.SelectObject(mem,old);g.DeleteObject(bmp);g.DeleteDC(mem);u.ReleaseDC(hwnd,dc)
if __name__=='__main__':
    p=subprocess.Popen([str(ROOT/'publish/FlexRequestExtractor.exe')])
    try:
        main=find(p.pid,'Flex 报文提取');time.sleep(1);capture(main,'ui-main-light.png')
        click(p.pid,main,'深色模式');capture(main,'ui-main-dark.png')
        click(p.pid,main,'浅色模式');click(p.pid,main,'配置')
        config=find(p.pid,'提取配置');capture(config,'ui-config.png')
        click(p.pid,config,'时序与资源上限');capture(config,'ui-advanced.png');click(p.pid,config,'目录与站位')
        click(p.pid,config,'新增');station=find(p.pid,'站位与子目录');capture(station,'ui-station.png')
        u.PostMessageW(station,0x10,0,0);time.sleep(.3);u.PostMessageW(config,0x10,0,0);time.sleep(.3);u.PostMessageW(main,0x10,0,0);p.wait(timeout=8)
    finally:
        if p.poll() is None:p.kill();p.wait()
