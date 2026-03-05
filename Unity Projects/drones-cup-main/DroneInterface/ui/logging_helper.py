import logging
import tkinter as tk
from tkinter import Text
from logging import LogRecord

class TextRedirector:
	def __init__(self, widget: Text, root: tk.Tk):
		self.widget = widget
		self.root = root

	def write(self, message: str):
		if not message:
			return

		def _append():
			try:
				self.widget.configure(state=tk.NORMAL)
				self.widget.insert(tk.END, message)
				self.widget.see(tk.END)
				self.widget.configure(state=tk.DISABLED)
			except Exception:
				pass

		try:
			self.root.after(0, _append)
		except Exception:
			pass

	def flush(self):
		return

class UILogHandler(logging.Handler):
	def __init__(self, widget: Text, root: tk.Tk):
		super().__init__()
		self.widget = widget
		self.root = root

	def emit(self, record: LogRecord):
		try:
			msg = self.format(record) + "\n"
		except Exception:
			msg = str(record) + "\n"

		def _append():
			try:
				self.widget.configure(state=tk.NORMAL)
				self.widget.insert(tk.END, msg)
				self.widget.see(tk.END)
				self.widget.configure(state=tk.DISABLED)
			except Exception:
				pass

		try:
			self.root.after(0, _append)
		except Exception:
			pass
