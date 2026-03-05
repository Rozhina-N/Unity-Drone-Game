### This file contains the configuration for this application.
## constants
# define the maximum values for the x, y, and z axes, if they are exceeded they will be set to the maximum value in meters.
max_x = 3.0
max_y = 3.0
max_z = 1.0

# define the drone addresses
droneAdress1 = "radio://0/80/2M/E7E7E7E7E7"
droneAdress2 = "radio://0/80/2M/E7E7E7E7E8"

# define the websocket server address
ws_address = "ws://localhost:8765/drone"

# define default values for the drone
default_takeoff_height = 1.0
default_takeoff_duration = 2.0
default_land_duration = 1.0

# UI settings
always_on_top = True