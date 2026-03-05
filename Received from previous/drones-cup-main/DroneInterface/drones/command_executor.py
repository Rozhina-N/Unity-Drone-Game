"""Command execution for Crazyflie drones.

This module handles all flight commands: takeoff, land, move_to, stop.
Extracted from DroneController as part of god-class refactoring (Phase 3).
"""

import logging
import math
import time
import threading
from threading import Lock, Timer
from typing import Optional, Any, Tuple, Callable, Dict, Union
from config_service import ConfigurationService


class CommandExecutor:
    """Manages flight command execution for a single drone.
    
    Responsibilities:
    - Flight commands: takeoff, land, move_to, stop
    - Rate limiting for move_to commands
    - Position clamping (safety boundaries)
    - Armed state management
    - Timer-based command queuing
    
    Callback Architecture:
    - get_cf: Callback to retrieve current Crazyflie instance
    - get_connected: Callback to check connection status
    - get_lighthouse_status: Callback to check lighthouse status
    - get_battery: Callback to check battery/charging state
    """
    
    def __init__(
        self,
        drone_id: str,
        config: Optional[ConfigurationService] = None,
        logger: Optional[logging.Logger] = None
    ):
        """Initialize command executor.
        
        Args:
            drone_id: Unique drone identifier
            config: Configuration service for safety limits and rate settings
            logger: Logger instance (defaults to new logger for drone)
        """
        self.drone_id = drone_id
        self._config = config
        self.logger = logger or logging.getLogger(f"Commands.{drone_id}")
        
        # Armed state tracking
        self._lock = Lock()
        self.armed = False
        
        # Rate limiting for move_to commands
        self._move_lock = Lock()
        self._last_move_ts = 0.0
        self._pending_move: Optional[Tuple[float, float, float, float]] = None
        self._move_timer: Optional[Timer] = None
        
        # Configure rate limiting from config
        try:
            if config:
                self._base_move_rate_hz = config.performance.move_to_rate_hz
            else:
                self._base_move_rate_hz = 10.0
        except Exception:
            self._base_move_rate_hz = 10.0
        
        self._move_min_interval = (
            1.0 / self._base_move_rate_hz if self._base_move_rate_hz > 0.0 else 0.0
        )
        
        # Callbacks for accessing drone state (set by DroneController)
        self.get_cf: Optional[Callable[[], Optional[Any]]] = None
        self.get_connected: Optional[Callable[[], bool]] = None
        self.get_lighthouse_status: Optional[Callable[[], int]] = None
        self.get_battery: Optional[Callable[[], Dict[str, Union[float, int, bool]]]] = None
    
    def takeoff(self, height: float = 0.5, duration: float = 2.0, bypass_safety: bool = False) -> None:
        """Execute takeoff command.
        
        Args:
            height: Target takeoff height in meters
            duration: Duration of takeoff maneuver in seconds
            bypass_safety: If True, bypass safety checks (lighthouse, charging)
        """
        if not self.get_connected or not self.get_connected():
            self.logger.warning("Takeoff denied: Not connected")
            return
        
        cf = self.get_cf() if self.get_cf else None
        if not cf:
            self.logger.warning("Takeoff failed: not connected to drone")
            return
        
        # Reset kalman filter before takeoff
        try:
            cf.param.set_value('kalman.resetEstimation', '1')
            self.logger.info("Kalman filter reset")
        except Exception as e:
            self.logger.warning(f"Failed to reset Kalman filter: {e}")
        
        # Safety checks (unless bypassed)
        bypass_safety_checks = self._config.safety.bypass_safety_checks if self._config else True
        if not bypass_safety and not bypass_safety_checks:
            lighthouse_status = self.get_lighthouse_status() if self.get_lighthouse_status else 0
            if lighthouse_status != 2:
                self.logger.warning(
                    f"Takeoff denied: Lighthouse not ready (status={lighthouse_status}). "
                    "Set safety.bypass_safety_checks=True to override."
                )
                return
            
            battery = self.get_battery() if self.get_battery else {}
            if battery.get("is_charging", False):
                self.logger.warning(
                    "Takeoff denied: Charging cable attached. Remove cable or set "
                    "safety.bypass_safety_checks=True to override."
                )
                return
        
        # Determine which commander to use
        lighthouse_status = self.get_lighthouse_status() if self.get_lighthouse_status else 0
        use_hl_commander = (lighthouse_status == 2) and not bypass_safety
        
        if use_hl_commander and cf.high_level_commander:
            self.logger.info(f"Takeoff to {height}m (high-level commander)")
            cf.high_level_commander.takeoff(height, duration)
            with self._lock:
                self.armed = True
        elif cf.commander:
            self.logger.info("Performing manual takeoff pulse (no positioning system)")
            
            def manual_takeoff_pulse():
                """Manual thrust pulse for takeoff without positioning."""
                commander = cf.commander
                # Unlock the commander by sending a 0-thrust setpoint
                commander.send_setpoint(0, 0, 0, 0)
                time.sleep(0.1)
                
                # Send a pulse of thrust
                self.logger.info(f"Sending thrust pulse for {duration} seconds...")
                end_time = time.time() + float(duration)
                while time.time() < end_time:
                    commander.send_setpoint(0, 0, 0, 40000)  # Thrust value may need tuning
                    time.sleep(0.02)
                
                # After the pulse, stop the thrust
                commander.send_setpoint(0, 0, 0, 0)
                self.logger.info("Manual takeoff pulse finished.")
                with self._lock:
                    self.armed = False  # Disarm after pulse
            
            with self._lock:
                self.armed = True
            t = threading.Thread(target=manual_takeoff_pulse, daemon=True)
            t.start()
        else:
            self.logger.warning("Takeoff failed: no high-level or standard commander available.")
    
    def land(self) -> None:
        """Execute landing command."""
        if not self.get_connected or not self.get_connected():
            self.logger.warning("Land denied: Not connected")
            return
        
        cf = self.get_cf() if self.get_cf else None
        if cf and cf.high_level_commander:
            self.logger.info("Landing")
            cf.high_level_commander.land(0.0, 2.0)
            with self._lock:
                self.armed = False
        else:
            self.logger.warning("Land failed: high_level_commander not available")
    
    def stop(self) -> None:
        """Execute stop command (emergency stop)."""
        cf = self.get_cf() if self.get_cf else None
        if cf and cf.commander:
            self.logger.info("Stopping")
            cf.commander.send_stop_setpoint()
            with self._lock:
                self.armed = False
        else:
            self.logger.warning("Stop failed: commander not available")
    
    def move_to(self, x_target: float, y_target: float, z_target: float, yaw: Optional[float] = None) -> None:
        """Move drone to target position with rate limiting.
        
        Args:
            x_target: Target x coordinate in meters
            y_target: Target y coordinate in meters
            z_target: Target z coordinate in meters
            yaw: Target yaw angle in degrees (optional, defaults to 0)
        """
        if not self.get_connected or not self.get_connected():
            return  # Don't send commands to disconnected drones
        
        # Validate and convert inputs
        try:
            x = float(x_target)
            y = float(y_target)
            z = float(z_target)
        except Exception:
            self.logger.debug("Move ignored: invalid position target")
            return
        
        try:
            yaw_f = 0.0 if yaw is None else float(yaw)
        except Exception:
            self.logger.debug("Move ignored: invalid yaw target")
            return
        
        if not (math.isfinite(x) and math.isfinite(y) and math.isfinite(z) and math.isfinite(yaw_f)):
            self.logger.debug("Move ignored: non-finite target")
            return
        
        # Clamp to safety boundaries
        x, y, z = self._clamp_move_target(x, y, z)
        
        # Queue with rate limiting
        self._queue_move_to(x, y, z, yaw_f)
    
    def _clamp_move_target(
        self,
        x_target: float,
        y_target: float,
        z_target: float
    ) -> Tuple[float, float, float]:
        """Clamp position targets to configured safety boundaries.
        
        Args:
            x_target: Desired x coordinate
            y_target: Desired y coordinate
            z_target: Desired z coordinate
        
        Returns:
            Tuple of clamped (x, y, z) coordinates
        """
        if self._config:
            max_x = self._config.drone.max_x
            max_y = self._config.drone.max_y
            max_z = self._config.drone.max_z
        else:
            max_x = max_y = max_z = 1.0
        
        x_target = max(-max_x, min(max_x, x_target))
        y_target = max(-max_y, min(max_y, y_target))
        z_target = max(0.0, min(max_z, z_target))
        return x_target, y_target, z_target
    
    def _send_position_setpoint(self, x_target: float, y_target: float, z_target: float, yaw: float) -> None:
        """Send position setpoint to drone commander.
        
        Args:
            x_target: Target x coordinate in meters
            y_target: Target y coordinate in meters
            z_target: Target z coordinate in meters
            yaw: Target yaw angle in degrees
        """
        if not self.get_connected or not self.get_connected():
            return  # Don't send commands to disconnected drones
        
        cf = self.get_cf() if self.get_cf else None
        if cf and cf.commander:
            try:
                cf.commander.send_position_setpoint(x_target, y_target, z_target, yaw)
            except Exception as e:
                self.logger.warning(f"Move failed: {e}")
        else:
            self.logger.warning("Move failed: commander not available")
    
    def _queue_move_to(self, x_target: float, y_target: float, z_target: float, yaw: float) -> None:
        """Queue move command with rate limiting.
        
        Implements rate limiting to prevent overwhelming the drone with too many
        position commands. If rate limit allows, sends immediately. Otherwise,
        queues command and starts timer for delayed send.
        
        Args:
            x_target: Target x coordinate in meters
            y_target: Target y coordinate in meters
            z_target: Target z coordinate in meters
            yaw: Target yaw angle in degrees
        """
        if self._move_min_interval <= 0.0:
            # No rate limiting, send immediately
            self._send_position_setpoint(x_target, y_target, z_target, yaw)
            return
        
        now = time.monotonic()
        payload = None
        send_now = False
        
        with self._move_lock:
            # Always update pending move (overwrites previous queued move)
            self._pending_move = (x_target, y_target, z_target, yaw)
            
            elapsed = now - self._last_move_ts
            if elapsed >= self._move_min_interval and self._move_timer is None:
                # Enough time has passed, send immediately
                payload = self._pending_move
                self._pending_move = None
                self._last_move_ts = now
                send_now = True
            elif self._move_timer is None:
                # Need to wait, start timer
                delay = max(0.0, self._move_min_interval - elapsed)
                t = Timer(delay, self._flush_pending_move)
                t.daemon = True
                self._move_timer = t
                try:
                    t.start()
                except Exception:
                    self._move_timer = None
        
        if send_now and payload:
            self._send_position_setpoint(*payload)
    
    def _flush_pending_move(self) -> None:
        """Flush pending move command (called by timer)."""
        payload = None
        with self._move_lock:
            self._move_timer = None
            if self._pending_move is not None:
                payload = self._pending_move
                self._pending_move = None
                self._last_move_ts = time.monotonic()
        
        if payload:
            self._send_position_setpoint(*payload)
    
    def _cancel_move_timer(self) -> None:
        """Cancel pending move timer and clear queued move."""
        with self._move_lock:
            t = self._move_timer
            self._move_timer = None
            self._pending_move = None
        
        if t:
            try:
                t.cancel()
            except Exception:
                pass
    
    def set_move_rate_hz(self, rate_hz: float) -> None:
        """Set move command rate limit in Hz.
        
        Args:
            rate_hz: Maximum rate for move commands in Hz (0 = no limit)
        """
        try:
            rate = float(rate_hz)
        except Exception:
            rate = 0.0
        
        try:
            self._cancel_move_timer()
        except Exception:
            pass
        
        with self._move_lock:
            self._move_min_interval = 1.0 / rate if rate > 0.0 else 0.0
            self._last_move_ts = 0.0
    
    def set_move_rate_scale(self, scale: float) -> None:
        """Scale the base move rate by a factor.
        
        Args:
            scale: Scale factor (1.0 = base rate, 2.0 = half rate, etc.)
        """
        try:
            scale_f = float(scale)
        except Exception:
            scale_f = 1.0
        
        if not self._base_move_rate_hz or self._base_move_rate_hz <= 0.0:
            self.set_move_rate_hz(0.0)
            return
        
        if scale_f <= 0.0:
            scale_f = 1.0
        
        self.set_move_rate_hz(self._base_move_rate_hz / scale_f)
    
    def set_move_budget_hz(self, rate_hz: float, scale: float = 1.0) -> None:
        """Set base move rate budget and scale factor.
        
        Args:
            rate_hz: Base move rate in Hz
            scale: Scale factor to apply
        """
        try:
            base = float(rate_hz)
        except Exception:
            base = 0.0
        
        self._base_move_rate_hz = base
        self.set_move_rate_scale(scale)
    
    def cleanup(self) -> None:
        """Clean up resources (cancel timers)."""
        try:
            self._cancel_move_timer()
        except Exception:
            pass
    
    def get_armed(self) -> bool:
        """Check if drone is armed (thread-safe).
        
        Returns:
            True if drone is armed (in flight or ready to fly)
        """
        with self._lock:
            return self.armed
    
    def set_armed(self, value: bool) -> None:
        """Set armed state (thread-safe).
        
        Args:
            value: Armed state to set
        """
        with self._lock:
            self.armed = value
