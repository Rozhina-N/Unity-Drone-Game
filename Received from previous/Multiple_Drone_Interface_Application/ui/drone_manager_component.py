import tkinter as tk
from tkinter import ttk, messagebox
import json
import os
from typing import Optional, Callable, List, Dict, Tuple, Any
from .dialogs import DroneEditDialog


class DroneManagerComponent:
    def __init__(self, parent_frame: tk.Frame, row: int, column: int, config_path: str, on_config_changed: Optional[Callable[[], None]] = None, compact: bool = False, config: Optional[Any] = None) -> None:
        self.frame: tk.Frame = parent_frame
        self.config_path: str = config_path
        self.on_config_changed: Optional[Callable[[], None]] = on_config_changed
        self.config = config
        self.max_drones = config.drone.max_drones if config else 4

        label_pady = (4, 2) if compact else (8, 4)
        listbox_height = 4 if compact else 6
        btn_pady = (0, 2) if compact else (0, 8)

        self._mgr_label: tk.Label = tk.Label(self.frame, text='Drone Manager', font=('Segoe UI', 12, 'bold'), bg='#2D2D2D', fg='#F1F1F1')
        self._mgr_label.grid(row=row, column=column, sticky='nw', padx=(0,0), pady=label_pady)

        list_frame: tk.Frame = tk.Frame(self.frame, bg='#2D2D2D')
        # Ensure list frame has enough width for 4 drones with long addresses
        list_frame.grid(row=row+1, column=column, sticky='nw', padx=(0,0), pady=(0, 2 if not compact else 0))
        try:
            list_frame.grid_columnconfigure(0, minsize=420)
        except Exception:
            pass

        # Wider listbox to fit full addresses (width in characters)
        self.cfg_listbox: tk.Listbox = tk.Listbox(list_frame, bg='#1e1e1e', fg='#fff', height=listbox_height, width=48)
        self.cfg_listbox.pack(side=tk.LEFT, anchor='nw')
        scrollbar: ttk.Scrollbar = ttk.Scrollbar(list_frame, orient='vertical', command=self.cfg_listbox.yview)  # type: ignore
        scrollbar.pack(side=tk.RIGHT, fill='y')
        self.cfg_listbox.config(yscrollcommand=scrollbar.set)

        btn_frame: tk.Frame = tk.Frame(self.frame, bg='#2D2D2D')
        btn_frame.grid(row=row+2, column=column, sticky='nw', padx=(0,0), pady=btn_pady)
        btn_frame.grid_columnconfigure(0, weight=1)
        btn_frame.grid_columnconfigure(1, weight=1)
        btn_frame.grid_columnconfigure(2, weight=1)
        btn_opts = {'bg': '#3a3a3a', 'fg': '#fff'}
        tk.Button(btn_frame, text='Add', command=self._cfg_add, **btn_opts).grid(row=0, column=0, padx=4, pady=4, sticky='ew')  # type: ignore
        tk.Button(btn_frame, text='Edit', command=self._cfg_edit, **btn_opts).grid(row=0, column=1, padx=4, pady=4, sticky='ew')  # type: ignore
        tk.Button(btn_frame, text='Remove', command=self._cfg_remove, **btn_opts).grid(row=0, column=2, padx=4, pady=4, sticky='ew')  # type: ignore

        self.cfg_list: List[Dict[str, str]] = []
        self._load_config()

    def _load_config(self) -> None:
        self.cfg_list = []
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, 'r', encoding='utf-8') as f:
                    self.cfg_list = json.load(f)
            except Exception:
                self.cfg_list = []
        self.cfg_list = self.cfg_list[:self.max_drones]
        self.cfg_listbox.delete(0, tk.END)
        for e in self.cfg_list:
            self.cfg_listbox.insert(tk.END, f"{e.get('id')} ({e.get('address')})")

    def _cfg_add(self) -> None:
        if len(self.cfg_list) >= self.max_drones:
            messagebox.showwarning('Limit', f'Maximum {self.max_drones} drones allowed')
            return
        suggested_addr = 'radio://0/80/2M/E7E7E7E7E7'
        suggested = {'id': f'drone{len(self.cfg_list)+1}', 'address': suggested_addr}
        dlg = DroneEditDialog(self.frame.winfo_toplevel(), initial=suggested)
        if dlg.result:
            self.cfg_list.append(dlg.result)
            self.cfg_listbox.insert(tk.END, f"{dlg.result.get('id')} ({dlg.result.get('address')})")
            self._save_config()

    def _cfg_edit(self) -> None:
        sel: Tuple[int, ...] = self.cfg_listbox.curselection()  # type: ignore
        if not sel:
            return
        idx: int = sel[0]  # type: ignore
        entry: Dict[str, str] = self.cfg_list[idx]
        dlg = DroneEditDialog(self.frame.winfo_toplevel(), initial=entry)
        if dlg.result:
            self.cfg_list[idx] = dlg.result
            self.cfg_listbox.delete(idx)  # type: ignore
            self.cfg_listbox.insert(idx, f"{dlg.result.get('id')} ({dlg.result.get('address')})")  # type: ignore
            self._save_config()

    def _cfg_remove(self) -> None:
        sel: Tuple[int, ...] = self.cfg_listbox.curselection()  # type: ignore
        if not sel:
            return
        idx: int = sel[0]  # type: ignore
        del self.cfg_list[idx]
        self.cfg_listbox.delete(idx)  # type: ignore
        self._save_config()

    def _save_config(self) -> None:
        try:
            with open(self.config_path, 'w', encoding='utf-8') as f:
                json.dump(self.cfg_list, f, indent=2)
        except Exception:
            pass
        if self.on_config_changed:
            self.on_config_changed()