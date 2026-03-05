from abc import ABC, abstractmethod
from typing import Dict, Any, Optional

class WebSocketHandler(ABC):
    def __init__(self, successor: Optional['WebSocketHandler'] = None) -> None:
        self._successor: Optional['WebSocketHandler'] = successor

    @abstractmethod
    async def handle(self, data: Dict[str, Any], handler: Any, websocket: Any) -> bool:
        if self._successor:
            return await self._successor.handle(data, handler, websocket)
        return False
