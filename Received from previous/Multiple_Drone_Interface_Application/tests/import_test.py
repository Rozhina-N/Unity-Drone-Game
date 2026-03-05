import importlib

mods = ["drone", "drone_manager", "websocket.websocket_client", "ui", "__main__"]
for m in mods:
    try:
        importlib.import_module(m)
        print("OK:", m)
    except Exception as e:
        print("ERR:", m, e)

print("Import test finished")
