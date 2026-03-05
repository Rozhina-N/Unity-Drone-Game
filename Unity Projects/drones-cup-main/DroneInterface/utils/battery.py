
from typing import Any


def voltage_to_percent(v: Any) -> float:
    """Convert LiPo 1S voltage to a percentage using a typical discharge curve.

    This uses a small set of anchor points representative of a typical LiPo 1S
    discharge curve and linearly interpolates between them. Values outside
    the range are clamped.

    Anchor points (volts -> percent):
      4.20 -> 100
      4.00 -> 85
      3.80 -> 60
      3.70 -> 40
      3.60 -> 20
      3.30 -> 0

    Args:
        v: voltage in volts
    Returns:
        percentage float in range [0.0, 100.0]
    """
    try:
        v = float(v)
    except Exception:
        return 0.0

    anchors = [
        (3.30, 0.0),
        (3.60, 20.0),
        (3.70, 40.0),
        (3.80, 60.0),
        (4.00, 85.0),
        (4.20, 100.0),
    ]

    if v <= anchors[0][0]:
        return 0.0
    if v >= anchors[-1][0]:
        return 100.0

    for i in range(len(anchors) - 1):
        v0, p0 = anchors[i]
        v1, p1 = anchors[i + 1]
        if v0 <= v <= v1:
            if v1 == v0:
                return float(p0)
            frac = (v - v0) / (v1 - v0)
            return p0 + frac * (p1 - p0)

    return 0.0


def pm_state_is_charging(pm_state: int) -> bool:
    """Return True if the Crazyflie PM state indicates charging.

    Crazyflie firmware historically used both small enums and bitfields for
    ``pm.state``. The current convention (see Bitcraze docs and cfclient) is::

        bit 0 (0x01): on USB/powered
        bit 1 (0x02): charging
        bit 2 (0x04): charged (full)

    On some older firmware versions values ``0``, ``1``, ``2`` were also used
    as enums. We treat any value that sets the charging bit as "charging" and
    keep the legacy enum mapping for compatibility.
    """
    try:
        pm = int(pm_state)
    except Exception:
        return False

    if pm <= 0:
        return False

    # Legacy enum-style values: 1 == charging, 2 == charged (treat as externally powered)
    if pm == 1 or pm == 2:
        return True

    # Bitfield: bit1 (0x02) means charging, bit2 (0x04) means charged/full (treat as powered)
    return bool(pm & 0x02) or bool(pm & 0x04)
