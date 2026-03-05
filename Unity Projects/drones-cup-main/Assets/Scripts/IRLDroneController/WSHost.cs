using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using UnityEngine.InputSystem;

public class WebSocketServerBehavior : WebSocketBehavior
{
    public static WebSocketServerBehavior Instance { get; private set; } // Singleton instance

    protected override void OnOpen()
    {
        base.OnOpen();
        Instance = this; // Set the instance when a connection is opened
        // Auto-bind this session to the first free drone if no id will be provided
        WSHost.Instance?.EnsureSessionAutoBound(ID);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        try
        {
            var json = JObject.Parse(e.Data);
            string sessionId = ID;

            // Case1: Aggregate payload with multiple drones
            if (json["drones"] is JObject dronesObj)
            {
                foreach (var prop in dronesObj)
                {
                    var inboundKey = prop.Key; // e.g., "Drone1"
                    string droneKey = WSHost.Instance?.ResolveDroneIdFromInboundKey(inboundKey);
                    if (!string.IsNullOrEmpty(droneKey))
                    {
                        WSHost.Instance?.RegisterOrUpdateDroneSession(droneKey, sessionId);
                        if (prop.Value is JObject perDroneData)
                        {
                            WSHost.Instance?.UpdateDroneData(droneKey, perDroneData);
                        }
                    }
                }
                return; // handled
            }

            // Case2: Single-drone payload (legacy) - assume 'id' or 'drone_id' holds the inbound key
            string droneKeyFromMsg = json.Value<string>("id") ?? json.Value<string>("drone_id");
            if (!string.IsNullOrEmpty(droneKeyFromMsg))
            {
                WSHost.Instance?.BindSessionToDrone(sessionId, droneKeyFromMsg);
            }
            else
            {
                WSHost.Instance?.EnsureSessionAutoBound(sessionId);
            }
            var resolvedDroneKey = WSHost.Instance?.GetDroneIdForSession(sessionId);
            if (!string.IsNullOrEmpty(resolvedDroneKey))
            {
                WSHost.Instance?.UpdateDroneData(resolvedDroneKey, json);
            }
            else
            {
                Debug.LogWarning("Geen drone gekoppeld aan sessie en geen id in bericht.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Kon JSON van Python niet verwerken: " + ex.Message);
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        WSHost.Instance?.UnbindSession(ID);
    }
}

[Serializable]
public class DroneBinding
{
    public GameObject VirtualDrone;
    public float FixedHeight = 1f;
    public string InboundKey; // Inbound key used by Python under 'drones' map (e.g. "Drone1")
    [Header("Drone player color")] public Color PlayerColor = Color.white;
}

public class WSHost : MonoBehaviour
{
    private WebSocketServer wss;
    public float Factor { get; private set; }
    public int WebSocketPort =8765;
    public List<DroneBinding> DroneBindings = new List<DroneBinding>();
    public float GameBorderValue;
    public float RealWorldBorderValue;

    // Per-drone state (keyed by inboundKey)
    private readonly Dictionary<string, DroneBinding> _droneObjects = new Dictionary<string, DroneBinding>();
    private readonly Dictionary<string, bool> _moveTo = new Dictionary<string, bool>();
    private readonly Dictionary<string, JObject> _latestData = new Dictionary<string, JObject>();
    private readonly Dictionary<string, string> _droneToSession = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _sessionToDrone = new Dictionary<string, string>();
    private readonly Dictionary<string, GameObject> _droneToPlayer = new Dictionary<string, GameObject>();

    private readonly object _sync = new object();

    public static WSHost Instance { get; private set; }

    // NEW: track last sent per-drone color to avoid redundant sends
    private readonly Dictionary<string, Color> _lastSentColor = new Dictionary<string, Color>();

    // NEW: track last sent real-world positions to smooth / limit jumps
    private readonly Dictionary<string, Vector3> _lastSentRealPos = new Dictionary<string, Vector3>();

    // Configurable smoothing: max meters per update (0 = no limit)
    private const float MaxSendDeltaMeters = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        // Initialize bindings
        _droneObjects.Clear();
        foreach (var droneBinding in DroneBindings)
            InitializeDroneBindings(droneBinding.InboundKey);
    }

    public void InitializeDroneBindings(string inboundKey)
    {
        var droneBinding = DroneBindings.First(drone => drone.InboundKey == inboundKey);
        if (droneBinding != null && !string.IsNullOrEmpty(droneBinding.InboundKey) && droneBinding.VirtualDrone != null)
        {
            _droneObjects[droneBinding.InboundKey] = droneBinding;
            _moveTo[droneBinding.InboundKey] = false;
            if (!_lastSentColor.ContainsKey(droneBinding.InboundKey)) _lastSentColor[droneBinding.InboundKey] = new Color(-1,-1,-1, -1); // sentinel
            if (!_lastSentRealPos.ContainsKey(droneBinding.InboundKey)) _lastSentRealPos[droneBinding.InboundKey] = new Vector3(float.NaN, float.NaN, float.NaN);
        }
    }

    // Try to resolve inbound key to an existing configured key (case/space insensitive match)
    public string ResolveDroneIdFromInboundKey(string inboundKey)
    {
        if (string.IsNullOrEmpty(inboundKey)) return null;
        lock (_sync)
        {
            if (_droneObjects.ContainsKey(inboundKey))
                return inboundKey;
            // Normalize and try matching ignoring spaces and case
            string norm(string s) => new string((s ?? string.Empty).ToLowerInvariant().Replace(" ", "").ToCharArray());
            var target = norm(inboundKey);
            foreach (var key in _droneObjects.Keys)
            {
                if (norm(key) == target)
                    return key;
            }
        }
        return null;
    }

    void Start()
    {
        wss = new WebSocketServer($"ws://0.0.0.0:{WebSocketPort}");
        wss.AddWebSocketService<WebSocketServerBehavior>("/drone");
        wss.Start();
        Debug.Log("WebSocket server started on ws://localhost:" + WebSocketPort);

        StartCoroutine(sendPositionCoroutine());
        Factor = calculateFactor(GameBorderValue, RealWorldBorderValue);
    }

    private void OnApplicationQuit()
    {
        this.LandDrone();
    }

    private void Update()
    {
        // Use the new Input System when available (Keyboard.current). Fall back to legacy Input if not.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                this.EmergencyStop();
            }
            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                this.TakeOffDrone();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                this.EmergencyStop();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                this.TakeOffDrone();
            }
        }

        // Auto push drone color changes
        foreach (var b in DroneBindings)
        {
            if (b == null || string.IsNullOrEmpty(b.InboundKey)) continue;
            if (!_droneObjects.ContainsKey(b.InboundKey)) continue;
            Color last;
            _lastSentColor.TryGetValue(b.InboundKey, out last);
            if (!ApproximatelySame(b.PlayerColor, last))
            {
                SetDronePlayerColor(b.InboundKey, b.PlayerColor);
                _lastSentColor[b.InboundKey] = b.PlayerColor;
            }
        }
    }
    
    public void BindPlayerToDrone(string droneKey, GameObject player)
    {
        if (string.IsNullOrEmpty(droneKey) || player == null) return;

        PlayerSetup setup = player.GetComponent<PlayerSetup>();
        if (setup == null) return;

        DroneBinding binding = DroneBindings.FirstOrDefault(d => d != null && d.InboundKey == droneKey);
        if (binding == null) return;

        binding.PlayerColor = setup.PlayerColor;
        _droneToPlayer[droneKey] = player;

        // forceer dat je bestaande Update() meteen opnieuw pusht
        if (!_lastSentColor.ContainsKey(droneKey))
            _lastSentColor[droneKey] = new Color(-1, -1, -1, -1);
        else
            _lastSentColor[droneKey] = new Color(-1, -1, -1, -1);

        if (!_lastSentRealPos.ContainsKey(droneKey))
            _lastSentRealPos[droneKey] = new Vector3(float.NaN, float.NaN, float.NaN);
        else
            _lastSentRealPos[droneKey] = new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private float calculateFactor(float value1, float value2)
    {
        if (value2 != 0)
        {
            float factor = value1 / value2;
            Debug.Log($"Factor berekend: {factor}");
            return factor;
        }
        else
        {
            Debug.LogWarning("Waarde2 mag niet nul zijn bij het berekenen van de Factor.");
            return 1f;
        }
    }

    // Session management
    public void RegisterOrUpdateDroneSession(string droneKey, string sessionId)
    {
        lock (_sync)
        {
            _droneToSession[droneKey] = sessionId;
            _sessionToDrone[sessionId] = droneKey;
            if (!_moveTo.ContainsKey(droneKey)) _moveTo[droneKey] = false;
        }
    }

    public void UpdateDroneData(string droneKey, JObject data)
    {
        lock (_sync)
        {
            bool first = !_latestData.ContainsKey(droneKey);
            _latestData[droneKey] = data;
            if (first)
            {
                Debug.Log($"Ontvangen eerste data voor drone {droneKey}: {data}");
            }
        }
    }

    public void EnsureSessionAutoBound(string sessionId)
    {
        lock (_sync)
        {
            if (_sessionToDrone.ContainsKey(sessionId)) return;
            var free = GetFirstFreeDroneId_NoLock();
            if (free != null)
            {
                _sessionToDrone[sessionId] = free;
                _droneToSession[free] = sessionId;
                if (!_moveTo.ContainsKey(free)) _moveTo[free] = false;
                Debug.Log($"Auto-bound session {sessionId} to drone {free}");
            }
            else
            {
                Debug.LogWarning("Geen vrije drone beschikbaar om sessie te binden.");
            }
        }
    }

    public void BindSessionToDrone(string sessionId, string droneKey)
    {
        lock (_sync)
        {
            // Unbind this session from any previous drone
            if (_sessionToDrone.TryGetValue(sessionId, out var prevDrone) && prevDrone != droneKey)
            {
                _droneToSession.Remove(prevDrone);
            }
            // If target drone was bound to another session, unbind that session
            if (_droneToSession.TryGetValue(droneKey, out var prevSession) && prevSession != sessionId)
            {
                _sessionToDrone.Remove(prevSession);
            }
            _sessionToDrone[sessionId] = droneKey;
            _droneToSession[droneKey] = sessionId;
            if (!_moveTo.ContainsKey(droneKey)) _moveTo[droneKey] = false;
        }
    }

    public string GetDroneIdForSession(string sessionId)
    {
        lock (_sync)
        {
            _sessionToDrone.TryGetValue(sessionId, out var id);
            return id;
        }
    }

    public void UnbindSession(string sessionId)
    {
        lock (_sync)
        {
            if (_sessionToDrone.TryGetValue(sessionId, out var droneKey))
            {
                _sessionToDrone.Remove(sessionId);
                if (!string.IsNullOrEmpty(droneKey))
                {
                    _droneToSession.Remove(droneKey);
                    if (_moveTo.ContainsKey(droneKey)) _moveTo[droneKey] = false;
                }
            }
        }
    }

    private string GetFirstFreeDroneId_NoLock()
    {
        foreach (var id in _droneObjects.Keys)
        {
            if (!_droneToSession.ContainsKey(id))
                return id;
        }
        return null;
    }

    public string GetAnyBoundDroneId()
    {
        lock (_sync)
        {
            foreach (var id in _droneToSession.Keys)
                return id;
            // fallback to first configured if none bound
            foreach (var id in _droneObjects.Keys)
                return id;
        }
        return null;
    }

    public bool HasData(string droneKey)
    {
        lock (_sync)
        {
            return _latestData.ContainsKey(droneKey) && _latestData[droneKey] != null;
        }
    }

    public bool HasAnyData()
    {
        lock (_sync)
        {
            foreach (var kv in _latestData)
                return true;
        }
        return false;
    }

    // Public API (applies to all drones if not specified)
    public void TakeOffDrone(float time = 2)
    {
        foreach (var kvp in _droneObjects)
        {
            TakeOffDrone(kvp.Key, time);
        }
    }

    public void TakeOffDrone(string droneKey, float time = 2)
    {
        if (!_droneObjects.ContainsKey(droneKey)) return;
        var height = _droneObjects[droneKey].FixedHeight;
        _moveTo[droneKey] = false;
        Debug.Log($"sending command: takeoff to {droneKey}, time = {time}, height = {height}.");
        JObject message = new JObject
        {
            ["id"] = droneKey,
            ["command"] = "takeoff",
            ["time"] = time,
            ["height"] = height
        };
        var droneObj = _droneObjects[droneKey].VirtualDrone;
        // ensure the visual move uses Unity Y as up (convert real meters -> Unity units via Factor)
        if (droneObj != null)
        {
            Vector3 targetPosition = new Vector3(droneObj.transform.position.x, height * Factor, droneObj.transform.position.z);
            StartCoroutine(moveOverTime(droneObj, targetPosition, time));
        }
        SendMessageToDrone(droneKey, message.ToString());
        // enable moveTo for this specific drone after takeoff time
        StartCoroutine(CallAfter(time, () => ToggleMoveTo(droneKey, true)));
    }

    public void LandDrone()
    {
        foreach (var kvp in _droneObjects)
        {
            LandDrone(kvp.Key);
        }
    }

    public void LandDrone(string droneKey)
    {
        if (!_droneObjects.ContainsKey(droneKey)) return;
        _moveTo[droneKey] = false;
        sendCommand(droneKey, "land");
    }

    public void EmergencyStop()
    {
        foreach (var kvp in _droneObjects)
        {
            ToggleMoveTo(kvp.Key, false);
            sendCommand(kvp.Key, "stop");
        }
        Debug.Log("Emergency stop! Locked all drones.");
    }

    public void LandDroneAt(string droneKey, Vector3 targetPosition, float duration)
    {
        if (!_droneObjects.ContainsKey(droneKey)) return;
        var droneObj = _droneObjects[droneKey].VirtualDrone;
        StartCoroutine(moveOverTime(droneObj, targetPosition, duration));
        StartCoroutine(CallAfter(duration, () => LandDrone(droneKey)));
    }

    private IEnumerator CallAfter(float time, Action action)
    {
        yield return new WaitForSeconds(time);
        action?.Invoke();
    }

    private IEnumerator moveOverTime(GameObject droneObj, Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = droneObj.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            droneObj.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        droneObj.transform.position = targetPosition;
    }

    public Vector3 getPosition(string droneKey)
    {
        lock (_sync)
        {
            if (_latestData.ContainsKey(droneKey) && _latestData[droneKey] != null)
            {
                var data = _latestData[droneKey];
                Debug.Log(data);
                // Accept nested 'pos' or 'position' or root x/y/z
                if (data["pos"] != null)
                {
                    JObject pos = (JObject)data["pos"];
                    float x = pos.Value<float>("x");
                    float y = pos.Value<float>("y");
                    float z = pos.Value<float>("z");
                    // Map drone->Unity: drone.x->X, drone.y->Z, drone.z (altitude)->Y
                    return new Vector3(x, z, y);
                }
                else if (data["position"] != null)
                {
                    JObject pos = (JObject)data["position"];
                    float x = pos.Value<float>("x");
                    float y = pos.Value<float>("y");
                    float z = pos.Value<float>("z");
                    return new Vector3(x, z, y);
                }
                else if (data["x"] != null && data["y"] != null && data["z"] != null)
                {
                    float x = data.Value<float>("x");
                    float y = data.Value<float>("y");
                    float z = data.Value<float>("z");
                    return new Vector3(x, z, y);
                }
            }
        }
        return Vector3.zero;
    }

    // Legacy methods map to any currently bound drone (fallback first configured)
    public Vector3 getPosition()
    {
        var id = GetAnyBoundDroneId();
        if (!string.IsNullOrEmpty(id))
            return getPosition(id);
        return Vector3.zero;
    }

    public float getYaw(string droneKey)
    {
        lock (_sync)
        {
            if (_latestData.ContainsKey(droneKey) && _latestData[droneKey] != null)
            {
                var data = _latestData[droneKey];
                if (data["yaw"] != null)
                {
                    return data.Value<float>("yaw");
                }
            }
        }
        return 0f;
    }

    public float getYaw()
    {
        var id = GetAnyBoundDroneId();
        if (!string.IsNullOrEmpty(id))
            return getYaw(id);
        return 0f;
    }

    private async void SendMessageToDrone(string droneKey, string message)
    {
        if (wss == null)
        {
            Debug.LogWarning($"Cannot send to {droneKey}: WebSocket server not initialized");
            return;
        }
        
        // Python app uses a single WebSocket connection for all drones.
        // Always broadcast - the message contains the drone id so Python routes it correctly.
        try
        {
            await Task.Run(() =>
            {
                var mgr = wss.WebSocketServices["/drone"].Sessions;
                if (mgr.Count > 0)
                {
                    mgr.Broadcast(message);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send message for {droneKey}: {ex.Message}");
        }
    }

    private async void SendMessageToAll(string message)
    {
        if (wss != null)
        {
            await Task.Run(() =>
            {
                var mgr = wss.WebSocketServices["/drone"].Sessions;
                mgr.Broadcast(message);
            });
        }
    }

    public void sendCommand(string droneKey, string command)
    {
        Debug.Log($"sending command: {command} to {droneKey}");
        JObject message = new JObject
        {
            ["id"] = droneKey,
            ["command"] = command
        };
        SendMessageToDrone(droneKey, message.ToString());
    }

    public void sendCommand(string command)
    {
        foreach (var kvp in _droneObjects)
        {
            sendCommand(kvp.Key, command);
        }
    }

    private bool IsDroneFlying(string droneKey)
    {
        lock (_sync)
        {
            if (_latestData.ContainsKey(droneKey) && _latestData[droneKey] != null && _latestData[droneKey]["armed"] != null)
            {
                float armedValue = _latestData[droneKey].Value<float>("armed");
                int bits = (int)armedValue;
                return (bits & 16) != 0;
            }
        }
        return false;
    }

    public void ToggleMoveTo(bool status)
    {
        var keys = new List<string>(_moveTo.Keys);
        foreach (var id in keys)
        {
            _moveTo[id] = status;
        }
    }

    public void ToggleMoveTo(string droneKey, bool status)
    {
        _moveTo[droneKey] = status;
    }

    public void ToggleMoveTo()
    {
        var keys = new List<string>(_moveTo.Keys);
        foreach (var id in keys)
        {
            _moveTo[id] = !_moveTo[id];
            Debug.Log("Toggling moveTo for " + id + " to: " + _moveTo[id]);
        }
    }

    IEnumerator sendPositionCoroutine()
    {
        while (true)
        {
            foreach (var kvp in _droneObjects)
            {
                var droneKey = kvp.Key;
                var droneObj = kvp.Value.VirtualDrone;
                var height = kvp.Value.FixedHeight;
                bool doMoveTo = _moveTo.ContainsKey(droneKey) && _moveTo[droneKey];

                if (doMoveTo)
                {
                    // Always use the configured FixedHeight for altitude (z)
                    float targetX = droneObj.transform.position.x / Factor;
                    float targetY = droneObj.transform.position.y / Factor;
                    float targetZ = droneObj.transform.position.z / Factor; 

                    JObject message = new JObject
                    {
                        ["id"] = droneKey,
                        ["command"] = "move_to",
                        ["x"] = targetX,
                        ["y"] = targetZ,
                        ["z"] = height,
                        ["yaw"] = droneObj.transform.rotation.eulerAngles.y
                    };
                    SendMessageToDrone(droneKey, message.ToString());
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }



    public void SetDronePlayerColor(string droneKey, Color color)
    {
        if (string.IsNullOrEmpty(droneKey) || !_droneObjects.ContainsKey(droneKey)) return;
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r *255f),0,255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g *255f),0,255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b *255f),0,255);
        JObject message = new JObject
        {
            ["id"] = droneKey,
            ["command"] = "set_ring",
            ["effect"] =19,
            ["redPlayer"] = r,
            ["greenPlayer"] = g,
            ["bluePlayer"] = b
        };
        Debug.Log($"sending command: set_ring to {droneKey} effect=19 rgb=({r},{g},{b})");
        SendMessageToDrone(droneKey, message.ToString());
    }

    private bool ApproximatelySame(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
    }

}
