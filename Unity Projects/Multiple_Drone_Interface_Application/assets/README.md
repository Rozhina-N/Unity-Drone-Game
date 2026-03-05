# App icon

Place your icon files in this folder so the app can use them for the title bar and Windows taskbar.

Preferred file:
- drone.ico — best for Windows taskbar and crisp scaling

Optional fallback:
- drone.png — will be used for the window icon; if Pillow is installed, it will be auto-converted to `drone.ico` on startup

Auto-conversion (optional):
- If `assets/drone.ico` is missing and `assets/drone.png` exists, the app will try to convert the PNG to ICO using Pillow.
- If both exist but the PNG is newer (modified more recently), the app will attempt to re-generate the ICO on startup (Pillow required).
- The converter script, when run without arguments, will automatically pick the most recently modified PNG in `assets` (so you don’t have to rename files). Use `--png` to pick a specific file.
- To enable this, install Pillow into your virtual environment:

```powershell
& ".venv/Scripts/python.exe" -m pip install pillow
```

Then restart the app.

One-off conversion script:
- You can also run the helper script to (re)generate `drone.ico` whenever you change the PNG:

```powershell
# From the project root
& ".venv/Scripts/python.exe" assets/convert_icon.py --png assets/drone.png --out assets/drone.ico
```

There is also a convenience batch file you can double-click:
- `assets/convert_icon.bat`
