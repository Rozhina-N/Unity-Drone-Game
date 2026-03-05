import sys
import json
import time
from pathlib import Path
from typing import Any, Protocol, cast


class CrtpModule(Protocol):
    def init_drivers(self) -> None:
        ...

    def scan_interfaces(self, address: str | None = None) -> list[object]:
        ...

crtp_module: CrtpModule

try:
    import cflib.crtp as _crtp  # type: ignore[reportMissingTypeStubs]
    from cflib.crazyflie import Crazyflie  # type: ignore[reportMissingTypeStubs]
    from cflib.crazyflie.syncCrazyflie import (  # type: ignore[reportMissingTypeStubs]
        SyncCrazyflie,
    )
    crtp_module = cast(CrtpModule, _crtp)
except Exception as e:
    print("IMPORT_ERROR:", e)
    sys.exit(2)


def get_uris_from_config() -> list[str]:
    """Return all addresses from drones.json (if available)."""
    p = Path(__file__).resolve().parents[1] / "drones" / "drones.json"
    uris: list[str] = []
    try:
        data: object = json.loads(p.read_text(encoding="utf-8"))
        if isinstance(data, list):
            data_list = cast(list[object], data)
            for entry in data_list:
                if isinstance(entry, dict):
                    entry_dict = cast(dict[str, Any], entry)
                    addr = entry_dict.get("address")
                    if isinstance(addr, str) and addr:
                        uris.append(addr)
    except Exception:
        pass
    return uris


def main():
    # Build URI list: CLI args or all from drones.json
    uris = [u for u in sys.argv[1:] if u]
    if not uris:
        uris = get_uris_from_config()
    if not uris:
        print(
            "No URIs provided and could not read drones.json. Usage: python tools/cf_link_test.py <uri1> [uri2 ...]"
        )
        sys.exit(1)

    print("INIT_DRIVERS")
    crtp_module.init_drivers()
    try:
        print("SCAN", crtp_module.scan_interfaces())
    except Exception:
        pass

    any_fail = False
    for uri in uris:
        print("TRY_CONNECT", uri, flush=True)
        try:
            with SyncCrazyflie(uri, cf=Crazyflie()) as _:
                print("CONNECTED", uri, flush=True)
                # keep link shortly
                time.sleep(0.8)
                print("DONE", uri, flush=True)
        except Exception as e:
            print("FAILED", uri, repr(e), flush=True)
            any_fail = True

    return 3 if any_fail else 0


if __name__ == "__main__":
    sys.exit(main())
