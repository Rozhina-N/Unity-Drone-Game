import threading
import signal
import tkinter as tk

from drone_handler import start_drone
from ui import DroneUI, DroneSelector
from websocket_client import WebSocketClient

crazyflie_handler = None
websocket_client = None

def shutdown_handler(sig, frame):
    global running, crazyflie_handler, websocket_client
    print("\nShutdown detected, closing connections...")
    if crazyflie_handler:
        crazyflie_handler.close()
    if websocket_client:
        websocket_client.stop()

if __name__ == "__main__":
    signal.signal(signal.SIGINT, shutdown_handler)
    selector = DroneSelector()
    selectedDrone = selector.get_selected_drone()
    if selectedDrone is None:
        print("No drone selected, exiting.")
    else:
        crazyflie_handler = start_drone(selectedDrone)
        # Start WebSocket client in a separate thread
        websocket_client = WebSocketClient("ws://localhost:8765/drone", crazyflie_handler)
        websocket_thread = threading.Thread(target=websocket_client.start, daemon=True)
        websocket_thread.start()

    # Start the UI
    try:
        root = tk.Tk()
        app = DroneUI(root, crazyflie_handler)
        root.mainloop()
    except KeyboardInterrupt:
        shutdown_handler(None, None)