import drones.drone_controller as dc
from unittest.mock import patch
from typing import Callable, Any


class FakeTimer:
    def __init__(self, delay: float, func: Callable[[], None]):
        self.delay = delay
        self.func = func
        self.daemon = False
        self.started = False

    def start(self):
        self.started = True


class DummyEvent:
    def __init__(self):
        self._cbs: list[Callable[[], None]] = []

    def add_callback(self, cb: Callable[[], None]):
        self._cbs.append(cb)


class DummyCF:
    def __init__(self, *args: Any, **kwargs: Any):
        self.connected = DummyEvent()
        self.disconnected = DummyEvent()
        self.connection_failed = DummyEvent()
        self.fully_connected = DummyEvent()
        self.param = type('P', (), {'set_value': lambda self, a, b: None})()  # type: ignore
        self.log = type('L', (), {'add_config': lambda self, cfg: None})()  # type: ignore
        self.commander = None
        self.high_level_commander = None

    def open_link(self, address: str):
        pass

    def close_link(self):
        pass


def test_schedule_on_connection_failed():
    with patch('drones.connection_manager.Timer', FakeTimer), patch('drones.connection_manager.random.uniform', return_value=0):
        ctrl = dc.DroneController('d1', 'addr')
        mgr = ctrl._connection_mgr
        assert getattr(mgr, '_reconnect_attempts', 0) == 0
        mgr._on_connection_failed_internal('uri', 'msg')
        assert isinstance(mgr._reconnect_timer, FakeTimer)
        assert mgr._reconnect_attempts == 1
        assert abs(mgr._reconnect_timer.delay - mgr._reconnect_base) < 1e-6


def test_connect_cancels_reconnect():
    with patch('drones.connection_manager.Timer', FakeTimer), patch('drones.connection_manager.random.uniform', return_value=0):
        ctrl = dc.DroneController('d1', 'addr', cflib=object(), crazyflie_cls=DummyCF)
        mgr = ctrl._connection_mgr
        mgr._schedule_reconnect()
        assert isinstance(mgr._reconnect_timer, FakeTimer)
        ctrl.connect()
        assert mgr._reconnect_timer is None
        assert mgr.get_cf() is not None
