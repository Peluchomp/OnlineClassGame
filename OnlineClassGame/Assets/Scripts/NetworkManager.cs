using NUnit.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using static UnityEditor.PlayerSettings;
using static UnityEngine.Rendering.DebugUI.Table;

public class NetworkManager : MonoBehaviour
{
    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;

    public static NetworkManager Instance; 

    public int port = 9050;
    public string serverAddress = "127.0.0.1";

    List<NetworkTransform> registeredTransforms = new List<NetworkTransform>();

    private Socket socket;
    private Thread serverThread;
    private Thread clientThread;
    private volatile bool m_cancel = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void RegisterTransform(NetworkTransform transform) 
    {  
        registeredTransforms.Add(transform);
        transform.SetNetworkId(registeredTransforms.Count - 1);
    }

    void Start()
    {
        if (role == NetworkRole.Server)
        {
            serverThread = new Thread(ServerProcess);
            serverThread.Start();
        }
        else if (role == NetworkRole.Client)
        {
            clientThread = new Thread(ClientProcess);
            clientThread.Start();
        }
        else if (role == NetworkRole.Host)
        {
            serverThread = new Thread(ServerProcess);
            clientThread = new Thread(ClientProcess);
            serverThread.Start();
            clientThread.Start();
        }
    }

    void OnDestroy()
    {
        m_cancel = true;
        if (serverThread != null && serverThread.IsAlive)
            serverThread.Abort();
        if (clientThread != null && clientThread.IsAlive)
            clientThread.Abort();
        socket?.Close();
    }

    private string SerializeTransforms()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var t in registeredTransforms)
        {
            sb.Append($"{t.networkId}|{t.netwPos.x:F2}.{t.netwPos.y:F2}.{t.netwPos.z:F2}|{t.netwRot.x:F2}.{t.netwRot.y:F2}.{t.netwRot.z:F2}.{t.netwRot.w:F2};");
        }

        Debug.Log("Serialized transforms: " + sb.ToString());
        return sb.ToString();
    }

    // Deserializa y actualiza los NetworkTransform registrados
    private void DeserializeAndApply(string data)
    {

        var entries = data.Split(';');
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split('|');
            if (parts.Length != 3) continue;

            int id;
            if (!int.TryParse(parts[0], out id)) continue;

            var posParts = parts[1].Split('.');
            var rotParts = parts[2].Split('.');

            if (posParts.Length != 3 || rotParts.Length != 4) continue;

            Vector3 pos = new Vector3(
                float.Parse(posParts[0]),
                float.Parse(posParts[1]),
                float.Parse(posParts[2])
            );
            Quaternion rot = new Quaternion(
                float.Parse(rotParts[0]),
                float.Parse(rotParts[1]),
                float.Parse(rotParts[2]),
                float.Parse(rotParts[3])
            );

            Debug.Log($"Updating transform with string: " + pos);

            var t = registeredTransforms.Find(x => x.networkId == id);
            if (t != null && !t.isLocalPlayer)
            {
                t.UpdateTransform(pos, rot);
            }
        }
    }


    void ServerProcess()
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
        serverSocket.Bind(ipep);

        byte[] buffer = new byte[1024];

        Debug.Log("Servidor UDP iniciado en el puerto " + port);

        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = 0;
            try
            {
                receivedBytes = serverSocket.ReceiveFrom(buffer, ref sender);
            }
            catch (SocketException) { break; }
            if (receivedBytes > 0)
            {
                string msg = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            //    Debug.Log($"[Servidor] Recibido de {sender}: {msg}");

                // Actualiza las posiciones recibidas
                DeserializeAndApply(msg);
                
                // Envía las posiciones actuales de todos los objetos
                string transformsData = SerializeTransforms();
                byte[] response = Encoding.UTF8.GetBytes(transformsData);
                Debug.Log($"[Server] Sending {sender}: {transformsData}");
                serverSocket.SendTo(response, sender);
            }
            Thread.Sleep(10);
        }
        serverSocket.Close();
    }

    void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        // Enviar primer mensaje
        clientSocket.SendTo(Encoding.UTF8.GetBytes("Hola desde el cliente"), serverEp);

        byte[] buffer = new byte[1024];

        Debug.Log("Cliente UDP iniciado, enviando a " + serverAddress + ":" + port);

        while (!m_cancel)
        {
            // Envía las posiciones actuales de los objetos locales
            string transformsData = SerializeTransforms();
            clientSocket.SendTo(Encoding.UTF8.GetBytes(transformsData), serverEp);

            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = 0;
            try
            {
                receivedBytes = clientSocket.ReceiveFrom(buffer, ref sender);
            }
            catch (SocketException) { break; }
            if (receivedBytes > 0)
            {
                string msg = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
              //  Debug.Log($"[Cliente] Recibido de {sender}: {msg}");

                // Actualiza las posiciones recibidas
                DeserializeAndApply(msg);
            }
            Thread.Sleep(10);
        }
        clientSocket.Close();
    }
}