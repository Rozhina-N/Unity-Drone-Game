from typing import Any, Dict, Union, Optional
from utils.battery import voltage_to_percent, pm_state_is_charging

class Drone:
    """
    Central Drone object encapsulating all drone properties, telemetry, battery, LED, and connection logic.
    All modules should refer to this object for state and actions.
    """
    def __init__(self, drone_id: str, address: str, handler: Any = None) -> None:
        self.id = drone_id
        self.address = address
        self.handler: Any = handler  # Connection/communication handler
        self.telemetry: Dict[str, float] = {
            "x": 0.0,
            "y": 0.0,
            "z": 0.0,
            "yaw": 0.0,
            # Add more telemetry fields as needed
        }
        self.battery: Dict[str, Union[float, int, bool]] = {
            "voltage": 4.0,
            "percent": voltage_to_percent(4.0),
            "is_charging": False,
            "pm_state": 0,
        }
        self.led: Dict[str, Optional[str]] = {
            "color": None,
            "effect": None,
        }
        self.connected = False
        self.armed = 0  # bit 16 convention
        self.bypass_safety = False

    # --- Drone Actions ---
    def takeoff(self, height: float = 1.0, duration: float = 2.0):
        import logging
        logger = logging.getLogger(f"Drone.{self.id}")
        
        if not self.is_connected():
            logger.warning(f"Takeoff blocked: Drone {self.id} not connected")
            return  # Don't send commands to disconnected drones
        
        logger.info(f"Takeoff requested for {self.id}: height={height}, duration={duration}")
        self.telemetry["z"] = height
        self.armed = int(getattr(self, "armed", 0)) | 16
        
        if self.handler and hasattr(self.handler, "takeoff"):
            logger.info(f"Forwarding takeoff to handler for {self.id}")
            self.handler.takeoff(height=height, duration=duration, bypass_safety=self.bypass_safety)
        else:
            logger.warning(f"No handler available for takeoff on {self.id}")

    def land(self) -> None:
        import logging
        logger = logging.getLogger(f"Drone.{self.id}")
        
        if not self.is_connected():
            logger.warning(f"Land blocked: Drone {self.id} not connected")
            return  # Don't send commands to disconnected drones
        
        logger.info(f"Land requested for {self.id}")
        self.telemetry["z"] = 0.0
        
        if self.handler and hasattr(self.handler, "land"):
            self.handler.land()

    def move_to(self, x: float, y: float, z: float, yaw: float = 0.0) -> None:
        if not self.is_connected():
            return  # Don't send commands to disconnected drones
        self.telemetry["x"] = x
        self.telemetry["y"] = y
        self.telemetry["z"] = z
        self.telemetry["yaw"] = yaw
        if self.handler and hasattr(self.handler, "move_to"):
            self.handler.move_to(x, y, z, yaw)

    def stop(self):
        # Stop should always try to execute even if disconnected (safety)
        if self.handler and hasattr(self.handler, "stop"):
            self.handler.stop()

    def reset_position(self) -> None:
        self.telemetry["x"] = 0.0
        self.telemetry["y"] = 0.0
        if self.handler and hasattr(self.handler, "reset_position"):
            self.handler.reset_position()

    def close(self) -> None:
        self.connected = False
        if self.handler and hasattr(self.handler, "close"):
            self.handler.close()

    def disconnect(self) -> None:
        """Compatibility alias for tests that call `disconnect()`.

        Should be idempotent.
        """
        try:
            self.close()
        except Exception:
            pass

    # --- Telemetry/Battery/LED Updates ---
    def update_telemetry(self, x: Optional[float] = None, y: Optional[float] = None, z: Optional[float] = None, yaw: Optional[float] = None) -> None:
        if x is not None:
            self.telemetry["x"] = x
        if y is not None:
            self.telemetry["y"] = y
        if z is not None:
            self.telemetry["z"] = z
        if yaw is not None:
            self.telemetry["yaw"] = yaw

    def update_battery(self, voltage: float, pm_state: int = 0) -> None:
        self.battery["voltage"] = voltage
        self.battery["percent"] = voltage_to_percent(voltage)
        self.battery["pm_state"] = pm_state
        self.battery["is_charging"] = pm_state_is_charging(pm_state)

    def set_led(self, color: str, effect: Optional[str] = None) -> None:
        self.led["color"] = color
        self.led["effect"] = effect
        # Optionally call handler/LED utils here

    # --- Getters for convenience ---
    def get_x(self) -> float:
        if self.handler and hasattr(self.handler, 'telemetry'):
            return self.handler.telemetry.get("x", self.telemetry["x"])
        return self.telemetry["x"]

    def get_y(self) -> float:
        if self.handler and hasattr(self.handler, 'telemetry'):
            return self.handler.telemetry.get("y", self.telemetry["y"])
        return self.telemetry["y"]

    def get_z(self) -> float:
        if self.handler and hasattr(self.handler, 'telemetry'):
            return self.handler.telemetry.get("z", self.telemetry["z"])
        return self.telemetry["z"]

    def get_yaw(self) -> float:
        if self.handler and hasattr(self.handler, 'telemetry'):
            return self.handler.telemetry.get("yaw", self.telemetry["yaw"])
        return self.telemetry["yaw"]

    def is_connected(self) -> bool:
        if self.handler:
            return getattr(self.handler, 'connected', False)
        return self.connected

    def get_drone_connected(self) -> bool:
        """Alias for is_connected() - used by websocket_client.py"""
        return self.is_connected()

    def is_armed(self) -> bool:
        return bool(self.armed & 16)

    @property
    def battery_voltage(self) -> float:
        """Get battery voltage from handler or local state"""
        if self.handler and hasattr(self.handler, 'battery'):
            return self.handler.battery.get("voltage", self.battery["voltage"])
        return self.battery["voltage"]

    @property
    def battery_status(self) -> str:
        """Return a human-readable battery status, preferring handler-provided value."""
        # Prefer handler-provided property if available
        if self.handler and hasattr(self.handler, 'battery_status'):
            try:
                return getattr(self.handler, 'battery_status')
            except Exception:
                pass
        # Fallback: use handler fields if present
        try:
            if self.handler:
                pm = getattr(self.handler, 'pm_state', None)
                from utils.battery import pm_state_is_charging, voltage_to_percent
                if pm is not None and pm_state_is_charging(pm):
                    return "Charging"
                volt = getattr(self.handler, 'battery_voltage', None)
                if volt is None:
                    volt = self.battery.get('voltage', 0.0)
                pct = voltage_to_percent(volt)
                if pct <= 0.0:
                    return "Empty"
                if pct < 20.0:
                    return "Low"
                return "Ok"
        except Exception:
            pass
        # Last resort: use stored battery percent
        p = self.battery.get('percent', 0.0)
        if p <= 0.0:
            return "Empty"
        if p < 20.0:
            return "Low"
        return "Ok"
