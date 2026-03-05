import asyncio
import json
from typing import Any

import websockets


WS_URI = "ws://localhost:8765"  # adjust if your websocket server uses another port
DRONE_ID = "drone3"             # adjust to an actually connected drone id


async def send_led_command(color: str, effect: int = 1) -> None:
    async with websockets.connect(WS_URI) as ws:
        msg: dict[str, Any] = {
            "command": "set_led_color",   # handled by websocket_client.py
            "drone_id": DRONE_ID,
            "playerColor": color,         # or use 'droneColor'
            "effect": effect,
        }
        await ws.send(json.dumps(msg))
        # Optional: read a response if your server sends one
        try:
            resp = await ws.recv()
            print("Response:", resp)
        except Exception:
            pass


async def main() -> None:
    # Cycle through a few colors so you can visually confirm
    for color in ["white", "red", "blue", "off"]:
        print(f"Setting {DRONE_ID} LED to {color}")
        try:
            await send_led_command(color, effect=1)
        except Exception as e:
            print(f"Failed to set color {color}: {e}")
        await asyncio.sleep(1.0)


if __name__ == "__main__":
    asyncio.run(main())
