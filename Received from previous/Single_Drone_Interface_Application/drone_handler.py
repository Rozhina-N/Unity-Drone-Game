import time
import threading
import cflib
import cflib.crtp
from cflib.crazyflie import Crazyflie
from cflib.crazyflie.log import LogConfig
from cflib.crazyflie.high_level_commander import HighLevelCommander
from cflib.utils.power_switch import PowerSwitch
from config import *

# URI voor de Crazyflie
# uri = uri_helper.uri_from_env(default='radio://0/80/2M/E7E7E7E7E7')
uri = droneAdress2
# Globale variabelen
drone_x = 0.0
drone_y = 0.0
drone_z = 0.0
drone_yaw = 0.0
battery_voltage = 0
battery_state = 0  # 0 = Battery, 1 = Charging, 2 = Charged, etc.
drone_connected = False
drone_armed = 0.0

# Offset-variabelen
drone_x_offset = 0.0
drone_y_offset = 0.0


class CrazyflieHandler:
    """Beheert de verbinding met de Crazyflie en leest data uit"""

    def __init__(self):
        self._cf = Crazyflie(rw_cache='./cache')
        self._cf.connected.add_callback(self._connected)
        self._cf.connection_lost.add_callback(self._disconnected)
        self._cf.open_link(uri)
        self._idling = False
        self._hl_commander = HighLevelCommander(self._cf)
        self.safety_enabled = False
        self._cf.param.set_value('commander.enHighLevel', '1')
        threading.Thread(target=self._safety_loop, daemon=True).start()
        print(f"Verbinden met Crazyflie op {uri}")

    def _connected(self, link_uri):
        """Callback wanneer verbonden"""
        global drone_connected, drone_armed, battery_state
        drone_connected = True
        print(f"Verbonden met {link_uri}")

        # Eerste logconfiguratie
        logconf1 = LogConfig(name='Position', period_in_ms=100)
        logconf1.add_variable('stateEstimate.x', 'float')
        logconf1.add_variable('stateEstimate.y', 'float')
        logconf1.add_variable('stateEstimate.z', 'float')
        logconf1.add_variable('stateEstimate.yaw', 'float')

        def log_callback1(timestamp, data, logconf):
            global drone_x, drone_y, drone_z, drone_yaw
            drone_x = round(float(data.get('stateEstimate.x', 0)), 4)
            drone_y = round(float(data.get('stateEstimate.y', 0)), 4)
            drone_z = round(float(data.get('stateEstimate.z', 0)), 4)
            drone_yaw = round(float(data.get('stateEstimate.yaw', 0)), 4)
            # print(f"Drone positie: x={drone_x}, y={drone_y}, z={drone_z}, yaw={drone_yaw}")

        logconf1.data_received_cb.add_callback(log_callback1)
        self._cf.log.add_config(logconf1)
        logconf1.start()

        # Tweede logconfiguratie
        logconf2 = LogConfig(name='Battery', period_in_ms=100)
        logconf2.add_variable('pm.vbat', 'float')
        logconf2.add_variable('pm.batteryLevel', 'float')
        logconf2.add_variable('supervisor.info', 'float')
        logconf2.add_variable('pm.state', 'float')

        def log_callback2(timestamp, data, logconf):
            global battery_voltage, drone_armed, battery_state
            battery_voltage = data.get('pm.batteryLevel', 0)
            drone_armed = float(data.get('supervisor.info', 14))
            battery_state = float(data.get('pm.state', 0))

        logconf2.data_received_cb.add_callback(log_callback2)
        self._cf.log.add_config(logconf2)
        logconf2.start()

        # derde logconfiguratie
        logconf3 = LogConfig(name='Lighthouse', period_in_ms=100)
        logconf3.add_variable('lighthouse.status', 'float')

        def log_callback3(timestamp, data, logconf):
            global lighthouse_status
            lighthouse_status = data.get('lighthouse.status', 0)

        logconf3.data_received_cb.add_callback(log_callback3)
        self._cf.log.add_config(logconf3)
        logconf3.start()
        print("Drone is verbonden en logt data.")

    def reboot_drone(self):
        """Herstart de drone"""
        print("Drone opnieuw opstarten...\n")
        self._cf.commander.send_setpoint(0, 0, 0, 0)
        time.sleep(1)
        power_switch = PowerSwitch(self._cf.link_uri)
        power_switch.stm_power_down()
        time.sleep(3)
        power_switch.stm_power_up()# Herstart de STM MCU
        print("Drone opnieuw opgestart.")
        time.sleep(15)
        self._cf.close_link()
        self.drone_connected = False
        self._attempt_reconnect()

    def _disconnected(self, args, reason):
        """Wordt aangeroepen als de drone disconnect."""
        print("Drone disconnected. Probeer opnieuw te verbinden..." + args, reason)
        global drone_connected
        drone_connected = False  # Zorg ervoor dat de status wordt bijgewerkt
        self._attempt_reconnect()

    def _attempt_reconnect(self):
        """Blijf proberen om verbinding te maken met de drone."""
        max_retries = 3  # Maximaal aantal pogingen
        retries = 0

        while not self._cf.is_connected() and retries < max_retries:
            try:
                print("Poging om opnieuw verbinding te maken met de drone...")
                self._cf.open_link(uri)
                time.sleep(4)  # Wacht even om de verbinding te laten stabiliseren

                if self._cf.is_connected():
                    print("Connection succesvol!")
                    global drone_connected
                    drone_connected = True
                    return  # Verlaat de methode als de verbinding succesvol is
                else:
                    print("Verbinding mislukt, opnieuw proberen...")
            except Exception as e:
                print(f"Fout tijdens opnieuw verbinden: {e}")
            finally:
                retries += 1
                time.sleep(2)  # Wacht 2 seconden voor de volgende poging

        if not self._cf.is_connected():
            print("Maximaal aantal reconnect-pogingen bereikt. Verbinding mislukt.")

    def stop(self):
        """Sluit de verbinding netjes af"""
        print("Crazyflie stoppen...")
        self._cf.commander.send_setpoint(0, 0, 0, 0)
        time.sleep(0.5)
        self._cf.close_link()
        print("Crazyflie afgesloten.")

    def switch_drone(self, address):
        """Verander de drone-adres"""
        global uri
        uri = address
        print(f"Drone-adres veranderd naar {uri}")
        self._cf.close_link()
        self._attempt_reconnect()

    def _safety_loop(self):
        """Minimal houdt de drone armed"""
        while True:
            if self.safety_enabled:
                # Stuur een setpoint als "heartbeat", niet noodzakelijk een `move_to`
                try:
                    self._cf.commander.send_setpoint(0, 0, 0, 0)  # Houd armed
                except Exception as e:
                    print(f"Fout in safety loop: {e}")
            time.sleep(0.1)  # Interval behouden, geen hogging

    ## AANPASSINGEN MET OFFSETS ##

    def get_x(self):
        """Haal de huidige x-positie op (rekening houdend met offset)"""
        return drone_x - drone_x_offset


    def get_y(self):
        """Haal de huidige y-positie op (rekening houdend met offset)"""
        return drone_y - drone_y_offset

    def get_z(self):
        """Haal de huidige hoogte op"""
        return drone_z

    def get_yaw(self):
        """Haal de huidige yaw op"""
        return drone_yaw

    def reset_position(self):
        """Stel de huidige positie in als het nieuwe nulpunt (offsets aanpassen)"""
        global drone_x_offset, drone_y_offset
        drone_x_offset = drone_x
        drone_y_offset = drone_y
        print(f"Positie gereset met offsets: x={drone_x_offset}, y={drone_y_offset}")

    ## BEWEGINGSMETHODES MET OFFSETS ##

    def move_up(self, distance):
        """Verplaats de drone omhoog"""
        self._execute_command(self._hl_commander.go_to, 0, 0, distance, 0, 2, relative=True)

    def move_down(self, distance):
        """Verplaats de drone omlaag"""
        self._execute_command(self._hl_commander.go_to, 0, 0, -distance, 0, 2, relative=True)

    def move_left(self, distance):
        """Verplaats de drone naar links"""
        self._execute_command(self._hl_commander.go_to, -distance, 0, 0, 0, 2, relative=True)

    def move_right(self, distance):
        """Verplaats de drone naar rechts"""
        self._execute_command(self._hl_commander.go_to, distance, 0, 0, 0, 2, relative=True)

    ## NIEUWE FUNCTIE OM NAAR EEN SPECIFIEK PUNT TE GAAN ##
    def takeoff(self, target_height=default_takeoff_height, takeoff_duration=default_takeoff_duration):
        """
        Laat de drone opstijgen naar de opgegeven hoogte en hover.
        :param target_height: De hoogte (in meter) waar de drone naar moet opstijgen. Standaard is dit 1 meter.
        :param takeoff_duration: De tijd die de drone erover doet om op te stijgen naar de target hoogte. Standaard is dit 2 seconden.
        """
        print(f"Drone opstijgen naar {target_height} meter... in {takeoff_duration} seconden")

        self._cf.commander.send_notify_setpoint_stop()
        self._execute_command(self._hl_commander.takeoff, target_height, takeoff_duration)  # 2.0 is de tijdsduur voor de takeoff

    def move_to(self, x_target, y_target, z_target, yaw=0.0):
        """
        Beweeg de drone naar een absoluut punt (x_target, y_target, z_target) met een absolute yaw.
        Als z_target < 0 wordt de huidige hoogte gebruikt.

        :param x_target: Doelpositie in de x-coördinaat in meters.
        :param y_target: Doelpositie in de y-coördinaat in meters.
        :param z_target: Doelpositie in de z-coördinaat in meters.
        :param yaw: Absolute yaw in graden (standaard 0.0).
        """

        # Controleer of z_target kleiner is dan 0. Als dat zo is, gebruik de huidige hoogte
        if z_target < 0.05:
            z_target = 0.05
        elif z_target > max_z:
            z_target = max_z
        if x_target > max_x:
            x_target = max_x
        elif x_target < -max_x:
            x_target = -max_x
        if y_target > max_y:
            y_target = max_y
        elif y_target < -max_y:
            y_target = -max_y

        self._cf.commander.send_position_setpoint(x_target, y_target, z_target, yaw)

    def is_drone_flying(self):
        """Check if the drone is flying based on the armed status."""
        return (int(drone_armed) & 16) != 0  # Cast drone_armed to int before bitwise operation



    def land(self):
        """Laat de drone landen met de standaard commander."""
        if not self.is_drone_flying():
            print("Drone is niet aan het vliegen, kan niet landen.")
            return
        print("drone laten landen...")
        self.safety_enabled = False

        self._cf.commander.send_notify_setpoint_stop()
        print("Drone laten landen...")
        self._hl_commander.land(0,default_land_duration)
        time.sleep(1)

        # Zet alle input uit als de drone de grond bereikt
        self._cf.commander.send_setpoint(0, 0, 0, 0)
        self.safety_enabled = True
        print("Drone geland.")

    def stop_drone(self):
        """Stop alle bewegingen en zet de motoren uit."""
        print("Commando om de drone te stoppen ontvangen.")
        self._cf.commander.send_setpoint(0, 0, 0, 0)

    def close(self):
        """Sluit de verbinding netjes af"""
        print("Drone afsluiten...")
        self._cf.close_link()
        print("Drone afgesloten.")

    def _execute_command(self, command, *args, **kwargs):
        """Voer een commando uit met tijdelijke uitschakeling van de safety loop"""
        self.safety_enabled = False
        command(*args, **kwargs)

    def get_drone_connected(self):
        return self._cf.is_connected()

    def manual_takeoff(self, thrust=40000):
        """
        Laat de drone handmatig opstijgen met ruwe thrust.
        :param thrust: De thrustwaarde (tussen 0 en 65535), standaard is 40000.
        """
        self.safety_enabled = False
        print("Drone handmatig laten opstijgen met thrust...")
        roll, pitch, yaw_rate = 0, 0, 0  # Geen roll/pitch/yaw voor recht omhoog

        try:
            # Verzend het commando meerdere keren om de drone op te laten stijgen
            for _ in range(100):  # Bijv. ~5 seconden opstijgen
                self._cf.commander.send_setpoint(roll, pitch, yaw_rate, thrust)
                time.sleep(0.05)  # Wacht 50 ms tussen commando's
        except Exception as e:
            print(f"Fout tijdens opstijgen: {e}")
        finally:
            # Zorg ervoor dat thrust wordt uitgeschakeld
            self._cf.commander.send_setpoint(0, 0, 0, 0)
            print("Thrust uitgeschakeld.")


# Start de Crazyflie-handler
def start_drone(drone_uri=droneAdress2):
    if battery_voltage == 0:
        cflib.crtp.init_drivers()
    global uri
    uri=drone_uri
    return CrazyflieHandler()


# Zorg ervoor dat drone-data beschikbaar blijft
def get_drone_data():
    return drone_z, battery_voltage, drone_connected, drone_armed, battery_state

def get_lh_status():
    return lighthouse_status
