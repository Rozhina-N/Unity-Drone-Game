//using System;
//using UnityEngine;
//using UnityEngine.AI;

//public class WebsocketPlayerMovement : MonoBehaviour
//{
//    public bool LogData = false;
//    public WebSocketClient WSClient;
//    Vector3 position;
//    // Start is called once before the first execution of Update after the MonoBehaviour is created
//    void Start()
//    {
//        position = this.transform.position;
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        if (LogData)
//        {
//            Debug.Log("Position: " + WSClient.getPosition());
//        }
//        if (WSClient == null)
//        {
//            Debug.Log("Websocket client not found, please select one in the inspector.");
//            return;
//        }
//        this.transform.position = WSClient.getPosition();
//    }
//}

