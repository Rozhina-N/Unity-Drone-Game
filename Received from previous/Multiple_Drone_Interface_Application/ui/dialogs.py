import tkinter as tk
from tkinter import messagebox
from typing import Optional, Dict, Union


class DroneEditDialog:
	def __init__(self, parent: Union[tk.Tk, tk.Toplevel], initial: Optional[Dict[str, str]] = None):
		self.result = None
		top = tk.Toplevel(parent)  # type: ignore
		top.transient(parent)  # type: ignore
		top.grab_set()
		tk.Label(top, text='Drone ID:').grid(row=0, column=0, sticky='e')
		self.id_entry = tk.Entry(top, width=40)
		self.id_entry.grid(row=0, column=1, padx=8, pady=4, sticky='w')

		tk.Label(top, text='Address:').grid(row=1, column=0, sticky='e')
		self.addr_entry = tk.Entry(top, width=40)
		self.addr_entry.grid(row=1, column=1, padx=8, pady=4, sticky='w')

		btns = tk.Frame(top)
		btns.grid(row=2, column=0, columnspan=2, pady=6)
		tk.Button(btns, text='OK', width=10, command=self._ok).pack(side=tk.LEFT, padx=6)
		tk.Button(btns, text='Cancel', width=10, command=self._cancel).pack(side=tk.LEFT)

		if initial:
			self.id_entry.insert(0, initial.get('id', ''))  # type: ignore
			self.addr_entry.insert(0, initial.get('address', ''))  # type: ignore

		parent.wait_window(top)  # type: ignore

	def _ok(self):
		did = self.id_entry.get().strip()
		addr = self.addr_entry.get().strip()
		if not did or not addr:
			messagebox.showwarning('Invalid', 'Both id and address are required')
			return
		self.result = {'id': did, 'address': addr}
		try:
			self.id_entry.master.destroy()
		except Exception:
			pass

	def _cancel(self):
		self.result = None
		try:
			self.id_entry.master.destroy()
		except Exception:
			pass
