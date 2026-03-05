import logging
from threading import Lock
from typing import Optional, Any, Type, Dict, Union, Callable
from config_service import ConfigurationService
from .connection_manager import ConnectionManager
from .telemetry_handler import TelemetryHandler
from .command_executor import CommandExecutor
try:
    from cflib.utils.power_switch import PowerSwitch  # type: ignore
except ImportError:
    PowerSwitch = None
# Expose the lightweight Drone wrapper for tests and UI convenience
try:
    from .drone import Drone  # noqa: F401
    drone_class: Optional[Type[Any]] = Drone
except Exception:
    try:
        from drones.drone import Drone  # noqa: ignore
        drone_class = Drone
    except Exception:
        drone_class = None

class DroneController:
    """Facade for drone operations, delegates to specialized components.
    
    Components:
    - ConnectionManager: Handles connection lifecycle
    - TelemetryHandler: Manages telemetry logging (position, battery, lighthouse)
    - CommandExecutor: Executes flight commands (takeoff, land, move_to, stop)
    
    This class now serves as a thin facade, delegating all major operations
    to specialized components while maintaining backward compatibility.
    """
    
    def __init__(
        self,
        drone_id: str,
        address: str,
        cflib: Optional[Any] = None,
        crazyflie_cls: Optional[Type[Any]] = None,
        log_config_cls: Optional[Type[Any]] = None,
        shutdown_event: Optional[Any] = None,
        cache_dir: Optional[str] = None,
        config: Optional[ConfigurationService] = None
    ):
        self.id = drone_id
        self.address = address
        self._config = config
        self._lock = Lock()
        self.logger = logging.getLogger(f"Drone.{drone_id}")
        self._shutdown_event = shutdown_event

        self.on_state_change: Optional[Callable[[], None]] = None

        # Connection management (delegated to ConnectionManager)
        self._connection_mgr = ConnectionManager(
            drone_id=drone_id,
            address=address,
            cflib=cflib,
            crazyflie_cls=crazyflie_cls,
            shutdown_event=shutdown_event,
            cache_dir=cache_dir,
            config=config,
            logger=self.logger
        )
        # Set up connection callbacks
        self._connection_mgr.on_fully_connected = self._on_fully_connected
        self._connection_mgr.on_disconnected = self._on_disconnected
        self._connection_mgr.on_connection_failed = self._on_connection_failed
        self._connection_mgr.on_state_change = lambda: self.on_state_change() if self.on_state_change else None
        
        # Telemetry management (delegated to TelemetryHandler)
        self._telemetry = TelemetryHandler(
            drone_id=drone_id,
            log_config_cls=log_config_cls,
            logger=self.logger
        )
        self._telemetry.on_telemetry_update = lambda: self.on_state_change() if self.on_state_change else None
        
        # Command execution (delegated to CommandExecutor)
        self._commands = CommandExecutor(
            drone_id=drone_id,
            config=config,
            logger=self.logger
        )
        # Set up command callbacks for accessing drone state
        self._commands.get_cf = lambda: self._connection_mgr.get_cf()
        self._commands.get_connected = lambda: self._connection_mgr.is_connected()
        self._commands.get_lighthouse_status = lambda: self._telemetry.get_lighthouse_status()
        self._commands.get_battery = lambda: self._telemetry.get_battery()
    
    # ========== Connection State Properties (delegate to ConnectionManager) ==========
    
    @property
    def connected(self) -> bool:
        """Check if drone is connected."""
        return self._connection_mgr.is_connected()
    
    @connected.setter
    def connected(self, value: bool) -> None:
        """Setter for backward compatibility (used in some tests)."""
        with self._connection_mgr._lock:
            self._connection_mgr.connected = value
    
    @property
    def connecting(self) -> bool:
        """Check if drone is in connecting state."""
        return self._connection_mgr.is_connecting()
    
    @connecting.setter
    def connecting(self, value: bool) -> None:
        """Setter for backward compatibility (used in some tests)."""
        with self._connection_mgr._lock:
            self._connection_mgr.connecting = value
    
    @property
    def _cf(self) -> Optional[Any]:
        """Get Crazyflie object."""
        return self._connection_mgr.get_cf()
    
    @_cf.setter
    def _cf(self, value: Optional[Any]) -> None:
        """Setter for backward compatibility."""
        with self._connection_mgr._lock:
            self._connection_mgr._cf = value
    
    def is_attempting_connect(self) -> bool:
        """Check if connection attempt is in progress."""
        return self._connection_mgr.is_attempting_connect()
    
    def has_pending_reconnect(self) -> bool:
        """Check if reconnect is scheduled."""
        return self._connection_mgr.has_pending_reconnect()
    
    # ========== Telemetry Properties (delegate to TelemetryHandler) ==========
    
    @property
    def telemetry(self) -> Dict[str, float]:
        """Get current position telemetry (thread-safe)."""
        return self._telemetry.get_telemetry()
    
    @telemetry.setter
    def telemetry(self, value: Dict[str, float]) -> None:
        """Setter for backward compatibility (used in some tests)."""
        with self._telemetry._lock:
            self._telemetry.telemetry.update(value)
    
    @property
    def battery(self) -> Dict[str, Union[float, int, bool]]:
        """Get current battery status (thread-safe)."""
        return self._telemetry.get_battery()
    
    @battery.setter
    def battery(self, value: Dict[str, Union[float, int, bool]]) -> None:
        """Setter for backward compatibility (used in some tests)."""
        with self._telemetry._lock:
            self._telemetry.battery.update(value)
    
    @property
    def lighthouse_status(self) -> int:
        """Get current lighthouse status (thread-safe)."""
        return self._telemetry.get_lighthouse_status()
    
    @lighthouse_status.setter
    def lighthouse_status(self, value: int) -> None:
        """Setter for backward compatibility (used in some tests)."""
        with self._telemetry._lock:
            self._telemetry.lighthouse_status = value
    
    @property
    def armed(self) -> bool:
        """Check if drone is armed (thread-safe)."""
        return self._commands.get_armed()
    
    @armed.setter
    def armed(self, value: bool) -> None:
        """Setter for backward compatibility (used in some tests)."""
        self._commands.set_armed(value)
    
    # ========== Connection Methods (delegate to ConnectionManager) ==========

    def connect(self) -> None:
        """Initiate connection to the drone."""
        self._connection_mgr.connect()
    
    # ========== ConnectionManager Callbacks ==========
    
    def _on_fully_connected(self, cf: Any) -> None:
        """Called when drone is fully connected and ready."""
        self.logger.info(f"Fully connected, setting up telemetry for {self.address}")
        self._telemetry.setup(cf)
        self._enable_high_level_commander()
    
    def _on_disconnected(self) -> None:
        """Called when drone disconnects."""
        self._telemetry.stop()
        self._commands.cleanup()
    
    def _on_connection_failed(self, msg: str) -> None:
        """Called when connection attempt fails."""
        self.logger.warning(f"Connection failed for {self.address}: {msg}")
    
    def _enable_high_level_commander(self) -> None:
        """Enable the high-level commander by setting the commander.enHighLevel parameter."""
        cf = self._connection_mgr.get_cf()
        if cf and cf.param:
            try:
                cf.param.set_value('commander.enHighLevel', '1')
                cf.param.set_value('stabilizer.estimator', '1')
                self.logger.info("High-level commander enabled, estimator set to complementary")
            except Exception as e:
                self.logger.warning(f"Failed to enable high-level commander or set estimator: {e}")

    def close(self):
        """Close connection to drone."""
        # Clean up command executor (cancel timers)
        self._commands.cleanup()
        
        # Stop telemetry logging
        self._telemetry.stop()
        
        # Delegate connection cleanup to ConnectionManager
        self._connection_mgr.close()
        
        self.logger.info(f"Disconnected from {self.address}")

    def takeoff(self, height: float = 0.5, duration: float = 2.0, bypass_safety: bool = False) -> None:
        """Execute takeoff command."""
        self._commands.takeoff(height, duration, bypass_safety)

    def land(self) -> None:
        """Execute landing command."""
        self._commands.land()

    def stop(self) -> None:
        """Execute stop command (emergency stop)."""
        self._commands.stop()
    
    def move_to(self, x_target: float, y_target: float, z_target: float, yaw: Optional[float] = None) -> None:
        """Move drone to target position with rate limiting."""
        self._commands.move_to(x_target, y_target, z_target, yaw)
    
    def set_move_rate_hz(self, rate_hz: float) -> None:
        """Set move command rate limit in Hz."""
        self._commands.set_move_rate_hz(rate_hz)
    
    def set_move_rate_scale(self, scale: float) -> None:
        """Scale the base move rate by a factor."""
        self._commands.set_move_rate_scale(scale)
    
    def set_move_budget_hz(self, rate_hz: float, scale: float = 1.0) -> None:
        """Set base move rate budget and scale factor."""
        self._commands.set_move_budget_hz(rate_hz, scale)

    def power_cycle(self) -> None:
        """Power cycle the drone's STM chip."""
        import time
        self.logger.info("Power cycling STM")
        try:
            # Close existing connection
            self._telemetry.stop()
            self._connection_mgr.close()
            
            self.logger.info("Disconnected for power cycle")
            
            if PowerSwitch:
                ps = PowerSwitch(self.address)
                ps.stm_power_cycle()
                self.logger.info("Power cycle command sent, waiting for drone to restart...")
            else:
                self.logger.warning("PowerSwitch not available (cflib not imported)")
            
            # Wait for drone to restart before allowing reconnection
            time.sleep(3.0)
            self.logger.info("Power cycle complete, drone should be ready for reconnection")
        except Exception as e:
            self.logger.error(f"Error power cycling: {e}")

    def reset_position(self) -> None:
        """Reset position telemetry to origin."""
        self._telemetry.reset()

    def get_status(self) -> Dict[str, Any]:
        with self._lock:
            return {
                "id": self.id,
                "address": self.address,
                "connected": self.connected,
                "telemetry": dict(self.telemetry),
                "battery": dict(self.battery),
                "lighthouse_status": self.lighthouse_status,
            }
