import logging
from typing import Dict, Any
from websocket.base_handler import WebSocketHandler

class TakeoffHandler(WebSocketHandler):
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        command = data.get("command")
        if command == "takeoff":
            handler.takeoff(
                height=data.get("height"),
                duration=data.get("duration"),
            )
            logging.getLogger(__name__).info(f"Handled takeoff for {data.get('drone_id')}")
            return True
        return await super().handle(data, handler, websocket)
