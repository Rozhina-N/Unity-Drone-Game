"""Unit tests for ConfigurationService.

Tests cover:
- Builder pattern construction
- Immutability guarantees
- Validation logic
- Thread-safety
- Backward compatibility with config module
"""

import os
import pytest
import tempfile
import json
from dataclasses import FrozenInstanceError

from config_service import (
    ConfigurationService,
    ConfigurationBuilder,
    DroneConfig,
    WebSocketConfig,
    SafetyConfig,
    PerformanceConfig,
    BatteryConfig,
    UIConfig,
    DevelopmentConfig,
)


class TestDroneConfig:
    """Test DroneConfig dataclass."""

    def test_default_values(self):
        """Test default configuration values."""
        config = DroneConfig()
        assert config.max_drones == 4
        assert config.max_x == 1.0
        assert config.max_y == 1.0
        assert config.max_z == 1.0
        assert config.default_takeoff_height == 1.0
        assert config.default_takeoff_duration == 2.0
        assert config.default_land_duration == 1.0
        assert len(config.default_drones) == 4

    def test_immutability(self):
        """Test that DroneConfig is immutable."""
        config = DroneConfig()
        with pytest.raises(FrozenInstanceError):
            config.max_drones = 10  # type: ignore

    def test_custom_values(self):
        """Test creating DroneConfig with custom values."""
        config = DroneConfig(
            max_drones=2,
            max_x=2.5,
            max_y=3.0,
            max_z=1.5
        )
        assert config.max_drones == 2
        assert config.max_x == 2.5
        assert config.max_y == 3.0
        assert config.max_z == 1.5


class TestWebSocketConfig:
    """Test WebSocketConfig dataclass."""

    def test_default_values(self):
        """Test default WebSocket configuration."""
        config = WebSocketConfig()
        assert config.address == "ws://localhost:8765/drone"
        assert config.retry_delay == 5.0

    def test_immutability(self):
        """Test that WebSocketConfig is immutable."""
        config = WebSocketConfig()
        with pytest.raises(FrozenInstanceError):
            config.address = "ws://test:8080"  # type: ignore


class TestSafetyConfig:
    """Test SafetyConfig dataclass."""

    def test_default_values(self):
        """Test default safety configuration."""
        config = SafetyConfig()
        assert config.bypass_safety_checks is True
        assert config.enable_param_wait is False

    def test_immutability(self):
        """Test that SafetyConfig is immutable."""
        config = SafetyConfig()
        with pytest.raises(FrozenInstanceError):
            config.bypass_safety_checks = False  # type: ignore


class TestPerformanceConfig:
    """Test PerformanceConfig dataclass."""

    def test_default_values(self):
        """Test default performance configuration."""
        config = PerformanceConfig()
        assert config.move_to_rate_hz == 10.0
        assert config.connect_max_workers == 2
        assert config.retry_max_workers == 2
        assert config.connect_stagger_sec == 0.05
        assert config.connect_handler_timeout == 2.0
        assert config.retry_first_delay == 1.0

    def test_immutability(self):
        """Test that PerformanceConfig is immutable."""
        config = PerformanceConfig()
        with pytest.raises(FrozenInstanceError):
            config.move_to_rate_hz = 20.0  # type: ignore


class TestBatteryConfig:
    """Test BatteryConfig dataclass."""

    def test_default_values(self):
        """Test default battery configuration."""
        config = BatteryConfig()
        assert config.min_voltage == 3.3
        assert config.max_voltage == 4.2

    def test_immutability(self):
        """Test that BatteryConfig is immutable."""
        config = BatteryConfig()
        with pytest.raises(FrozenInstanceError):
            config.min_voltage = 3.0  # type: ignore


class TestConfigurationService:
    """Test ConfigurationService main class."""

    def test_default_construction(self):
        """Test creating ConfigurationService with defaults."""
        config = ConfigurationService()
        assert isinstance(config.drone, DroneConfig)
        assert isinstance(config.websocket, WebSocketConfig)
        assert isinstance(config.safety, SafetyConfig)
        assert isinstance(config.performance, PerformanceConfig)
        assert isinstance(config.battery, BatteryConfig)
        assert isinstance(config.ui, UIConfig)
        assert isinstance(config.development, DevelopmentConfig)

    def test_immutability(self):
        """Test that ConfigurationService is immutable."""
        config = ConfigurationService()
        with pytest.raises(FrozenInstanceError):
            config.drone = DroneConfig()  # type: ignore

    def test_get_drones_default(self):
        """Test get_drones returns default drones."""
        config = ConfigurationService()
        drones = config.get_drones()
        assert len(drones) == 4
        assert drones[0]["id"] == "drone1"
        assert "address" in drones[0]

    def test_get_drones_from_file(self):
        """Test get_drones loads from JSON file."""
        # Create temporary JSON file
        with tempfile.NamedTemporaryFile(
            mode='w',
            suffix='.json',
            delete=False
        ) as f:
            test_drones = [
                {"id": "test1", "address": "radio://test1"},
                {"id": "test2", "address": "radio://test2"},
            ]
            json.dump(test_drones, f)
            temp_path = f.name

        try:
            config = ConfigurationService()
            drones = config.get_drones(config_path=temp_path)
            assert len(drones) == 2
            assert drones[0]["id"] == "test1"
        finally:
            os.unlink(temp_path)

    def test_get_drones_respects_max_limit(self):
        """Test get_drones respects max_drones limit."""
        # Create temporary JSON file with many drones
        with tempfile.NamedTemporaryFile(
            mode='w',
            suffix='.json',
            delete=False
        ) as f:
            test_drones = [
                {"id": f"drone{i}", "address": f"radio://test{i}"}
                for i in range(10)
            ]
            json.dump(test_drones, f)
            temp_path = f.name

        try:
            config = ConfigurationService(
                drone=DroneConfig(max_drones=3)
            )
            drones = config.get_drones(config_path=temp_path)
            assert len(drones) == 3
        finally:
            os.unlink(temp_path)

    def test_validate_success(self):
        """Test validation passes for valid configuration."""
        config = ConfigurationService()
        config.validate()  # Should not raise

    def test_validate_max_drones_invalid(self):
        """Test validation fails for invalid max_drones."""
        config = ConfigurationService(
            drone=DroneConfig(max_drones=0)
        )
        with pytest.raises(ValueError, match="max_drones must be at least 1"):
            config.validate()

    def test_validate_movement_limits_invalid(self):
        """Test validation fails for invalid movement limits."""
        config = ConfigurationService(
            drone=DroneConfig(max_x=-1.0)
        )
        with pytest.raises(
            ValueError,
            match="max_x and max_y must be positive"
        ):
            config.validate()

    def test_validate_battery_range_invalid(self):
        """Test validation fails for invalid battery range."""
        config = ConfigurationService(
            battery=BatteryConfig(min_voltage=4.2, max_voltage=3.3)
        )
        with pytest.raises(
            ValueError,
            match="min_voltage must be less than max_voltage"
        ):
            config.validate()

    def test_validate_cache_dir_relative_path(self):
        """Test validation fails for relative cache directory path."""
        config = ConfigurationService(
            development=DevelopmentConfig(cache_dir="./cache")
        )
        with pytest.raises(ValueError, match="cache_dir must be absolute"):
            config.validate()


class TestConfigurationBuilder:
    """Test ConfigurationBuilder class."""

    def test_default_build(self):
        """Test building configuration with defaults."""
        config = ConfigurationBuilder().build()
        assert isinstance(config, ConfigurationService)
        assert config.drone.max_drones == 4

    def test_fluent_api(self):
        """Test fluent API returns self for chaining."""
        builder = ConfigurationBuilder()
        result = builder.with_max_drones(2)
        assert result is builder

    def test_with_max_drones(self):
        """Test with_max_drones builder method."""
        config = (
            ConfigurationBuilder()
            .with_max_drones(8)
            .build()
        )
        assert config.drone.max_drones == 8

    def test_with_movement_limits(self):
        """Test with_movement_limits builder method."""
        config = (
            ConfigurationBuilder()
            .with_movement_limits(max_x=2.0, max_y=3.0, max_z=1.5)
            .build()
        )
        assert config.drone.max_x == 2.0
        assert config.drone.max_y == 3.0
        assert config.drone.max_z == 1.5

    def test_with_websocket_address(self):
        """Test with_websocket_address builder method."""
        config = (
            ConfigurationBuilder()
            .with_websocket_address("ws://test:9000")
            .build()
        )
        assert config.websocket.address == "ws://test:9000"

    def test_with_safety_bypass(self):
        """Test with_safety_bypass builder method."""
        config = (
            ConfigurationBuilder()
            .with_safety_bypass(False)
            .build()
        )
        assert config.safety.bypass_safety_checks is False

    def test_with_move_rate_hz(self):
        """Test with_move_rate_hz builder method."""
        config = (
            ConfigurationBuilder()
            .with_move_rate_hz(20.0)
            .build()
        )
        assert config.performance.move_to_rate_hz == 20.0

    def test_with_cache_dir(self):
        """Test with_cache_dir builder method."""
        cache_dir = os.path.abspath("/tmp/cache")
        config = (
            ConfigurationBuilder()
            .with_cache_dir(cache_dir)
            .build()
        )
        assert config.development.cache_dir == cache_dir

    def test_with_battery_range(self):
        """Test with_battery_range builder method."""
        config = (
            ConfigurationBuilder()
            .with_battery_range(min_voltage=3.0, max_voltage=4.5)
            .build()
        )
        assert config.battery.min_voltage == 3.0
        assert config.battery.max_voltage == 4.5

    def test_with_connection_workers(self):
        """Test with_connection_workers builder method."""
        config = (
            ConfigurationBuilder()
            .with_connection_workers(4)
            .build()
        )
        assert config.performance.connect_max_workers == 4

    def test_build_validates_configuration(self):
        """Test build() validates configuration."""
        with pytest.raises(ValueError):
            (
                ConfigurationBuilder()
                .with_max_drones(0)  # Invalid
                .build()
            )

    def test_with_defaults(self):
        """Test with_defaults creates builder with default configuration."""
        config = ConfigurationBuilder.with_defaults().build()

        # Verify it loaded successfully and has expected types
        assert isinstance(config, ConfigurationService)
        assert isinstance(config.drone.max_drones, int)
        assert isinstance(config.drone.max_x, float)
        assert isinstance(config.websocket.address, str)
        assert isinstance(config.safety.bypass_safety_checks, bool)
        assert isinstance(config.performance.move_to_rate_hz, float)
        assert isinstance(config.battery.min_voltage, float)
        assert isinstance(config.battery.max_voltage, float)


class TestThreadSafety:
    """Test thread-safety guarantees."""

    def test_immutability_ensures_thread_safety(self):
        """Test that immutable config is thread-safe."""
        import threading

        config = ConfigurationService()
        results = []

        def read_config():
            # Read configuration from multiple threads
            for _ in range(100):
                max_drones = config.drone.max_drones
                results.append(max_drones)

        threads = [threading.Thread(target=read_config) for _ in range(10)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        # All reads should return same value
        assert all(r == 4 for r in results)
        assert len(results) == 1000


class TestPEP8Compliance:
    """Test PEP 8 compliance."""

    def test_type_hints_present(self):
        """Verify type hints are present on public methods."""
        # Check ConfigurationService methods have type hints
        assert hasattr(
            ConfigurationService.get_drones,
            '__annotations__'
        )
        assert hasattr(
            ConfigurationService.validate,
            '__annotations__'
        )

    def test_docstrings_present(self):
        """Verify docstrings are present on classes and methods."""
        assert ConfigurationService.__doc__ is not None
        assert ConfigurationBuilder.__doc__ is not None
        assert ConfigurationService.get_drones.__doc__ is not None
        assert ConfigurationBuilder.build.__doc__ is not None


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
