"""
PNG -> ICO converter for the app icon.

Usage (run from project root or the assets folder, ideally inside the venv):

    python assets/convert_icon.py --png assets/drone.png --out assets/drone.ico

If --png/--out are omitted, defaults are used:
    --png defaults to assets/drone.png
    --out defaults to assets/drone.ico

Requires: Pillow (pip install pillow)
"""

from __future__ import annotations
import argparse
import os
import sys

SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def main() -> int:
    base_dir = os.path.dirname(os.path.abspath(__file__))
    default_png = os.path.join(base_dir, "drone.png")
    default_out = os.path.join(base_dir, "drone.ico")

    parser = argparse.ArgumentParser(description="Convert PNG icon to ICO for the app")
    parser.add_argument(
        "--png",
        default=default_png,
        help="Path to source PNG (default: assets/drone.png)",
    )
    parser.add_argument(
        "--out",
        default=default_out,
        help="Path to output ICO (default: assets/drone.ico)",
    )
    args = parser.parse_args()

    try:
        from PIL import Image  # type: ignore
    except Exception:
        print(
            "This script requires Pillow. Install it with: pip install pillow",
            file=sys.stderr,
        )
        return 2

    src = os.path.normpath(args.png)
    dst = os.path.normpath(args.out)

    # If the user did not explicitly provide --png, auto-pick the most recent PNG in assets
    user_specified_src = any(arg.startswith("--png") for arg in sys.argv[1:])
    if not user_specified_src:
        try:
            candidates = []
            for name in os.listdir(base_dir):
                if name.lower().endswith(".png"):
                    full = os.path.join(base_dir, name)
                    try:
                        mtime = os.path.getmtime(full)
                    except Exception:
                        mtime = 0
                    candidates.append((mtime, full))
            if candidates:
                candidates.sort(reverse=True)
                newest = candidates[0][1]
                # Prefer newest png if default.png is older or not selected
                if (
                    os.path.exists(src)
                    and os.path.getmtime(newest) > os.path.getmtime(src)
                ) or (not os.path.exists(src)):
                    src = newest
                    print(f"Using detected PNG: {src}")
        except Exception:
            pass

    if not os.path.exists(src):
        # Try to auto-detect a PNG in the assets folder if the default is missing
        candidates = []
        try:
            for name in os.listdir(base_dir):
                if name.lower().endswith(".png"):
                    full = os.path.join(base_dir, name)
                    try:
                        mtime = os.path.getmtime(full)
                    except Exception:
                        mtime = 0
                    candidates.append((mtime, full))
        except Exception:
            candidates = []

        if candidates:
            # Prefer the most recently modified PNG
            candidates.sort(reverse=True)
            src = candidates[0][1]
            print(f"Using detected PNG: {src}")
        else:
            print(f"PNG not found: {src}", file=sys.stderr)
            return 1

    out_dir = os.path.dirname(dst) or base_dir
    os.makedirs(out_dir, exist_ok=True)

    try:
        img = Image.open(src).convert("RGBA")
        img.save(dst, sizes=SIZES)
    except Exception as e:
        print(f"Failed to write ICO: {e}", file=sys.stderr)
        return 1

    print(f"Wrote ICO: {dst}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
