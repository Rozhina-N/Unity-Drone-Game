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
    private WebSocketServer wss;
    public float Factor { get; private set; } // Maak Factor publiek leesbaar
    public int WebSocketPort = 8765;
    public GameObject VirtualDrone;
    public bool moveTo = false;

    public float GameBorderValue;
    public float RealWorldBorderValue;

    private JObject latestData = new JObject();

    public static WSHostOLD Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
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
        wss.Stop();
        this.LandDrone();
    }

    private void Update()
    {
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
        else {
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                this.EmergencyStop();
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                this.TakeOffDrone();
            }
        }
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
            Debug.LogWarning("Waarde 2 mag niet nul zijn bij het berekenen van de Factor.");
            return 1f;
        }
    }

    public void TakeOffDrone(float time = 2, float height = 1)
    {
        // disables moveTo, and sends to takeoff command 
        moveTo = false;
        Debug.Log($"sending command: takeoff, time = {time}, height = {height}.");
        JObject message = new JObject
        {
            ["command"] = "takeoff",
            ["time"] = time,
            ["height"] = height
        };
        Vector3 targetPosition = new Vector3(VirtualDrone.transform.position.x, height * Factor, VirtualDrone.transform.position.z);
        StartCoroutine(moveOverTime(targetPosition, time));
        SendMessageToClients(message.ToString());

        //2 seconds after takeoff moveTo is enabled
        Invoke("ToggleMoveTo", time);
    }

    public void LandDrone()
    {
        // disables moveTo, and sends to land command 
        moveTo = false;
        sendCommand("land");
    }

    public void EmergencyStop()
    {
        ToggleMoveTo(false);
        sendCommand("stop");
        Debug.Log("Emergency stop! Locked drone.");
    }

    public void LandDroneAt(Vector3 targetPosition, float duration)
    {
        StartCoroutine(moveOverTime(targetPosition, duration));
        Invoke("LandDrone", duration);

    }

    private IEnumerator moveOverTime(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = VirtualDrone.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smoothstep maakt het vloeiender (ease in/out)
            t = t * t * (3f - 2f * t);

            VirtualDrone.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        VirtualDrone.transform.position = targetPosition; // Zorg dat hij precies eindigt op de target
    }

    public Vector3 getPosition()
    {
        latestData = WebSocketServerBehaviorOLD.Instance?.GetJsonData();
        if (latestData != null && latestData["pos"] != null)
        {
            JObject pos = (JObject)latestData["pos"];
            float x = pos.Value<float>("x");
            float y = pos.Value<float>("y");
            float z = pos.Value<float>("z");
            Vector3 newPos = new Vector3(x, y, z);
            return newPos;
        }
        return Vector3.zero;
    }

    public float getYaw()
    {
        latestData = WebSocketServerBehaviorOLD.Instance?.GetJsonData();
        if (latestData != null && latestData["yaw"] != null)
        {
            return latestData.Value<float>("yaw");
        }
        return 0f;
    }


    private async void SendMessageToClients(string message) // Renamed to avoid conflict
    {
        if (wss != null && wss.WebSocketServices["/drone"].Sessions.Count > 0)
        {
            foreach (var session in wss.WebSocketServices["/drone"].Sessions.Sessions)
            {
                if (session.Context.WebSocket.IsAlive)
                {
                    await Task.Run(() =>
                    {
                        session.Context.WebSocket.Send(message); // Send message in a separate thread
                        //Debug.Log("Sending message: " + message);
                    });
                }
            }
        }
    }

    public void sendCommand(string command)

    {
        Debug.Log("sending command: " + command);
        JObject message = new JObject
        {
            ["command"] = command
        };
        SendMessageToClients(message.ToString());
    }

    private bool IsDroneFlying()
    {
        // Retrieve the latest JSON data
        latestData = WebSocketServerBehaviorOLD.Instance?.GetJsonData();
        if (latestData != null && latestData["armed"] != null)
        {
            float armedValue = latestData.Value<float>("armed");
            int bits = (int)armedValue;

            // Check if the "Is flying" bit (16) is set
            return (bits & 16) != 0;
        }
        return false; // Default to false if data is unavailable
    }

    public void ToggleMoveTo(bool status)
    {
        moveTo = status;
    }

    public void ToggleMoveTo()
    {
        moveTo = !moveTo;
        Debug.Log("Toggling moveTo to: " + moveTo);
    }

    IEnumerator sendPositionCoroutine()
    {
        while (true)
        {
            JObject message = new JObject
            {
                ["x"] = VirtualDrone.transform.position.x / Factor,
                ["y"] = VirtualDrone.transform.position.y / Factor,
                ["z"] = VirtualDrone.transform.position.z / Factor,
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
                Debug.Log(message);
            }

            SendMessageToClients(message.ToString());

            // sends position data every 0.1 seconds
            // When moveTo is true, it sends the command to move to the new position
            yield return new WaitForSeconds(0.1f);
        }
    }
}
