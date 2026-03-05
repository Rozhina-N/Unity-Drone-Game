import logging
from typing import Dict, Any
from websocket.base_handler import WebSocketHandler

class MoveToHandler(WebSocketHandler):
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        command = data.get("command")
        if command == "move_to":
            try:
                handler.move_to(
                    data.get("x"),
                    data.get("y"),
                    data.get("z"),
                    data.get("yaw", 0.0),
                )
                logging.getLogger(__name__).debug(
                    "Handled move_to for %s", data.get("drone_id") or data.get("id")
                )
            except Exception:
                logging.getLogger(__name__).exception("Error handling move_to command")
            return True
        return await super().handle(data, handler, websocket)
