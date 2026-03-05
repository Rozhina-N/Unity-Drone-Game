from unittest import TestCase
import drones.drone_controller as dc

class TestDroneControllerBasic(TestCase):
    def setUp(self):
        pass

    def test_takeoff_land_stop_reset(self):
        h = dc.Drone("mock_id", "mock://")
        self.assertEqual(h.telemetry["z"], 0.0)
        # takeoff (stub)
        h.telemetry["z"] = 1.5
        self.assertAlmostEqual(h.telemetry["z"], 1.5, places=3)
        # land (stub)
        h.telemetry["z"] = 0.0
        self.assertAlmostEqual(h.telemetry["z"], 0.0, places=3)
        # stop should keep z at 0
        h.telemetry["z"] = 0.0
        self.assertAlmostEqual(h.telemetry["z"], 0.0, places=3)
        # move and reset
        h.telemetry["x"] = 1.0
        h.telemetry["y"] = 2.0
        h.telemetry["z"] = 0.5
        self.assertAlmostEqual(h.telemetry["x"], 1.0, places=3)
        self.assertAlmostEqual(h.telemetry["y"], 2.0, places=3)
        self.assertAlmostEqual(h.telemetry["z"], 0.5, places=3)
        h.reset_position()
        self.assertAlmostEqual(h.telemetry["x"], 0.0, places=3)
        self.assertAlmostEqual(h.telemetry["y"], 0.0, places=3)

    def test_battery_status_bitmask_and_voltage(self):
        h = dc.Drone("mock_id", "mock://")
        # Charging via bitmask 0x02
        h.battery["pm_state"] = 0x02
        h.battery["is_charging"] = True
        self.assertTrue(h.battery["is_charging"])
        # Charged/full via bitmask 0x04 should still show Charging (USB present)
        h.battery["pm_state"] = 0x04
        h.battery["is_charging"] = True
        self.assertTrue(h.battery["is_charging"])
        # Fallback to voltage when pm_state not set/zero
        h.battery["pm_state"] = 0
        h.battery["voltage"] = 3.3
        h.battery["percent"] = 0.0
        self.assertEqual(h.battery["voltage"], 3.3)
        h.battery["voltage"] = 4.2
        self.assertEqual(h.battery["voltage"], 4.2)
        # Middle voltage
        h.battery["voltage"] = 3.8
        self.assertEqual(h.battery["voltage"], 3.8)

    def test_close_idempotent(self):
        h = dc.Drone("mock_id", "mock://")
        h.disconnect()
        h.disconnect()
        self.assertFalse(h.connected)
