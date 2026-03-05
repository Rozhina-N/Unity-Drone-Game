import tkinter as tk
import logging
import json
import os
import math
from typing import Optional, Any, Union, Tuple, Callable, Dict


class SettingsComponent:
    def __init__(
        self,
        parent_frame: tk.Frame,
        row: int,
        column: int,
        padx: int,
        pady: Union[int, Tuple[int, int]],
        manager: Optional[Any] = None,
        ui_settings_path: Optional[str] = None,
        compact: bool = False,
        config: Optional[Any] = None
    ) -> None:
        self.frame: tk.Frame = parent_frame
        self.manager: Optional[Any] = manager
        self.ui_settings_path: Optional[str] = ui_settings_path
        self.config = config
        self._load_ui_settings()
        label_pady = (4, 2) if compact else pady
        settings_frame_pady = 2 if compact else 4
        self._settings_label: tk.Label = tk.Label(
            self.frame, text='Settings', font=('Segoe UI', 12, 'bold'),
            bg='#2D2D2D', fg='#F1F1F1'
        )
        self._settings_label.grid(
            row=row, column=column, sticky='w', padx=padx, pady=label_pady
        )

        settings_frame: tk.Frame = tk.Frame(self.frame, bg='#2D2D2D')
        settings_frame.grid(row=row+1, column=column, sticky='nw', padx=padx, pady=settings_frame_pady)

        # Always on top
        self.always_on_top_var = tk.BooleanVar(value=self._get_always_on_top())
        cb_top = tk.Checkbutton(settings_frame, text='Always on top', variable=self.always_on_top_var,
                                command=self._toggle_top, bg='#2D2D2D', fg='#fff', selectcolor='#2D2D2D')
        cb_top.pack(anchor='nw')

        # Verbose connection logs
        self.verbose_conn_var = tk.BooleanVar(value=self._get_verbose_conn())
        cb_conn = tk.Checkbutton(settings_frame, text='Verbose connection logs', variable=self.verbose_conn_var,
                                 command=self._toggle_verbose_conn, bg='#2D2D2D', fg='#fff', selectcolor='#2D2D2D')
        cb_conn.pack(anchor='nw')

        budget_frame = tk.Frame(settings_frame, bg='#2D2D2D')
        budget_frame.pack(anchor='nw', pady=(6, 0))
        tk.Label(budget_frame, text='Move budget (Hz)', bg='#2D2D2D', fg='#fff').pack(side=tk.LEFT)
        self.move_budget_var = tk.StringVar(value=self._get_move_budget())
        self.move_budget_entry = tk.Entry(
            budget_frame, textvariable=self.move_budget_var, width=6, bg='#3a3a3a', fg='#fff', insertbackground='#fff'
        )
        self.move_budget_entry.pack(side=tk.LEFT, padx=(6, 4))
        apply_btn = tk.Button(budget_frame, text='Apply', command=self._apply_move_budget, bg='#3a3a3a', fg='#fff')
        apply_btn.pack(side=tk.LEFT)
        try:
            self.move_budget_entry.bind('<Return>', lambda _e: self._apply_move_budget())
        except Exception:
            pass

    def _get_always_on_top(self) -> bool:
        if self.config:
            return self.config.ui.always_on_top
        return False

    def _get_verbose_conn(self) -> bool:
        if self.config:
            return self.config.development.connection_log_verbose
        return False

    def _toggle_top(self) -> None:
        try:
            val = bool(self.always_on_top_var.get())
            self.frame.winfo_toplevel().attributes('-topmost', val)  # type: ignore
        except Exception:
            pass

    def _toggle_verbose_conn(self) -> None:
        # Note: Cannot mutate immutable config, but we can log the preference
        try:
            val = bool(self.verbose_conn_var.get())
            if val:
                logging.getLogger().setLevel(logging.INFO)
            logging.info(f"Verbose connection logging preference: {val}")
        except Exception:
            pass

    def _get_move_budget(self) -> str:
        if self.config:
            return str(self.config.performance.move_to_rate_hz)
        return "0"

    def _apply_move_budget(self) -> None:
        try:
            raw = self.move_budget_var.get()
            rate = float(raw)
            if not math.isfinite(rate):
                raise ValueError("Move budget must be finite")
        except Exception:
            logging.warning("Invalid move budget; keeping previous value.")
            return
        if self.manager is not None and hasattr(self.manager, 'set_move_budget_hz'):
            try:
                self.manager.set_move_budget_hz(rate)  # type: ignore
            except Exception:
                pass
        self._save_move_budget(rate)

    def _load_ui_settings(self) -> None:
        if not self.ui_settings_path:
            return
        data: Dict[str, Any] = {}
        try:
            if os.path.exists(self.ui_settings_path):
                with open(self.ui_settings_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
        except Exception:
            data = {}
        move_budget = data.get('move_budget_hz')
        try:
            if move_budget is not None:
                rate = float(move_budget)
                if math.isfinite(rate):
                    if self.manager is not None and hasattr(self.manager, 'set_move_budget_hz'):
                        try:
                            self.manager.set_move_budget_hz(rate)  # type: ignore
                        except Exception:
                            pass
        except Exception:
            pass

    def _save_move_budget(self, rate: float) -> None:
        if not self.ui_settings_path:
            return
        data: Dict[str, Any] = {}
        try:
            if os.path.exists(self.ui_settings_path):
                with open(self.ui_settings_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
        except Exception:
            data = {}
        data['move_budget_hz'] = rate
        try:
            with open(self.ui_settings_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
        except Exception:
            pass
