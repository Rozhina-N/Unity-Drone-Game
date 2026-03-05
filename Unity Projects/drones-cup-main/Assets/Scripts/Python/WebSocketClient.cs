//using System;
//using System.Collections;
//using UnityEngine;
//using UnityEngine.Networking.PlayerConnection;
//using WebSocketSharp;

//public class WebSocketClient : MonoBehaviour
//{
//    private WebSocket ws;
//    private bool wsopen = false;
//    public float drone_x { get; private set; }
//    public float drone_y { get; private set; }
//    public float drone_z { get; private set; }
//    public string WebsocketIp = "localhost:8765";
//    public bool LogData = false;
//    public float corrector = 1000;
//    public int decimalPlaces = 2;

//    void Start()
//    {
//        drone_x = 1f;
//        drone_y = 1f;
//        drone_z = 1f;
//        Debug.Log("Loaded WS Script");
//        ConnectWebSocket();
//    }

//    private void ConnectWebSocket()
//    {
//        if (ws != null)
//        {
//            ws.Close();
//            ws = null;
//        }

//        ws = new WebSocket("ws://" + WebsocketIp);
//        ws.OnOpen += OnOpenHandler;
//        ws.OnMessage += OnMessageHandler;

//        ws.OnClose += OnWebsocketClose;
//        ws.OnError += OnWebsocketError;
//        ws.ConnectAsync();
//    }

//    private void OnOpenHandler(object sender, EventArgs e)
//    {
//        wsopen = true;
//        Debug.Log("Connected to WebSocket");
//        ws.Send("Player1");
//    }

//    private void OnMessageHandler(object sender, WebSocketSharp.MessageEventArgs e)
//    {
//        if (LogData)
//            Debug.Log("WebSocket server said: " + e.Data);

//        try
//        {
//            if (e.Data.StartsWith("Coords:"))
//            {
//                string[] coords = e.Data.Substring(7).Split(',');
//                if (coords.Length == 3)
//                {
//                    drone_x = float.Parse(coords[0].Trim());
//                    drone_y = float.Parse(coords[1].Trim());
//                    drone_z = float.Parse(coords[2].Trim());
//                }
//                else
//                {
//                    Debug.Log("Invalid coordinates format");
//                }
//            }
//            else
//            {
//                Debug.Log("Message does not contain coordinates");
//            }
//        }
//        catch (Exception ex)
//        {
//            Debug.Log("Error parsing coordinates: " + ex.Message);
//        }
//    }

//    private void OnWebsocketClose(object sender, CloseEventArgs e)
//    {
//        Debug.Log("WebSocket closed: " + e.Reason);
//        wsopen = false;
//        StartCoroutine(Reconnect());
//    }

//    private void OnWebsocketError(object sender, ErrorEventArgs e)
//    {
//        Debug.Log("WebSocket error: " + e.Message);
//        wsopen = false;
//        StartCoroutine(Reconnect());
//    }

//    private IEnumerator Reconnect()
//    {
//        while (!wsopen)
//        {
//            Debug.Log("Attempting to reconnect...");
//            yield return new WaitForSeconds(3);
//            ConnectWebSocket();
//        }
//    }

//    public Vector3 getPosition()
//    {
//        return new Vector3(
//            (float)Math.Round(drone_x / corrector, decimalPlaces),
//            (float)Math.Round(drone_z / corrector, decimalPlaces),
//            (float)Math.Round(drone_y / corrector, decimalPlaces)
//        );
//    }
//}
