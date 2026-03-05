STATUS_ORDER_DEFAULT = [
	'Connected',
	'Connecting',
	'Disconnected',
	'Charging',
	'Low battery',
	'Not registered',
]

STATUS_COLORS_DEFAULT = {
	'Connected': '#27ae60',
	'Connecting': '#F1C40F',
	'Disconnected': '#c0392b',
	'Charging': '#2E8BC0',
	'Low battery': '#E87E04',
	'Not registered': '#7f8c8d',
}

LEGACY_STATUS_LABEL_MAP = {'Not connected': 'Disconnected'}
LEGACY_STATUS_ORDERS = [
	['Connected', 'Charging', 'Low battery', 'Not connected', 'Not registered'],
	['Connected', 'Charging', 'Low battery', 'Disconnected', 'Not registered'],
]
