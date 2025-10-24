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
        var ids = new List<int>();
        var floats = new List<float>();

        foreach (var t in registeredTransforms)
        {
            ids.Add(t.networkId);
            floats.Add(t.netwPos.x);
            floats.Add(t.netwPos.y);
            floats.Add(t.netwPos.z);
            floats.Add(t.netwRot.x);
            floats.Add(t.netwRot.y);
            floats.Add(t.netwRot.z);
            floats.Add(t.netwRot.w);
            floats.Add(t.netwScale.x);
            floats.Add(t.netwScale.y);
            floats.Add(t.netwScale.z);
        }

        var data = new object[] { ids, floats };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private void DeserializeAndApplyBinary(byte[] data, int length)
    {
        try
        {
            using (var memoryStream = new MemoryStream(data, 0, length))
            {
                var binaryFormatter = new BinaryFormatter();
                var obj = (object[])binaryFormatter.Deserialize(memoryStream);

                var ids = (List<int>)obj[0];
                var floats = (List<float>)obj[1];

                int idx = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    int networkId = ids[i];
                    float posX = floats[idx++];
                    float posY = floats[idx++];
                    float posZ = floats[idx++];
                    float rotX = floats[idx++];
                    float rotY = floats[idx++];
                    float rotZ = floats[idx++];
                    float rotW = floats[idx++];
                    float scaleX = floats[idx++];
                    float scaleY = floats[idx++];
                    float scaleZ = floats[idx++];

                    var t = registeredTransforms.Find(x => x.networkId == networkId);
                    if (t != null && !t.isLocalPlayer)
                    {
                        t.UpdateTransform(
                            new Vector3(posX, posY, posZ),
                            new Quaternion(rotX, rotY, rotZ, rotW),
                            new Vector3(scaleX, scaleY, scaleZ)
                        );
                    }
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
                DeserializeAndApplyBinary(buffer, receivedBytes);

                byte[] response = SerializeTransformsBinary();
                serverSocket.SendTo(response, sender);
            }
            Thread.Sleep(33);
        }
        serverSocket.Close();
    }

    void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

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