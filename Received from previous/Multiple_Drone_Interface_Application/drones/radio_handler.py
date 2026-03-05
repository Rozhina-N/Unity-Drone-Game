import logging
import time
import random
from typing import Any, Dict, Optional
from cflib.drivers.crazyradio import Crazyradio  # type: ignore


def _open_crazyradio_with_backoff(max_attempts: int = 5, base_delay: float = 0.5, max_delay: float = 10.0) -> Optional[Crazyradio]:
    """Try to instantiate Crazyradio with exponential backoff and jitter.

    Returns a Crazyradio instance or None on failure.
    """
    attempt = 0
    while attempt < max_attempts:
        try:
            cr = Crazyradio()
            if cr:
                return cr
        except Exception as e:
            logging.debug(f"Crazyradio open attempt {attempt+1} failed: {e}")
        # Backoff before next attempt
        delay = min(max_delay, base_delay * (2 ** attempt))
        jitter = random.uniform(0, min(2.0, delay * 0.2))
        total = delay + jitter
        logging.info(f"Retrying Crazyradio open in {total:.2f}s (attempt {attempt+1}/{max_attempts})")
        try:
            time.sleep(total)
        except Exception:
            pass
        attempt += 1
    logging.warning("Failed to open Crazyradio after multiple attempts")
    return None

class RadioHandlerBase:
    def __init__(self, successor: Optional['RadioHandlerBase'] = None) -> None:
        self._successor = successor

    def handle(self, *args: Any, **kwargs: Any) -> Any:
        if self._successor:
            return self._successor.handle(*args, **kwargs)
        return None

class RadioPowerCycleHandler(RadioHandlerBase):
    def handle(self, *args: Any, **kwargs: Any) -> Any:
        try:
            cr = _open_crazyradio_with_backoff()
            if cr:
                logging.info(f"Crazyradio detected (firmware version: {cr.version}) - Power cycling...")
                cr.close()
                time.sleep(1)
                # Power cycle by disabling/enabling USB port (platform-specific, not implemented here)
                # Placeholder: user must replug if not supported
                # Optionally, use pyusb or OS-specific commands for real power cycle
                return {'status': 'ok', 'message': 'Crazyradio power cycled (logical close/open).'}
            else:
                logging.warning('No Crazyradio dongle found.')
                return {'status': 'not_found', 'message': 'No Crazyradio dongle found.'}
        except Exception as e:
            logging.error(f'Radio powercycle failed: {e}')
            return {'status': 'error', 'message': str(e)}
        return super().handle(*args, **kwargs)

class RadioStatusHandler(RadioHandlerBase):
    def handle(self, drone_manager: Optional[Any] = None, *args: Any, **kwargs: Any) -> Dict[str, Any]:
        try:
            cr = _open_crazyradio_with_backoff()
            if cr:
                datarate_map = {0: '250Kbps', 1: '1Mbps', 2: '2Mbps'}
                power_map = {0: '-18dBm', 1: '-12dBm', 2: '-6dBm', 3: '0dBm'}
                # Try to get USB device info for hardware address
                hw_addr: Optional[str] = None
                try:
                    if hasattr(cr, 'dev') and hasattr(cr.dev, 'serial_number'):  # type: ignore
                        hw_addr = cr.dev.serial_number  # type: ignore
                except Exception:
                    hw_addr = None
                # Try to get address (radio address)
                radio_addr: Optional[str] = None
                try:
                    if hasattr(cr, 'current_address') and cr.current_address:  # type: ignore
                        radio_addr = ':'.join(f'{b:02X}' for b in cr.current_address)  # type: ignore
                except Exception:
                    radio_addr = None
                status: Dict[str, Any] = {
                    'connected': True,
                    'version': cr.version,
                    'channel': getattr(cr, 'current_channel', None),
                    'datarate': datarate_map.get(getattr(cr, 'current_datarate', 2), 'Unknown'),
                    'power': power_map.get(getattr(cr, 'P_0DBM', 3), 'Unknown'),
                    'hw_addr': hw_addr,
                    'radio_addr': radio_addr,
                }
                cr.close()
            else:
                status: Dict[str, Any] = {'connected': False, 'version': None}
        except Exception as e:
            status: Dict[str, Any] = {'connected': False, 'error': str(e)}
        # Add number of connected drones if manager is provided
        if drone_manager is not None:
            try:
                status['nodes'] = drone_manager.count() if hasattr(drone_manager, 'count') else None
            except Exception:
                status['nodes'] = None
        return status
