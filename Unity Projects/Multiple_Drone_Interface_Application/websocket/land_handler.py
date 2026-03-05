import logging
from typing import Dict, Any
from websocket.base_handler import WebSocketHandler

class LandHandler(WebSocketHandler):
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        command = data.get("command")
        if command == "land":
            handler.land()
            logging.getLogger(__name__).info(f"Handled land for {data.get('drone_id')}")
            return True
        return await super().handle(data, handler, websocket)
