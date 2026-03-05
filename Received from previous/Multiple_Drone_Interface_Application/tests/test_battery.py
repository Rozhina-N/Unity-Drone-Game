import unittest
from utils.battery import voltage_to_percent
from config_service import ConfigurationBuilder


class TestBatteryUtils(unittest.TestCase):
    def setUp(self):
        """Initialize config for tests"""
        self.config = ConfigurationBuilder.with_defaults().build()
    
    def test_below_min(self):
        self.assertEqual(voltage_to_percent(3.0), 0.0)

    def test_at_min(self):
        self.assertEqual(voltage_to_percent(self.config.battery.min_voltage), 0.0)

    def test_at_max(self):
        self.assertEqual(voltage_to_percent(self.config.battery.max_voltage), 100.0)

    def test_mid(self):
        mid = (self.config.battery.min_voltage + self.config.battery.max_voltage) / 2.0
        self.assertAlmostEqual(voltage_to_percent(mid), 50.0, places=6)

    def test_curve_points(self):
        # tests that match the discharge anchors roughly
        self.assertAlmostEqual(voltage_to_percent(4.2), 100.0, places=2)
        self.assertAlmostEqual(voltage_to_percent(4.0), 85.0, places=1)
        self.assertAlmostEqual(voltage_to_percent(3.8), 60.0, places=1)
        self.assertAlmostEqual(voltage_to_percent(3.6), 20.0, places=1)

    def test_invalid(self):
        self.assertEqual(voltage_to_percent("bad"), 0.0)


if __name__ == "__main__":
    unittest.main()
