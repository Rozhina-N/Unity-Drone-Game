
# Crazyflie Drone Interface

  

The *Crazyflie Drone Interface* is a Python application that allows you to control a Crazyflie 2.x drone using a simple user interface and a WebSocket connection to an external application (such as a Unity game).

  

The application supports live telemetry, basic drone control (takeoff, land, move_to), and connection status feedback. It can safely switch between multiple drones and supports automatic reconnection.

  

---

  

## Features

  

- ✅ Control Crazyflie drone from a GUI

- ✅ WebSocket server integration for remote control from Unity

- ✅ Real-time position, battery, yaw and lighthouse status monitoring

- ✅ Emergency stop and safe landing

- ✅ Support for multiple drones (switch between two addresses)

- ✅ Modular design for easy expansion

  

---

  

## Architecture Overview

  
+-------------------+ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎+------------------------------+
|‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎‎ ‎ ‎ ‎|‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎‎ |‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |
|‎ ‎ Unity / Game‎ ‎ ‎ ‎|‎ ‎ ‎ <-‎->‎‎ ‎ |  WebSocketClient ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |
|‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎‎ ‎ ‎  ‎ |‎ ‎ (websocket_client.py)  ‎ ‎ ‎ ‎ ‎ ‎|
+-------------------+‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎+------------------------------+
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ V
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ +------------------------+
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎    |‎ ‎ ‎ CrazyflieHandler‎ ‎‎‎ ‎  |
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |    (drone_handler.py) ‎  |
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ +------------------------+
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎V
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ +------------------------+
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ |   Crazyflie 2.1 Drone  |
‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ ‎ +------------------------+


---

## Project Structure

project-root/  
├── **main**.py # Entry point, application startup  
├── drone_handler.py # Main logic for Crazyflie communication  
├── websocket_client.py # WebSocket server client logic  
├── ui.py # Simple Tkinter GUI for drone control & monitoring  
├── config.py # Central configuration (limits, addresses, etc.)  
└── requirements.txt # Dependencies (optional to add)


---

## Usage

### 1️⃣ Install dependencies

```bash
pip install cflib
pip install websockets
```
_Note:_ Tkinter is included by default in most Python installations.

### 2️⃣ Run the application

```bash
`python __main__.py`
```

### 3️⃣ Control options

-   Use the **DroneSelector** to select your drone (Drone 1 or Drone 2).
    
-   The main window provides:
    
    -   Current drone position (X, Y, Z)
        
    -   Battery status
        
    -   Lighthouse status
        
    -   Armed state (is the drone allowed to fly)
        
-   Buttons:
    
    -   Stop: immediately stops the drone.
        
    -   Reboot: restarts the drone (STM reset, doesn't restart lighthouse deck properly).
        
    -   Switch Drone: switch between Drone 1 and Drone 2.
        

----------

### 4️⃣ WebSocket API

The app connects to the WebSocket server at:
`ws://localhost:8765/drone` 

#### Incoming commands (from Unity):

```json
{  "command":  "takeoff",  "height":  1.0,  "duration":  2.0  } 
```
```json 
{  "command":  "land"  }
```
```json
 {  "command":  "reset_position"  } 
```
```json
{  "command":  "stop"  } 
```
```json
 {  "command":  "move_to",  "x":  1.0,  "y":  1.0,  "z":  1.0,  "yaw":  0.0  }
``` 

#### Outgoing data (to Unity):

```json
{  "pos":  {"x": X,  "y": Z,  "z": Y},  "height": Z,  "battery": BATTERY_LEVEL,  "connected":  true|false,  "armed": SUPERVISOR_STATUS,  "battery_state": BATTERY_STATE,  "yaw": YAW }
``` 

----------

## Notes

-   The system supports **safe boundaries**: `max_x`, `max_y`, `max_z` are configurable in `config.py`.
    
-   The drone is prevented from moving outside the safe flight area.
    
-   Lighthouse positioning is required for accurate flight control.
    

----------

## Future Improvements

-   Add unit tests
    
-   Add support for more drones (scalable architecture)
    
-   Improve reconnect logic
    
-   Support for headless (non-GUI) mode
    

----------
