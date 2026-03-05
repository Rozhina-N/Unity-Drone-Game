"""Configuration Service with immutable, type-safe configuration.

This module provides a clean dependency injection approach to configuration
management, replacing the global mutable config module pattern.

Design Pattern: Builder Pattern + Dependency Injection
PEP 8 Compliant: Type hints, docstrings, max line length 88

Usage:
    # Build configuration from existing config module
    config = ConfigurationBuilder.from_config_module().build()

    # Build custom configuration for testing
    config = ConfigurationBuilder()
        .with_max_drones(2)
        .with_websocket_address("ws://test:8765")
        .build()

    # Inject into components
    drone_manager = DroneManager(config=config)
"""

from dataclasses import dataclass, field, replace
from typing import Dict, List, Optional
import os
import json


@dataclass(frozen=True)
class DroneConfig:
    """Drone-specific configuration settings.

    Attributes:
        default_drones: List of default drone configurations
        max_drones: Maximum number of drones allowed
        max_x: Maximum X-axis movement in meters
        max_y: Maximum Y-axis movement in meters
        max_z: Maximum Z-axis movement in meters
        default_takeoff_height: Default takeoff height in meters
        default_takeoff_duration: Default takeoff duration in seconds
        default_land_duration: Default landing duration in seconds
    """

    default_drones: List[Dict[str, str]] = field(default_factory=lambda: [
        {"id": "drone1", "address": "radio://0/80/2M/E7E7E7E7E1"},
        {"id": "drone2", "address": "radio://0/80/2M/E7E7E7E7E2"},
        {"id": "drone3", "address": "radio://0/80/2M/E7E7E7E7E3"},
        {"id": "drone4", "address": "radio://0/80/2M/E7E7E7E7E4"},
    ])
    max_drones: int = 4
    max_x: float = 1.0
    max_y: float = 1.0
    max_z: float = 1.0
    default_takeoff_height: float = 1.0
    default_takeoff_duration: float = 2.0
    default_land_duration: float = 1.0


@dataclass(frozen=True)
class WebSocketConfig:
    """WebSocket client configuration.

    Attributes:
        address: WebSocket server address
        retry_delay: Delay between reconnection attempts in seconds
    """

    address: str = "ws://localhost:8765/drone"
    retry_delay: float = 5.0


@dataclass(frozen=True)
class SafetyConfig:
    """Safety-related configuration settings.

    Attributes:
        bypass_safety_checks: If True, bypasses lighthouse and charging
            cable checks (WARNING: Only for testing!)
        enable_param_wait: If True, wait for cflib parameter TOC before
            attempting param.set_value/get_value calls
    """

    bypass_safety_checks: bool = True
    enable_param_wait: bool = False


@dataclass(frozen=True)
class PerformanceConfig:
    """Performance tuning configuration.

    Attributes:
        move_to_rate_hz: Total move_to command rate budget (Hz) shared
            across all connected drones
        connect_max_workers: Number of drones to connect in parallel
        retry_max_workers: Number of parallel retries for failed drones
        connect_stagger_sec: Stagger between connection tasks (seconds)
        connect_handler_timeout: Handler creation timeout (seconds)
        retry_first_delay: Initial retry delay for failed drones (seconds)
    """

    move_to_rate_hz: float = 10.0
    connect_max_workers: int = 2
    retry_max_workers: int = 2
    connect_stagger_sec: float = 0.05
    connect_handler_timeout: float = 2.0
    retry_first_delay: float = 1.0


@dataclass(frozen=True)
class BatteryConfig:
    """Battery monitoring configuration.

    Attributes:
        min_voltage: Minimum battery voltage (volts)
        max_voltage: Maximum battery voltage (volts)
    """

    min_voltage: float = 3.3
    max_voltage: float = 4.2


@dataclass(frozen=True)
class UIConfig:
    """User interface configuration.

    Attributes:
        always_on_top: Keep window always on top
        close_console_on_ui: Close console when UI launches (Windows)
    """

    always_on_top: bool = False
    close_console_on_ui: bool = True


@dataclass(frozen=True)
class DevelopmentConfig:
    """Development and debugging configuration.

    Attributes:
        disable_pycache: Prevent Python from writing .pyc files
        cache_dir: Optional cache directory for cflib
        connection_log_verbose: Enable verbose connection logging
    """

    disable_pycache: bool = True
    cache_dir: Optional[str] = None
    connection_log_verbose: bool = True


@dataclass(frozen=True)
class ConfigurationService:
    """Immutable configuration service for the drone application.

    This class provides type-safe, immutable access to all application
    configuration. Use ConfigurationBuilder to construct instances.

    Attributes:
        drone: Drone-specific configuration
        websocket: WebSocket configuration
        safety: Safety settings
        performance: Performance tuning
        battery: Battery monitoring settings
        ui: User interface settings
        development: Development/debugging settings
    """

    drone: DroneConfig = field(default_factory=DroneConfig)
    websocket: WebSocketConfig = field(default_factory=WebSocketConfig)
    safety: SafetyConfig = field(default_factory=SafetyConfig)
    performance: PerformanceConfig = field(default_factory=PerformanceConfig)
    battery: BatteryConfig = field(default_factory=BatteryConfig)
    ui: UIConfig = field(default_factory=UIConfig)
    development: DevelopmentConfig = field(default_factory=DevelopmentConfig)

    def get_drones(
        self,
        config_path: Optional[str] = None
    ) -> List[Dict[str, str]]:
        """Load drone configurations from JSON file or return defaults.

        Args:
            config_path: Optional path to drones.json file

        Returns:
            List of drone configurations limited by max_drones
        """
        if config_path is None:
            # Default path relative to this module
            config_path = os.path.join(
                os.path.dirname(__file__),
                "drones",
                "drones.json"
            )

        try:
            if os.path.exists(config_path):
                with open(config_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    if isinstance(data, list):
                        return data[:self.drone.max_drones]
        except Exception:
            pass

        return self.drone.default_drones[:self.drone.max_drones]

    def validate(self) -> None:
        """Validate configuration values.

        Raises:
            ValueError: If any configuration value is invalid
        """
        # Validate drone configuration
        if self.drone.max_drones < 1:
            raise ValueError("max_drones must be at least 1")
        if self.drone.max_x <= 0 or self.drone.max_y <= 0:
            raise ValueError("max_x and max_y must be positive")
        if self.drone.max_z <= 0:
            raise ValueError("max_z must be positive")

        # Validate performance configuration
        if self.performance.move_to_rate_hz < 0:
            raise ValueError("move_to_rate_hz cannot be negative")
        if self.performance.connect_max_workers < 1:
            raise ValueError("connect_max_workers must be at least 1")

        # Validate battery configuration
        if self.battery.min_voltage >= self.battery.max_voltage:
            raise ValueError(
                "min_voltage must be less than max_voltage"
            )

        # Validate cache directory if specified
        if self.development.cache_dir:
            if not os.path.isabs(self.development.cache_dir):
                raise ValueError(
                    f"cache_dir must be absolute path: "
                    f"{self.development.cache_dir}"
                )


class ConfigurationBuilder:
    """Builder for constructing ConfigurationService instances.

    Provides a fluent API for building configuration objects with
    validation and sensible defaults.

    Example:
        config = (ConfigurationBuilder()
            .with_max_drones(4)
            .with_websocket_address("ws://localhost:8765")
            .with_safety_bypass(False)
            .build())
    """

    def __init__(self):
        """Initialize builder with default configuration."""
        self._drone = DroneConfig()
        self._websocket = WebSocketConfig()
        self._safety = SafetyConfig()
        self._performance = PerformanceConfig()
        self._battery = BatteryConfig()
        self._ui = UIConfig()
        self._development = DevelopmentConfig()

    @classmethod
    def with_defaults(cls) -> "ConfigurationBuilder":
        """Create builder with default configuration values.

        Returns:
            ConfigurationBuilder with all default values
        """
        return cls()

    def with_max_drones(self, max_drones: int) -> "ConfigurationBuilder":
        """Set maximum number of drones."""
        self._drone = replace(self._drone, max_drones=max_drones)
        return self

    def with_movement_limits(
        self,
        max_x: float,
        max_y: float,
        max_z: float
    ) -> "ConfigurationBuilder":
        """Set movement limits in meters."""
        self._drone = replace(
            self._drone,
            max_x=max_x,
            max_y=max_y,
            max_z=max_z
        )
        return self

    def with_websocket_address(
        self,
        address: str
    ) -> "ConfigurationBuilder":
        """Set WebSocket server address."""
        self._websocket = replace(self._websocket, address=address)
        return self

    def with_safety_bypass(
        self,
        bypass: bool
    ) -> "ConfigurationBuilder":
        """Set safety check bypass flag."""
        self._safety = replace(self._safety, bypass_safety_checks=bypass)
        return self

    def with_move_rate_hz(
        self,
        rate_hz: float
    ) -> "ConfigurationBuilder":
        """Set move command rate limit in Hz."""
        self._performance = replace(
            self._performance,
            move_to_rate_hz=rate_hz
        )
        return self

    def with_cache_dir(
        self,
        cache_dir: Optional[str]
    ) -> "ConfigurationBuilder":
        """Set cache directory for cflib."""
        self._development = replace(
            self._development,
            cache_dir=cache_dir
        )
        return self

    def with_battery_range(
        self,
        min_voltage: float,
        max_voltage: float
    ) -> "ConfigurationBuilder":
        """Set battery voltage range."""
        self._battery = replace(
            self._battery,
            min_voltage=min_voltage,
            max_voltage=max_voltage
        )
        return self

    def with_connection_workers(
        self,
        max_workers: int
    ) -> "ConfigurationBuilder":
        """Set number of parallel connection workers."""
        self._performance = replace(
            self._performance,
            connect_max_workers=max_workers
        )
        return self

    def build(self) -> ConfigurationService:
        """Build and validate the configuration.

        Returns:
            Immutable ConfigurationService instance

        Raises:
            ValueError: If configuration is invalid
        """
        config = ConfigurationService(
            drone=self._drone,
            websocket=self._websocket,
            safety=self._safety,
            performance=self._performance,
            battery=self._battery,
            ui=self._ui,
            development=self._development,
        )

        # Validate before returning
        config.validate()

        return config
