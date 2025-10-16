using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;

    public int port = 9050;
    public string serverAddress = "127.0.0.1";

    private Socket socket;
    private Thread serverThread;
    private Thread clientThread;
    private volatile bool m_cancel = false;

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
                Debug.Log($"[Servidor] Recibido de {sender}: {msg}");
                byte[] response = Encoding.UTF8.GetBytes("Hola desde el servidor");
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
                Debug.Log($"[Cliente] Recibido de {sender}: {msg}");
                clientSocket.SendTo(Encoding.UTF8.GetBytes("Hola desde el cliente"), sender);
            }
            Thread.Sleep(10);
        }
        clientSocket.Close();
    }
}
