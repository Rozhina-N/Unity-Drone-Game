import importlib

MODULES = [
    "drones.drone",
    "drones.drone_manager",
    "websocket.websocket_client",
    "ui",
]


def test_core_modules_import():
    for m in MODULES:
        importlib.import_module(m)
