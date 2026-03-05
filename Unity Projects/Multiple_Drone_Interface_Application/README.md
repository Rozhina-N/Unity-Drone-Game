# Crazyflie Drone Interface

A modular Python application for controlling multiple Crazyflie 2.x drones via a GUI and WebSocket interface for integration with external applications such as Unity games.

---

## ✨ Features

- 🎮 **Multi-drone support** - Simultaneous control of multiple drones (up to 4 drones)
- 🖥️ **Intuitive GUI** - Tkinter-based interface with real-time status monitoring
- 🌐 **WebSocket integration** - Bidirectional communication with external applications
- 📊 **Real-time telemetry** - Live position, battery, yaw and lighthouse status
- 🔒 **Safety systems** - Configurable flight boundaries and emergency stop
- 🔄 **Automatic reconnect** - Automatically restores connection on signal loss
- 🧪 **Unit tests** - Comprehensive test suite with pytest
- ⚙️ **Configuration service** - Type-safe, immutable configuration with dependency injection
- 📝 **Logging system** - Rotating file logs with UI terminal integration
- 🎨 **LED control** - Control drone LED rings and individual LED colors

---

## 🏗️ Architecture

```
┌─────────────────────┐
│   Unity / Game      │
│   (External App)    │
└──────────┬──────────┘
           │
           │ WebSocket (ws://localhost:8765/drone)
           │
┌──────────▼──────────────────────────────────┐
│  WebSocketClient                            │
│  - TakeoffHandler                           │
│  - LandHandler                              │
│  - MoveToHandler                            │
│  - LEDColorHandler                          │
│  - RingHandler                              │
└──────────┬──────────────────────────────────┘
           │
┌──────────▼──────────────────────────────────┐
│  DroneManager                               │
│  - Central management of multiple drones    │
│  - Rate limiting & command distribution     │
└──────────┬──────────────────────────────────┘
           │
     ┌─────┴─────┬─────────┬─────────┐
     │           │         │         │
┌────▼────┐ ┌───▼────┐ ┌──▼─────┐ ┌─▼──────┐
│ Drone 1 │ │ Drone 2│ │ Drone 3│ │ Drone 4│
│ Handler │ │ Handler│ │ Handler│ │ Handler│
└────┬────┘ └───┬────┘ └──┬─────┘ └─┬──────┘
     │          │          │          │
     │  ┌───────┴──────────┴─────┐    │
     │  │  ConnectionManager     │    │
     │  │  RadioHandler          │    │
     └──┤  TelemetryHandler      ├────┘
        │  CommandExecutor       │
        └───────────┬────────────┘
                    │
        ┌───────────▼────────────┐
        │  Crazyflie 2.x Drones  │
        │  (via Crazyradio PA)   │
        └────────────────────────┘
```

### Components

#### Core Modules
- **`DroneManager`** - Central management of multiple drone handlers
- **`Drone`** - Individual drone representation with state management
- **`ConnectionManager`** - Management of Crazyflie connections
- **`TelemetryHandler`** - Process telemetry data (position, battery, etc.)
- **`RadioHandler`** - Radio communication and link quality monitoring
- **`CommandExecutor`** - Thread-safe command execution queue

#### Configuration
- **`ConfigurationService`** - Immutable, type-safe configuration with builder pattern
- **`drones.json`** - Drone definitions (IDs and radio addresses)

#### WebSocket Handlers
- **`TakeoffHandler`** - Takeoff commands
- **`LandHandler`** - Landing procedures
- **`MoveToHandler`** - Position navigation with rate limiting
- **`LEDColorHandler`** - LED color control
- **`RingHandler`** - LED ring effects

#### Utilities
- **`battery.py`** - LiPo discharge curve and percentage calculation
- **`led.py`** - LED utility functions

---

## 📁 Project Structure

```
DroneInterface/
├── __main__.py                 # Applicatie entry point
├── config_service.py           # Configuratie service (builder pattern)
├── requirements.txt            # Python dependencies
├── pytest.ini                  # Pytest configuratie
│
├── drones/                     # Drone core modules
│   ├── drone_manager.py        # Multi-drone manager
│   ├── drone.py                # Drone class
│   ├── drone_controller.py     # Legacy controller (deprecated)
│   ├── connection_manager.py   # Connection management
│   ├── telemetry_handler.py    # Telemetry processing
│   ├── radio_handler.py        # Radio communication
│   ├── command_executor.py     # Command queue executor
│   └── drones.json             # Drone configuration
│
├── websocket/                  # WebSocket server & handlers
│   ├── websocket_client.py     # WebSocket server
│   ├── base_handler.py         # Base handler class
│   ├── takeoff_handler.py
│   ├── land_handler.py
│   ├── move_to_handler.py
│   ├── led_color_handler.py
│   └── ring_handler.py
│
├── ui/                         # Tkinter GUI components
│   ├── main_window.py          # Main window
│   ├── drone_panel.py          # Drone control panel
│   ├── drone_status_component.py
│   ├── drone_manager_component.py
│   ├── radio_status_component.py
│   ├── settings_component.py
│   ├── terminal_component.py
│   └── dialogs.py
│
├── utils/                      # Utility modules
│   ├── battery.py              # Battery calculations
│   └── led.py                  # LED utilities
│
├── tests/                      # Unit tests
│   ├── test_battery.py
│   ├── test_battery_status.py
│   ├── test_config_service.py
│   ├── test_drone_controller_unit.py
│   ├── test_drone_manager.py
│   ├── test_led.py
│   ├── test_reconnect.py
│   └── test_websocket_mock.py
│
├── logging_config/             # Logging setup
├── logs/                       # Runtime logs
├── cache/                      # Drone cache files
└── docs/                       # Documentation
```

---

## 🚀 Installation & Usage

### Requirements

**Hardware:**
- Crazyradio PA USB dongle
- Crazyflie 2.x drone(s) with Lighthouse deck
- Computer with USB port (Windows/Linux/macOS)

**Software:**
- Python 3.10 or higher
- pip (Python package manager)

That's all you need! The application will handle the rest, including virtual environment setup when using the provided batch files.

### Quick Start (Windows)

The easiest way to get started on Windows:

1. **Clone or download this repository**

2. **Run the setup script:**
   ```cmd
   start.bat
   ```

The `start.bat` script will automatically:
- Create a Python virtual environment (`.venv`) if it doesn't exist
- Install all required dependencies (`cflib`, `websockets`)
- Activate the virtual environment
- Start the application

**Note:** On first run, the script will take a moment to set up the environment and install dependencies.

### Manual Setup

If you prefer manual setup or are using Linux/macOS:

1. **Create virtual environment:**

```bash
python -m venv .venv
```

2. **Activate virtual environment:**

**Windows (PowerShell):**
```powershell
.venv\Scripts\Activate.ps1
```

**Windows (CMD):**
```cmd
.venv\Scripts\activate.bat
```

**Linux/macOS:**
```bash
source .venv/bin/activate
```

3. **Install dependencies:**

```bash
pip install -r requirements.txt
```

**Core dependencies:**
- `cflib` - Crazyflie Python library
- `websockets` - WebSocket server

**Development dependencies (optional):**
```bash
pip install -U pytest black ruff
```

### Configuration

1. **Edit `drones/drones.json`** with your drone addresses:

```json
[
  {
    "id": "drone1",
    "address": "radio://0/80/2M/E7E7E7E7E1"
  },
  {
    "id": "drone2",
    "address": "radio://0/80/2M/E7E7E7E7E2"
  }
]Usage

### GUI Interface

The main interface displays for each drone:

- **Status indicators:**
  - 🔌 Connection status
  - 🔋 Battery voltage and percentage
  - 📡 Radio link quality
  - 🏠 Lighthouse deck status
  - ✈️ Armed state (ready for flight)
  - 📍 Current position (x, y, z)
  - 🧭 Yaw (orientation)

- **Control buttons:**
  - **Connect/Disconnect** - Connect or disconnect from drone
  - **Stop** - Emergency stop (immediate)
  - **Reboot** - Re
```

**Manual start with console:**
```bash
python __main__.py
```

**Without console (Windows):**
```bash
pythonw __main__.py
```

**Additional batch files:**
- `run_tests.bat` - Run the test suite

---

## 🎮 Gebruik

### GUI Interface

De hoofdinterface toont voor elke drone:

- **Status indicators:**
  - 🔌 Verbindingsstatus
  - 🔋 Batterij voltage en percentage
  - 📡 Radio link quality
  - 🏠 Lighthouse deck status
  - ✈️ Armed state (klaar voor vlucht)
  - 📍 Huidige positie (x, y, z)
  - 🧭 Yaw (orientatie)

- **Control buttons:**
  - **Connect/Disconnect** - Verbind of verbreek verbinding
  - **Stop** - Emergency stop (immediate)
  - **Reboot** - Herstart drone controller

De WebSocket server draait standaard op **`ws://localhost:8765/drone`**

#### 📥 Incoming Commands (van Unity/Client naar Drone)

**Takeoff:**
```json
{
  "command": "takeoff",
  "height": 1.0,
  "duration": 2.0,
  "drone_id": "drone1"
}
```

**Land:**
```json
{
  "command": "land",
  "drone_id": "drone1"
}
```

**Move To Position:**
```json
{
  "command": "move_to",
  "x": 1.0,
  "y": 1.0,
  "z": 1.0,
  "yaw": 0.0,
  "drone_id": "drone1"
}
```

**Emergency Stop:**
```json
{
  "command": "stop",from Drone to
  "drone_id": "drone1"
}
```

**LED Color Control:**
```json
{
  "command": "led_color",
  "r": 255,
  "g": 0,
  "b": 0,
  "drone_id": "drone1"
}
```

**LED Ring Effect:**
```json
{
  "command": "ring",
  "effect": "solidColor",
  "r": 0,
  "g": 255,
  "b": 0,
  "drone_id": "drone1"
}
```

**Reset Position:**
```json
{
  "command": "reset_position",
  "drone_id": "drone1"
}
```

#### 📤 Outgoing Data (van Drone naar Unity/Client)

**Multi-drone status:**
```json
{
  "drones": {
    "drone1": {
      "x": 0.0,
      "y": 0.0,
      "z": 0.5,
      "yaw": 0.0,
      "battery_v": 3.95,
      "battery_pct": 72.5,
      "connected": true,
      "armed": true,
      "battery_state": "CHARGED",
      "link_quality": 95
    },
    "drone2": {
      "x": 0.1,
      "y": -0.1,
      "z": 0.0,
      "yaw": 45.0,
      "battery_v": 3.42,
      "battery_pct": 15.0,
      "connected": false,
      "armed": false,
      "battery_state": "LOW_POWER",
      "link_quality": 0
    }
  }
}
```

**Rate limiting:**
- Move commands are automatically rate-limited to prevent overload
- Configurable via `performance.move_to_rate_hz` in config

---

## 🧪 Testing

The project contains a comprehensive test suite with pytest.

### Available Tests

```
tests/
├── test_battery.py              # Battery berekening tests
├── test_battery_status.py       # Battery status logic tests
├── test_config_service.py       # Configuration service tests
├── test_drone_controller_unit.py # Drone controller unit tests
├── test_drone_manager.py        # Drone manager tests
├── test_led.py                  # LED utility tests
├── test_reconnect.py            # Reconnection logic tests
└── test_websocket_mock.py       # WebSocket mock tests
```

### Running Tests

**All tests:**
```bash
pytest
```

**Met verbose output:**
```bash
pytest -v
```

**Specific test file:**
```bash
pytest tests/test_battery.py
```

**Met coverage:**
```bash
pytest --cov=drones --cov=websocket --cov=utils
```

**Tests met output:**
```bash
pytest -s
```

### Test Utilities

- Mock-based tests for hardware-independent testing
- Configuration builder for test scenarios
- Isolated tests without external dependencies

---

## 📝 Logging

The logging system writes to multiple outputs:

### File Logging

**Location:** `logs/drone_app.log`

**Configuration:**
- Rotating file handler
- Max 5 MB per file
- 5 backup files kept (≈25 MB total)
- Format: `HH:MM:SS LEVEL: message`

**Module-specific logs:**
- `logs/module_load.txt` - Module load diagnostics

### UI Terminal

- Live log feed in the GUI terminal component
- Color-coded log levels
- Scrollable history

### Adjusting Configuration

Edit `__main__.py` or `logging_config/logging_setup.py`:

```python
RotatingFileHandler(
    filename="logs/drone_app.log",
    maxBytes=5 * 1024 * 1024,  # 5 MB
    backupCount=5
)
```

---

## ⚙️ Configuration Service

The project uses a modern configuration service with:

- **Immutable configuration** - Frozen dataclasses
- **Type-safe** - Full type hints
- **Builder pattern** - Flexible configuration construction
- **Dependency injection** - Clean architecture

### Configuration Structure

```python
ConfigurationService
├── drone: DroneConfig
│   ├── max_drones
│   ├── connection_timeout
│   └── reconnect_delay
├── flight: FlightConfig
│   ├── max_x, max_y, max_z
│   ├── takeoff_height
│   └── landing_height
├── websocket: WebSocketConfig
│   ├── address
│   ├── port
│   └── reconnect_interval
├── performance: PerformanceConfig
│   ├── move_to_rate_hz
│   └── telemetry_rate_hz
└── paths: PathConfig
    ├── drones_json
    └── cache_dir
```

### Usage Example

```python
from config_service import ConfigurationBuilder

# Build from default config
config = ConfigurationBuilder.from_config_module().build()

# Custom configuration
config = ConfigurationBuilder()
    .with_max_drones(4)
    .with_websocket_port(8765)
    .with_flight_boundaries(2.0, 2.0, 1.5)
    .build()

# Inject in components
drone_manager = DroneManager(config=config)
```

---

## 🔒 Safety Systems

### Flight Boundaries

Configurable flight boundaries prevent drones from flying outside the safe zone:

```python
# In config_service.py
flight = FlightConfig(
    max_x=2.0,  # meters
    max_y=2.0,  # meters
    max_z=1.5   # meters
)
```

Commands that exceed these boundaries are automatically rejected.

### Emergency Stop

- **Stop button** in GUI - Immediate stop
- **WebSocket stop command** - Remote emergency stop
- **Connection loss** - Automatic landing procedure

### Battery Monitoring

- **Real-time voltage monitoring**
- **Percentage calculation** via LiPo discharge curve
- **State detection** (CHARGED, DISCHARGING, LOW_POWER, CRITICAL)
- **Warnings** for low battery

**Reference battery:** Turnigy nano-tech 750mAh 1S 35-70C
[Hobbyking Link](https://hobbyking.com/en_us/turnigy-nano-tech-750mah-1s-35-70c-lipo-pack-fits-nine-eagles-solo-pro-180.html)

### Lighthouse Positioning

- **Required** for accurate position control
- **Status monitoring** in real-time
- **Warning** on loss of lighthouse signal

---

## 🛠️ Development

### Code Formatting

**Black (formatter):**
```bash
black .
```

**Ruff (linter):**
```bash
ruff check .
```

**Ruff met auto-fix:**
```bash
ruff check --fix .
```

**Met unsafe fixes:**
```bash
ruff check --fix --unsafe-fixes .
```

### Code Style

- **PEP 8 compliant**
- **Type hints** voor alle functies
- **Docstrings** voor modules, classes en functies
- **Max line length:** 88 (Black default)

### Git Workflow

- Feature branches
- Pull requests met review
- Automated testing via GitHub Actions (indien geconfigureerd)

---

## 🔄 Multi-Drone Support

### Configuration

Edit `drones/drones.json`:

```json
[
  {
    "id": "drone1",
    "address": "radio://0/80/2M/E7E7E7E7E1"
  },
  {
    "id": "drone2",
    "address": "radio://0/80/2M/E7E7E7E7E2"
  },
  {
    "id": "drone3",
    "address": "radio://0/80/2M/E7E7E7E7E3"
  },
  {
    "id": "drone4",
    "address": "radio://0/80/2M/E7E7E7E7E4"
  }
]
```

### Rate Limiting

Command rate limiting is applied during multi-drone operations:

- **Move commands** distributed across available drones
- **Configurable rate** via `performance.move_to_rate_hz`
- **Fair scheduling** via DroneManager

### Simultaneous Control

- Each drone has its own handler and command queue
- Independent telemetry streams
- Aggregated status in WebSocket output
- Independent connection management

---

## 📚 Technical Details

### Threading Model

- **Main thread:** GUI event loop
- **Per drone:** Command executor thread
- **WebSocket:** Async I/O met asyncio
- **Thread-safe:** Queue-based command distribution

### State Management

- **Connection state:** Per drone tracking
- **Flight state:** Armed/disarmed monitoring
- **Battery state:** Voltage-based state machine
- **Position state:** Real-time Lighthouse updates

### Error Handling

- **Connection errors:** Automatic retry met exponential backoff
- **Command errors:** Graceful degradation
- **Telemetry loss:** Timeout detection en reconnect
- **Hardware errors:** Logging en user notification

---

## 🔧 Troubleshooting

### Connection Problems

**Drone won't connect:**
1. Check Crazyradio PA USB connection
2. Verify drone address in `drones.json`
3. Check drone battery level
4. Review logs in `logs/drone_app.log`

**Lighthouse not working:**
1. Check that lighthouse base stations are powered on
2. Verify line-of-sight to base stations
3. Check lighthouse deck firmware

### Performance Issues

**High latency:**
1. Lower `move_to_rate_hz` in config
2. Reduce number of simultaneous drones
3. Check for WiFi interference (2.4GHz)

**Connection drops:**
1. Check radio link quality in GUI
2. Reduce distance to Crazyradio
3. Minimize metal objects in the environment

### Testing Problems

**Tests failing:**
1. Check that virtual environment is activated
2. Install test dependencies: `pip install -U pytest`
3. Run with verbose: `pytest -v`

---

## 📖 Documentation

Additional documentation in `docs/` directory:
- `README_ADDITIONS.md` - Extra features and usage

---

## 🎯 Future Improvements

- [ ] Headless mode (zonder GUI)
- [ ] REST API naast WebSocket
- [ ] Swarm coordinatie algoritmes
- [ ] Trajectory planning
- [ ] Obstacle avoidance
- [ ] Video streaming integratie
- [ ] Enhanced autonomy features
- [ ] Mission scripting language

---

## 📄 License

This project is developed for educational purposes.

---

## 👥 Contributors

Developed for the Fontys Drones Cup project.

**Semester:** 5  
**Year:** 2025-2026
