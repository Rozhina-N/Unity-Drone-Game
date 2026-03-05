"""Telemetry management for Crazyflie drones.

This module handles all telemetry logging: position, battery, lighthouse status.
Extracted from DroneController as part of god-class refactoring (Phase 2).
"""

import logging
from threading import Lock
from typing import Optional, Any, Type, Dict, Union, List, Callable


class TelemetryHandler:
    """Manages telemetry logging for a single drone.
    
    Responsibilities:
    - Position logging (x, y, z, yaw)
    - Battery logging (voltage, percent, charging state)
    - Lighthouse status logging
    - Thread-safe data access
    - Log config lifecycle management
    
    Callback Architecture:
    - on_telemetry_update: Called when any telemetry data changes
    """
    
    def __init__(
        self,
        drone_id: str,
        log_config_cls: Optional[Type[Any]] = None,
        logger: Optional[logging.Logger] = None
    ):
        """Initialize telemetry handler.
        
        Args:
            drone_id: Unique drone identifier
            log_config_cls: LogConfig class from cflib (for creating log subscriptions)
            logger: Logger instance (defaults to new logger for drone)
        """
        self.drone_id = drone_id
        self._log_config_cls = log_config_cls
        self.logger = logger or logging.getLogger(f"Telemetry.{drone_id}")
        
        # Thread-safe data storage
        self._lock = Lock()
        self.telemetry: Dict[str, float] = {"x": 0.0, "y": 0.0, "z": 0.0, "yaw": 0.0}
        self.battery: Dict[str, Union[float, int, bool]] = {
            "voltage": 0.0,
            "percent": 0.0,
            "is_charging": False,
            "pm_state": 0
        }
        self.lighthouse_status = 0
        
        # Active log configurations
        self._logconfs: List[Any] = []
        
        # Optional callback for telemetry updates
        self.on_telemetry_update: Optional[Callable[[], None]] = None
    
    def setup(self, cf: Any) -> None:
        """Set up telemetry logging for a connected Crazyflie.
        
        Creates and starts three log configurations:
        1. Position (x, y, z, yaw) - 100ms interval
        2. Battery (voltage, pm_state) - 500ms interval
        3. Lighthouse status - 500ms interval
        
        Args:
            cf: Connected Crazyflie instance
        """
        if not self._log_config_cls or not cf:
            self.logger.warning("Cannot setup telemetry: missing LogConfig class or Crazyflie instance")
            return
        
        self.logger.info(f"Setting up telemetry logging for drone {self.drone_id}")
        
        try:
            self._setup_position_logging(cf)
            self._setup_battery_logging(cf)
            self._setup_lighthouse_logging(cf)
            self.logger.info(f"Telemetry logging started ({len(self._logconfs)} log configs)")
        except Exception as e:
            self.logger.error(f"Failed to setup telemetry: {e}")
    
    def _setup_position_logging(self, cf: Any) -> None:
        """Set up position telemetry logging (x, y, z, yaw)."""
        pos = self._log_config_cls(name='pos', period_in_ms=100)
        pos.add_variable('stateEstimate.x', 'float')
        pos.add_variable('stateEstimate.y', 'float')
        pos.add_variable('stateEstimate.z', 'float')
        pos.add_variable('stabilizer.yaw', 'float')
        
        def _pos_cb(ts: Any, data: Dict[str, Any], logconf: Any) -> None:
            """Callback for position data updates."""
            with self._lock:
                self.telemetry["x"] = float(data.get('stateEstimate.x', self.telemetry["x"]))
                self.telemetry["y"] = float(data.get('stateEstimate.y', self.telemetry["y"]))
                self.telemetry["z"] = float(data.get('stateEstimate.z', self.telemetry["z"]))
                self.telemetry["yaw"] = float(data.get('stabilizer.yaw', self.telemetry["yaw"]))
            
            # Notify listeners
            if self.on_telemetry_update is not None:
                try:
                    self.on_telemetry_update()
                except Exception as e:
                    self.logger.error(f"Error in telemetry update callback: {e}")
        
        pos.data_received_cb.add_callback(_pos_cb)
        cf.log.add_config(pos)
        pos.start()
        self._logconfs.append(pos)
    
    def _setup_battery_logging(self, cf: Any) -> None:
        """Set up battery telemetry logging (voltage, state, charging)."""
        from utils.battery import voltage_to_percent, pm_state_is_charging
        
        bat = self._log_config_cls(name='bat', period_in_ms=500)
        bat.add_variable('pm.vbat', 'float')
        bat.add_variable('pm.state', 'uint8_t')
        
        def _bat_cb(ts: Any, data: Dict[str, Any], logconf: Any) -> None:
            """Callback for battery data updates."""
            with self._lock:
                v = float(data.get('pm.vbat', self.battery["voltage"]))
                pm = int(data.get('pm.state', self.battery["pm_state"]))
                self.battery["voltage"] = v
                self.battery["percent"] = voltage_to_percent(v)
                self.battery["pm_state"] = pm
                self.battery["is_charging"] = pm_state_is_charging(pm)
            
            # Notify listeners
            if self.on_telemetry_update is not None:
                try:
                    self.on_telemetry_update()
                except Exception as e:
                    self.logger.error(f"Error in telemetry update callback: {e}")
        
        bat.data_received_cb.add_callback(_bat_cb)
        cf.log.add_config(bat)
        bat.start()
        self._logconfs.append(bat)
    
    def _setup_lighthouse_logging(self, cf: Any) -> None:
        """Set up lighthouse status telemetry logging."""
        lh_log = self._log_config_cls(name='lh_status', period_in_ms=500)
        lh_log.add_variable('lighthouse.status', 'uint8_t')
        
        def _lh_cb(ts: Any, data: Dict[str, Any], logconf: Any) -> None:
            """Callback for lighthouse status updates."""
            with self._lock:
                self.lighthouse_status = int(data.get('lighthouse.status', self.lighthouse_status))
            
            # Notify listeners
            if self.on_telemetry_update is not None:
                try:
                    self.on_telemetry_update()
                except Exception as e:
                    self.logger.error(f"Error in telemetry update callback: {e}")
        
        lh_log.data_received_cb.add_callback(_lh_cb)
        cf.log.add_config(lh_log)
        lh_log.start()
        self._logconfs.append(lh_log)
    
    def stop(self) -> None:
        """Stop all telemetry logging and clear log configs."""
        with self._lock:
            for logconf in self._logconfs:
                try:
                    logconf.stop()
                    logconf.delete()
                except Exception as e:
                    self.logger.debug(f"Error stopping log config: {e}")
            self._logconfs = []
        self.logger.info(f"Telemetry logging stopped for drone {self.drone_id}")
    
    def get_telemetry(self) -> Dict[str, float]:
        """Get current position telemetry (thread-safe).
        
        Returns:
            Dict with keys: x, y, z, yaw
        """
        with self._lock:
            return self.telemetry.copy()
    
    def get_battery(self) -> Dict[str, Union[float, int, bool]]:
        """Get current battery status (thread-safe).
        
        Returns:
            Dict with keys: voltage, percent, is_charging, pm_state
        """
        with self._lock:
            return self.battery.copy()
    
    def get_lighthouse_status(self) -> int:
        """Get current lighthouse status (thread-safe).
        
        Returns:
            Lighthouse status code (0=not detected, 2=ready)
        """
        with self._lock:
            return self.lighthouse_status
    
    def reset(self) -> None:
        """Reset all telemetry data to initial values."""
        with self._lock:
            self.telemetry = {"x": 0.0, "y": 0.0, "z": 0.0, "yaw": 0.0}
            self.battery = {
                "voltage": 0.0,
                "percent": 0.0,
                "is_charging": False,
                "pm_state": 0
            }
            self.lighthouse_status = 0
        self.logger.debug(f"Telemetry data reset for drone {self.drone_id}")
