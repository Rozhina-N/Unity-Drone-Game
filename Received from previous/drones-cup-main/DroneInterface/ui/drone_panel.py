import tkinter as tk
from typing import Dict, Any, Optional, Callable

try:
	from utils.battery import voltage_to_percent
except ImportError:
	def voltage_to_percent(v: float) -> float:
		return 0.0

class DronePanel:
	def __init__(self, parent: tk.Frame, idx: int, callbacks: Dict[str, Callable[..., None]]) -> None:
		self.idx: int = idx
		self.callbacks: Dict[str, Callable[..., None]] = callbacks
		self.frame: tk.Frame = tk.Frame(parent, bg='#3a3a3a', bd=1, relief=tk.RIDGE, width=360, height=180)
		self.frame.grid_propagate(False)

		self.title_bar: tk.Frame = tk.Frame(self.frame, bg='#7f8c8d')
		self.title_bar.pack(fill='x')
		self.title_label: tk.Label = tk.Label(self.title_bar, text=f'Slot {idx+1}', font=('Segoe UI', 11, 'bold'), bg=self.title_bar['bg'], fg='#fff')
		self.title_label.pack(side=tk.LEFT, padx=8, pady=4, anchor='w')
		self.addr_label: tk.Label = tk.Label(self.title_bar, text='', font=('Segoe UI', 9), bg=self.title_bar['bg'], fg='#dcdcdc')
		self.addr_label.pack(side=tk.RIGHT, padx=8)

		self.content: tk.Frame = tk.Frame(self.frame, bg='#3a3a3a')
		self.content.pack(fill='both', expand=True, padx=8, pady=6)
		self.content.grid_columnconfigure(0, weight=1)
		self.content.grid_columnconfigure(1, weight=0)

		self.text_label: tk.Label = tk.Label(self.content, text='', bg='#3a3a3a', fg='#fff', justify='left', anchor='nw', wraplength=260)
		self.text_label.grid(row=0, column=0, sticky='nw')

		self.ctrl_frame: tk.Frame = tk.Frame(self.content, bg='#3a3a3a')
		self.ctrl_frame.grid(row=0, column=1, sticky='ne', padx=(8, 0))
		btn_opts = {'bg': '#3a3a3a', 'fg': '#fff', 'width': 14}  # type: ignore
		tk.Button(self.ctrl_frame, text='Takeoff', command=lambda: self.callbacks['takeoff'](self.idx), **btn_opts).pack(pady=6)  # type: ignore
		tk.Button(self.ctrl_frame, text='Land', command=lambda: self.callbacks['land'](self.idx), **btn_opts).pack(pady=6)  # type: ignore
		tk.Button(self.ctrl_frame, text='Stop', command=lambda: self.callbacks['stop'](self.idx), **btn_opts).pack(pady=6)  # type: ignore
		tk.Button(self.ctrl_frame, text='Power Cycle', command=lambda: self.callbacks['power_cycle'](self.idx), **btn_opts).pack(pady=6)  # type: ignore

		self.bypass_var: tk.BooleanVar = tk.BooleanVar()
		self.bypass_cb: tk.Checkbutton = tk.Checkbutton(self.ctrl_frame, text='Bypass Safety', variable=self.bypass_var,
			command=lambda: self.callbacks['bypass'](self.idx, self.bypass_var),
			bg='#3a3a3a', fg='#fff', selectcolor='#3a3a3a')
		self.bypass_cb.pack(pady=6)

	def update(self, drone_obj: Optional[Any], config_entry: Optional[Dict[str, Any]], status_colors: Dict[str, str]) -> None:
		# Update the panel UI with the latest drone state
		try:
			percent = 0.0  # Initialize percent for low battery check
			addr = getattr(drone_obj, 'address', '') if drone_obj else ''
			self.addr_label.config(text=addr)
			display_name = getattr(drone_obj, 'id', None) or (config_entry.get('id') if config_entry else f'Slot {self.idx+1}')
			if display_name is None:
				display_name = f'Slot {self.idx+1}'
			self.title_label.config(text=display_name)

			# Determine connection status
			if drone_obj:
				connected = getattr(drone_obj, 'connected', False)
				connecting = getattr(drone_obj, 'connecting', False)
				connection = 'Connected' if connected else ('Connecting' if connecting else 'Disconnected')
				armed = 'Armed' if getattr(drone_obj, 'armed', 0) else 'Unarmed'
				battery_state = 'Idle'
				is_charging = False
				percent = 0.0  # Initialize percent
				if connected:
					# Battery info
					voltage = 0.0
					percent = 0.0
					battery = getattr(drone_obj, 'battery', None)
					if battery:
						voltage = float(battery.get('voltage', 0.0))
						percent = float(battery.get('percent', 0.0))
						is_charging = bool(battery.get('is_charging', False))
						battery_state = 'Charging' if is_charging else 'Idle'
					else:
						voltage = float(getattr(drone_obj, 'battery_voltage', 0.0))
						percent = voltage_to_percent(voltage)
						battery_state = getattr(drone_obj, 'battery_status', 'Idle')
						is_charging = getattr(drone_obj, 'is_charging', False)
					# Position
					x = float(getattr(drone_obj, 'get_x', lambda: 0.0)())
					y = float(getattr(drone_obj, 'get_y', lambda: 0.0)())
					z = float(getattr(drone_obj, 'get_z', lambda: 0.0)())
					yaw = float(getattr(drone_obj, 'get_yaw', lambda: 0.0)())
					text = (
						f"{connection}\n{armed}\nBattery: {battery_state}\nVoltage: {voltage:.2f} V ({percent:.0f}%)\n"
						f"Position:\n  X={x:.2f}\n  Y={y:.2f}\n  Z={z:.2f}\n  Yaw={yaw:.2f}"
					)
				else:
					text = f"{connection}\n{armed}\nNo telemetry data"
			else:
				text = "No data"
				battery_state = 'Idle'
				is_charging = False
			self.text_label.config(text=text)

			# Update title bar color based on status
			color = status_colors.get('Connected', '#27ae60')
			if drone_obj:
				registered = bool(getattr(drone_obj, 'id', None)) or bool(config_entry and config_entry.get('id'))
				if not registered:
					color = status_colors.get('Not registered', '#7f8c8d')
				elif is_charging or battery_state == 'Charging':
					color = status_colors.get('Charging', '#2E8BC0')
				elif not getattr(drone_obj, 'connected', False):
					if getattr(drone_obj, 'connecting', False):
						color = status_colors.get('Connecting', '#F1C40F')
					else:
						color = status_colors.get('Disconnected', '#c0392b')
				else:
					# Check for low battery
					try:
						low_threshold = 20.0
						if percent < low_threshold:
							color = status_colors.get('Low battery', '#E87E04')
						else:
							color = status_colors.get('Connected', '#27ae60')
					except Exception:
						color = status_colors.get('Connected', '#27ae60')
			self.title_bar.config(bg=color)
			self.title_label.config(bg=color)
			self.addr_label.config(bg=color)
		except Exception:
			pass
