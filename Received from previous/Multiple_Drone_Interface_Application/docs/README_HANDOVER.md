## 3. Drone Interface Application

### 3.1 Overzicht

Deze applicatie is gebouwd om Crazyflie 2.x drones aan te sturen via twee interfaces:
1. **Directe bediening**: Tkinter desktop GUI voor handmatige controle en monitoring
2. **Externe controle**: WebSocket server voor Unity/game integratie

De modulaire opbouw maakt het eenvoudig om nieuwe functies toe te voegen of bestaande componenten aan te passen. Elke component heeft een duidelijke verantwoordelijkheid, wat debugging en uitbreiding vergemakkelijkt.

---

### 3.2 Architectuur

De applicatie gebruikt een gelaagde opbouw waarbij elke laag een specifieke taak heeft:

```
Unity/Game → WebSocket → WebSocketClient → DroneManager → DroneController → Crazyflie Hardware
                                                              ↓
                                                    ┌─────────┼─────────┐
                                           ConnectionManager  │  CommandExecutor
                                                    TelemetryHandler
```

**Waarom deze opbouw?**
- **DroneController** = Facade die complexiteit verbergt
- **ConnectionManager, TelemetryHandler, CommandExecutor** = Gescheiden verantwoordelijkheden (Single Responsibility Principle)
- **DroneManager** = Centrale plek voor multi-drone beheer
- **DroneUI** = Werkt parallel, geen dependencies op WebSocket

---

### 3.3 Classes

#### 3.3.1 Application (__main__.py)

**Functie**: Entry point die alle componenten opstart en coördineert.

**Wat doet het:**
- Laadt configuratie (defaults + `drones.json`)
- Start logging systeem
- Creëert DroneManager met alle drones
- Start WebSocket op aparte thread (zodat UI niet blokkeert)
- Start Tkinter UI op main thread

**Startup volgorde** (belangrijk bij debugging):
```
1. ConfigurationBuilder → 2. Logging → 3. DroneManager → 4. WebSocket thread → 5. UI main thread
```

**Tip voor uitbreidingen**: Nieuwe componenten toevoegen? Voeg toe tussen stap 3 en 4, zodat WebSocket en UI er gebruik van kunnen maken.

---

#### 3.3.2 DroneManager (drone_manager.py)

**Functie**: Centrale plek om alle drones te beheren en events te distribueren.

**Wat doet het:**
- **Registry**: Houdt alle DroneControllers bij in een OrderedDict (drone_id → handler)
- **Event systeem**: Observer pattern - components kunnen subscriben op drone events
- **Multi-drone**: Ondersteunt tot 4 drones (configureerbaar via `max_drones`)
- **Rate limiting**: Voorkomt command flooding via `move_to_rate_hz`

**Belangrijke methodes:**
- `add(drone_id, handler)`: Nieuwe drone registreren
- `remove(drone_id)`: Drone verwijderen
- `subscribe(callback)`: Event listener toevoegen (bijv. voor UI updates)
- `_notify(event, drone_id)`: Alle subscribers notificeren

**Uitbreiden**: Meer drones nodig? Pas `max_drones` aan in `drones.json` configuratie.

---

#### 3.3.3 DroneController (drone_controller.py)

**Functie**: Hoofd interface voor drone operaties. Verbergt complexiteit door te delegeren naar gespecialiseerde componenten.

**Waarom gesplitst?** Elke component heeft één taak (Single Responsibility):

**1. ConnectionManager** (`connection_manager.py`)
- Verbinding opzetten/verbreken
- Link quality monitoren
- Auto-reconnect bij verbindingsverlies

**2. TelemetryHandler** (`telemetry_handler.py`)
- Real-time positie (X, Y, Z)
- Battery percentage
- Lighthouse positioning status
- Yaw (rotatie)

**3. CommandExecutor** (`command_executor.py`)
- Vlucht commando's uitvoeren
- Safety validatie (battery, lighthouse, positie limits)
- LED ring control
- Emergency stop

**Veel gebruikte methodes:**
```python
controller.connect()                    # Start verbinding
controller.takeoff(height, duration)    # Opstijgen
controller.move_to(x, y, z, velocity)   # Beweeg naar positie
controller.land(duration)               # Landen
controller.emergency_stop()             # Noodstop
```

**Safety features** (automatisch gecontroleerd):
- Positie binnen limits (max_x, max_y, max_z)
- Battery level voldoende
- Lighthouse actief voor positionering
- Drone connected voordat commando's worden verstuurd

---

#### 3.3.4 WebSocketClient (websocket_client.py)

**Functie**: WebSocket server waarmee Unity/games drones kunnen besturen.

**Message formaat** (JSON):
```json
{
  "drone_id": "drone1",
  "command": "takeoff",
  "params": {"height": 0.5}
}
```

**Command handlers** (Chain of Responsibility pattern):
- `TakeoffHandler`: Opstijgen
- `LandHandler`: Landen  
- `MoveToHandler`: Naar positie bewegen (x, y, z, velocity)
- `LedColorHandler`: LED kleuren instellen
- `RingHandler`: LED effecten

**Hoe werkt de handler chain?**
1. Bericht komt binnen
2. Eerste handler controleert: "Is dit mijn command?"
3. Zo ja → verwerk en stuur response
4. Zo nee → geef door aan volgende handler

**Nieuwe command toevoegen:**
1. Maak nieuwe handler in `websocket/` folder
2. Extend `BaseHandler`
3. Implementeer `can_handle()` en `handle()`
4. Registreer in WebSocketClient handler chain

**Auto-reconnect**: WebSocket probeert automatisch opnieuw te verbinden bij disconnect (configureerbaar via `retry_delay`).

---

#### 3.3.5 DroneUI (main_window.py)

**Functie**: Desktop GUI voor handmatige drone controle en real-time monitoring.

**UI Componenten** (modulair opgebouwd):
- **DroneManagerComponent**: Drone selectie, connect/disconnect knoppen
- **DroneStatusComponent**: Live positie, battery %, lighthouse status
- **RadioStatusComponent**: Link quality indicator
- **TerminalComponent**: Command logs en error messages
- **SettingsComponent**: Runtime configuratie aanpassingen

**Styling**: Dark theme (#2D2D2D), always-on-top, gecentreerd bij start

**UI updaten vanuit code:**
```python
main_window.update_status()  # Refresh alle status displays
```

**Component toevoegen:**
1. Maak nieuwe class in `ui/` folder
2. Extend Tkinter Frame
3. Implementeer UI layout in `__init__()`
4. Registreer in `main_window._create_components()`

---

#### 3.3.6 ConfigurationService (config_service.py)

**Functie**: Centrale configuratie management met type-safety.

**Waarom zo complex?**
- **Immutable**: Configuratie kan niet per ongeluk worden aangepast (frozen dataclasses)
- **Type-safe**: IDE autocomplete en compile-time checks
- **Thread-safe**: Geen race conditions bij multi-threading

**Configuratie secties:**

**DroneConfig** - Drone instellingen
```python
default_drones: list       # Lijst van drones uit drones.json
max_drones: int = 4        # Maximum aantal drones
max_x, max_y, max_z        # Veiligheidslimieten voor beweging
default_takeoff_height     # Standaard opstijg hoogte
```

**WebSocketConfig** - WebSocket server
```python
address: str = "ws://localhost:8765"  # WebSocket URL
retry_delay: float                    # Reconnect interval
```

**PerformanceConfig** - Performance tuning
```python
move_to_rate_hz              # Command rate limiting (Hz)
telemetry_update_rate_ms     # Telemetrie refresh rate (ms)
```

**DevelopmentConfig** - Development settings
```python
disable_pycache: bool   # .pyc files uitschakelen
log_level: str          # Logging verbosity
```

**Configuratie aanpassen:**
1. Pas `drones.json` aan voor drone specifieke settings
2. Voor code changes: wijzig defaults in `config_service.py`
3. Configuratie wordt geladen via: `ConfigurationBuilder().with_defaults()`

---

### 3.4 Workflows

#### 3.4.1 Application Startup

**Volgorde** (belangrijk bij troubleshooting):
```
1. ConfigurationBuilder laadt defaults + drones.json
2. Logging systeem start (file + console handlers)
3. DroneManager + DroneControllers per drone
4. WebSocket start op background thread
5. DroneUI start op main thread (Tkinter event loop)
```

**Debugging tip**: Check `logs/module_load.txt` voor startup errors.

---

#### 3.4.2 Drone Verbinden

**Flow:**
```
Gebruiker selecteert drone → UI roept connect() aan → ConnectionManager opent cflib link
→ Callbacks registreren → TelemetryHandler start (200ms updates)
→ UI toont "Connected" + real-time data
```

**Troubleshooting**:
- Geen verbinding? Check Crazyradio PA USB dongle
- Link quality laag? Verminder afstand of interferentie
- Lighthouse required? Zorg dat positioning systeem actief is

---

#### 3.4.3 Commando's Uitvoeren

**Via UI (handmatig):**
```
Button click → DroneController method → CommandExecutor validatie
→ Safety checks (connected? lighthouse? battery?)
→ Command naar Crazyflie → Feedback in terminal
```

**Via WebSocket (Unity):**
```
Unity stuurt JSON → WebSocketClient parse → DroneManager zoekt handler
→ Handler chain verwerkt command → DroneController method
→ Response terug naar Unity
```

**Safety validatie** (automatisch):
- ✓ Drone connected?
- ✓ Lighthouse actief?
- ✓ Battery level OK?
- ✓ Positie binnen limits?

---

#### 3.4.4 Afsluiten (Graceful Shutdown)

**Volgorde** (voorkomt crashes/data loss):
```
Window close / Ctrl+C → Application.shutdown() aangeroepen
→ Emergency land voor drones in de lucht
→ Stop telemetry logging
→ Sluit alle drone connecties
→ Stop WebSocket server
→ Join background threads (wacht tot klaar)
→ Flush logging handlers
→ Exit applicatie
```

**Waarom deze volgorde?** Voorkomt dat drones blijven vliegen of data verloren gaat bij crash.

---

### 3.5 Testing

**Tests uitvoeren**: Dubbelklik `run_tests.bat` of draai `pytest` in terminal.

#### Unit Tests (`tests/`)

Testen individuele componenten zonder hardware:

- **test_drone_controller_unit.py**: DroneController logica (met mock cflib)
- **test_drone_manager.py**: Handler registry, event systeem
- **test_battery.py**: Battery monitoring en warnings
- **test_config_service.py**: Configuration builder, immutability

**Wanneer draaien**: Voor elke code commit, draait lokaal zonder hardware.

---

#### Integration Tests

Testen samenwerking tussen componenten:

- **test_websocket_mock.py**: WebSocket → DroneManager → DroneController flow
- **test_reconnect.py**: Auto-reconnect scenario's

**Wanneer draaien**: Voor major features, test eind-tot-eind flows.

---

#### Hardware Tests (`tools/`)

Vereisen fysieke hardware:

- **cf_link_test.py**: Test Crazyradio PA connectie en link quality
- **multi_drone_radio_test.py**: Test meerdere drones tegelijk (interferentie)

**Wanneer draaien**: Bij hardware problemen of nieuwe drone setup.

---

#### Utilities

- **test_imports.py**: Valideer alle imports werken (geen circular dependencies)

**Tip**: Draai dit eerst bij import errors om de oorzaak te vinden.

---

#### Nieuwe Test Toevoegen

1. Maak file in `tests/` folder
2. Naam: `test_<component>.py`
3. Gebruik pytest fixtures voor setup
4. Mock externe dependencies (cflib, hardware)

**Voorbeeld:**
```python
import pytest
from unittest.mock import Mock

def test_my_feature():
    # Arrange
    mock_cflib = Mock()
    
    # Act
    result = my_function(mock_cflib)
    
    # Assert
    assert result == expected_value
```

---

### 3.6 Aan de Slag

#### Vereisten

**Software:**
- Python 3.8 of hoger
- Virtual environment is pre-configured (`.venv` folder)
- Dependencies: cflib, websockets (automatisch geïnstalleerd)
- Tkinter (included bij Python)

**Hardware:**
- Crazyradio PA USB dongle (voor radio communicatie)
- Crazyflie 2.x drone(s)
- Lighthouse positioning systeem (sterk aanbevolen voor stabiele positionering)

---

#### Quick Start

**Eerste keer opstarten:**
1. Zorg dat Python 3.8+ geïnstalleerd is
2. Dubbelklik `Drone Interface.lnk`
3. Wacht terwijl automatisch setup draait (eerste keer ~1 minuut)

**Wat gebeurt er automatisch?**
```
Drone Interface.lnk → start.bat → controleert .venv
→ installeert dependencies (indien nodig) → start __main__.py
```

**Handmatig starten** (voor development):
```powershell
# Optie 1: Via batch script
start.bat

# Optie 2: Direct Python
.venv\Scripts\python.exe __main__.py
```

---

#### Drone Configuratie

**drones.json** (optioneel, plaats in project root):
```json
{
  "drones": [
    {
      "id": "drone1",
      "address": "radio://0/80/2M/E7E7E7E701"
    },
    {
      "id": "drone2",
      "address": "radio://0/80/2M/E7E7E7E702"
    }
  ]
}
```

**Radio address vinden:**
1. Gebruik Crazyflie Client software
2. Scan for drones
3. Kopieer het radio:// adres

---

#### Nuttige Scripts

| Script | Functie | Wanneer gebruiken |
|--------|---------|-------------------|
| `start.bat` | Start applicatie | Normale gebruik |
| `run_tests.bat` | Draai alle tests | Voor commits/debugging |
| `tools/cf_link_test.py` | Test radio link | Hardware troubleshooting |

---

#### Troubleshooting

**Applicatie start niet:**
- Check `last_startup_error.txt` voor error details
- Check `logs/module_load.txt` voor import errors

**Geen verbinding met drone:**
- Crazyradio PA USB dongle aangesloten?
- Drone aan? (power button > 3 sec)
- Juiste radio address in `drones.json`?

**Import errors:**
- Draai `run_tests.bat` → kijk naar test_imports.py output
- Herinstalleer dependencies: `.venv\Scripts\pip install -r requirements.txt`

---

### 3.7 Development Guidelines

#### Code Organisatie

**Single Responsibility Principle**: Elke class/module heeft één duidelijke taak.

**Voorbeelden:**
- ✓ `ConnectionManager` doet alleen connectie beheer
- ✓ `TelemetryHandler` doet alleen sensor data
- ✗ Vermijd "god classes" die alles doen

**Dependency Injection**: Geef dependencies mee via constructor, geen globals.
```python
# ✓ Goed
class DroneController:
    def __init__(self, config: DroneConfig):
        self.config = config

# ✗ Slecht  
class DroneController:
    def __init__(self):
        self.config = GLOBAL_CONFIG  # Moeilijk te testen
```

**Type Hints**: Gebruik overal type hints voor IDE support.
```python
def move_to(x: float, y: float, z: float) -> bool:
    ...
```

---

#### Error Handling

**Altijd loggen**: Exceptions naar file én UI terminal.
```python
try:
    drone.takeoff()
except Exception as e:
    logger.error(f"Takeoff failed: {e}")
    ui.show_error("Kon niet opstijgen")  # User-friendly
```

**Graceful degradation**: App blijft werken als onderdeel faalt.
- Geen hardware? → Toon warning, blijf draaien
- WebSocket disconnect? → Auto-reconnect
- Drone disconnect? → UI blijft responsief

---

#### Threading

**Thread verdeling** (belangrijk!):
- **Main thread**: Tkinter UI (Tkinter requirement)
- **Background thread**: WebSocket asyncio
- **Callback threads**: Drone cflib callbacks

**Thread-safety**: Gebruik locks bij shared resources.
```python
with self._lock:
    self._shared_data = new_value
```

**Shutdown**: Join threads in juiste volgorde (zie 3.4.4).

---

#### Performance

**Rate Limiting**: Voorkom command flooding.
```python
# Configureerbaar via performance.move_to_rate_hz
if time.time() - last_command < min_interval:
    return  # Skip command
```

**Object Pooling**: Hergebruik objecten voor telemetrie logs.

**Observer Pattern**: Efficient event distribution zonder polling.

---

#### Nieuwe Features Toevoegen

**Checklist:**
1. ✓ Bepaal welk component verantwoordelijk is
2. ✓ Schrijf unit test eerst (TDD)
3. ✓ Implementeer met type hints
4. ✓ Voeg error handling toe
5. ✓ Update documentatie
6. ✓ Test met echte hardware

**Voorbeeld: Nieuwe WebSocket command**
1. Maak `websocket/my_handler.py`
2. Extend `BaseHandler`
3. Implementeer `can_handle()` en `handle()`
4. Registreer in `websocket_client.py` handler chain
5. Schrijf test in `tests/test_websocket_mock.py`
