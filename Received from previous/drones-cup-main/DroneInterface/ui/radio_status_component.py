
import tkinter as tk
from typing import Callable, Dict, Any, Tuple

class RadioStatusComponent:

    def __init__(self, parent_frame: tk.Frame, row: int, column: int, status_getter: Callable[[], Dict[str, Any]], padx: Tuple[int, int] = (0, 0), pady: Tuple[int, int] = (0, 0), compact: bool = False):
        self.frame = parent_frame
        self.status_getter = status_getter
        self._labels: Dict[str, tk.Label] = {}

        # Main content frame (no border/frame, just like other components)
        # Normal spacing when not compact
        content_pady = (8, 2) if not compact else (2, 2)
        self._content_frame = tk.Frame(self.frame, bg='#2D2D2D')  # type: ignore
        self._content_frame.grid(row=row, column=column, sticky='nw', padx=padx, pady=content_pady)  # type: ignore

        # Title label (smaller font, less padding)
        self._title_label = tk.Label(self._content_frame, text='Radio', font=('Segoe UI', 12, 'bold'), bg='#2D2D2D', fg='#F1F1F1')
        self._title_label.grid(row=0, column=0, sticky='nw', padx=(0,0), pady=(4, 4))

        # Status indicator and text (smaller font, less padding)
        self._status_row = tk.Frame(self._content_frame, bg='#2D2D2D')
        self._status_row.grid(row=1, column=0, sticky='nw', padx=(0,0), pady=(0, 4))
        self._status_canvas = tk.Canvas(self._status_row, width=12, height=12, bg='#2D2D2D', highlightthickness=0)
        self._status_canvas.pack(side=tk.LEFT, padx=(0, 4))
        self._status_text = tk.Label(self._status_row, text='Status: Unknown', font=('Segoe UI', 8), bg='#2D2D2D', fg='#F1F1F1')
        self._status_text.pack(side=tk.LEFT)

        # Info labels (smaller font, less padding, compact width)
        self._info_frame = tk.Frame(self._content_frame, bg='#2D2D2D')
        self._info_frame.grid(row=2, column=0, sticky='nw', padx=(0,0), pady=(0, 4))
        self._labels = {}
        info_fields = [
            ('Firmware', 'version'),
            ('HW Addr', 'hw_addr'),
            ('Radio Addr', 'radio_addr'),
            ('Channel', 'channel'),
            ('Data rate', 'datarate'),
            ('Power', 'power'),
            ('Nodes', 'nodes'),
        ]
        for i, (label, key) in enumerate(info_fields):
            l = tk.Label(self._info_frame, text=f'{label}:', font=('Segoe UI', 8), bg='#2D2D2D', fg='#aaa', anchor='w', width=9)
            l.grid(row=i, column=0, sticky='w')
            v = tk.Label(self._info_frame, text='-', font=('Segoe UI', 8, 'bold'), bg='#2D2D2D', fg='#F1F1F1', anchor='w', width=13)
            v.grid(row=i, column=1, sticky='w')
            self._labels[key] = v

        self.update_status()

    def update_status(self):
        status: Dict[str, Any] = self.status_getter()
        # Status indicator color
        if status.get('connected'):
            color = '#27ae60'  # green
            text = 'Connected'
        elif status.get('status') == 'not_found':
            color = '#c0392b'  # red
            text = 'Not Found'
        elif status.get('status') == 'error':
            color = '#c0392b'
            text = f"Error: {status.get('message')}"
        else:
            color = '#c0392b'
            text = 'Disconnected'
        self._status_canvas.delete('all')
        self._status_canvas.create_oval(2, 2, 14, 14, fill=color, outline=color)
        self._status_text.config(text=f'Status: {text}', fg=color)

        # Info fields
        info_map: Dict[str, Callable[[Any], str]] = {
            'version': lambda v: v if v is not None else '-',
            'hw_addr': lambda v: v if v else '-',
            'radio_addr': lambda v: v if v else '-',
            'channel': lambda v: str(v) if v is not None else '-',
            'datarate': lambda v: v if v is not None else '-',
            'power': lambda v: v if v is not None else '-',
            'nodes': lambda v: str(v) if v is not None else '-',
        }
        for key, label in self._labels.items():
            val = info_map[key](status.get(key))
            label.config(text=val)  # type: ignore
