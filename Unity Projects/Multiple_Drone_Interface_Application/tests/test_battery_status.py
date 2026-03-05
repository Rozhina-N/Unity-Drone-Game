import unittest

from drones.drone import Drone


class TestBatteryStatusDetection(unittest.TestCase):
    def test_drone_wrapper_fallback(self):
        # Create a wrapper with an inner mock that lacks battery_status to force fallback
        # Temporarily remove the property by shadowing with an attribute access error
        # Instead, simulate by wrapping a plain object with needed attrs
        class Inner:
            def __init__(self):
                self.battery_voltage = 4.0
                self.pm_state = 0
                self.x = self.y = self.z = self.yaw = 0.0

            def get_x(self):
                return 0.0

            def get_y(self):
                return 0.0

            def get_z(self):
                return 0.0

            def get_yaw(self):
                return 0.0

            def get_drone_connected(self):
                return True

        i = Inner()
        d = Drone("d1", "addr", handler=i)
        # charging via bitmask
        i.pm_state = 0x02
        self.assertEqual(d.battery_status, "Charging")
        # charged via bitmask (USB connected) should still be reported as 'Charging'
        i.pm_state = 0x04
        self.assertEqual(d.battery_status, "Charging")
        # empty via voltage
        i.pm_state = 0
        i.battery_voltage = 3.3
        self.assertEqual(d.battery_status, "Empty")


if __name__ == "__main__":
    unittest.main()
