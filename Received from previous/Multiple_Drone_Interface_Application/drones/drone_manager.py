from collections import OrderedDict
from typing import Dict, List, Callable, Tuple, Optional, Any
import threading
from config_service import ConfigurationService


class DroneManager:
    """Manage multiple CrazyflieHandler instances and provide a simple API for the UI and websocket client.

    Usage:
        dm = DroneManager(config=config)
        dm.add('drone1', handler1)
        dm.add('drone2', handler2)
        handlers = dm.handlers  # OrderedDict
    """

    def __init__(self, config: ConfigurationService):
        """Create a manager.

        config: Configuration service instance
        """
        self._config = config
        self._handlers: Dict[str, Any] = OrderedDict()
        self._max = config.drone.max_drones
        # subscribers will be called with (event_name: str, drone_id: str)
        self._subscribers: List[Callable[[str, str], None]] = []
        self._last_move_scale_key: Optional[Tuple[float, int]] = None

    @property
    def handlers(self):
        """Return the ordered dict of handlers."""
        return self._handlers

    def _get_move_budget_hz(self) -> float:
        return self._config.performance.move_to_rate_hz

    def _is_connected(self, handler: Any) -> bool:
        if handler is None:
            return False
        try:
            if hasattr(handler, "is_connected"):
                return bool(handler.is_connected())
        except Exception:
            pass
        try:
            if hasattr(handler, "get_drone_connected"):
                return bool(handler.get_drone_connected())
        except Exception:
            pass
        try:
            if hasattr(handler, "connected"):
                return bool(handler.connected)
        except Exception:
            pass
        try:
            inner = getattr(handler, "handler", None)
            if inner is not None and inner is not handler:
                return self._is_connected(inner)
        except Exception:
            pass
        return False

    def _count_connected_drones(self) -> int:
        count = 0
        for handler in list(self._handlers.values()):
            if self._is_connected(handler):
                count += 1
        return count

    def _apply_move_rate_scaling(self, force: bool = False) -> None:
        base_rate = self._get_move_budget_hz()
        connected_count = self._count_connected_drones()
        key = (base_rate, connected_count)
        if not force and key == self._last_move_scale_key:
            return
        self._last_move_scale_key = key
        scale = max(1, connected_count)
        for handler in list(self._handlers.values()):
            target = handler
            if hasattr(handler, "handler") and getattr(handler, "handler") is not None:
                target = getattr(handler, "handler")
            if hasattr(target, "set_move_budget_hz"):
                try:
                    target.set_move_budget_hz(base_rate, scale)
                except Exception:
                    pass
            elif hasattr(target, "set_move_rate_hz"):
                try:
                    rate = base_rate / scale if base_rate > 0.0 else 0.0
                    target.set_move_rate_hz(rate)
                except Exception:
                    pass

    def refresh_move_rate_scaling(self) -> None:
        try:
            self._apply_move_rate_scaling()
        except Exception:
            pass

    def set_move_budget_hz(self, rate_hz: float) -> None:
        # Note: Cannot mutate immutable config, but we can update the scaling
        # This method is kept for API compatibility but doesn't persist changes
        self._apply_move_rate_scaling(force=True)

    def add(self, id: str, handler: Any) -> None:
        """Add a handler with a unique id."""
        if id in self._handlers:
            raise KeyError(f"Handler id '{id}' already exists")
        if self._max is not None and len(self._handlers) >= self._max:
            raise RuntimeError(f"Cannot add more than {self._max} drones")
        self._handlers[id] = handler
        # Auto-arm newly added handler/wrapper where possible so drones are
        # treated as 'armed' by the UI/status at all times per user request.
        try:
            # If handler is a wrapper with .handler attribute and it contains
            # the actual handler, set the inner handler's armed flag.
            if hasattr(handler, "handler") and getattr(handler, "handler") is not None:
                inner = getattr(handler, "handler")
                try:
                    inner.armed = int(getattr(inner, "armed", 0)) | 16
                except Exception:
                    try:
                        inner.armed = 16
                    except Exception:
                        pass
            else:
                # Handler may be a raw handler object itself; set its armed flag
                if hasattr(handler, "armed"):
                    try:
                        handler.armed = int(getattr(handler, "armed", 0)) | 16
                    except Exception:
                        try:
                            handler.armed = 16
                        except Exception:
                            pass
        except Exception:
            pass
        # notify subscribers
        for cb in list(self._subscribers):
            try:
                cb("added", id)
            except Exception:
                pass
        try:
            self._apply_move_rate_scaling()
        except Exception:
            pass

    def remove(self, id: str) -> None:
        """Remove a handler by id."""
        if id in self._handlers:
            try:
                # close in background to avoid blocking caller (UI thread)
                def _close(h: Any) -> None:
                    try:
                        h.close()
                    except Exception:
                        pass

                threading.Thread(
                    target=_close, args=(self._handlers[id],), daemon=True
                ).start()
            except Exception:
                pass
            del self._handlers[id]
            for cb in list(self._subscribers):
                try:
                    cb("removed", id)
                except Exception:
                    pass
            try:
                self._apply_move_rate_scaling()
            except Exception:
                pass

    def get(self, id: str):
        return self._handlers.get(id)

    def set_handler(self, id: str, handler: Any) -> None:
        """Attach or replace the handler object for an existing drone id.

        This method updates the stored value for `id` and notifies subscribers
        with an 'updated' event so UI code can refresh panels.
        """
        import logging

        if id not in self._handlers:
            raise KeyError(f"Handler id '{id}' does not exist")
        current = self._handlers.get(id)
        # If the current value is a Drone-like wrapper that holds a .handler
        # attribute, attach into that wrapper so external code keeps the
        # same object identity. Otherwise replace the mapping value.
        if hasattr(current, "handler"):
            try:
                current.handler = handler  # type: ignore
                logging.info(f"Attached handler into existing wrapper for {id}")
            except Exception:
                logging.exception(f"Failed to attach handler into wrapper for {id}")
            # If we attached into a wrapper, ensure the inner handler is auto-armed
            try:
                inner = getattr(current, "handler", None)
                if inner is not None and hasattr(inner, "armed"):
                    try:
                        inner.armed = int(getattr(inner, "armed", 0)) | 16  # type: ignore
                    except Exception:
                        try:
                            inner.armed = 16  # type: ignore
                        except Exception:
                            pass
            except Exception:
                pass
        else:
            self._handlers[id] = handler
            logging.info(f"Replaced handler mapping for {id}")
            # If we replaced the mapping with a raw handler object, set its armed flag
            try:
                obj = self._handlers.get(id)
                if obj is not None and hasattr(obj, "armed"):
                    try:
                        obj.armed = int(getattr(obj, "armed", 0)) | 16  # type: ignore
                    except Exception:
                        try:
                            obj.armed = 16  # type: ignore
                        except Exception:
                            pass
            except Exception:
                pass

        for cb in list(self._subscribers):
            try:
                cb("updated", id)
            except Exception:
                pass
        try:
            self._apply_move_rate_scaling()
        except Exception:
            pass

    def clear(self):
        for h in list(self._handlers.keys()):
            self.remove(h)
        for cb in list(self._subscribers):
            try:
                cb("cleared", "")
            except Exception:
                pass
        try:
            self._apply_move_rate_scaling()
        except Exception:
            pass

    def subscribe(self, callback: Callable[[str, str], None]) -> None:
        """Subscribe to manager events. Callback signature: (event_name: str, drone_id: str)"""
        if callback not in self._subscribers:
            self._subscribers.append(callback)

    def unsubscribe(self, callback: Callable[[str, str], None]) -> None:
        if callback in self._subscribers:
            self._subscribers.remove(callback)

    def count(self) -> int:
        return len(self._handlers)

    def ids(self):
        return list(self._handlers.keys())
