"""Example demonstrating ConfigurationService usage.

This example shows how to migrate from the legacy config module
to the new ConfigurationService architecture.
"""

# ============================================================================
# BEFORE (Legacy Pattern - Global Mutable State)
# ============================================================================
# import config as cfg
#
# max_drones = cfg.MAX_DRONES
# cfg.MOVE_TO_RATE_HZ = 20.0  # ❌ Runtime mutation!
# ws_address = cfg.ws_address


# ============================================================================
# AFTER (New Pattern - Immutable Configuration with Dependency Injection)
# ============================================================================
from config_service import ConfigurationBuilder, ConfigurationService


def example_basic_usage():
    """Example 1: Load configuration from existing config.py."""
    # Build configuration from legacy config module
    config = ConfigurationBuilder.from_config_module().build()

    # Access configuration values (type-safe)
    max_drones = config.drone.max_drones
    ws_address = config.websocket.address
    move_rate = config.performance.move_to_rate_hz

    print(f"Max drones: {max_drones}")
    print(f"WebSocket: {ws_address}")
    print(f"Move rate: {move_rate} Hz")

    # Get drone list
    drones = config.get_drones()
    print(f"Configured drones: {len(drones)}")

    return config


def example_custom_configuration():
    """Example 2: Build custom configuration for testing."""
    config = (
        ConfigurationBuilder()
        .with_max_drones(2)
        .with_websocket_address("ws://test:8765")
        .with_safety_bypass(False)
        .with_move_rate_hz(15.0)
        .build()
    )

    print(f"Test config max drones: {config.drone.max_drones}")
    print(f"Safety bypass: {config.safety.bypass_safety_checks}")

    return config


def example_dependency_injection(config: ConfigurationService):
    """Example 3: Accept configuration as dependency.

    This is the preferred pattern for new code.
    """
    # Configuration is passed in, not imported globally
    max_x = config.drone.max_x
    max_y = config.drone.max_y
    max_z = config.drone.max_z

    print(f"Movement limits: X={max_x}, Y={max_y}, Z={max_z}")

    # Configuration is immutable - this will raise FrozenInstanceError
    # config.drone.max_x = 2.0  # ❌ Error!


def example_validation():
    """Example 4: Configuration validation."""
    try:
        # Invalid configuration will fail validation
        invalid_config = (
            ConfigurationBuilder()
            .with_max_drones(0)  # Invalid!
            .build()
        )
    except ValueError as e:
        print(f"Validation error: {e}")

    # Valid configuration passes
    valid_config = (
        ConfigurationBuilder()
        .with_max_drones(4)
        .build()
    )
    print("Valid configuration created successfully")

    return valid_config


def example_thread_safety():
    """Example 5: Thread-safe configuration access."""
    import threading

    config = ConfigurationBuilder.from_config_module().build()

    def worker():
        # Safe to read from multiple threads
        # No locks needed - configuration is immutable
        for _ in range(1000):
            max_drones = config.drone.max_drones

    threads = [threading.Thread(target=worker) for _ in range(10)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    print("Thread-safety test completed successfully")


if __name__ == "__main__":
    print("=" * 70)
    print("ConfigurationService Examples")
    print("=" * 70)

    print("\n1. Basic Usage:")
    example_basic_usage()

    print("\n2. Custom Configuration:")
    example_custom_configuration()

    print("\n3. Dependency Injection:")
    config = example_basic_usage()
    example_dependency_injection(config)

    print("\n4. Validation:")
    example_validation()

    print("\n5. Thread Safety:")
    example_thread_safety()

    print("\n" + "=" * 70)
    print("All examples completed successfully!")
    print("=" * 70)
