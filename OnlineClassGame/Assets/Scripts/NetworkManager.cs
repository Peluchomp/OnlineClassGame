using NUnit.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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

    struct NetworkTransformData
    {
        public int networkId;

        public float netPositionX;
        public float netPositionY;
        public float netPositionZ;

        public float netRotationX;
        public float netRotationY;
        public float netRotationZ;
        public float netRotationW;

        public float netScaleX;
        public float netScaleY;
        public float netScaleZ;
    }

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

    private byte[] SerializeTransformsBinary()
    {
        List<NetworkTransformData> dataList = new List<NetworkTransformData>();
        foreach (var t in registeredTransforms)
        {
            dataList.Add(new NetworkTransformData
            {
                networkId = t.networkId,
                netPositionX = t.netwPos.x,
                netPositionY = t.netwPos.y,
                netPositionZ = t.netwPos.z,
                netRotationX = t.netwRot.x,
                netRotationY = t.netwRot.y,
                netRotationZ = t.netwRot.z,
                netRotationW = t.netwRot.w,
                netScaleX = t.netwScale.x,
                netScaleY = t.netwScale.y,
                netScaleZ = t.netwScale.z
            });
        }

        MemoryStream memoryStream = new MemoryStream();

        BinaryFormatter binaryFormatter = new BinaryFormatter();
        binaryFormatter.Serialize(memoryStream, dataList);
        return memoryStream.ToArray();
    }

    private void DeserializeAndApplyBinary(byte[] data, int length)
    {
        try
        {
            MemoryStream memoryStream = new MemoryStream(data, 0, length);

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            var dataList = (List<NetworkTransformData>)binaryFormatter.Deserialize(memoryStream);

            foreach (var item in dataList)
            {
                var t = registeredTransforms.Find(x => x.networkId == item.networkId);
                if (t != null && !t.isLocalPlayer)
                {
                    t.UpdateTransform(
                        new Vector3(item.netPositionX, item.netPositionY, item.netPositionZ),
                        new Quaternion(item.netRotationX, item.netRotationY, item.netRotationZ, item.netRotationW),
                        new Vector3(item.netScaleX, item.netScaleY, item.netScaleZ)
                    );
                }
            }

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error deserializando datos: {e.Message}");
        }
    }


    void ServerProcess()
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
        serverSocket.Bind(ipep);

        byte[] buffer = new byte[2048];

        Debug.Log("Servidor UDP (Binario) iniciado en el puerto " + port);

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
                if (buffer[0] < 32 || buffer[0] > 126)
                {
                    DeserializeAndApplyBinary(buffer, receivedBytes);

                    byte[] response = SerializeTransformsBinary();
                    serverSocket.SendTo(response, sender);
                }
                else
                {
                    Debug.Log("Mensaje de texto recibido, ignorado.");
                }
            }
            Thread.Sleep(33);
        }
        serverSocket.Close();
    }

    void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        clientSocket.SendTo(Encoding.UTF8.GetBytes("Hola desde el cliente"), serverEp);

        byte[] buffer = new byte[2048];

        while (!m_cancel)
        {
            byte[] transformsData = SerializeTransformsBinary();
            clientSocket.SendTo(transformsData, serverEp);

            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = 0;
            try
            {
                receivedBytes = clientSocket.ReceiveFrom(buffer, ref sender);
            }
            catch (SocketException) { break; }

            if (receivedBytes > 0)
            {
                DeserializeAndApplyBinary(buffer, receivedBytes);
            }
            Thread.Sleep(33);
        }
        clientSocket.Close();
    }
}