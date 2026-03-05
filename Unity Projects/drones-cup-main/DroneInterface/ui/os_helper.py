from typing import Optional
import os
import sys

def set_windows_app_id(app_id: str) -> None:
	"""Set the Windows AppUserModelID for proper taskbar grouping (no-op elsewhere)."""
	try:
		if os.name == "nt":
			import ctypes
			ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(app_id)
	except Exception:
		pass

def relaunch_headless(close_console: bool, script_path: Optional[str] = None) -> bool:
	"""On Windows, relaunch via pythonw.exe to detach from console if requested.
	Returns True if a relaunch was initiated and the current process should exit.
	"""
	try:
		if not close_console or os.name != "nt":
			return False
		if os.environ.get("DRONE_APP_RELAUNCHED"):
			return False
		import ctypes
		hwnd = ctypes.windll.kernel32.GetConsoleWindow()
		exe = sys.executable.lower()
		if not hwnd or not exe.endswith("python.exe"):
			return False
		pyw = os.path.join(os.path.dirname(sys.executable), "pythonw.exe")
		if not os.path.exists(pyw):
			return False
		import subprocess
		env = os.environ.copy()
		env["DRONE_APP_RELAUNCHED"] = "1"
		if script_path is None:
			script_path = os.path.abspath(sys.argv[0])
		subprocess.Popen([pyw, script_path], cwd=os.path.dirname(script_path), env=env)
		return True
	except Exception:
		return False

def free_console(close_console: bool) -> None:
	"""Detach and close the console if present (Windows only)."""
	if not close_console or os.name != "nt":
		return
	try:
		import ctypes
		hwnd = ctypes.windll.kernel32.GetConsoleWindow()
		if hwnd:
			ctypes.windll.kernel32.FreeConsole()
	except Exception:
		pass
