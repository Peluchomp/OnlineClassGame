// NetworkManager.cs
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    public static NetworkManager Instance { get; private set; }

    // --- Public Fields ---
    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;
    public int port = 9050;
    public string serverAddress = "127.0.0.1";
    public float sendRate = 20f; // Messages per second

    // --- Private Fields ---
    // KEY CHANGE: A single socket for the instance, shared between threads.
    private Socket socket;
    private Thread networkThread;
    private volatile bool m_cancel = false;

    private readonly ConcurrentQueue<string> receivedMessages = new ConcurrentQueue<string>();
    private readonly Dictionary<int, NetworkTransform> networkTransforms = new Dictionary<int, NetworkTransform>();
    // A thread-safe way to manage connected clients for the server
    private readonly ConcurrentDictionary<EndPoint, byte> clients = new ConcurrentDictionary<EndPoint, byte>();
    private int nextNetworkId = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Start the appropriate network process in a background thread
        if (role == NetworkRole.Server)
        {
            networkThread = new Thread(ServerProcess);
        }
        else if (role == NetworkRole.Client)
        {
            networkThread = new Thread(ClientProcess);
        }
        else if (role == NetworkRole.Host)
        {
            // A host runs the server logic
            networkThread = new Thread(ServerProcess);
        }

        networkThread.Start();

        // The Host and Client need to send data periodically
        if (role == NetworkRole.Host || role == NetworkRole.Client)
        {
            StartCoroutine(SendDataLoop());
        }
    }

    void Update()
    {
        if (role == NetworkRole.Client || role == NetworkRole.Client)
        {
            Debug.Log(receivedMessages.Count + " mensajes en cola" );
        } 

        while (receivedMessages.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }
    }

    void OnDestroy()
    {
        m_cancel = true;
        // Close the socket first to interrupt the thread's blocking ReceiveFrom call
        socket?.Close();
        networkThread?.Join(); // Wait for the thread to finish cleanly
    }

    public void RegisterTransform(NetworkTransform nt)
    {
        nt.SetNetworkId(nextNetworkId);
        networkTransforms.Add(nextNetworkId, nt);
        nextNetworkId++;
    }

    // --- Data Serialization and Deserialization (Unchanged) ---
    private string SerializeTransforms()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var nt in networkTransforms.Values.Where(t => t.isLocalPlayer))
        {
            Transform t = nt.transform;
            sb.Append($"T|{nt.networkId}|");
            sb.Append($"{t.position.x.ToString(CultureInfo.InvariantCulture)}|{t.position.y.ToString(CultureInfo.InvariantCulture)}|{t.position.z.ToString(CultureInfo.InvariantCulture)}|");
            sb.Append($"{t.rotation.x.ToString(CultureInfo.InvariantCulture)}|{t.rotation.y.ToString(CultureInfo.InvariantCulture)}|{t.rotation.z.ToString(CultureInfo.InvariantCulture)}|{t.rotation.w.ToString(CultureInfo.InvariantCulture)}\n");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private void ProcessMessage(string message)
    {
        var lines = message.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split('|');
            if (parts[0] != "T") continue;

            int id = int.Parse(parts[1]);
            if (networkTransforms.TryGetValue(id, out NetworkTransform nt) && !nt.isLocalPlayer)
            {
                Vector3 pos = new Vector3(
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture),
                    float.Parse(parts[4], CultureInfo.InvariantCulture));

                Quaternion rot = new Quaternion(
                    float.Parse(parts[5], CultureInfo.InvariantCulture),
                    float.Parse(parts[6], CultureInfo.InvariantCulture),
                    float.Parse(parts[7], CultureInfo.InvariantCulture),
                    float.Parse(parts[8], CultureInfo.InvariantCulture));

                nt.UpdateTransform(pos, rot);
            }
        }
    }

    // --- Sending Coroutine (Client/Host) ---
    private System.Collections.IEnumerator SendDataLoop()
    {
        // KEY CHANGE: Wait until the network thread has created the socket.
        while (socket == null)
        {
            yield return null;
        }

        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        var wait = new WaitForSeconds(1f / sendRate);

        while (!m_cancel)
        {
            string message = SerializeTransforms();
            if (!string.IsNullOrEmpty(message))
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                // KEY CHANGE: Use the single, shared socket to send.
                socket.SendTo(data, serverEp);
            }
            yield return wait;
        }
    }

    // --- Network Threads ---
    void ServerProcess()
    {
        // KEY CHANGE: Initialize the class-level socket.
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        Debug.Log("Servidor UDP iniciado en el puerto " + port);

        byte[] buffer = new byte[1024];

        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                int receivedBytes = socket.ReceiveFrom(buffer, ref sender);
                if (receivedBytes > 0)
                {
                    // Add new clients to our list
                    clients.TryAdd(sender, 0);

                    string msg = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                    receivedMessages.Enqueue(msg);

                    // Broadcast the received data to all *other* clients
                    foreach (var client in clients.Keys)
                    {
                        if (!client.Equals(sender))
                        {
                            socket.SendTo(buffer, 0, receivedBytes, SocketFlags.None, client);
                        }
                    }
                }
            }
            catch (SocketException)
            {
                if (!m_cancel) Debug.LogError("Socket exception in server thread.");
                break;
            }
        }
    }

    void ClientProcess()
    {
        // KEY CHANGE: Initialize the class-level socket.
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        // Bind to an ephemeral port to receive data from the server
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        Debug.Log("Cliente UDP listo para enviar y recibir...");

        byte[] buffer = new byte[1024];

        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                int receivedBytes = socket.ReceiveFrom(buffer, ref sender);
                if (receivedBytes > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                    receivedMessages.Enqueue(msg);
                }
            }
            catch (SocketException)
            {
                if (!m_cancel) Debug.LogError("Socket exception in client thread.");
                break;
            }
        }
    }
}