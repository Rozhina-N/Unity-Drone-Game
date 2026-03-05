import tkinter as tk
import threading
import drone_handler
from drone_handler import get_drone_data, get_lh_status
from config import droneAdress1, droneAdress2, always_on_top

def armed_status_message(sup_value: float) -> str:
    """Zet het supervisor bitfield om in een leesbare statusstring."""
    bits = int(sup_value)
    statuses = []
    if bits & 1:
        statuses.append("Can be armed")
    if bits & 2:
        statuses.append("Is armed")
    if bits & 4:
        statuses.append("Auto arm")
    if bits & 8:
        statuses.append("Can fly")
    if bits & 16:
        statuses.append("Is flying")
    if bits & 32:
        statuses.append("Is tumbled")
    if bits & 64:
        statuses.append("Is locked")
    return ", ".join(statuses) if statuses else "Unknown"

class DroneSelector:
    """Simpele UI om een drone te selecteren."""
    def __init__(self):
        self.selected_drone = None
        self.root = tk.Tk()
        self.root.title("Drone Selector")
        self.root.geometry("300x200")
        self.root.configure(bg="#2D2D2D")

        # Label
        label = tk.Label(self.root, text="Selecteer een drone:", font=("Helvetica", 16), bg="#2D2D2D", fg="#F1F1F1")
        label.pack(pady=20)

        # Knoppen
        drone1_button = tk.Button(self.root, text="Drone 1", command=lambda: self.select_drone(droneAdress1),
                                  font=("Helvetica", 14), bg="#4CAF50", fg="white", padx=10, pady=5)
        drone1_button.pack(pady=5)

        drone2_button = tk.Button(self.root, text="Drone 2", command=lambda: self.select_drone(droneAdress2),
                                  font=("Helvetica", 14), bg="#4CAF50", fg="white", padx=10, pady=5)
        drone2_button.pack(pady=5)

    def select_drone(self, address):
        """Sla het geselecteerde drone-adres op en sluit de UI."""
        self.selected_drone = address
        self.root.destroy()

    def get_selected_drone(self):
        """Start de UI en retourneer het geselecteerde drone-adres."""
        self.root.mainloop()
        return self.selected_drone


class DroneUI:
    """Simpele UI om de Crazyflie te bedienen"""
    def __init__(self, root, crazyflie_handler):
        self.root = root
        self.root.title("Crazyflie Controller")
        self.root.geometry("400x475")
        self.root.configure(bg="#2D2D2D")  # donkere achtergrond
        self.crazyflie_handler = crazyflie_handler

        # Always on top checkbox (top right)
        self.always_on_top_var = tk.BooleanVar(value=False)
        self.always_on_top_checkbox = tk.Checkbutton(
            root, text="Always on top", variable=self.always_on_top_var,
            command=self.toggle_always_on_top, bg="#2D2D2D", fg="#F1F1F1",
            selectcolor="#2D2D2D", anchor="e"
        )
        self.always_on_top_checkbox.place(relx=1.0, y=0, anchor="ne")

        if always_on_top == True:
            self.toggle_always_on_top()
            self.always_on_top_checkbox.toggle()

        # Label voor het weergeven van hoogte en batterij percentage
        self.label = tk.Label(root, text="Hoogte: 0 cm", font=("Helvetica", 16, "bold"),
                              bg="#2D2D2D", fg="#F1F1F1")
        self.label.pack(pady=20)

        self.lh_status_label = tk.Label(root, text="Lighthouse status: ", font=("Helvetica", 14),
                                        bg="#2D2D2D", fg="#F1F1F1")
        self.lh_status_label.pack(pady=10)

        # Voeg een frame toe om de knoppen naast elkaar te plaatsen
        button_frame = tk.Frame(root, bg="#2D2D2D")
        button_frame.pack(pady=5)

        # Voeg de knoppen toe aan het frame 
        self.drone1_button = tk.Button(button_frame, text="Drone 1", command=lambda: self.switch_drone(droneAdress1),
                                       font=("Helvetica", 14), bg="#4CAF50", fg="white",
                                       activebackground="#45A049", bd=0, padx=10, pady=5)
        self.drone1_button.pack(side=tk.LEFT, padx=5)

        self.drone2_button = tk.Button(button_frame, text="Drone 2", command=lambda: self.switch_drone(droneAdress2),
                                       font=("Helvetica", 14), bg="#4CAF50", fg="white",
                                       activebackground="#45A049", bd=0, padx=10, pady=5)
        self.drone2_button.pack(side=tk.LEFT, padx=5)

        # Stopknop
        self.stop_button = tk.Button(root, text="Stop", command=self.stop_drone,
                                     font=("Helvetica", 14), bg="#f44336", fg="white",
                                     activebackground="#e53935", bd=0, padx=10, pady=5)
        self.stop_button.pack(pady=5)

        # Reboot-knop
        self.reboot_button = tk.Button(root, text="Reboot", command=self.reboot_drone,
                                       font=("Helvetica", 14), bg="#FF9800", fg="white",
                                       activebackground="#FB8C00", bd=0, padx=10, pady=5)
        self.reboot_button.pack(pady=5)

        self.running = True
        threading.Thread(target=self.update_labels, daemon=True).start()

        # Voeg een handler toe voor het sluiten van het venster
        self.root.protocol("WM_DELETE_WINDOW", self.close)

    def update_label(self, text):
        """Update UI-label veilig vanuit een andere thread"""
        self.root.after(0, self.label.config, {"text": text})

    def update_lighthouse_status(self, status):
        """Update lighthouse-status veilig vanuit een andere thread"""
        status_str = ""
        if status > 0:
            status_str = "enabled"
        else :
            status_str = "disabled"
        self.root.after(0, self.lh_status_label.config, {
                        "text": f"Lighthouse status: {status_str}"})

    def update_labels(self):
        """Blijf de hoogte en status van de drone updaten met self.root.after"""
        if self.running:
            # Haal de data op
            drone_z, battery_voltage, drone_connected, drone_armed, battery_state = get_drone_data()

            # Verwerk de status
            if drone_connected:
                if battery_state == 1:
                    battery_text = f"⚡ {battery_voltage}%"
                elif battery_state == 2:
                    battery_text = f"🔋 {battery_voltage}%"
                elif battery_state == 3:
                    battery_text = f"🪫 {battery_voltage}%"
                else:
                    battery_text = f"{battery_voltage}%"
                armed_text = armed_status_message(drone_armed)
                text = (
                    f"X: {self.crazyflie_handler.get_x():.2f} m\n"
                    f"Y: {self.crazyflie_handler.get_y():.2f} m\n"
                    f"Z: {self.crazyflie_handler.get_z():.2f} m\n"
                    f"Battery: {battery_text}\n"
                    f"Status: {armed_text}\n"
                    f"Connected: {self.crazyflie_handler.get_drone_connected()}\n"
                )
            else:
                text = "Geen verbinding!"

            # Update de UI veilig
            self.update_label(text)
            self.update_lighthouse_status(get_lh_status())

            # verander de kleur van de knoppen op basis van de status
            if drone_handler.uri == droneAdress1:
                if self.crazyflie_handler.get_drone_connected:
                    self.drone1_button.config(bg="#4CAF50")
                else:
                    self.drone1_button.config(bg="#FF5722")
                self.drone2_button.config(bg="#FF5722")
            elif drone_handler.uri == droneAdress2:
                if self.crazyflie_handler.get_drone_connected:
                    self.drone2_button.config(bg="#4CAF50")
                else:
                    self.drone2_button.config(bg="#FF5722")
                self.drone1_button.config(bg="#FF5722")


            # Stel opnieuw een tijd in voor de volgende update
            self.root.after(100, self.update_labels)  # Houd de 100 ms interval consistent

    def switch_drone(self, address):
        """Commando om van drone te switchen."""
        threading.Thread(target=lambda: self.crazyflie_handler.switch_drone(address), daemon=True).start()

    def stop_drone(self):
        """Commando om de drone te stoppen"""
        threading.Thread(
            target=self.crazyflie_handler.stop_drone, daemon=True).start()
        print("Drone gestopt!")

    def connect_drone(self):
        """Roep de reconnect methode van de CrazyflieHandler aan."""
        print("Opnieuw verbinden met de drone...")
        self.crazyflie_handler._attempt_reconnect()

    def reboot_drone(self):
        """Commando om de drone opnieuw op te starten"""
        threading.Thread(
            target=self.crazyflie_handler.reboot_drone, daemon=True).start()
        print("Drone opnieuw opgestart!")

    def close(self):
        """Sluit de UI af"""
        self.running = False
        self.crazyflie_handler.close()
        self.root.destroy()

    def toggle_always_on_top(self):
        self.root.attributes("-topmost", self.always_on_top_var.get())

def run_ui(crazyflie_handler):
    root = tk.Tk()
    app = DroneUI(root, crazyflie_handler)
    root.mainloop()
