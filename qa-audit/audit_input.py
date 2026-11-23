import ctypes
import os
import time

def key(name):
    code = {"ESC":27,"ESCAPE":27,"TAB":9,"SPACE":32,"ENTER":13,"RETURN":13,"F1":112,"F2":113,"UP":38,"DOWN":40,"LEFT":37,"RIGHT":39}.get(name.upper())
    if code is None:
        code = ord(name.upper())
    user32 = ctypes.windll.user32
    target_pid = int(os.environ["AUDIT_PLAYER_PID"])
    callback_type = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    @callback_type
    def select(hwnd, _):
        pid = ctypes.c_ulong()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        if pid.value == target_pid and user32.IsWindowVisible(hwnd):
            user32.ShowWindow(hwnd, 5)
            user32.SetForegroundWindow(hwnd)
            return False
        return True
    user32.EnumWindows(select, 0)
    user32.keybd_event(code, 0, 0, 0)
    time.sleep(.22)
    user32.keybd_event(code, 0, 2, 0)
    time.sleep(.65)
