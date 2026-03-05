import pytest
from drones.drone_manager import DroneManager


class DummyHandler:
    def close(self):
        pass


def test_max_drones_enforced():
    dm = DroneManager(max_drones=2)
    dm.add("a", DummyHandler())
    dm.add("b", DummyHandler())
    with pytest.raises(RuntimeError):
        dm.add("c", DummyHandler())

    assert dm.count() == 2

    # removing should allow adding again
    dm.remove("a")
    dm.add("c", DummyHandler())
    assert sorted(dm.ids()) == ["b", "c"]
