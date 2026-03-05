"""
Connection Manager for Drone Controller

Handles all connection lifecycle operations including:
- Connection establishment and teardown
- Reconnection with exponential backoff
- Heartbeat monitoring and liveness detection
- Connection state management
- Connection callbacks and event handling

Extracted from DroneController to improve separation of concerns.
CODE_VERSION: 2025-01-05-22-00-FORCE-RELOAD-TEST
"""

import logging
import random
import time
from threading import Lock, Timer
from typing import Optional, Any, Callable

# STARTUP CHECK - will print to console AND log if new code loads
_CONN_MGR_VERSION = "2025-01-05-22-05-FILE-WRITE-TEST"
print(f"[MODULE LOAD] connection_manager.py VERSION {_CONN_MGR_VERSION}")

# Write to a file to prove module loaded (since logging might not be ready)
try:
    import os
    _test_file = os.path.join(os.path.dirname(__file__), "..", "logs", "module_load.txt")
    with open(_test_file, "a") as f:
        import datetime
        f.write(f"{datetime.datetime.now()}: connection_manager.py VERSION {_CONN_MGR_VERSION} LOADED\n")
except Exception as e:
    pass  # Silent fail

try:
    import logging
    _startup_logger = logging.getLogger("ConnectionManager.Startup")
    _startup_logger.info(f"[MODULE LOAD] connection_manager.py VERSION {_CONN_MGR_VERSION}")
except:
    pass


# Connection timeout in seconds - if no connection established, reset is_connecting
CONNECTION_TIMEOUT = 60.0


class ConnectionManager:
    """Manages connection lifecycle for a single drone.
    
    Responsibilities:
    - Establish and maintain Crazyflie radio link
    - Handle connection state transitions
    - Implement reconnection with exponential backoff
    - Monitor connection health via heartbeat
    - Coordinate with cflib Crazyflie object
    """
    
    def __init__(
        self,
        drone_id: str,
        address: str,
        cflib: Optional[Any] = None,
        crazyflie_cls: Optional[Any] = None,
        shutdown_event: Optional[Any] = None,
        cache_dir: Optional[str] = None,
        config: Optional[Any] = None,
        logger: Optional[logging.Logger] = None
    ):
        """Initialize connection manager.
        
        Args:
            drone_id: Unique identifier for the drone
            address: Radio address (e.g., "radio://0/80/2M/E7E7E7E7E1")
            cflib: Crazyflie library module
            crazyflie_cls: Crazyflie class constructor
            shutdown_event: Threading event to signal shutdown
            cache_dir: Directory for caching connection data
            config: Configuration service instance
            logger: Logger instance (will create one if not provided)
        """
        self.drone_id = drone_id
        self.address = address
        self._cflib = cflib
        self._crazyflie_cls = crazyflie_cls
        self._shutdown_event = shutdown_event
        self._cache_dir = cache_dir
        self._config = config
        self.logger = logger or logging.getLogger(f"Drone.{drone_id}.Connection")
        
        # Connection state
        self._lock = Lock()
        self.connected = False
        self.connecting = False  # UI "connecting" state (set after link responds)
        self._connect_in_progress = False  # Track whether a link attempt is in flight
        self._abort_connect = False
        
        # Crazyflie object (managed by this class)
        self._cf: Optional[Any] = None
        
        # Connection timeout timer
        self._connection_timer: Optional[Timer] = None
        
        # Reconnect/backoff state
        self._reconnect_attempts = 0
        self._reconnect_timer: Optional[Timer] = None
        self._reconnect_base = 1.0
        self._reconnect_max = 60.0
        
        # Heartbeat/liveness monitoring
        self._last_heartbeat: Optional[float] = None
        self._heartbeat_interval = 5.0
        self._heartbeat_timer: Optional[Timer] = None
        self._stale_threshold = 15.0
        
        # Callbacks for connection events
        self.on_fully_connected: Optional[Callable[[Any], None]] = None
        self.on_disconnected: Optional[Callable[[], None]] = None
        self.on_connection_failed: Optional[Callable[[str], None]] = None
        self.on_state_change: Optional[Callable[[], None]] = None
    
    def get_cf(self) -> Optional[Any]:
        """Get the Crazyflie object (thread-safe)."""
        with self._lock:
            return self._cf
    
    def is_connected(self) -> bool:
        """Check if drone is connected (thread-safe)."""
        with self._lock:
            return self.connected
    
    def is_connecting(self) -> bool:
        """Check if drone is in connecting state (thread-safe)."""
        with self._lock:
            return self.connecting
    
    def is_attempting_connect(self) -> bool:
        """Check if a connection attempt is in progress (thread-safe)."""
        with self._lock:
            return bool(self._connect_in_progress)
    
    def has_pending_reconnect(self) -> bool:
        """Check if a reconnect timer is scheduled (thread-safe)."""
        with self._lock:
            return self._reconnect_timer is not None
    
    def connect(self) -> None:
        """Initiate connection to the drone.
        
        This method is idempotent and safe to call multiple times.
        Will skip if already connected or connecting.
        """
        self.logger.debug(
            f"connect() called, connected={self.connected}, connecting={self.connecting}, "
            f"in_progress={self._connect_in_progress}"
        )
        
        if not self._cflib or not self._crazyflie_cls:
            self.logger.error("Crazyflie library/classes not provided!")
            return
        
        if self._shutdown_event and self._shutdown_event.is_set():
            self.logger.info(f"Shutdown event set, aborting connect for {self.address}")
            return
        
        # Cancel any scheduled reconnect
        self._cancel_reconnect_timer()
        
        # Check and close any stale connection outside of the main lock
        # to avoid deadlock with _on_disconnected callback
        cf_to_close = None
        with self._lock:
            if self.connected:
                self.logger.debug("Already connected, skipping connect")
                return
            
            # Don't start a new connection if one is already in progress
            if self.connecting or self._connect_in_progress:
                self.logger.debug("Already connecting, skipping connect")
                return
            
            # Only respect _abort_connect if it was set during shutdown
            if self._abort_connect and self._shutdown_event and self._shutdown_event.is_set():
                return
            
            # Mark for cleanup if there's a stale cf object
            if self._cf is not None:
                cf_to_close = self._cf
                self._cf = None
        
        # Close stale connection outside of lock to avoid deadlock
        if cf_to_close is not None:
            try:
                cf_to_close.close_link()
            except Exception:
                pass
        
        # Now acquire lock again to set up new connection
        with self._lock:
            # Re-check state in case it changed while we were closing the old link
            if self.connected or self.connecting or self._connect_in_progress:
                self.logger.debug("State changed during cleanup, skipping connect")
                return
            
            # Reset abort flag for this connection attempt
            self._abort_connect = False
            self._connect_in_progress = True  # Mark that we're attempting to open a link
            
            try:
                if self._cache_dir:
                    import os
                    ro_cache_dir = os.path.join(self._cache_dir, 'ro')
                    self.logger.debug(f"Using cache_dir={self._cache_dir}, ro_cache={ro_cache_dir}")
                    self._cf = self._crazyflie_cls(ro_cache=ro_cache_dir, rw_cache=self._cache_dir)
                else:
                    self._cf = self._crazyflie_cls()
            except Exception as e:
                self.logger.exception(f"Failed to create Crazyflie instance for {self.address}: {e}")
                # Reset attempt flag and schedule reconnect
                with self._lock:
                    self._connect_in_progress = False
                    self._cf = None
                try:
                    self._schedule_reconnect()
                except Exception:
                    pass
                return
            
            # Register callbacks
            if self._cf is None:
                self.logger.error(f"Failed to create Crazyflie instance for {self.address}")
                with self._lock:
                    self._connect_in_progress = False
                return
            
            self._cf.connected.add_callback(self._on_connected)
            self._cf.disconnected.add_callback(self._on_disconnected_internal)
            self._cf.connection_failed.add_callback(self._on_connection_failed_internal)
            self._cf.fully_connected.add_callback(self._on_fully_connected_internal)
            
            self._log_conn(f"Connecting to {self.address}", level="info", noisy=True)
            self._cf.open_link(self.address)
            
            # Start connection timeout timer
            def connection_timeout():
                schedule = False
                with self._lock:
                    if self._connect_in_progress and not self.connected:
                        self._log_conn(
                            f"Connection timeout for {self.address}, resetting connect attempt",
                            level="warning",
                            noisy=True,
                        )
                        self._connect_in_progress = False
                        self.connecting = False
                        schedule = True
                
                # Trigger UI update
                if self.on_state_change:
                    try:
                        self.on_state_change()
                    except Exception:
                        pass
                
                if schedule:
                    try:
                        self._schedule_reconnect()
                    except Exception:
                        pass
            
            self._connection_timer = Timer(CONNECTION_TIMEOUT, connection_timeout)
            self._connection_timer.daemon = True
            self._connection_timer.start()
    
    def close(self) -> None:
        """Close the connection and cleanup resources.
        
        Sets abort flag to prevent automatic reconnection.
        Safe to call multiple times.
        """
        self.logger.info(f"Closing connection to {self.address}")
        
        # Set abort flag and cancel timers
        with self._lock:
            self._abort_connect = True
        
        self._cancel_reconnect_timer()
        self._cancel_connection_timer()
        self._stop_heartbeat()
        
        # Close Crazyflie link outside of lock
        cf_to_close = None
        with self._lock:
            cf_to_close = self._cf
            self._cf = None
        
        if cf_to_close:
            try:
                cf_to_close.close_link()
            except Exception as e:
                self.logger.warning(f"Error closing link: {e}")
        
        self._reset_connection_state()
    
    def reset_connection_state(self) -> None:
        """Public interface to reset connection state."""
        self._reset_connection_state()
    
    def _reset_connection_state(self) -> None:
        """Reset connection state to allow reconnection attempts."""
        with self._lock:
            self.connected = False
            self.connecting = False
            self._connect_in_progress = False
            self._cf = None
    
    # ========== Internal Callbacks ==========
    
    def _on_connected(self, uri: str) -> None:
        """Internal callback when initial link is established."""
        self._cancel_connection_timer()
        with self._lock:
            self.logger.info(f"Link established: {uri}")
            self.connecting = True  # Only now, after initial response
            self._connect_in_progress = True
    
    def _on_fully_connected_internal(self, uri: str) -> None:
        """Internal callback when fully connected (all TOCs synced)."""
        try:
            # Prevent duplicate callback execution
            with self._lock:
                # If already marked as connected, this is a duplicate callback - ignore it
                if self.connected:
                    self.logger.warning(f"Ignoring duplicate fully_connected callback for {uri}")
                    return

            
            # VERSION MARKER - THIS PROVES NEW CODE IS RUNNING
            self.logger.info(">>>>>>> _on_fully_connected_internal VERSION 22-20 DUPLICATE-CHECK <<<<<<<<")
            
            try:
                self._cancel_connection_timer()
            except Exception as e:
                self.logger.error(f"ERROR canceling connection timer: {e}")
            
            # Update connection state within lock
            with self._lock:
                self.logger.info(f"Fully connected to {uri}")
                self.connected = True
                self.connecting = False  # Connection attempt completed successfully
                self._connect_in_progress = False
                # Reset backoff state on success
                self._reconnect_attempts = 0
                # Cancel any scheduled reconnects
                try:
                    self._cancel_reconnect_timer()
                except Exception:
                    pass
            
            self.logger.info(f"Connection state updated for {uri}")
            
            # Start heartbeat monitor (needs lock internally, so must be outside the above lock block)
            try:
                self._start_heartbeat()
            except Exception as e:
                self.logger.error(f"Heartbeat start failed: {e}")
            
            # Notify external callback
            if self.on_fully_connected:
                try:
                    self.on_fully_connected(self._cf)
                except Exception as e:
                    self.logger.error(f"Error in on_fully_connected callback: {e}")
                    import traceback
                    self.logger.error(f"Traceback: {traceback.format_exc()}")
        
        except Exception as callback_error:
            # CATCH ALL EXCEPTIONS in this callback
            self.logger.error(f"FATAL ERROR in _on_fully_connected_internal: {callback_error}")
            import traceback
            self.logger.error(f"Full traceback:\n{traceback.format_exc()}")
            # Re-raise to let cflib know something went wrong
            raise
    
    def _on_disconnected_internal(self, uri: str) -> None:
        """Internal callback when connection is lost."""
        try:
            self.logger.info(f"[DISCONNECT] Starting disconnect handler for {uri}")
            self._cancel_connection_timer()
            
            with self._lock:
                was_connected = bool(self.connected)
            
            if was_connected:
                self.logger.info(f"Disconnected: {uri}")
            else:
                self._log_conn(f"Disconnected: {uri}", level="info", noisy=True)
            
            self.logger.info(f"[DISCONNECT] Resetting connection state for {uri}")
            self._reset_connection_state()
            
            # Stop heartbeat
            self.logger.info(f"[DISCONNECT] Stopping heartbeat for {uri}")
            try:
                self._stop_heartbeat()
            except Exception as e:
                self.logger.error(f"Error stopping heartbeat: {e}")
            
            # Notify external callback
            self.logger.info(f"[DISCONNECT] Calling external on_disconnected callback for {uri}")
            if self.on_disconnected:
                try:
                    self.on_disconnected()
                except Exception as e:
                    self.logger.exception(f"Error in on_disconnected callback: {e}")
            
            # Schedule reconnect unless aborting or shutting down
            self.logger.info(f"[DISCONNECT] Scheduling reconnect for {uri}")
            try:
                self._schedule_reconnect()
            except Exception as e:
                self.logger.error(f"Error scheduling reconnect: {e}")
            
            self.logger.info(f"[DISCONNECT] Completed disconnect handler for {uri}")
        except Exception as fatal_error:
            self.logger.error(f"FATAL ERROR in _on_disconnected_internal: {fatal_error}")
            import traceback
            self.logger.error(f"Traceback:\n{traceback.format_exc()}")
            pass
    
    def _on_connection_failed_internal(self, uri: str, msg: str) -> None:
        """Internal callback when connection attempt fails."""
        self._cancel_connection_timer()
        self._log_conn(f"Connection failed: {uri}: {msg}", level="warning", noisy=True)
        
        if self._cf:
            try:
                self._cf.close_link()
            except Exception:
                pass
        
        self._reset_connection_state()
        
        # Notify external callback
        if self.on_connection_failed:
            try:
                self.on_connection_failed(msg)
            except Exception as e:
                self.logger.exception(f"Error in on_connection_failed callback: {e}")
        
        try:
            self._schedule_reconnect()
        except Exception:
            pass
    
    # ========== Reconnection Logic ==========
    
    def _perform_reconnect(self) -> None:
        """Internal wrapper executed by timer to attempt reconnect."""
        with self._lock:
            self._reconnect_timer = None
        
        try:
            self.connect()
        except Exception:
            pass
    
    def _schedule_reconnect(self) -> None:
        """Schedule a reconnect attempt with exponential backoff and jitter."""
        with self._lock:
            if self._abort_connect:
                self.logger.debug("Reconnect aborted due to abort flag")
                return
            
            if self._shutdown_event and self._shutdown_event.is_set():
                self.logger.debug("Shutdown set; not scheduling reconnect")
                return
            
            # If already scheduled, skip
            if self._reconnect_timer is not None:
                self.logger.debug("Reconnect already scheduled, skipping")
                return
            
            # Compute backoff delay
            attempt = max(0, self._reconnect_attempts)
            delay = min(self._reconnect_max, self._reconnect_base * (2 ** attempt))
            # Add small jitter up to 20% of delay (max 5s)
            jitter = random.uniform(0, min(5.0, delay * 0.2))
            delay += jitter
            self._reconnect_attempts = attempt + 1
            
            self._log_conn(
                f"Scheduling reconnect to {self.address} in {delay:.1f}s (attempt {self._reconnect_attempts})",
                level="info",
                noisy=True,
            )
            
            t = Timer(delay, self._perform_reconnect)
            t.daemon = True
            self._reconnect_timer = t
            try:
                t.start()
            except Exception as e:
                self.logger.warning(f"Failed to start reconnect timer: {e}")
    
    def _cancel_reconnect_timer(self) -> None:
        """Cancel any pending reconnect timer."""
        if self._reconnect_timer is not None:
            try:
                self._reconnect_timer.cancel()
            except Exception:
                pass
            self._reconnect_timer = None
    
    # ========== Heartbeat Monitoring ==========
    
    def _start_heartbeat(self) -> None:
        """Start periodic heartbeat checks to detect stale connections."""
        with self._lock:
            self._last_heartbeat = time.time()
            if self._heartbeat_timer is not None:
                try:
                    self._heartbeat_timer.cancel()
                except Exception:
                    pass
            t = Timer(self._heartbeat_interval, self._heartbeat_tick)
            t.daemon = True
            self._heartbeat_timer = t
            t.start()
    
    def _stop_heartbeat(self) -> None:
        """Stop heartbeat monitoring."""
        with self._lock:
            if self._heartbeat_timer is not None:
                try:
                    self._heartbeat_timer.cancel()
                except Exception:
                    pass
                self._heartbeat_timer = None
    
    def _heartbeat_tick(self) -> None:
        """Heartbeat timer callback: check liveness and reschedule."""
        try:
            now = time.time()
            with self._lock:
                last = self._last_heartbeat or now
            
            # If connected, update last heartbeat timestamp
            if self.connected:
                with self._lock:
                    self._last_heartbeat = now
            else:
                # If not connected and last heartbeat is too old, schedule reconnect
                if now - last > self._stale_threshold:
                    self.logger.warning(f"Connection stale for {self.address}, scheduling reconnect")
                    try:
                        self._schedule_reconnect()
                    except Exception:
                        pass
            
            # Reschedule next heartbeat
            with self._lock:
                if not (self._shutdown_event and self._shutdown_event.is_set()):
                    t = Timer(self._heartbeat_interval, self._heartbeat_tick)
                    t.daemon = True
                    self._heartbeat_timer = t
                    t.start()
        except Exception:
            pass
    
    # ========== Timer Management ==========
    
    def _cancel_connection_timer(self) -> None:
        """Cancel the connection timeout timer if it exists."""
        if self._connection_timer:
            try:
                self._connection_timer.cancel()
            except Exception:
                pass
            self._connection_timer = None
    
    # ========== Logging Helpers ==========
    
    def _conn_verbose(self) -> bool:
        """Check if verbose connection logging is enabled."""
        if self._config:
            return self._config.development.connection_log_verbose
        return False
    
    def _log_conn(self, msg: str, level: str = "info", noisy: bool = False) -> None:
        """Log connection message with verbosity control.
        
        Args:
            msg: Message to log
            level: Log level (info, warning, error, debug)
            noisy: If True, only logs if verbose mode is enabled
        """
        if noisy and not self._conn_verbose():
            self.logger.debug(msg)
            return
        
        log_fn = getattr(self.logger, level, None)
        if not callable(log_fn):
            log_fn = self.logger.info
        log_fn(msg)
