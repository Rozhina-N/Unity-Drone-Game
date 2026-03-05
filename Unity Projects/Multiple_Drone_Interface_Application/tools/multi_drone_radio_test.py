#!/usr/bin/env python3
"""
Multi-drone radio communication test.

This is a HARDWARE TEST (not a unit test) that requires:
- Crazyradio PA dongle connected
- Multiple Crazyflie drones powered on

This script tests connecting to multiple drones simultaneously and sending
commands to verify radio communication works without interference.

No flying required - drones stay on the ground.

Usage:
    python multi_drone_radio_test.py [duration_seconds]
    
Examples:
    python multi_drone_radio_test.py        # 30 second test
    python multi_drone_radio_test.py 60     # 60 second test
"""

import logging
import time
import threading
from typing import Callable, Protocol, cast

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s %(levelname)s [%(name)s]: %(message)s',
    datefmt='%H:%M:%S'
)
logger = logging.getLogger("RadioTest")

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
    def send_setpoint(self, roll: float, pitch: float, yawrate: float, thrust: int) -> None:
        ...


class CrazyflieProto(Protocol):
    connected: CallbackRegistry
    disconnected: CallbackRegistry
    connection_failed: CallbackRegistry
    connection_lost: CallbackRegistry
    link_quality_updated: CallbackRegistry
    log: LogProto
    commander: CommanderProto

    def open_link(self, link_uri: str) -> None:
        ...

    def close_link(self) -> None:
        ...


class CrtpModule(Protocol):
    def init_drivers(self) -> None:
        ...


crtp_module: CrtpModule = cast(CrtpModule, crtp)


class DroneConnection:
    """Simple drone connection for testing."""
    
    def __init__(self, drone_id: str, uri: str):
        self.id = drone_id
        self.uri = uri
        self.connected = False
        self.connecting = False
        self.battery_voltage = 0.0
        self.link_quality = 0
        self._cf: CrazyflieProto | None = None
        self._log_conf: LogConfigProto | None = None
        self._lock = threading.Lock()
        
    def connect(self):
        """Connect to the drone."""
        logger.info(f"[{self.id}] Connecting to {self.uri}...")
        self.connecting = True
        
        self._cf = cast(CrazyflieProto, Crazyflie())
        self._cf.connected.add_callback(self._on_connected)
        self._cf.disconnected.add_callback(self._on_disconnected)
        self._cf.connection_failed.add_callback(self._on_connection_failed)
        self._cf.connection_lost.add_callback(self._on_connection_lost)
        self._cf.link_quality_updated.add_callback(self._on_link_quality)
        
        self._cf.open_link(self.uri)
        
    def _on_connected(self, uri: str) -> None:
        logger.info(f"[{self.id}] Connected to {uri}")
        with self._lock:
            self.connected = True
            self.connecting = False
        self._setup_logging()
        
    def _on_disconnected(self, uri: str) -> None:
        logger.warning(f"[{self.id}] Disconnected from {uri}")
        with self._lock:
            self.connected = False
            self.connecting = False
            
    def _on_connection_failed(self, uri: str, msg: str) -> None:
        logger.error(f"[{self.id}] Connection failed to {uri}: {msg}")
        with self._lock:
            self.connected = False
            self.connecting = False
            
    def _on_connection_lost(self, uri: str, msg: str) -> None:
        logger.error(f"[{self.id}] Connection lost to {uri}: {msg}")
        with self._lock:
            self.connected = False
            
    def _on_link_quality(self, quality: int) -> None:
        self.link_quality = quality
        
    def _setup_logging(self):
        """Setup battery logging."""
        try:
            if self._cf is None:
                raise RuntimeError("Crazyflie not initialized")
            self._log_conf = cast(LogConfigProto, LogConfig(name='battery', period_in_ms=1000))
            self._log_conf.add_variable('pm.vbat', 'float')
            self._log_conf.data_received_cb.add_callback(self._on_battery_data)
            self._cf.log.add_config(self._log_conf)
            self._log_conf.start()
            logger.info(f"[{self.id}] Battery logging started")
        except Exception as e:
            logger.error(f"[{self.id}] Failed to setup logging: {e}")
            
    def _on_battery_data(self, timestamp: int, data: dict[str, float], logconf: LogConfigProto) -> None:
        self.battery_voltage = data.get('pm.vbat', 0.0)
        
    def send_test_packet(self):
        """Send a test packet (setpoint of 0,0,0,0 which is harmless)."""
        if not self.connected or not self._cf:
            return False
        try:
            # Send a zero setpoint - drone won't move but radio is tested
            self._cf.commander.send_setpoint(0, 0, 0, 0)
            return True
        except Exception as e:
            logger.error(f"[{self.id}] Failed to send test packet: {e}")
            return False
            
    def get_status(self) -> dict[str, object]:
        """Get current status."""
        return {
            "id": self.id,
            "uri": self.uri,
            "connected": self.connected,
            "connecting": self.connecting,
            "battery_v": round(self.battery_voltage, 2),
            "link_quality": self.link_quality
        }
        
    def disconnect(self):
        """Disconnect from the drone."""
        if self._cf:
            try:
                self._cf.close_link()
            except:
                pass
        self.connected = False
        logger.info(f"[{self.id}] Disconnected")


def run_test(drone_uris: list[tuple[str, str]], duration_seconds: int = 30):
    """
    Run the multi-drone radio test.
    
    Args:
        drone_uris: List of (drone_id, uri) tuples
        duration_seconds: How long to run the test
    """
    # Initialize drivers
    logger.info("Initializing Crazyradio drivers...")
    crtp_module.init_drivers()
    
    # Create connections
    drones: list[DroneConnection] = []
    for drone_id, uri in drone_uris:
        drones.append(DroneConnection(drone_id, uri))
    
    # Connect all drones
    logger.info(f"\n{'='*60}")
    logger.info(f"PHASE 1: Connecting to {len(drones)} drones...")
    logger.info(f"{'='*60}\n")
    
    for drone in drones:
        drone.connect()
        time.sleep(0.5)  # Small delay between connection attempts
    
    # Wait for connections
    logger.info("Waiting for connections (20 seconds)...")
    time.sleep(20)
    
    # Check connection status
    connected_count = sum(1 for d in drones if d.connected)
    logger.info(f"\n{'='*60}")
    logger.info(f"PHASE 2: Connection status - {connected_count}/{len(drones)} connected")
    logger.info(f"{'='*60}\n")
    
    for drone in drones:
        status = drone.get_status()
        state = "CONNECTED" if status["connected"] else ("CONNECTING" if status["connecting"] else "FAILED")
        logger.info(f"  [{drone.id}] {state} - Battery: {status['battery_v']}V, Link: {status['link_quality']}%")
    
    if connected_count == 0:
        logger.error("No drones connected! Check radio and drone power.")
        return
    
    # Run communication test
    logger.info(f"\n{'='*60}")
    logger.info(f"PHASE 3: Running communication test for {duration_seconds} seconds...")
    logger.info(f"{'='*60}\n")
    
    start_time = time.time()
    packet_counts: dict[str, dict[str, int]] = {d.id: {"sent": 0, "failed": 0} for d in drones}
    disconnects: dict[str, int] = {d.id: 0 for d in drones}
    
    iteration = 0
    while time.time() - start_time < duration_seconds:
        iteration += 1
        
        # Send test packets to all connected drones
        for drone in drones:
            if drone.connected:
                if drone.send_test_packet():
                    packet_counts[drone.id]["sent"] += 1
                else:
                    packet_counts[drone.id]["failed"] += 1
            else:
                # Track if drone disconnected during test
                if packet_counts[drone.id]["sent"] > 0:
                    disconnects[drone.id] += 1
        
        # Log status every 5 seconds
        if iteration % 50 == 0:
            elapsed = int(time.time() - start_time)
            logger.info(f"[{elapsed}s] Status update:")
            for drone in drones:
                status = drone.get_status()
                sent = packet_counts[drone.id]["sent"]
                failed = packet_counts[drone.id]["failed"]
                state = "OK" if status["connected"] else "DISCONNECTED"
                logger.info(f"  [{drone.id}] {state} - Sent: {sent}, Failed: {failed}, Link: {status['link_quality']}%")
        
        time.sleep(0.1)  # 10 packets per second per drone
    
    # Final report
    logger.info(f"\n{'='*60}")
    logger.info(f"PHASE 4: Test Complete - Final Report")
    logger.info(f"{'='*60}\n")
    
    total_sent = 0
    total_failed = 0
    
    for drone in drones:
        status = drone.get_status()
        sent = packet_counts[drone.id]["sent"]
        failed = packet_counts[drone.id]["failed"]
        disc = disconnects[drone.id]
        total_sent += sent
        total_failed += failed
        
        success_rate = (sent / (sent + failed) * 100) if (sent + failed) > 0 else 0
        state = "CONNECTED" if status["connected"] else "DISCONNECTED"
        
        logger.info(f"[{drone.id}] Final: {state}")
        logger.info(f"  - Packets sent: {sent}")
        logger.info(f"  - Packets failed: {failed}")
        logger.info(f"  - Success rate: {success_rate:.1f}%")
        logger.info(f"  - Disconnects during test: {disc}")
        logger.info(f"  - Battery: {status['battery_v']}V")
        logger.info(f"  - Link quality: {status['link_quality']}%")
        logger.info("")
    
    logger.info(f"TOTAL: {total_sent} packets sent, {total_failed} failed")
    
    if total_failed > 0 or any(d > 0 for d in disconnects.values()):
        logger.warning("\n[WARNING] ISSUES DETECTED - Radio interference or connection problems!")
    else:
        logger.info("\n[OK] All tests passed - Radio communication is stable!")
    
    # Disconnect all
    logger.info("\nDisconnecting all drones...")
    for drone in drones:
        drone.disconnect()
    
    time.sleep(1)
    logger.info("Test complete!")


if __name__ == "__main__":
    import sys
    
    # Default drone configuration - modify these to match your setup
    DEFAULT_DRONES = [
        ("drone1", "radio://0/80/2M/E7E7E7E7E1"),
        ("drone2", "radio://0/80/2M/E7E7E7E7E2"),
        ("drone3", "radio://0/80/2M/E7E7E7E7E3"),
        ("drone4", "radio://0/80/2M/E7E7E7E7E4"),
    ]
    
    # Parse command line args
    if len(sys.argv) > 1:
        if sys.argv[1] in ["-h", "--help"]:
            print("Usage: python multi_drone_radio_test.py [duration_seconds]")
            print("\nThis script tests radio communication with multiple drones.")
            print("Drones will NOT fly - it only tests the radio link.")
            print("\nDefault drones (edit script to change):")
            for d_id, d_uri in DEFAULT_DRONES:
                print(f"  {d_id}: {d_uri}")
            sys.exit(0)
        duration = int(sys.argv[1])
    else:
        duration = 30
    
    print("\n" + "="*60)
    print("MULTI-DRONE RADIO COMMUNICATION TEST")
    print("="*60)
    print("\nThis test will:")
    print("1. Connect to multiple drones simultaneously")
    print("2. Send test packets to all drones")
    print("3. Monitor for disconnections or failures")
    print("\n[!] DRONES WILL NOT FLY - This is a radio-only test!")
    print("="*60 + "\n")
    
    input("Press ENTER to start the test...")
    
    run_test(DEFAULT_DRONES, duration)
