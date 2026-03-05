import json
import unittest
from typing import Dict, List, Any

from websocket.websocket_client import WebSocketClient


class DummyHandler:
    def __init__(self, name: str, battery_v: float = 4.0) -> None:
        self._name: str = name
        self._battery_v: float = battery_v
        self.x: float = 0.1
        self.y: float = -0.2
        self.z: float = 0.5
        self.yaw: float = 0.0

    def get_x(self) -> float:
        return self.x

    def get_y(self) -> float:
        return self.y

    def get_z(self) -> float:
        return self.z

    def get_yaw(self) -> float:
        return self.yaw

    def get_drone_connected(self) -> bool:
        return True

    def takeoff(self, height: float, duration: float) -> None:
        self.z = height

    def land(self) -> None:
        self.z = 0.0

    def stop_drone(self) -> None:
        pass

    @property
    def battery_voltage(self) -> float:
        return self._battery_v


class FakeWebSocket:
    def __init__(self) -> None:
        self.sent: List[str] = []

    async def send(self, data: str) -> None:
        self.sent.append(data)


class TestWebSocketMock(unittest.IsolatedAsyncioTestCase):
    async def test_process_message_and_status(self) -> None:
        handlers: Dict[str, DummyHandler] = {
            "drone1": DummyHandler("drone1", battery_v=3.9),
            "drone2": DummyHandler("drone2", battery_v=3.5),
        }
        client = WebSocketClient(handlers=handlers)

        fake_ws = FakeWebSocket()

        # send a takeoff command to drone1
        msg = json.dumps(
            {"drone_id": "drone1", "command": "takeoff", "height": 0.8, "duration": 2.0}
        )
        await client.process_message(msg, fake_ws)

        # Last sent message should be a status JSON
        self.assertTrue(fake_ws.sent)
        status: Dict[str, Any] = json.loads(fake_ws.sent[-1])
        self.assertIn("drones", status)
        self.assertIn("drone1", status["drones"])
        self.assertAlmostEqual(status["drones"]["drone1"]["z"], 0.8, places=2)
        # battery fields
        self.assertIn("battery_v", status["drones"]["drone1"])
        self.assertIn("battery_pct", status["drones"]["drone1"])


if __name__ == "__main__":
    unittest.main()
