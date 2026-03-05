import tkinter as tk
import sys
import logging
from typing import Union, Tuple
from .logging_helper import UILogHandler, TextRedirector


class TerminalComponent:
    def __init__(self, parent_frame: tk.Frame, row: int, column: int, columnspan: int, sticky: str, padx: Union[int, Tuple[int, int]], pady: Union[int, Tuple[int, int]]):
        self.frame = parent_frame
        self._terminal_label = tk.Label(self.frame, text='Terminal', font=('Segoe UI', 12, 'bold'), bg='#2D2D2D', fg='#F1F1F1')
        self._terminal_label.grid(row=row, column=column, sticky='w', padx=padx, pady=(8, 4))  # type: ignore

        self.terminal_frame = tk.Frame(self.frame, bg='#1e1e1e')
        self.terminal_frame.grid(row=row+1, column=column, columnspan=columnspan, sticky=sticky, padx=padx, pady=pady)  # type: ignore

        self.terminal = tk.Text(self.terminal_frame, bg='#1e1e1e', fg='#fff', insertbackground='white', height=8, wrap='word', state=tk.DISABLED)
        self.scrollbar = tk.Scrollbar(self.terminal_frame, command=self.terminal.yview)  # type: ignore
        self.terminal.config(yscrollcommand=self.scrollbar.set)

        self.terminal.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        self._orig_stdout = sys.stdout
        self._orig_stderr = sys.stderr
        sys.stdout = TextRedirector(self.terminal, self.frame)  # type: ignore
        sys.stderr = TextRedirector(self.terminal, self.frame)  # type: ignore

        self._ui_log_handler = UILogHandler(self.terminal, self.frame)  # type: ignore
        fmt = logging.Formatter('%(asctime)s %(levelname)s: %(message)s', datefmt='%H:%M:%S')
        self._ui_log_handler.setFormatter(fmt)
        logging.getLogger().addHandler(self._ui_log_handler)

    def close(self):
        sys.stdout = self._orig_stdout
        sys.stderr = self._orig_stderr
        logging.getLogger().removeHandler(self._ui_log_handler)