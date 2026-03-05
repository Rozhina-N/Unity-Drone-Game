import logging
from typing import Any

logger = logging.getLogger(__name__)


# Mapping from logical player/drone color names to RGB (0-255)
PLAYER_COLORS = {
    "red": (255, 0, 0),
    "green": (0, 255, 0),
    "blue": (0, 0, 255),
    "yellow": (255, 255, 0),
    "purple": (128, 0, 128),
    "cyan": (0, 255, 255),
    "white": (255, 255, 255),
    "off": (0, 0, 0),
}


def apply_led_color(cf: Any, player_color: str, effect: int = 1) -> bool:
    """Apply LED ring color via Crazyflie param interface.

    cf: Crazyflie instance with .param API.
    player_color: logical color name (e.g. 'red', 'blue', 'off').
    effect: firmware-specific effect index (1 is often solid color).
    Returns True if parameters were sent, False otherwise.
    """

    if cf is None:
        return False

    key = (player_color or "").strip().lower()
    if key not in PLAYER_COLORS:
        logger.debug("LED: unknown color '%s'", player_color)
        return False

    r, g, b = PLAYER_COLORS[key]

    # Clamp to valid range 0-255
    r = max(0, min(255, int(r)))
    g = max(0, min(255, int(g)))
    b = max(0, min(255, int(b)))

    try:
        if not hasattr(cf, "param") or cf.param is None:
            logger.debug("LED: param interface not available on Crazyflie")
            return False

        # Use per-channel player color parameters so firmware can
        # combine player color with other ring effects when needed.
        cf.param.set_value("ring.red", str(r))
        cf.param.set_value("ring.green", str(g))
        cf.param.set_value("ring.blue", str(b))
        cf.param.set_value("ring.effect", str(effect))

        logger.info(
            "LED ring set to %s (r=%d, g=%d, b=%d, effect=%d)",
            player_color,
            r,
            g,
            b,
            effect,
        )
        return True
    except Exception:
        logger.debug("LED: failed to set ring parameters", exc_info=True)
        return False


def set_led_ring_color_from_handler(handler: Any, player_color: str, effect: int = 1) -> bool:
    """Best-effort LED ring color setter that uses the existing handler.

    Operates only via the Crazyflie object on the handler (handler._cf),
    without changing the handler implementation.

    handler: CrazyflieHandler or wrapper exposing a `.handler` with `_cf`.
    player_color: logical color name (e.g. 'red', 'blue', 'off').
    effect: firmware-specific effect index (1 is often solid color).

    Returns True if parameters were sent, False otherwise.
    """

    if handler is None:
        return False

    # Unwrap Drone-like wrapper if needed
    try:
        inner = getattr(handler, "handler", None)
        if inner is not None:
            handler = inner
    except Exception:
        pass

    # Expect Crazyflie instance on handler._cf
    try:
        cf = getattr(handler, "_cf", None)
    except Exception:
        cf = None

    if cf is None:
        logger.debug("LED: Crazyflie object not available on handler")
        return False

    ok = apply_led_color(cf, player_color, effect)

    if ok:
        # Remember color on handler for optional UI display and telemetry
        try:
            player_color_key = (player_color or "").strip().lower()
            setattr(handler, "current_color", player_color_key)
            r, g, b = PLAYER_COLORS.get(player_color_key, (0, 0, 0))
            setattr(handler, "ring_effect", int(effect))
            setattr(handler, "ring_redPlayer", int(r))
            setattr(handler, "ring_greenPlayer", int(g))
            setattr(handler, "ring_bluePlayer", int(b))
        except Exception:
            pass

    return ok


def apply_ring_params(
    cf: Any,
    effect: int | None = None,
    red: int | None = None,
    green: int | None = None,
    blue: int | None = None,
    empty_charge: float | None = None,
    full_charge: float | None = None,
) -> bool:
    """Apply detailed ring parameters via Crazyflie param interface.

    This helper allows Unity to control the ring effect, player-color
    channels and optional battery thresholds used by the firmware's
    battery-status visualization.
    """

    if cf is None:
        return False

    try:
        if not hasattr(cf, "param") or cf.param is None:
            logger.debug("LED: param interface not available on Crazyflie")
            return False

        if effect is not None:
            cf.param.set_value("ring.effect", str(int(effect)))

        def _clamp_rgb(v: int | None) -> int | None:
            if v is None:
                return None
            try:
                return max(0, min(255, int(v)))
            except Exception:
                return None

        red_c = _clamp_rgb(red)
        green_c = _clamp_rgb(green)
        blue_c = _clamp_rgb(blue)

        if red_c is not None:
            cf.param.set_value("ring.redPlayer", str(red_c))
        if green_c is not None:
            cf.param.set_value("ring.greenPlayer", str(green_c))
        if blue_c is not None:
            cf.param.set_value("ring.bluePlayer", str(blue_c))

        if empty_charge is not None:
            cf.param.set_value("ring.emptyCharge", str(float(empty_charge)))
        if full_charge is not None:
            cf.param.set_value("ring.fullCharge", str(float(full_charge)))

        logger.info(
            "Ring params set: effect=%s, rgb=(%s,%s,%s), emptyCharge=%s, fullCharge=%s",
            effect,
            red_c,
            green_c,
            blue_c,
            empty_charge,
            full_charge,
        )
        return True
    except Exception:
        logger.debug("LED: failed to set ring player/battery params", exc_info=True)
        return False


def set_ring_from_handler(
    handler: Any,
    effect: int | None = None,
    red: int | None = None,
    green: int | None = None,
    blue: int | None = None,
    empty_charge: float | None = None,
    full_charge: float | None = None,
) -> bool:
    """Best-effort setter for detailed ring parameters using an existing handler.

    This mirrors the Unity `set_ring` / `set_ring_effect` contract and stores
    the last-applied values on the handler for telemetry.
    """

    if handler is None:
        return False

    # Unwrap Drone-like wrapper if needed
    try:
        inner = getattr(handler, "handler", None)
        if inner is not None:
            handler = inner
    except Exception:
        pass

    try:
        cf = getattr(handler, "_cf", None)
    except Exception:
        cf = None

    if cf is None:
        logger.debug("LED: Crazyflie object not available on handler for ring params")
        return False

    ok = apply_ring_params(
        cf,
        effect=effect,
        red=red,
        green=green,
        blue=blue,
        empty_charge=empty_charge,
        full_charge=full_charge,
    )

    if ok:
        try:
            if effect is not None:
                setattr(handler, "ring_effect", int(effect))
            if red is not None:
                setattr(handler, "ring_redPlayer", int(red))
            if green is not None:
                setattr(handler, "ring_greenPlayer", int(green))
            if blue is not None:
                setattr(handler, "ring_bluePlayer", int(blue))
            if empty_charge is not None:
                setattr(handler, "ring_emptyCharge", float(empty_charge))
            if full_charge is not None:
                setattr(handler, "ring_fullCharge", float(full_charge))
        except Exception:
            pass

    return ok
