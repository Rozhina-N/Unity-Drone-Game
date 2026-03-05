import types
from typing import List, Tuple, Optional

from utils.led import apply_led_color, set_led_ring_color_from_handler, PLAYER_COLORS


class DummyParam:
    def __init__(self) -> None:
        self.set_calls: List[Tuple[str, str]] = []

    def set_value(self, name: str, value: str) -> None:
        self.set_calls.append((name, value))


class DummyCF:
    def __init__(self, with_param: bool = True) -> None:
        self.param: Optional[DummyParam] = DummyParam() if with_param else None


class DummyHandler:
    def __init__(self, cf: Optional[DummyCF]) -> None:
        self._cf = cf
        self.current_color: Optional[str] = None


def test_apply_led_color_known_color_succeeds():
    cf = DummyCF()
    assert cf.param is not None
    ok = apply_led_color(cf, "red", effect=1)

    assert ok is True
    # Expect correct parameters to be sent
    assert ("ring.red", "255") in cf.param.set_calls
    assert ("ring.green", "0") in cf.param.set_calls
    assert ("ring.blue", "0") in cf.param.set_calls
    assert ("ring.effect", "1") in cf.param.set_calls


def test_apply_led_color_unknown_color_returns_false():
    cf = DummyCF()
    assert cf.param is not None
    ok = apply_led_color(cf, "not-a-color", effect=1)

    assert ok is False
    # No param calls should be made
    assert cf.param.set_calls == []


def test_set_led_ring_color_from_handler_updates_handler_and_uses_cf():
    cf = DummyCF()
    assert cf.param is not None
    handler = DummyHandler(cf)

    ok = set_led_ring_color_from_handler(handler, "white", effect=1)

    assert ok is True
    # Handler should remember the normalized color name
    assert handler.current_color == "white"
    # Underlying Crazyflie param interface should have been used
    assert ("ring.red", str(PLAYER_COLORS["white"][0])) in cf.param.set_calls


def test_set_led_ring_color_from_handler_with_wrapper_object():
    """Handler may be wrapped in an object exposing `.handler`."""
    cf = DummyCF()
    inner = DummyHandler(cf)
    wrapper = types.SimpleNamespace(handler=inner)

    ok = set_led_ring_color_from_handler(wrapper, "blue", effect=2)

    assert ok is True
    # Inner handler should get the color stored
    assert inner.current_color == "blue"
    # Params should have been set on the same CF instance
    assert ("ring.blue", str(PLAYER_COLORS["blue"][2])) is not None


def test_set_led_ring_color_from_handler_no_cf_returns_false():
    handler = DummyHandler(cf=None)

    ok = set_led_ring_color_from_handler(handler, "red", effect=1)

    assert ok is False
    assert handler.current_color is None
