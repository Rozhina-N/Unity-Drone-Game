import asyncio
import websockets
import json
import logging
from typing import Dict, Any, Optional, cast

from config_service import ConfigurationService
from websocket.base_handler import WebSocketHandler
from websocket.takeoff_handler import TakeoffHandler
from websocket.land_handler import LandHandler
from websocket.led_color_handler import LedColorHandler
from websocket.move_to_handler import MoveToHandler
from websocket.ring_handler import RingHandler

class WebSocketClient:
	def __init__(
		self,
		uri: str,
		handlers: Optional[Dict[str, Any]] = None,
		config: Optional[ConfigurationService] = None
	) -> None:
		self.uri: str = uri
		self._config = config
		if handlers is None:
			self._handlers: Dict[str, Any] = {}
		elif hasattr(handlers, "handlers"):
			self._handlers = handlers.handlers  # type: ignore
		else:
			self._handlers = handlers
		self._normalized_handlers: Dict[str, Any] = {}
		self._rebuild_normalized_handlers()
		self.running: bool = True
		# Chain of Responsibility for command handling
		self.handler_chain: WebSocketHandler = TakeoffHandler(
			successor=LandHandler(
				successor=MoveToHandler(
					successor=LedColorHandler(
						successor=RingHandler()
					)
				)
			)
		)

	def _rebuild_normalized_handlers(self) -> None:
		def _norm(key: str) -> str:
			return "".join(key.split()).lower()
		self._normalized_handlers = {_norm(k): v for k, v in (self._handlers or {}).items()}

	def _find_handler(self, inbound_key: Optional[str]) -> Optional[Any]:
		if not inbound_key:
			return None
		def _norm(key: str) -> str:
			return "".join(key.split()).lower()
		nk = _norm(inbound_key)
		return self._normalized_handlers.get(nk)

	async def connect(self) -> None:
		retry_delay = self._config.websocket.retry_delay if self._config else 5.0
		while self.running:
			try:
				async with websockets.connect(self.uri) as websocket:
					logging.getLogger(__name__).info("Connected to WebSocket server")
					await self.handle_connection(websocket)
			except (
				ConnectionRefusedError,
				OSError,
				websockets.exceptions.ConnectionClosedOK,
			) as e:
				logging.getLogger(__name__).warning(
					f"Connection error: {e}. Retrying in {retry_delay} seconds..."
				)
				await asyncio.sleep(float(retry_delay))

	async def handle_connection(self, websocket: Any) -> None:
		while self.running:
			try:
				response = await websocket.recv()
				await self.process_message(response, websocket)
			except websockets.exceptions.ConnectionClosed as e:
				logging.getLogger(__name__).warning(
					f"Connection closed: {e}. Reconnecting..."
				)
				break

	async def process_message(self, message: str, websocket: Any) -> None:
		try:
			data = json.loads(message)
			target_id = data.get("drone_id") or data.get("id")
			handler = None
			if target_id:
				handler = self._find_handler(target_id)
			if handler is None and len(self._handlers) == 1:
				handler = next(iter(self._handlers.values()))
			# Chain of Responsibility for single drone commands
			if handler and "command" in data:
				await self.handler_chain.handle(data, handler, websocket)
			# Aggregate drones command support
			drones_block: Optional[Dict[str, Any]] = data.get("drones")
			if isinstance(drones_block, dict):
				for inbound_key, payload in drones_block.items():
					if not isinstance(payload, dict):
						continue
					payload_dict = cast(Dict[str, Any], payload)
					h = self._find_handler(inbound_key)
					if h is None and len(self._handlers) == 1:
						h = next(iter(self._handlers.values()))
					if h is None:
						logging.getLogger(__name__).warning(
							f"No handler found for aggregate drones key '{inbound_key}'"
						)
						continue
					if "command" in payload_dict:
						await self.handler_chain.handle(payload_dict, h, websocket)
			# Build a combined status for all handlers and send it
			status: Dict[str, Any] = {}
			for hid, h in self._handlers.items():
				try:
					battery = getattr(h, "battery_voltage", 0)
					try:
						battery = float(battery)
					except Exception:
						battery = 0.0
					try:
						from utils.battery import voltage_to_percent
						battery_pct = round(voltage_to_percent(battery), 2)
					except Exception:
						battery_pct = None
					sup_info = int(getattr(h, "supervisor_info", 0) or 0)
					can_fly = int(getattr(h, "can_fly", 0) or 0)
					is_flying = int(getattr(h, "is_flying", 0) or 0)
					is_locked = int(getattr(h, "is_locked", 0) or 0)
					is_crashed = int(getattr(h, "is_crashed", 0) or 0)
					drone_status: Dict[str, Any] = {
						"x": h.get_x(),
						"y": h.get_y(),
						"z": h.get_z(),
						"yaw": h.get_yaw(),
						"battery_v": round(battery, 2),
						"battery_pct": battery_pct,
						"connected": h.get_drone_connected(),
						"armed": getattr(h, "armed", 0),
						"can_fly": can_fly,
						"is_flying": is_flying,
						"is_locked": is_locked,
						"is_crashed": is_crashed,
						"supervisor_info": sup_info,
					}
					ring_effect = getattr(h, "ring_effect", None)
					ring_r = getattr(h, "ring_redPlayer", None)
					ring_g = getattr(h, "ring_greenPlayer", None)
					ring_b = getattr(h, "ring_bluePlayer", None)
					ring_empty = getattr(h, "ring_emptyCharge", None)
					ring_full = getattr(h, "ring_fullCharge", None)
					if any(v is not None for v in (ring_effect, ring_r, ring_g, ring_b, ring_empty, ring_full)):
						ring_obj = {}
						if ring_effect is not None:
							ring_obj["effect"] = ring_effect
							drone_status["ring_effect"] = ring_effect
							drone_status.setdefault("effect", ring_effect)
						if ring_r is not None:
							ring_obj["redPlayer"] = ring_r
						if ring_g is not None:
							ring_obj["greenPlayer"] = ring_g
						if ring_b is not None:
							ring_obj["bluePlayer"] = ring_b
						if ring_empty is not None:
							ring_obj["emptyCharge"] = ring_empty
						if ring_full is not None:
							ring_obj["fullCharge"] = ring_full
						drone_status["ring"] = ring_obj
					status[hid] = drone_status
				except Exception:
					status[hid] = {"error": "unavailable"}
			status_message = json.dumps({"drones": status})
			await websocket.send(status_message)
		except json.JSONDecodeError:
			logging.getLogger(__name__).warning(f"Invalid JSON received: {message}")

	def start(self) -> None:
		try:
			self._loop = asyncio.new_event_loop()
			asyncio.set_event_loop(self._loop)
			self._main_task = self._loop.create_task(self.connect())
			self._loop.run_until_complete(self._main_task)
		except KeyboardInterrupt:
			logging.getLogger(__name__).info("WebSocketClient received KeyboardInterrupt, stopping...")
		except Exception as e:
			logging.getLogger(__name__).warning(f"WebSocketClient stopped with exception: {e}")
		finally:
			logging.getLogger(__name__).info("WebSocketClient stopped.")
			if hasattr(self, '_loop'):
				self._loop.close()
