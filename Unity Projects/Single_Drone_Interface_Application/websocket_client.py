import asyncio
import websockets
import json
from drone_handler import get_drone_data
from config import ws_address,default_takeoff_height, default_takeoff_duration

class WebSocketClient:
    def __init__(self, uri=ws_address, crazyflie_handler=None):
        self.uri = uri
        self.cfHandler = crazyflie_handler
        self.running = True
        if self.cfHandler:
            return
        else:
            raise ValueError("Crazyflie handler must be provided")

    async def connect(self):
        """Connect to the WebSocket server and handle communication."""
        while self.running:
            try:
                async with websockets.connect(self.uri) as websocket:
                    print("Connected to WebSocket server")
                    await self.handle_connection(websocket)
            except (ConnectionRefusedError, OSError, websockets.exceptions.ConnectionClosedOK) as e:
                print(f"Connection error: {e}. Retrying in 2 seconds...")
                await asyncio.sleep(2)

    async def handle_connection(self, websocket):
        """Handle communication with the WebSocket server."""
        while self.running:
            try:
                # Receive JSON message from the server
                response = await websocket.recv()
                await self.process_message(response, websocket)
            except websockets.exceptions.ConnectionClosed as e:
                print(f"Connection closed: {e}. Reconnecting...")
                break

    async def process_message(self, message, websocket):
        """Process incoming messages and send responses."""
        try:
            data = json.loads(message)
            if "command" in data:
                command = data["command"]
                print(f"Received command: {command}")
                if self.cfHandler:
                    if command == "takeoff":
                        self.cfHandler.takeoff(data.get("height", default_takeoff_height), data.get("duration", default_takeoff_duration))
                    elif command == "land":
                        self.cfHandler.land()
                    elif command == "reset_position":
                        self.cfHandler.reset_position()
                    elif command == "stop":
                        self.cfHandler.stop_drone()
                    elif command == "move_to":
                        x_target = data.get("x", 0.0)
                        y_target = data.get("y", 0.0)
                        z_target = data.get("z", 0.0)
                        yaw_target = data.get("yaw", 0.0)
                        self.cfHandler.move_to(x_target, z_target, y_target, yaw_target)
                    else:
                        print(f"Unknown command: {command}")
            # Send status data back
            if self.cfHandler:
                current_height, battery_voltage, drone_connected, drone_armed, battery_state = get_drone_data()
                x = self.cfHandler.get_x()
                y = self.cfHandler.get_y()
                z = self.cfHandler.get_z()
                yaw = self.cfHandler.get_yaw()
                status_message = json.dumps({
                    "pos": {"x": x, "y": z, "z": y},
                    "height": current_height,
                    "battery": battery_voltage,
                    "connected": drone_connected,
                    "armed": drone_armed,
                    "battery_state": battery_state,
                    "yaw": yaw
                })
                await websocket.send(status_message)
        except json.JSONDecodeError:
            print(f"Invalid JSON received: {message}")

    def start(self):
        """Start the WebSocket client."""
        asyncio.run(self.connect())

    def stop(self):
        """Stop the WebSocket client."""
        self.running = False