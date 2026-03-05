import tkinter as tk
import json
import os
from typing import Callable, Dict, List, Any, Optional
from .constants import STATUS_ORDER_DEFAULT, STATUS_COLORS_DEFAULT, LEGACY_STATUS_LABEL_MAP, LEGACY_STATUS_ORDERS
from .drone_panel import DronePanel


class DroneStatusComponent:
    def __init__(self, parent_frame: tk.Frame, row: int, column: int, ui_settings_path: str, handlers_getter: Callable[[], Dict[str, Any]], cfg_list_getter: Callable[[], List[Any]], status_colors_getter: Callable[[], Dict[str, str]], config: Optional[Any] = None):
        self.frame = parent_frame
        self.ui_settings_path = ui_settings_path
        self.get_handlers = handlers_getter
        self.get_cfg_list = cfg_list_getter
        self.get_status_colors = status_colors_getter
        self.config = config

        self.max_status_panels = config.drone.max_drones if config else 4

        self.status_order: List[str] = list(STATUS_ORDER_DEFAULT)
        self.status_colors: Dict[str, str] = dict(STATUS_COLORS_DEFAULT)
        self.panels: Dict[str, Any] = {}
        self._status_legend: Optional[Any] = None
        self._load_ui_settings()

        self._overview_frame = tk.Frame(self.frame, bg='#2D2D2D')  # type: ignore
        self._overview_frame.grid(row=row, column=column, sticky='nsew', padx=(12,0), pady=(8, 4))  # type: ignore

        self._overview_label = tk.Label(self._overview_frame, text='Drone Overview', font=('Segoe UI', 12, 'bold'), bg='#2D2D2D', fg='#F1F1F1')
        self._overview_label.pack(side=tk.LEFT, anchor='w')

        self.left_container = tk.Frame(self.frame, bg='#2D2D2D')  # type: ignore
        self.left_container.grid(row=row+1, column=column, sticky='nw', pady=(0, 8), padx=(12,8))  # type: ignore

        self.panels_frame = tk.Frame(self.left_container, bg='#2D2D2D')
        self.panels_frame.grid(row=0, column=0, sticky='nw')
        self.panels = {}
        self._build_status_legend()
        self._rebuild_panels()

    def _load_ui_settings(self):
        data: Dict[str, Any] = {}
        try:
            if os.path.exists(self.ui_settings_path):  # type: ignore
                with open(self.ui_settings_path, 'r', encoding='utf-8') as f:  # type: ignore
                    data = json.load(f)
        except Exception:
            data = {}

        changed = False
        raw_order = data.get('status_order')
        if isinstance(raw_order, list):
            order: List[str] = [str(item) for item in raw_order if isinstance(item, str)]  # type: ignore
        else:
            order = list(STATUS_ORDER_DEFAULT)
            changed = True

        order = [LEGACY_STATUS_LABEL_MAP.get(name, name) for name in order]
        if order in LEGACY_STATUS_ORDERS:
            order = list(STATUS_ORDER_DEFAULT)
            changed = True
        if 'Connecting' not in order:
            insert_idx = 1 if 'Connected' in order else 0
            order.insert(insert_idx, 'Connecting')
            changed = True
        for status in STATUS_ORDER_DEFAULT:
            if status not in order:
                order.append(status)
                changed = True

        colors = data.get('status_colors')
        merged_colors = dict(STATUS_COLORS_DEFAULT)
        if isinstance(colors, dict):
            for raw_key, value in colors.items():  # type: ignore
                if isinstance(raw_key, str) and isinstance(value, str) and value.startswith('#'):
                    key = LEGACY_STATUS_LABEL_MAP.get(raw_key, raw_key)
                    merged_colors[key] = value
                    if key != raw_key:
                        changed = True
        else:
            changed = True

        if 'Connecting' not in merged_colors:
            merged_colors['Connecting'] = STATUS_COLORS_DEFAULT['Connecting']
            changed = True

        data['status_order'] = order
        data['status_colors'] = merged_colors
        self.status_order = order
        self.status_colors = merged_colors

        if changed:
            try:
                with open(self.ui_settings_path, 'w', encoding='utf-8') as f:  # type: ignore
                    json.dump(data, f, indent=2)
            except Exception:
                pass

        self._build_status_legend()

    def _build_status_legend(self):
        try:
            if self._status_legend is not None:
                self._status_legend.destroy()
        except Exception:
            pass

        try:
            legend = tk.Frame(self.left_container, bg='#2D2D2D')
            legend.grid(row=0, column=1, sticky='nw', padx=(8, 0))
            for text in self.status_order:
                col = self.status_colors.get(text, '#7f8c8d')
                item = tk.Frame(legend, bg='#2D2D2D')
                item.pack(side='top', anchor='w', pady=2)
                rect = tk.Frame(item, bg=col, width=64, height=12, bd=1, relief=tk.SUNKEN)
                rect.pack(side=tk.LEFT, padx=(0, 8))
                lbl = tk.Label(item, text=text, font=('Segoe UI', 9), bg='#2D2D2D', fg='#F1F1F1')
                lbl.pack(side=tk.LEFT)
            self._status_legend = legend
        except Exception:
            pass

    def _rebuild_panels(self):
        for p in list(self.panels.values()):  # type: ignore
            try:
                p.destroy()  # type: ignore
            except Exception:
                pass
        self.panels = {}

        max_cols = 2
        callbacks: Dict[str, Callable[..., None]] = {
            'takeoff': lambda idx: self._do_takeoff(int(idx)),  # type: ignore
            'land': lambda idx: self._do_land(int(idx)),  # type: ignore
            'stop': lambda idx: self._do_stop(int(idx)),  # type: ignore
            'power_cycle': lambda idx: self._do_power_cycle(int(idx)),  # type: ignore
            'bypass': lambda idx, var: self._toggle_bypass(int(idx), var)  # type: ignore
        }

        for idx in range(self.max_status_panels):
            row = idx // max_cols
            col = idx % max_cols

            panel = DronePanel(self.panels_frame, idx, callbacks)
            panel.frame.grid(row=row, column=col, padx=6, pady=6, sticky='nsew')

            key = f'__slot_{idx}'
            self.panels[key] = panel

    def update_labels(self):
        for idx in range(self.max_status_panels):
            key = f'__slot_{idx}'
            panel = self.panels.get(key)
            if not panel:
                continue

            h = self._get_handler_for_slot(idx)
            if h is not None:
                drone_obj = h
                if hasattr(h, 'handler') and getattr(h, 'handler', None) is not None:
                    drone_obj = h.handler

                config_entry = self.get_cfg_list()[idx] if idx < len(self.get_cfg_list()) else None
                panel.update(drone_obj, config_entry, self.status_colors)

    def _get_handler_for_slot(self, idx: int) -> Optional[Any]:
        try:
            handlers = self.get_handlers()  # type: ignore
            if isinstance(handlers, dict):  # type: ignore
                hid = list(handlers.keys())[idx] if idx < len(handlers) else None  # type: ignore
                if hid is not None:
                    return handlers.get(hid)  # type: ignore
        except Exception:
            pass
        return None

    def _toggle_bypass(self, idx: int, var: Any):
        import logging
        h = self._get_handler_for_slot(idx)
        if h:
            try:
                h.bypass_safety = var.get()  # type: ignore
                logging.info(f"Drone {h.id} bypass_safety set to {h.bypass_safety}")  # type: ignore
            except Exception as e:
                logging.error(f"Failed to set bypass_safety for drone {h.id}: {e}")

    def _do_takeoff(self, idx: int):
        from tkinter import messagebox
        import threading
        h = self._get_handler_for_slot(idx)
        if h is None:
            messagebox.showwarning('No drone', 'No drone handler attached for this slot')
            return
        try:
            default_height = self.config.drone.default_takeoff_height if self.config else 0.3
            default_duration = self.config.drone.default_takeoff_duration if self.config else 2.0
            if hasattr(h, 'takeoff'):
                threading.Thread(target=lambda: h.takeoff(height=default_height, duration=default_duration), daemon=True).start()  # type: ignore
            else:
                inner = getattr(h, 'handler', None)
                if inner and hasattr(inner, 'takeoff'):
                    threading.Thread(target=lambda: inner.takeoff(height=default_height, duration=default_duration), daemon=True).start()
        except Exception as e:
            messagebox.showerror('Takeoff error', str(e))

    def _do_land(self, idx: int):
        from tkinter import messagebox
        import threading
        h = self._get_handler_for_slot(idx)
        if h is None:
            return
        try:
            if hasattr(h, 'land'):
                threading.Thread(target=lambda: h.land(), daemon=True).start()  # type: ignore
            else:
                inner = getattr(h, 'handler', None)
                if inner and hasattr(inner, 'land'):
                    threading.Thread(target=lambda: inner.land(), daemon=True).start()
        except Exception as e:
            messagebox.showerror('Land error', str(e))

    def _do_stop(self, idx: int):
        from tkinter import messagebox
        import threading
        h = self._get_handler_for_slot(idx)
        if h is None:
            return
        try:
            if hasattr(h, 'stop'):
                threading.Thread(target=lambda: h.stop(), daemon=True).start()  # type: ignore
            else:
                inner = getattr(h, 'handler', None)
                if inner and hasattr(inner, 'stop'):
                    threading.Thread(target=lambda: inner.stop(), daemon=True).start()
        except Exception as e:
            messagebox.showerror('Stop error', str(e))

    def _do_power_cycle(self, idx: int):
        from tkinter import messagebox
        import threading
        h = self._get_handler_for_slot(idx)
        if h is None:
            return
        try:
            if hasattr(h, 'power_cycle'):
                threading.Thread(target=lambda: h.power_cycle(), daemon=True).start()  # type: ignore
            else:
                inner = getattr(h, 'handler', None)
                if inner and hasattr(inner, 'power_cycle'):
                    threading.Thread(target=lambda: inner.power_cycle(), daemon=True).start()
        except Exception as e:
            messagebox.showerror('Power cycle error', str(e))