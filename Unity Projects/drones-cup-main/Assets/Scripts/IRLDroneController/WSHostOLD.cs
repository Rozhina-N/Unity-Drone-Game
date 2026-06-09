using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine.InputSystem;

public class WebSocketServerBehaviorOLD : WebSocketBehavior
{
    public static WebSocketServerBehaviorOLD Instance { get; private set; } // Singleton instance
    private JObject jsonData = new JObject();

    protected override void OnOpen()
    {
        base.OnOpen();
        Instance = this; // Set the instance when a connection is opened
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        //Debug.Log("Received from Python: " + e.Data);
        try
        {
            jsonData = JObject.Parse(e.Data); // Store the JSON data
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Kon JSON van Python niet verwerken: " + ex.Message);
        }
    }

    public JObject GetJsonData()
    {
        return jsonData; // Provide access to the JSON data
    }
}

public class WSHostOLD : MonoBehaviour
{
    public static WSHostOLD Instance { get; private set; }

    private WebSocketServer wss;

    [Header("WebSocket Settings")]
    public int WebSocketPort = 8765;

    [Header("Drone")]
    public GameObject VirtualDrone;
    public bool moveTo = false;

    [Header("Unity Bounds")]
    public Transform UnityMinTransform;
    public Transform UnityMaxTransform;

    [Header("Drone Real World Bounds")]
    public Vector3 DroneMinPosition;
    public Vector3 DroneMaxPosition;

    public float GameBorderValue;
    public float RealWorldBorderValue;

    public float Factor { get; private set; }

    // 🔒 ONLY ONE CONTROLLER ALLOWED
    private GameObject controllingDrone;
    private bool hasController = false;

    public bool HasController => hasController;

    private JObject latestData = new JObject();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        wss = new WebSocketServer($"ws://0.0.0.0:{WebSocketPort}");
        wss.AddWebSocketService<WebSocketServerBehaviorOLD>("/drone");
        wss.Start();

        Debug.Log("WebSocket started on port " + WebSocketPort);


        StartCoroutine(SendPositionCoroutine());
    }

    void Stop()
    {
        wss.Stop();
    }

    // 🔥 FIRST PLAYER LOCKS CONTROL
    public bool BindController(GameObject drone)
    {
        if (hasController)
        {
            Debug.LogWarning($"Controller already assigned to {controllingDrone.name}. Ignoring {drone.name}");
            return false;
        }

        controllingDrone = drone;
        hasController = true;

        VirtualDrone = drone;

        TakeOffDrone();

        Debug.Log($"WSHost locked to controller: {drone.name}");
        return true;
    }

    // ─────────────────────────────
    // TELEMETRY (GLOBAL)
    // ─────────────────────────────
    public Vector3 GetPosition()
    {
        latestData = WebSocketServerBehaviorOLD.Instance?.GetJsonData();

        if (latestData != null && latestData["pos"] != null)
        {
            JObject pos = (JObject)latestData["pos"];

            return new Vector3(
                pos.Value<float>("x"),
                pos.Value<float>("y"),
                pos.Value<float>("z")
            );
        }

        return Vector3.zero;
    }

    public float GetYaw()
    {
        latestData = WebSocketServerBehaviorOLD.Instance?.GetJsonData();

        if (latestData != null && latestData["yaw"] != null)
            return latestData.Value<float>("yaw");

        return 0f;
    }

    // ─────────────────────────────
    // POSITION STREAM
    // ─────────────────────────────
    IEnumerator SendPositionCoroutine()
    {
        while (true)
        {
            if (VirtualDrone == null)
            {
                yield return new WaitForSeconds(0.1f);
                Debug.Log("oof");
                continue;
            }

            Vector3 dronePos = ConvertUnityToDronePosition(VirtualDrone.transform.position);

            JObject message = new JObject
            {
                ["x"] = dronePos.x,
                ["y"] = dronePos.y,
                ["z"] = dronePos.z,
                ["yaw"] = VirtualDrone.transform.rotation.eulerAngles.y
            };

            if (moveTo)
            {
                message = new JObject
                {
                    ["command"] = "move_to",
                    ["x"] = message["x"],
                    ["y"] = message["y"],
                    ["z"] = message["z"],
                    ["yaw"] = message["yaw"]
                };
            }

            Debug.Log($"message: {message}");

            SendMessageToClients(message.ToString());

            yield return new WaitForSeconds(0.1f);
        }
    }

    // ─────────────────────────────
    // CONVERSION (RESTORED ✅)
    // ─────────────────────────────
    private Vector3 ConvertUnityToDronePosition(Vector3 unityPosition)
    {
        Vector3 unityMin = UnityMinTransform.position;
        Vector3 unityMax = UnityMaxTransform.position;

        Vector3 normalized = new Vector3(
            Mathf.InverseLerp(unityMin.x, unityMax.x, unityPosition.x),
            Mathf.InverseLerp(unityMin.y, unityMax.y, unityPosition.y),
            Mathf.InverseLerp(unityMin.z, unityMax.z, unityPosition.z)
        );

        return new Vector3(
            Mathf.Lerp(DroneMinPosition.x, DroneMaxPosition.x, normalized.x),
            Mathf.Lerp(DroneMinPosition.y, DroneMaxPosition.y, normalized.y),
            Mathf.Lerp(DroneMinPosition.z, DroneMaxPosition.z, normalized.z)
        );
    }

    // ─────────────────────────────
    // COMMANDS
    // ─────────────────────────────
    public void TakeOffDrone(float time = 2, float height = 0.3f)
    {
        Debug.Log("attemptTakeOff");
        if (VirtualDrone == null) return;
        Debug.Log("yes");
        
        moveTo = false;

        JObject message = new JObject
        {
            ["command"] = "takeoff",
            ["time"] = time,
            ["height"] = height
        };

        Vector3 target = new Vector3(
            VirtualDrone.transform.position.x,
            height,
            VirtualDrone.transform.position.z
        );

        StartCoroutine(MoveOverTime(target, time));
        SendMessageToClients(message.ToString());

        Invoke(nameof(ToggleMoveTo), time);
    }

    public void LandDrone()
    {
        moveTo = false;
        SendCommand("land");
    }

    public void EmergencyStop()
    {
        ToggleMoveTo(false);
        SendCommand("stop");
        Debug.Log("Emergency stop triggered");
    }

    public void SendCommand(string command)
    {
        JObject msg = new JObject
        {
            ["command"] = command
        };

        SendMessageToClients(msg.ToString());
    }

    public void ToggleMoveTo(bool state)
    {
        moveTo = state;
    }

    public void ToggleMoveTo()
    {
        moveTo = !moveTo;
    }

    private IEnumerator MoveOverTime(Vector3 target, float duration)
    {
        Vector3 start = VirtualDrone.transform.position;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.SmoothStep(0, 1, t / duration);

            VirtualDrone.transform.position =
                Vector3.Lerp(start, target, lerp);

            yield return null;
        }

        VirtualDrone.transform.position = target;
    }

    private async void SendMessageToClients(string message)
    {
        if (wss == null) return;

        var sessions = wss.WebSocketServices["/drone"].Sessions;

        foreach (var session in sessions.Sessions)
        {
            if (session.Context.WebSocket.IsAlive)
            {
                await Task.Run(() =>
                {
                    session.Context.WebSocket.Send(message);
                });
            }
        }
    }
}