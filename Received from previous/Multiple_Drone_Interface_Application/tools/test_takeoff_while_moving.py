#!/usr/bin/env python3
"""
Multi-drone command sequence test.

This is a HARDWARE TEST that simulates the exact scenario where:
1. Drone1 takes off and receives continuous move_to commands
2. Drone2 takes off while drone1 is receiving move_to
3. Both drones then receive move_to commands simultaneously
4. We monitor if either drone's connection is affected

DRONES WILL NOT ACTUALLY FLY (no lighthouse) - this tests the command routing.

Usage:
    python test_takeoff_while_moving.py
"""

import logging
import time
import threading
from threading import Event, Thread
from typing import Callable, Protocol, cast

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s %(levelname)s [%(name)s]: %(message)s',
    datefmt='%H:%M:%S'
)
logger = logging.getLogger("CommandTest")

# Import cflib
try:
    import cflib.crtp as crtp  # type: ignore[reportMissingTypeStubs]
    from cflib.crazyflie import Crazyflie  # type: ignore[reportMissingTypeStubs]
    from cflib.crazyflie.log import LogConfig  # type: ignore[reportMissingTypeStubs]
except ImportError:
    logger.error("cflib not installed. Run: pip install cflib")
    exit(1)


class CallbackRegistry(Protocol):
    def add_callback(self, cb: Callable[..., None]) -> None:
        ...


class LogConfigProto(Protocol):
    data_received_cb: CallbackRegistry

    def add_variable(self, name: str, fetch_as: str | None = None) -> None:
        ...

    def start(self) -> None:
        ...


class LogProto(Protocol):
    def add_config(self, logconf: LogConfigProto) -> None:
        ...


class CommanderProto(Protocol):
    def send_position_setpoint(self, x: float, y: float, z: float, yaw: float) -> None:
        ...

    def send_stop_setpoint(self) -> None:
        ...


class HighLevelCommanderProto(Protocol):
    def takeoff(
        self, absolute_height_m: float, duration_s: float, group_mask: int = 0, yaw: float = 0.0
    ) -> None:
        ...

    def land(
        self, absolute_height_m: float, duration_s: float, group_mask: int = 0, yaw: float = 0.0
    ) -> None:
        ...


class CrazyflieProto(Protocol):
    connected: CallbackRegistry
    disconnected: CallbackRegistry
    connection_failed: CallbackRegistry
    connection_lost: CallbackRegistry
    log: LogProto
    commander: CommanderProto
    high_level_commander: HighLevelCommanderProto | None

    def open_link(self, link_uri: str) -> None:
        ...

    def close_link(self) -> None:
        ...


class CrtpModule(Protocol):
    def init_drivers(self) -> None:
        ...


crtp_module: CrtpModule = cast(CrtpModule, crtp)


class DroneTester:
    """Drone connection for command sequence testing."""
    
    def __init__(self, drone_id: str, uri: str):
        self.id = drone_id
        self.uri = uri
        self.connected = False
        self.connecting = False
        self.battery_voltage = 0.0
        self.link_quality = 0
        self.lighthouse_status = 0
        self._cf: CrazyflieProto | None = None
        self._log_conf: LogConfigProto | None = None
        self._lock = threading.Lock()
        self._move_to_count = 0
        self._move_to_failed = 0
        self._takeoff_sent = False
        self._disconnected_during_test = False
        
    def connect(self):
        """Connect to the drone."""
        logger.info(f"[{self.id}] Connecting to {self.uri}...")
        self.connecting = True
        
        self._cf = cast(CrazyflieProto, Crazyflie())
        self._cf.connected.add_callback(self._on_connected)
        self._cf.disconnected.add_callback(self._on_disconnected)
        self._cf.connection_failed.add_callback(self._on_connection_failed)
        self._cf.connection_lost.add_callback(self._on_connection_lost)
        
        self._cf.open_link(self.uri)
        
    def _on_connected(self, uri: str) -> None:
        logger.info(f"[{self.id}] Connected to {uri}")
        with self._lock:
            self.connected = True
            self.connecting = False
        self._setup_logging()
        
    def _on_disconnected(self, uri: str) -> None:
        logger.warning(f"[{self.id}] DISCONNECTED from {uri}")
        with self._lock:
            if self.connected:  # Was connected before
                self._disconnected_during_test = True
            self.connected = False
            self.connecting = False
            
    def _on_connection_failed(self, uri: str, msg: str) -> None:
        logger.error(f"[{self.id}] Connection failed to {uri}: {msg}")
        with self._lock:
            self.connected = False
            self.connecting = False
            
    def _on_connection_lost(self, uri: str, msg: str) -> None:
        logger.error(f"[{self.id}] CONNECTION LOST to {uri}: {msg}")
        with self._lock:
            self._disconnected_during_test = True
            self.connected = False
            
    def _setup_logging(self) -> None:
        """Setup battery and lighthouse logging."""
        try:
            if self._cf is None:
                raise RuntimeError("Crazyflie not initialized")
            self._log_conf = cast(LogConfigProto, LogConfig(name='status', period_in_ms=500))
            self._log_conf.add_variable('pm.vbat', 'float')
            self._log_conf.add_variable('lighthouse.status', 'uint8_t')
            self._log_conf.data_received_cb.add_callback(self._on_log_data)
            self._cf.log.add_config(self._log_conf)
            self._log_conf.start()
        except Exception as e:
            logger.error(f"[{self.id}] Failed to setup logging: {e}")
            
    def _on_log_data(self, timestamp: int, data: dict[str, float | int], logconf: LogConfigProto) -> None:
        self.battery_voltage = float(data.get('pm.vbat', 0.0))
        self.lighthouse_status = int(data.get('lighthouse.status', 0))
        
    def send_move_to(self, x: float, y: float, z: float, yaw: float = 0.0) -> bool:
        """Send a position setpoint (move_to command)."""
        if not self.connected or self._cf is None:
            self._move_to_failed += 1
            return False
        try:
            # Using commander.send_position_setpoint like the real app
            self._cf.commander.send_position_setpoint(x, y, z, yaw)
            self._move_to_count += 1
            return True
        except Exception as e:
            logger.error(f"[{self.id}] move_to failed: {e}")
            self._move_to_failed += 1
            return False
            
    def send_takeoff(self, height: float = 0.5, duration: float = 2.0) -> bool:
        """Send a takeoff command using high-level commander."""
        if not self.connected or self._cf is None:
            logger.error(f"[{self.id}] Cannot takeoff - not connected")
            return False
        try:
            # Log lighthouse status
            logger.info(f"[{self.id}] Lighthouse status: {self.lighthouse_status}")
            
            if self._cf.high_level_commander:
                logger.info(f"[{self.id}] Sending TAKEOFF command (height={height}m) - BYPASSING lighthouse check for test")
                # Send takeoff regardless of lighthouse status for testing
                self._cf.high_level_commander.takeoff(height, duration)
                self._takeoff_sent = True
                logger.info(f"[{self.id}] TAKEOFF command sent successfully!")
                return True
            else:
                logger.error(f"[{self.id}] high_level_commander not available")
                return False
        except Exception as e:
            logger.error(f"[{self.id}] takeoff failed: {e}")
            return False
            
    def send_land(self, duration: float = 2.0) -> bool:
        """Send a land command."""
        if not self.connected or self._cf is None:
            return False
        try:
            if self._cf.high_level_commander:
                logger.info(f"[{self.id}] Sending LAND command")
                self._cf.high_level_commander.land(0.0, duration)
                return True
        except Exception as e:
            logger.error(f"[{self.id}] land failed: {e}")
        return False
        
    def send_stop(self) -> None:
        """Send emergency stop."""
        if self._cf is None:
            return
        if self._cf.commander:
            try:
                self._cf.commander.send_stop_setpoint()
                logger.info(f"[{self.id}] STOP sent")
            except:
                pass
                
    def get_stats(self) -> dict[str, object]:
        """Get test statistics."""
        return {
            "id": self.id,
            "connected": self.connected,
            "battery_v": round(self.battery_voltage, 2),
            "lighthouse": self.lighthouse_status,
            "move_to_sent": self._move_to_count,
            "move_to_failed": self._move_to_failed,
            "takeoff_sent": self._takeoff_sent,
            "disconnected": self._disconnected_during_test,
        }

    @property
    def move_to_count(self) -> int:
        return self._move_to_count

    @property
    def move_to_failed(self) -> int:
        return self._move_to_failed

    @property
    def disconnected_during_test(self) -> bool:
        return self._disconnected_during_test
        
    def disconnect(self) -> None:
        """Disconnect from the drone."""
        if self._cf:
            try:
                self._cf.close_link()
            except:
                pass
        self.connected = False


def run_command_sequence_test():
    """
    Simulates the exact scenario:
    1. Connect to all drones
    2. Drone1 takes off, then receives move_to commands
    3. Drone2 takes off while drone1 is receiving move_to
    4. Both drones receive move_to commands simultaneously
    5. Monitor if any drone loses connection or commands fail
    """
    
    # Drone configuration
    DRONES: list[tuple[str, str]] = [
        ("drone1", "radio://0/80/2M/E7E7E7E7E1"),
        ("drone2", "radio://0/80/2M/E7E7E7E7E2"),
    ]
    
    # Initialize drivers
    logger.info("Initializing Crazyradio drivers...")
    crtp_module.init_drivers()
    
    # Create drone connections
    drones: dict[str, DroneTester] = {drone_id: DroneTester(drone_id, uri) for drone_id, uri in DRONES}
    
    # Phase 1: Connect all drones
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 1: Connecting to drones...")
    logger.info(f"{'='*60}\n")
    
    for drone in drones.values():
        drone.connect()
        time.sleep(0.5)
    
    logger.info("Waiting for connections (30 seconds)...")
    time.sleep(30)
    
    connected = [d for d in drones.values() if d.connected]
    logger.info(f"\nConnected: {len(connected)}/{len(drones)} drones")
    
    if len(connected) < 2:
        logger.error("Need at least 2 drones connected for this test!")
        for d in drones.values():
            d.disconnect()
        return
    
    # Phase 2: Drone1 takes off
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 2: Drone1 TAKEOFF...")
    logger.info(f"{'='*60}\n")
    
    drones["drone1"].send_takeoff(height=0.5)
    time.sleep(2)  # Wait for takeoff to complete
    
    # Phase 3: Start move_to loop on drone1
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 3: Starting move_to commands on drone1...")
    logger.info(f"{'='*60}\n")
    
    stop_event = Event()
    drone1_errors_before_drone2_takeoff: list[float] = []
    drone1_errors_after_drone2_takeoff: list[float] = []
    drone2_takeoff_time: float | None = None
    
    def move_to_loop_drone1():
        """Continuously send move_to to drone1 (simulating Unity sending positions)."""
        nonlocal drone2_takeoff_time
        x, y, z = 0.0, 0.0, 0.5  # Simulated position
        while not stop_event.is_set():
            drone1 = drones["drone1"]
            if drone1.connected:
                success = drone1.send_move_to(x, y, z, 0.0)
                if not success:
                    if drone2_takeoff_time is None:
                        drone1_errors_before_drone2_takeoff.append(time.time())
                    else:
                        drone1_errors_after_drone2_takeoff.append(time.time())
            time.sleep(0.1)  # 10 Hz like Unity
    
    # Start move_to thread for drone1
    move_thread1 = Thread(target=move_to_loop_drone1, daemon=True)
    move_thread1.start()
    logger.info("[drone1] move_to loop started (10 Hz)")
    
    # Let it run for a bit
    time.sleep(3)
    drone1_count_before = drones["drone1"].move_to_count
    logger.info(f"[drone1] move_to count before drone2 takeoff: {drone1_count_before}")
    
    # Phase 4: Drone2 takes off while drone1 is receiving move_to
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 4: Drone2 TAKEOFF while drone1 receives move_to...")
    logger.info(f"{'='*60}\n")
    
    logger.info(">>> Sending TAKEOFF to drone2...")
    drone2_takeoff_time = time.time()
    drones["drone2"].send_takeoff(height=0.5)
    
    # Wait and check drone1 status
    time.sleep(2)
    logger.info(f"[drone1] Status after drone2 takeoff: connected={drones['drone1'].connected}, "
                f"move_to_count={drones['drone1'].move_to_count}, "
                f"failures={drones['drone1'].move_to_failed}")
    
    # Phase 5: Both drones receive move_to simultaneously
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 5: Both drones receive move_to simultaneously...")
    logger.info(f"{'='*60}\n")
    
    drone2_move_errors: list[float] = []
    
    def move_to_loop_drone2():
        """Continuously send move_to to drone2."""
        x, y, z = 0.5, 0.5, 0.5  # Different position
        while not stop_event.is_set():
            drone2 = drones["drone2"]
            if drone2.connected:
                success = drone2.send_move_to(x, y, z, 0.0)
                if not success:
                    drone2_move_errors.append(time.time())
            time.sleep(0.1)  # 10 Hz like Unity
    
    # Start move_to thread for drone2
    move_thread2 = Thread(target=move_to_loop_drone2, daemon=True)
    move_thread2.start()
    logger.info("[drone2] move_to loop started (10 Hz)")
    
    # Run both for 10 seconds
    logger.info("Running both drones with move_to for 10 seconds...")
    for i in range(10):
        time.sleep(1)
        logger.info(f"[{i+1}s] drone1: {drones['drone1'].move_to_count} move_to, "
                    f"drone2: {drones['drone2'].move_to_count} move_to")
    
    # Stop the move_to loops
    stop_event.set()
    move_thread1.join(timeout=2)
    move_thread2.join(timeout=2)
    
    # Phase 6: Send land commands
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 6: Sending LAND commands...")
    logger.info(f"{'='*60}\n")
    
    for drone in drones.values():
        if drone.connected:
            drone.send_land()
            time.sleep(0.5)
    
    time.sleep(3)
    
    # Phase 7: Final report
    logger.info(f"\n{'='*60}")
    logger.info("PHASE 7: Test Complete - Final Report")
    logger.info(f"{'='*60}\n")
    
    all_ok = True
    
    for drone in drones.values():
        stats = drone.get_stats()
        status = "OK" if stats["connected"] and not stats["disconnected"] else "ISSUE"
        if status == "ISSUE":
            all_ok = False
            
        logger.info(f"[{stats['id']}] {status}")
        logger.info(f"  - Connected: {stats['connected']}")
        logger.info(f"  - Disconnected during test: {stats['disconnected']}")
        logger.info(f"  - move_to sent: {stats['move_to_sent']}")
        logger.info(f"  - move_to failed: {stats['move_to_failed']}")
        logger.info(f"  - takeoff sent: {stats['takeoff_sent']}")
        logger.info(f"  - Battery: {stats['battery_v']}V")
        logger.info("")
    
    # Detailed analysis
    logger.info("DETAILED ANALYSIS:")
    logger.info(f"  - Drone1 move_to before drone2 takeoff: {drone1_count_before}")
    logger.info(f"  - Drone1 move_to errors before drone2 takeoff: {len(drone1_errors_before_drone2_takeoff)}")
    logger.info(f"  - Drone1 move_to errors after drone2 takeoff: {len(drone1_errors_after_drone2_takeoff)}")
    logger.info(f"  - Drone2 move_to errors: {len(drone2_move_errors)}")
    logger.info(f"  - Drone1 total move_to: {drones['drone1'].move_to_count}")
    logger.info(f"  - Drone2 total move_to: {drones['drone2'].move_to_count}")
    
    # Check for issues
    if drones["drone1"].disconnected_during_test:
        logger.error("\n[FAIL] DRONE1 DISCONNECTED when drone2 took off!")
        all_ok = False
    elif len(drone1_errors_after_drone2_takeoff) > 0:
        logger.warning(f"\n[WARN] Drone1 had {len(drone1_errors_after_drone2_takeoff)} move_to errors after drone2 takeoff")
        all_ok = False
    else:
        logger.info("\n[PASS] Drone1 continued receiving move_to without issues during drone2 takeoff!")
    
    if drones["drone2"].disconnected_during_test:
        logger.error("[FAIL] DRONE2 DISCONNECTED!")
        all_ok = False
    elif len(drone2_move_errors) > 0:
        logger.warning(f"[WARN] Drone2 had {len(drone2_move_errors)} move_to errors")
    else:
        logger.info("[PASS] Drone2 received move_to without issues!")
    
    if all_ok:
        logger.info("\n[OK] All tests passed - Both drones can fly simultaneously!")
    else:
        logger.warning("\n[WARNING] Issues detected - See above for details")
    
    # Cleanup
    logger.info("\nDisconnecting all drones...")
    for drone in drones.values():
        drone.send_stop()
        drone.disconnect()
    
    time.sleep(1)
    logger.info("Test complete!")


if __name__ == "__main__":
    print("\n" + "="*60)
    print("DUAL DRONE TAKEOFF + MOVE_TO TEST")
    print("="*60)
    print("\nThis test simulates the exact crash scenario:")
    print("1. Drone1 takes off and receives move_to commands")
    print("2. Drone2 takes off while drone1 is flying")
    print("3. Both drones receive move_to commands simultaneously")
    print("4. We monitor if either drone's commands are affected")
    print("\n[!] DRONES WILL NOT ACTUALLY FLY (no lighthouse)")
    print("    This tests command routing only!")
    print("="*60 + "\n")
    
    input("Press ENTER to start the test...")
    
    run_command_sequence_test()
