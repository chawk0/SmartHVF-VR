using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArduinoBluetoothAPI;

public class BluetoothManager : MonoBehaviour
{
    private BluetoothHelper helper;

    void Start()
    {
        BluetoothHelper.BLE = false;
        helper = BluetoothHelper.GetInstance();
        helper.OnConnected += OnConnected;
        helper.OnConnectionFailed += OnConnectionFailed;
        helper.OnDataReceived += OnDataReceived;
        helper.setFixedLengthBasedStream(1); //data is received byte by byte
        helper.setDeviceName("HC-05");
    }

    void OnConnected(BluetoothHelper helper)
    {
        Debug.Log("BLUETOOTH: Connected...");
        helper.StartListening();        
    }

    void OnConnectionFailed(BluetoothHelper helper)
    {
        Debug.Log("BLUETOOTH: Failed to connect");
    }

    void OnDataReceived(BluetoothHelper helper)
    {
        Debug.Log("BLUETOOTH: Data received:");
        string msg = helper.Read();

        if (msg.Length > 0)
            Debug.Log($"Received message [{msg}]");
    }

    public void Connect()
    {
        helper.Connect();
    }

    public void Disconnect()
    {
        helper.Disconnect();
    }

    public void sendData(string d)
    {
        helper.SendData(d);
    }

    // Update is called once per frame
    /*void Update()
    {
        
    }*/
}
