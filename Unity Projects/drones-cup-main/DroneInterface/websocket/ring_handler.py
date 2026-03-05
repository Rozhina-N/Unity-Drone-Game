import logging
from typing import Dict, Any
from websocket.base_handler import WebSocketHandler
from utils.led import set_ring_from_handler

class RingHandler(WebSocketHandler):
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        command = data.get("command")
        if command == "set_ring":
            effect = data.get("effect")
            red = data.get("redPlayer")
            green = data.get("greenPlayer")
            blue = data.get("bluePlayer")
            empty_ch = data.get("emptyCharge")
            full_ch = data.get("fullCharge")
            try:
                ok = set_ring_from_handler(
                    handler,
                    effect=effect,
                    red=red,
                    green=green,
                    blue=blue,
                    empty_charge=empty_ch,
                    full_charge=full_ch,
                )
                logging.getLogger(__name__).info(
                    f"set_ring: effect={effect} rgb=({red},{green},{blue}) empty={empty_ch} full={full_ch} ok={ok} for {data.get('drone_id')}"
                )
            except Exception:
                logging.getLogger(__name__).exception("Error executing set_ring command")
            return True
        return await super().handle(data, handler, websocket)
