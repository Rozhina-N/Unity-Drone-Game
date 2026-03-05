import threading
import signal
import tkinter as tk
import logging
import os
import traceback
from typing import Optional, List, Dict, Any

# Initialize application-wide logging early
try:
    from logging_config.logging_setup import init_logging
    # Avoid attaching a console StreamHandler; we run GUI-only and may free the console.
    # Use INFO as initial level; we may raise to DEBUG later once file/UI
    # handlers are attached.
    init_logging(level=logging.INFO, enable_console=False)
except Exception:
    pass

from ui import DroneUI
from websocket.websocket_client import WebSocketClient
from config_service import ConfigurationBuilder, ConfigurationService

# Load and build configuration
_config = ConfigurationBuilder.with_defaults().build()

# Apply pycache setting from config
if _config.development.disable_pycache:
    os.environ['PYTHONDONTWRITEBYTECODE'] = '1'
from drones.drone_manager import DroneManager
from drones.drone import Drone
try:
    from cflib.crazyflie import Crazyflie  # type: ignore
    from cflib.crazyflie.log import LogConfig  # type: ignore
    import cflib  # type: ignore
except ImportError:
    Crazyflie = None
    LogConfig = None
    cflib = None
from logging_config.logging_setup import init_file_logging
from ui import set_windows_app_id, free_console
from drones.drone_controller import DroneController
from drones.radio_handler import RadioPowerCycleHandler


class Application:
    def __init__(self, config: ConfigurationService, drones_config: Optional[List[Dict[str, str]]] = None):
        self.config = config
        if drones_config is None:
            drones_config = config.get_drones()
        self.websocket_uri = config.websocket.address
        self.manager = DroneManager(config=config)
        self.websocket_client: Optional[WebSocketClient] = None
        self.websocket_thread: Optional[threading.Thread] = None  # type: ignore

        # Initialization state tracking
        self.interfaces_found = False
        self.hardware_initialized = False
        self.controllers_ready = False
        self.shutdown_event: threading.Event = threading.Event()
        self.drone_threads: List[threading.Thread] = []
        self.drones_config = drones_config

        # Validate cache_dir synchronously (fast operation)
        try:
            cache_dir = self.config.development.cache_dir
            if cache_dir:
                try:
                    import os
                    os.makedirs(cache_dir, exist_ok=True)
                    testfile = os.path.join(cache_dir, '.write_test')
                    with open(testfile, 'w'):
                        pass
                    try:
                        os.remove(testfile)
                    except Exception:
                        pass
                except Exception:
                    logging.warning(f"cache_dir '{cache_dir}' not writable")
        except Exception:
            logging.exception("Failed to validate cache_dir")
        
        # Note: Radio initialization, controller creation, and power cycle
        # are now deferred to initialize_hardware() to enable fast UI startup

    def initialize_hardware(self) -> None:
        """Initialize radio hardware and create drone controllers in background.
        
        This method runs asynchronously after UI is displayed to avoid blocking startup.
        Performs: radio driver init, dongle detection, power cycle, controller creation.
        """
        logging.info("Starting hardware initialization...")
        
        # Step 1: Initialize Crazyflie radio drivers
        try:
            import cflib  # type: ignore
            cflib.crtp.init_drivers()  # type: ignore
            logging.info('cflib.crtp.init_drivers() called successfully')
            
            # Step 2: Check if Crazyradio dongle is connected
            try:
                from cflib.drivers.crazyradio import Crazyradio  # type: ignore
                cr = Crazyradio()
                if cr:
                    logging.info(f'Crazyradio detected (firmware version: {cr.version})')
                    self.interfaces_found = True
                    cr.close()
                else:
                    logging.warning('No Crazyradio dongle found.')
            except Exception as radio_err:
                logging.warning(f'No Crazyradio dongle found: {radio_err}')
        except Exception as e:
            logging.error(f'Failed to initialize cflib drivers: {e}')
        
        # Step 3: Power cycle radio
        try:
            radio_handler = RadioPowerCycleHandler()
            radio_result = radio_handler.handle()  # type: ignore
            if radio_result and radio_result.get('status') == 'ok':
                logging.info(radio_result.get('message'))
            elif radio_result and radio_result.get('status') == 'not_found':
                logging.warning(radio_result.get('message'))
            elif radio_result and radio_result.get('status') == 'error':
                logging.error(radio_result.get('message'))
        except Exception as e:
            logging.error(f'Radio power cycle failed: {e}')
        
        self.hardware_initialized = True
        logging.info("Hardware initialization complete")
        
        # Step 4: Create drone controllers
        self._create_drone_controllers()
    
    def _create_drone_controllers(self) -> None:
        """Create DroneController instances for all configured drones.
        
        Called after hardware initialization. Creates controller objects and
        registers them with the DroneManager.
        """
        logging.info("Creating drone controllers...")
        max_drones = self.config.drone.max_drones
        cache_dir = self.config.development.cache_dir
        
        for entry in self.drones_config[:max_drones]:
            did = entry.get('id')
            addr = entry.get('address')
            if not did or not addr:
                continue
            
            try:
                controller = DroneController(
                    did, addr, cflib, Crazyflie, LogConfig,
                    shutdown_event=self.shutdown_event,
                    cache_dir=cache_dir,
                    config=self.config
                )
                drone = Drone(did, addr, handler=controller)
                self.manager.add(did, drone)  # type: ignore
                logging.info(f"Created controller for drone {did}")
            except Exception as e:
                logging.error(f"Failed to create controller for drone {did}: {e}")
        
        self.controllers_ready = True
        logging.info(f"Drone controllers ready ({len(list(self.manager.handlers.values()))} drones)")

    def start_websocket(self):
        self.websocket_client = WebSocketClient(
            self.websocket_uri, self.manager, config=self.config  # type: ignore[arg-type]
        )
        self.websocket_thread = threading.Thread(target=self.websocket_client.start)
        self.websocket_thread.daemon = True  # type: ignore
        self.websocket_thread.start()  # type: ignore

    def create_ui(self):
        self.root = tk.Tk()
        self.ui = DroneUI(self.root, self.manager, config=self.config)
        return self.root

    def run_ui(self):
        if hasattr(self, 'root') and self.root:
            logging.info('Entering Tkinter mainloop')
            self.root.mainloop()


    def start_connections(self, drones_config: List[Dict[str, str]]):
        if not self.hardware_initialized:
            logging.warning("Cannot start connections: Hardware not yet initialized.")
            return
        
        if not self.interfaces_found:
            logging.critical("Cannot start connections: No Crazyflie interfaces found. Please check radio dongle connection and drivers.")
            return
        
        if not self.controllers_ready:
            logging.warning("Cannot start connections: Drone controllers not yet ready.")
            return

        import threading
        import time
        if not hasattr(self, 'shutdown_event'):
            self.shutdown_event = threading.Event()
        if not hasattr(self, 'drone_threads'):
            self.drone_threads = []
        
        def connect_drone(drone: Drone, shutdown_event: threading.Event) -> None:
            """Connection loop for a single drone. Retries continuously without blocking others."""
            drone_id = getattr(drone, 'id', '?')
            loop_count = 0
            last_state = None

            def _conn_verbose():
                return self.config.development.connection_log_verbose

            while not shutdown_event.is_set():
                try:
                    handler = drone.handler  # type: ignore
                    is_connected = drone.is_connected()  # type: ignore
                    connecting = getattr(handler, 'connecting', False)  # type: ignore
                    attempting = False
                    pending_reconnect = False
                    if hasattr(handler, 'is_attempting_connect'):  # type: ignore
                        attempting = bool(handler.is_attempting_connect())  # type: ignore
                    else:
                        attempting = bool(getattr(handler, '_connect_in_progress', False))  # type: ignore
                    if hasattr(handler, 'has_pending_reconnect'):  # type: ignore
                        pending_reconnect = bool(handler.has_pending_reconnect())  # type: ignore

                    loop_count += 1
                    verbose = _conn_verbose()
                    if verbose:
                        state = (is_connected, connecting, attempting, pending_reconnect)
                        if loop_count % 30 == 0 or state != last_state:
                            logging.info(
                                f"Connection loop for {drone_id}: connected={is_connected}, "
                                f"connecting={connecting}, attempting={attempting}, pending_reconnect={pending_reconnect}"
                            )
                        last_state = state
                    
                    # Only attempt connection if not connected, not in progress, and no backoff timer pending
                    if not is_connected and not connecting and not attempting and not pending_reconnect:
                        if verbose:
                            logging.info(f"Attempting to connect to drone {drone_id}")
                        else:
                            logging.debug(f"Attempting to connect to drone {drone_id}")
                        handler.connect()  # type: ignore
                    
                    # Short wait before next check - allows quick retry after failed connection
                    # while not spinning too fast
                    for _ in range(10):  # 1 second total
                        if shutdown_event.is_set():
                            break
                        time.sleep(0.1)
                except Exception:
                    logging.exception(f"Error in connection loop for drone {drone_id}")
                    # Shorter wait on exception to allow faster recovery
                    for _ in range(20):  # 2 seconds
                        if shutdown_event.is_set():
                            break
                        time.sleep(0.1)
            logging.info(f"Drone thread for {drone_id} exiting due to shutdown.")
        
        # Start a thread for each drone - they run independently
        logging.info(f"Starting connection threads for {len(list(self.manager.handlers.values()))} drones")
        for drone in self.manager.handlers.values():
            drone_id = getattr(drone, 'id', '?')
            logging.info(f"Creating connection thread for drone {drone_id}")
            t = threading.Thread(target=connect_drone, args=(drone, self.shutdown_event), name=f"ConnectThread-{drone_id}")  # type: ignore
            t.daemon = True
            t.start()
            self.drone_threads.append(t)  # type: ignore
            logging.info(f"Connection thread started for drone {drone_id} (thread={t.name})")

    def shutdown(self):
        import os
        self.shutdown_event.set()
        if hasattr(self, 'root') and self.root:
            try:
                self.root.destroy()
            except Exception:
                pass
        os._exit(0)


def shutdown_handler(app: Optional[Application], sig: int, frame: Optional[Any]) -> None:
    logging.info("SIGINT received, forcing exit.")
    os._exit(0)


if __name__ == "__main__":
    # Note: relaunch_headless removed - console closing handled after UI initialization
    # to avoid config loading issues

    # Route early logs to a rotating file in a logs/ folder so pythonw.exe runs are diagnosable
    try:
        init_file_logging(base_dir=os.path.dirname(__file__))
        logging.info("Launcher starting")
        # Enable DEBUG globally so module-level debug logs (including
        # pm.state telemetry from drone_telemetry) are visible in the UI (now handled by Drone class)
        # terminal and log file while we diagnose charging state.
        # logging.getLogger().setLevel(logging.DEBUG)
    except Exception:
        pass

    # Set AppUserModelID early so Windows uses our app identity for taskbar icon grouping
    try:
        set_windows_app_id("Fontys.DroneController")
    except Exception:
        pass

    try:
        config = ConfigurationBuilder.with_defaults().build()
        drones_cfg = config.get_drones()
        
        # Create app with lightweight constructor (no blocking operations)
        logging.info("Creating application instance (fast path)...")
        app = Application(config=config, drones_config=drones_cfg)
        signal.signal(signal.SIGINT, lambda s, f: shutdown_handler(app, s, f))

        # Create UI immediately - this shows window to user fast
        logging.info("Creating UI (fast path)...")
        root = app.create_ui()

        # Set the custom shutdown logic when the user closes the window
        root.protocol("WM_DELETE_WINDOW", app.shutdown)

        # Optionally detach/close console window when UI is up (Windows only)
        try:
            free_console(config.ui.close_console_on_ui)
        except Exception:
            pass

        # Start background initialization after UI is visible
        # This includes: hardware init, websocket, and connections
        import threading
        def start_background():
            def bg():
                # Initialize hardware first (radio, controllers)
                app.initialize_hardware()
                # Then start network and drone connections
                app.start_websocket()
                app.start_connections(drones_cfg)
            threading.Thread(target=bg, daemon=True).start()
        
        # Trigger background init immediately after UI event loop starts
        root.after_idle(start_background)
        logging.info("Entering UI mainloop (background tasks will run async)...")
        app.run_ui()
    except KeyboardInterrupt:
        try:
            app.shutdown()  # type: ignore
        except Exception:
            pass
    except Exception as e:
        # Log and notify user in a basic way even without Tk stdout
        try:
            logging.exception('Fatal error during startup: %s', e)
            err_path: str | None = os.path.join(os.path.dirname(__file__), 'last_startup_error.txt')
            with open(err_path, 'w', encoding='utf-8') as f:  # type: ignore
                f.write('Fatal startup error:\n')
                f.write(traceback.format_exc())
        except Exception:
            err_path = None
        try:
            import ctypes
            msg = f'Failed to start application. See log or {err_path} for details.'
            ctypes.windll.user32.MessageBoxW(0, msg, 'Drone Controller', 0x00000010)
        except Exception:
            pass
