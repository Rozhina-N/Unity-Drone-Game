import logging
from typing import Dict, Any
from websocket.base_handler import WebSocketHandler
from utils.led import set_led_ring_color_from_handler

class LedColorHandler(WebSocketHandler):
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        command = data.get("command")
        if command in ("set_led_color", "set_drone_color", "set_color"):
            color = data.get("playerColor") or data.get("droneColor")
            if color is None:
                logging.getLogger(__name__).warning("No color specified in LED command")
                return True
            effect = data.get("effect", 1)
            try:
                ok = set_led_ring_color_from_handler(handler, color, effect)
                logging.getLogger(__name__).info(
                    f"LED command: color={color}, effect={effect}, ok={ok} for {data.get('drone_id')}"
                )
            except Exception:
                logging.getLogger(__name__).exception("Error executing LED color command")
            return True
        return await super().handle(data, handler, websocket)
