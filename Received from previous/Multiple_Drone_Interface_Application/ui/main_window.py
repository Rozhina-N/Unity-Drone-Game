import tkinter as tk
from tkinter import messagebox
import os
import logging
from typing import Optional, Any, Dict

from config_service import ConfigurationService

# Type alias for manager object
ManagerType = Optional[Any]

from .constants import *
from .os_helper import *
from .logging_helper import *
from .formatters import *
from .terminal_component import TerminalComponent
from .settings_component import SettingsComponent
from .drone_manager_component import DroneManagerComponent
from .drone_status_component import DroneStatusComponent
from .radio_status_component import RadioStatusComponent


class DroneUI:
    def __init__(
        self,
        root: tk.Tk,
        manager: ManagerType = None,
        config: Optional[ConfigurationService] = None
    ):
        self.root = root
        self.manager: ManagerType = manager
        self.config = config
        # If a manager is provided, use its handlers mapping directly so updates
        # (set_handler/add/remove) are visible to the UI without copying.
        try:
            self.handlers: Dict[str, Any] = manager.handlers if manager is not None else {}
        except Exception:
            self.handlers = {}

        self.root.title('Drone Controller')
        self.root.configure(bg='#2D2D2D')
        # Place window at the top center of the screen, always visible
        try:
            self.root.update_idletasks()
            # Set a default size if not already set
            default_width = 1200
            default_height = 800
            self.root.geometry(f"{default_width}x{default_height}")
            self.root.update_idletasks()
            width = self.root.winfo_width()
            height = self.root.winfo_height()
            screen_width = self.root.winfo_screenwidth()
            # Place window at the top center (y=0)
            x = int((screen_width - width) / 2)
            y = 0
            self.root.geometry(f"{width}x{height}+{x}+{y}")
        except Exception as e:
            logging.warning(f"Could not set window position: {e}")
        # Set window icon to drone.ico
        try:
            icon_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'assets', 'drone.ico')
            if os.path.exists(icon_path):
                self.root.iconbitmap(icon_path)  # type: ignore
        except Exception as e:
            logging.warning(f'Could not set window icon: {e}')

        # Paths are relative to the package or root
        # Since this file is in ui/, we go up one level to find the json files in root
        base_dir = os.path.dirname(os.path.dirname(__file__))
        self.ui_settings_path = os.path.join(os.path.dirname(__file__), 'ui_settings.json')
        self.config_path = os.path.join(base_dir, 'drones', 'drones.json')

        self._setup_grid()
        self._create_components()
        self._setup_event_handling()

        self.running = True
        self._rebuild_in_progress = False
        self.root.after(500, self.update_labels)
        try:
            self.root.deiconify()
            self.root.lift()  # type: ignore
            self.root.focus_force()
            logging.info('DroneUI window deiconified, lifted, and focused (should be visible and in front)')
        except Exception as e:
            logging.error(f'Failed to deiconify/lift/focus DroneUI window: {e}')

    def _setup_grid(self):
        for r in range(8):
            self.root.grid_rowconfigure(r, weight=0)
        try:
            self.root.grid_rowconfigure(7, minsize=120)
        except Exception:
            pass
        try:
            self.root.grid_rowconfigure(7, minsize=80)
        except Exception:
            pass
        # After UI built, warn about cache_dir if necessary
        try:
            if self.config:
                cache_dir = self.config.development.cache_dir
                if cache_dir:
                    import os
                    if not os.path.isdir(cache_dir) or not os.access(cache_dir, os.W_OK):
                        messagebox.showwarning(
                            "Cache directory not writable",
                            f"cache_dir set to '{cache_dir}', but it's not writable."
                        )
        except Exception:
            pass

        self.root.grid_columnconfigure(0, weight=0)
        self.root.grid_columnconfigure(1, weight=0)
        self.root.grid_columnconfigure(2, weight=0)
        self.root.grid_columnconfigure(3, weight=1)

        self.left_pane = tk.Frame(self.root, bg='#2D2D2D')
        left_margin = 12
        # Place at the very top, no vertical padding
        self.left_pane.grid(row=0, column=0, sticky='n', padx=(left_margin, 0), pady=(0, 0))
        try:
            self.left_pane.grid_columnconfigure(0, weight=0, minsize=360)  # Drone Manager (adjusted to align spacing)
            self.left_pane.grid_columnconfigure(1, weight=0, minsize=1)    # Divider
            self.left_pane.grid_columnconfigure(2, weight=0, minsize=240)  # Settings
            self.left_pane.grid_columnconfigure(3, weight=0, minsize=1)    # Divider
            self.left_pane.grid_columnconfigure(4, weight=0, minsize=240)  # Radio
        except Exception:
            pass

        try:
            hdiv_top_left = tk.Frame(self.root, bg='#3b3b3b', height=1)
            hdiv_top_left.grid(row=1, column=0, sticky='ew', padx=8, pady=(4, 4))
            hdiv_top_right = tk.Frame(self.root, bg='#3b3b3b', height=1)
            hdiv_top_right.grid(row=1, column=2, sticky='ew', padx=8, pady=(4, 4))
        except Exception:
            pass

        try:
            hdiv_mid_left = tk.Frame(self.root, bg='#3b3b3b', height=1)
            hdiv_mid_left.grid(row=4, column=0, sticky='ew', padx=8, pady=(4, 4))
            hdiv_mid_right = tk.Frame(self.root, bg='#3b3b3b', height=1)
            hdiv_mid_right.grid(row=4, column=2, sticky='ew', padx=8, pady=(4, 4))
        except Exception:
            pass

        try:
            # Divider between Drone Manager and Settings
            local_vdiv = tk.Frame(self.left_pane, bg='#3b3b3b', width=1)
            local_vdiv.grid(row=0, column=1, rowspan=3, sticky='ns', padx=(0, 0), pady=(0, 0))
            # Divider between Settings and Radio
            settings_vdiv = tk.Frame(self.left_pane, bg='#3b3b3b', width=1)
            settings_vdiv.grid(row=0, column=3, rowspan=3, sticky='ns', padx=(0, 0), pady=(0, 0))
        except Exception:
            pass

        try:
            min_panel_width = 360
            min_panel_height = 180
            total_width = min_panel_width + 240 + 240 + 8 * 3 + 12  # 360 + 240 + 240 + 24 + 12 = 876
            terminal_min_height = 160
            total_height = (min_panel_height * 2) + 40 + terminal_min_height + 40
            total_height = max(total_height, 950)
            try:
                self.root.geometry(f"{total_width}x{total_height}")
            except Exception:
                pass
            self.root.minsize(total_width, total_height)
            self.root.resizable(False, False)
        except Exception:
            pass

    def _create_components(self):
        # Drone Manager Component
        # Use wrapper frames so each column aligns to the same top
        mgr_col = tk.Frame(self.left_pane, bg='#2D2D2D')
        mgr_col.grid(row=0, column=0, sticky='nw', padx=(0,12), pady=(0,0))
        settings_col = tk.Frame(self.left_pane, bg='#2D2D2D')
        # Add left padding equal to window margin so content aligns from divider
        settings_col.grid(row=0, column=2, sticky='nw', padx=(12,12), pady=(0,0))
        radio_col = tk.Frame(self.left_pane, bg='#2D2D2D')
        # Same left padding for radio column
        radio_col.grid(row=0, column=4, sticky='nw', padx=(12,12), pady=(0,0))

        self.drone_manager = DroneManagerComponent(
            mgr_col, row=0, column=0, config_path=self.config_path, on_config_changed=self._on_config_changed, config=self.config
        )

        # Settings Component (center column)
        # Use normal spacing for settings
        self.settings = SettingsComponent(
            settings_col,
            row=0,
            column=0,
            padx=0,
            pady=(8, 2),
            manager=self.manager,
            ui_settings_path=self.ui_settings_path,
            config=self.config
        )

        # Radio Status Component (right column)
        from drones.radio_handler import RadioStatusHandler
        def get_radio_status():
            handler = RadioStatusHandler()
            # Pass manager for node count
            return handler.handle(drone_manager=self.manager)
        # Use normal spacing for radio status
        self.radio_status = RadioStatusComponent(radio_col, row=0, column=0, status_getter=get_radio_status, padx=(0,0), pady=(8, 2))

        # Drone Status Component
        self.drone_status = DroneStatusComponent(
            self.root, row=2, column=0, ui_settings_path=self.ui_settings_path,  # type: ignore
            handlers_getter=lambda: self.handlers,
            cfg_list_getter=lambda: self.drone_manager.cfg_list,
            status_colors_getter=lambda: self.drone_status.status_colors,
            config=self.config
        )

        # Terminal Component
        self.terminal = TerminalComponent(
            self.root, row=5, column=0, columnspan=4, sticky='ew', padx=8, pady=(0, 8)  # type: ignore
        )

    def _setup_event_handling(self):
        try:
            if self.manager is not None and hasattr(self.manager, 'subscribe'):
                self.manager.subscribe(self._on_manager_event)
        except Exception:
            pass

    def _on_config_changed(self):
        # Trigger rebuild of panels when config changes
        try:
            self.root.after(0, self.drone_status._rebuild_panels)  # type: ignore
        except Exception:
            pass

    def _on_manager_event(self, event_name: str, drone_id: str):
        try:
            if event_name in ('added', 'removed', 'updated', 'cleared'):
                try:
                    self.handlers = self.manager.handlers if self.manager is not None else {}
                except Exception:
                    self.handlers = {}
                try:
                    self.root.after(0, self.drone_status._rebuild_panels)  # type: ignore
                except Exception:
                    pass
        except Exception:
            pass

    def update_labels(self):
        if not getattr(self, 'running', False):
            return

        try:
            if self.manager is not None and hasattr(self.manager, 'refresh_move_rate_scaling'):
                self.manager.refresh_move_rate_scaling()
        except Exception:
            pass
        self.drone_status.update_labels()
        self.root.after(500, self.update_labels)

    def close(self):
        try:
            self.running = False
            self.terminal.close()
            try:
                if self.manager is not None:
                    if hasattr(self.manager, 'clear'):
                        self.manager.clear()
                    if hasattr(self.manager, 'unsubscribe'):
                        self.manager.unsubscribe(self._on_manager_event)
            except Exception:
                pass
        except Exception:
            pass


def run_ui(handlers: ManagerType = None):
    root = tk.Tk()
    ui = DroneUI(root, manager=handlers)
    try:
        root.mainloop()
    finally:
        try:
            ui.close()
        except Exception:
            pass
