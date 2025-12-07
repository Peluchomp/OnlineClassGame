using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public enum MessageType : byte
    {
        TransformSync = 1,
        SpawnObjectBroadcast = 2,
        SpawnObjectRequest = 3,
        SpawnObjectBroadcastOwned = 4, 
        DestroyObject = 5,
        SceneObjectSync = 6,
        SpawnRopeAttachments = 7
    }
    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;

    public static NetworkManager Instance;

    public int port = 9050;
    public string serverAddress = "127.0.0.1";

    private Thread discoveryThread;
    private const int discoveryPort = 9051;

    // will move it away from networkManager on a later delivery
    public Transform fixedRopeAnchor;

    [HideInInspector]
    public int connectedClientsCount => clientEndpoints.Count;

    public List<GameObject> spawnablePrefabs = new List<GameObject>();

    private List<object[]> pendingSpawnRequests = new List<object[]>();
    private Dictionary<object[],EndPoint> pendingServerSpawnRequests = new Dictionary<object[], EndPoint>();
    private List<int> pendingNetIdsToDestroy = new List<int>();
    private List<object[]> pendingRpcCalls = new List<object[]>();

    Dictionary<int, NetworkIdentity> networkIdentities = new Dictionary<int, NetworkIdentity>();
    Dictionary<string, NetworkIdentity> sceneIdentities = new Dictionary<string, NetworkIdentity>();
    private int nextNetworkId = 1;

    private Socket socket;
    private Thread serverThread;
    private Thread clientThread;
    private volatile bool m_cancel = false;

    private HashSet<EndPoint> clientEndpoints = new HashSet<EndPoint>();

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

    public void RegisterIdentity(NetworkIdentity identity)
    {
        if (identity == null) return;

        if (identity.networkId != 0 && networkIdentities.ContainsKey(identity.networkId))
            return;

        if (!string.IsNullOrEmpty(identity.sceneId))
        {
            if (sceneIdentities.ContainsKey(identity.sceneId))
            {
                Debug.LogError($"SceneId '{identity.sceneId}' is duplicated. SceneIds must be unique.");
                return;
            }
            sceneIdentities[identity.sceneId] = identity;

            if (role == NetworkRole.Server || role == NetworkRole.Host)
            {
                int newId = nextNetworkId++;
                identity.SetNetworkId(newId);             
                networkIdentities[newId] = identity;
            }
        }
        else 
        {
            if (role == NetworkRole.Server || role == NetworkRole.Host)
            {
                int newId = nextNetworkId++;
                identity.SetNetworkId(newId);
                networkIdentities[newId] = identity;
            }
        }
    }

    public void StartServer()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        role = NetworkRole.Server;
        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
        socket.Bind(ipep);
        serverThread = new Thread(ServerProcess);
        serverThread.Start();

        // Iniciar hilo de descubrimiento
        discoveryThread = new Thread(ServerDiscoveryProcess);
        discoveryThread.Start();

        Debug.Log("Servidor UDP (Binario) iniciado en el puerto " + port);
    }

    public void StartClient()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        role = NetworkRole.Client;

        if (role == NetworkRole.Client)
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        }
        clientThread = new Thread(ClientProcess);
        clientThread.Start();
       
    }

    public void StartHost()
    {
        //role = NetworkRole.Host;
        //IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
        //socket.Bind(ipep);
        //serverThread = new Thread(ServerProcess);
        //serverThread.Start();
        //Debug.Log("Servidor UDP (Binario) iniciado en el puerto " + port);
        //clientThread = new Thread(ClientProcess);
        //clientThread.Start();
    }

    private void ServerDiscoveryProcess()
    {
        using (var discoverySocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, discoveryPort);
            discoverySocket.Bind(ipep);

            byte[] buffer = new byte[256];
            while (!m_cancel)
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int received = 0;
                try
                {
                    if (discoverySocket.Available == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    received = discoverySocket.ReceiveFrom(buffer, ref sender);
                }
                catch (SocketException) { break; }
                catch (System.ObjectDisposedException) { break; }

                if (received > 0)
                {
                    string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, received);
                    if (msg == "DISCOVER_SERVER")
                    {
                        string localIp = GetLocalIPAddress();
                        string response = "SERVER_HERE|" + localIp;
                        byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                        discoverySocket.SendTo(responseBytes, sender);
                    }
                }
            }
        }
    }
    public void DiscoverServer(int timeoutMs = 2000)
    {
        using (var discoverySocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            discoverySocket.EnableBroadcast = true;
            discoverySocket.ReceiveTimeout = timeoutMs;

            byte[] discoveryMsg = System.Text.Encoding.UTF8.GetBytes("DISCOVER_SERVER");
            IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            // Enviar broadcast
            discoverySocket.SendTo(discoveryMsg, broadcastEp);

            // Esperar respuesta
            EndPoint serverEp = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                byte[] buffer = new byte[256];
                int received = discoverySocket.ReceiveFrom(buffer, ref serverEp);
                string response = System.Text.Encoding.UTF8.GetString(buffer, 0, received);
                if (response.StartsWith("SERVER_HERE"))
                {
                    string[] parts = response.Split('|');
                    if (parts.Length > 1)
                    {
                        serverAddress = parts[1];
                        Debug.Log("Servidor descubierto en: " + serverAddress);
                    }
                }
            }
            catch (SocketException)
            {
                Debug.LogWarning("No se encontró ningún servidor en la red local.");
            }
        }
    }
    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private void Update()
    {     

        if (pendingSpawnRequests.Count > 0 && role == NetworkRole.Client)
        {
            lock (pendingSpawnRequests)
            {
                foreach (var request in pendingSpawnRequests)
                {
                    ClientHandleSpawnBroadcast(request);
                }
                pendingSpawnRequests.Clear();
            }
        }

        if ((role == NetworkRole.Host || role == NetworkRole.Server) && pendingServerSpawnRequests.Count > 0)
        {
            lock (pendingServerSpawnRequests)
            {
                foreach (var kvp in pendingServerSpawnRequests.ToList())
                {
                    HandleSpawnRequest(kvp.Key, kvp.Value);
                    pendingServerSpawnRequests.Remove(kvp.Key);
                }
            }
        }

        if (pendingNetIdsToDestroy.Count > 0)
        {
            lock (pendingNetIdsToDestroy)
            {
                foreach (var netId in pendingNetIdsToDestroy)
                {
                    HandleDestroyObject(netId);
                }
                pendingNetIdsToDestroy.Clear();
            }
        }

        if (pendingRpcCalls.Count > 0)
        {
            lock (pendingRpcCalls)
            {
                foreach (var rpcData in pendingRpcCalls)
                {
                    HandleRpc(rpcData);
                }
                pendingRpcCalls.Clear();
            }
        }
    }
    void OnDestroy()
    {
        m_cancel = true;

        serverThread?.Abort();
        clientThread?.Abort();
        discoveryThread?.Abort();

        if (socket != null)
        {
            socket.Close();
            socket = null;
        }
    }

    #region SerializeNetworkMessages

    private byte[] SerializeSpawnBroadcast(int prefabId, int networkId, Vector3 pos, Quaternion rot, MessageType messageType)
    {
        object[] data = new object[]
        {
            (byte)messageType,
            prefabId,
            networkId,
            pos.x, pos.y, pos.z,
            rot.x, rot.y, rot.z, rot.w
        };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeTransformsBinary()
    {
        var ids = new List<int>();
        var floats = new List<float>();
        var sceneSyncs = new List<object[]>();


        if (role != NetworkRole.Client)
        {
            foreach (var kvp in sceneIdentities)
            {
                if (kvp.Value.networkId != 0)
                {
                    sceneSyncs.Add(new object[] { kvp.Key, kvp.Value.networkId });
                }
            }
        }

        foreach (var identity in networkIdentities.Values)
        {
            var t = identity.NetworkTransform;
            if (t != null && identity.isLocalPlayer == true && t.sendData == true)
            {
                ids.Add(identity.networkId);
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
        }

        var data = new object[] { (byte)MessageType.TransformSync, sceneSyncs, ids, floats };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeSpawnRequest(int prefabId, Vector3 pos, Quaternion rot)
    {
        object[] data = new object[]
        {
            (byte)MessageType.SpawnObjectRequest,
            prefabId,
            pos.x, pos.y, pos.z,
            rot.x, rot.y, rot.z, rot.w
        };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeDestroyObject(int networkId)
    {
        object[] data = new object[]
        {
        (byte)MessageType.DestroyObject,
        networkId
        };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeDestroyRequest(int networkId)
    {
        object[] data = new object[]
        {
        (byte)MessageType.DestroyObject,
        networkId
        };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeSceneObjectsSync()
    {
        var sceneSyncs = new List<object[]>();
        foreach (var kvp in sceneIdentities)
        {
            if (kvp.Value.networkId != 0)
            {
                sceneSyncs.Add(new object[] { kvp.Key, kvp.Value.networkId });
            }
        }
        object[] data = new object[] { (byte)MessageType.SceneObjectSync, sceneSyncs };
        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, data);
            return memoryStream.ToArray();
        }
    }

    private byte[] SerializeRpc(object[] rpcData)
    {
        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, rpcData);
            return memoryStream.ToArray();
        }
    }

    #endregion

    #region SpawnMethods

    [ContextMenu("Spawn Players For All Connections")]
    public void SpawnPlayersContextMenu()
    {
        int playerPrefabId = 0;
        Vector3 spawnPosition = transform.position + Vector3.up * 2;
        SpawnPlayerForEachConnection(playerPrefabId, spawnPosition);
    }

    public void SpawnPlayerForEachConnection(int playerPrefabId, Vector3 spawnPosition)
    {
        if (role == NetworkRole.Client)
        {
            Debug.LogWarning("SpawnPlayerForEachConnection can only be called on the server or host.");
            return;
        }

        Debug.Log("Spawning player for server/host.");
        ServerSpawnAndBroadcast(playerPrefabId, spawnPosition, Quaternion.identity, null);

        foreach (var clientEndPoint in clientEndpoints)
        {
            Debug.Log($"Spawning player for client {clientEndPoint}.");

            ServerSpawnAndBroadcast(playerPrefabId, spawnPosition, Quaternion.identity, clientEndPoint);
        }
    }

    public NetworkIdentity ServerSpawnAndBroadcast(int prefabId, Vector3 position, Quaternion rotation, EndPoint owner = null)
    {
        if (role == NetworkRole.Client) return null;

        if (prefabId < 0 || prefabId >= spawnablePrefabs.Count)
        {
            Debug.LogError($"Invalid prefabId: {prefabId}");
            return null;
        }

        GameObject prefab = spawnablePrefabs[prefabId];
        GameObject spawnedObject = Instantiate(prefab, position, rotation);
        NetworkIdentity identity = spawnedObject.GetComponent<NetworkIdentity>();

        if (identity == null)
        {
            Debug.LogError("Spawned prefab does not have a NetworkIdentity component.");
            Destroy(spawnedObject);
            return null;
        }

        RegisterIdentity(identity);

        identity.SetIsLocalPlayer(owner == null);

        byte[] spawnMessageRemote = SerializeSpawnBroadcast(prefabId, identity.networkId, position, rotation, MessageType.SpawnObjectBroadcast);
        byte[] spawnMessageOwned = SerializeSpawnBroadcast(prefabId, identity.networkId, position, rotation, MessageType.SpawnObjectBroadcastOwned);

        foreach (var ep in clientEndpoints)
        {
            try
            {
                if (owner != null && ep.Equals(owner))
                {
                    socket.SendTo(spawnMessageOwned, ep);
                }
                else
                {
                    socket.SendTo(spawnMessageRemote, ep);
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Failed to send spawn broadcast to {ep}: {e.Message}");
            }
        }
        Debug.Log($"Spawned and broadcasted object {prefab.name} with NetworkId {identity.networkId}. Owner: {(owner == null ? "Server" : owner.ToString())}");
        return identity;
    }

    private void HandleSpawnRequest(object[] rootData, EndPoint requester)
    {
        int prefabId = (int)rootData[1];
        Vector3 position = new Vector3((float)rootData[2], (float)rootData[3], (float)rootData[4]);
        Quaternion rotation = new Quaternion((float)rootData[5], (float)rootData[6], (float)rootData[7], (float)rootData[8]);

        if (prefabId < 0 || prefabId >= spawnablePrefabs.Count)
        {
            Debug.LogWarning($"Client sent invalid prefabId: {prefabId}");
            return;
        }

        ServerSpawnAndBroadcast(prefabId, position, rotation, requester);
    }

    public void ClientRequestSpawn(int prefabId, Vector3 position, Quaternion rotation)
    {
        if (role == NetworkRole.Server) return;

        byte[] requestMessage = SerializeSpawnRequest(prefabId, position, rotation);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        try
        {
            socket.SendTo(requestMessage, serverEp);
        }
        catch (SocketException e)
        {
            Debug.LogError($"Failed to send spawn request: {e.Message}");
        }
    }

    private void ClientHandleSpawnBroadcast(object[] rootData)
    {
        MessageType messageType = (MessageType)(byte)rootData[0];
        int prefabId = (int)rootData[1];
        int networkId = (int)rootData[2];

        if (networkIdentities.ContainsKey(networkId))
        {
            if (role == NetworkRole.Host && networkIdentities.TryGetValue(networkId, out var id))
            {
                id.SetIsLocalPlayer(messageType == MessageType.SpawnObjectBroadcastOwned);
            }
            return;
        }

        if (prefabId < 0 || prefabId >= spawnablePrefabs.Count)
        {
            Debug.LogError($"Invalid prefabId received: {prefabId}");
            return;
        }

        Vector3 position = new Vector3((float)rootData[3], (float)rootData[4], (float)rootData[5]);
        Quaternion rotation = new Quaternion((float)rootData[6], (float)rootData[7], (float)rootData[8], (float)rootData[9]);

        GameObject prefab = spawnablePrefabs[prefabId];
        GameObject spawnedObject = Instantiate(prefab, position, rotation);
        NetworkIdentity identity = spawnedObject.GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            identity.SetNetworkId(networkId);

            identity.SetIsLocalPlayer(messageType == MessageType.SpawnObjectBroadcastOwned);

            networkIdentities[networkId] = identity;
        }
        else
        {
            Debug.LogError($"Spawned prefab {prefab.name} has no NetworkIdentity.");
            Destroy(spawnedObject);
        }
    }


    #endregion

    private void HandleData(byte[] data, int length, EndPoint sender)
    {
        try
        {
            var memoryStream = new MemoryStream(data, 0, length);
            var binaryFormatter = new BinaryFormatter();
            object[] rootData = (object[])binaryFormatter.Deserialize(memoryStream);

            if (rootData == null || rootData.Length == 0)
                return;

            if (!clientEndpoints.Contains(sender))
            {
                clientEndpoints.Add(sender);
                Debug.Log("Nuevo cliente conectado: " + sender.ToString());

                byte[] sceneSyncMsg = SerializeSceneObjectsSync();
                socket.SendTo(sceneSyncMsg, sender);
            }

            MessageType messageType = (MessageType)(byte)rootData[0];

            switch (messageType)
            {
                case MessageType.TransformSync:
                    if (role != NetworkRole.Client)
                    {
                        ApplyTransformSync(rootData);

                        byte[] response = SerializeTransformsBinary();
                        socket.SendTo(response, sender);
                    }
                    else
                    {
                        ApplyTransformSync(rootData);
                    }
                    break;

                case MessageType.SpawnObjectBroadcast:
                case MessageType.SpawnObjectBroadcastOwned:
                    if (role != NetworkRole.Server)
                    {
                        lock (pendingSpawnRequests)
                        {
                            pendingSpawnRequests.Add(rootData);
                        }
                    }
                    break;

                case MessageType.SpawnObjectRequest:
                    if (role != NetworkRole.Client)
                    {
                        lock (pendingServerSpawnRequests)
                        {
                            pendingServerSpawnRequests[rootData] = sender;
                        }
                    }
                    break;
                case MessageType.DestroyObject:

                    int networkIdToDestroy = (int)rootData[1];
                    if (role == NetworkRole.Server || role == NetworkRole.Host)
                    {
                        BroadcastDestroyObject(networkIdToDestroy);
                    }
                    else if (role == NetworkRole.Client)
                    {
                        pendingNetIdsToDestroy.Add(networkIdToDestroy);
                    }
                    break;
                case MessageType.SceneObjectSync:
                    if (role == NetworkRole.Client)
                    {
                        var sceneSyncs = (List<object[]>)rootData[1];
                        foreach (var syncData in sceneSyncs)
                        {
                            string sceneId = (string)syncData[0];
                            int networkId = (int)syncData[1];
                            NetworkIdentity identity;
                            if (sceneIdentities.TryGetValue(sceneId, out identity))
                            {
                                identity.SetNetworkId(networkId);
                                networkIdentities[networkId] = identity;
                            }
                        }
                    }
                    break;
                case MessageType.SpawnRopeAttachments:
                    lock (pendingRpcCalls)
                    {
                        pendingRpcCalls.Add(rootData);
                    }
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error deserializando datos: {e.Message} from {sender}");
        }
    }

    private void HandleDestroyObject(int networkId)
    {
        if (networkIdentities.TryGetValue(networkId, out var identity))
        {
            networkIdentities.Remove(networkId);
            if (!string.IsNullOrEmpty(identity.sceneId))
                sceneIdentities.Remove(identity.sceneId);

            Destroy(identity.gameObject);
            Debug.Log($"Destroyed {networkId}.");
        }
        else
        {
            Debug.LogWarning($"NetworkIdentity {networkId} not found to destroy.");
        }
    }

    public void ClientRequestDestroy(int networkId)
    {
        if (role != NetworkRole.Client)
        {
            Debug.LogWarning("Clients are the only one who can ask for permission, server can broadcast directly.");
            return;
        }

        byte[] requestMessage = SerializeDestroyRequest(networkId);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        try
        {
            socket.SendTo(requestMessage, serverEp);
        }
        catch (SocketException e)
        {
            Debug.LogError($"Error sending request for destruction: {e.Message}");
        }
    }

    public void BroadcastDestroyObject(int networkId)
    {
        if (role == NetworkRole.Client)
        {
            Debug.LogWarning("Clients cannot broadcast destroy messages.");
            return;
        }
        
        byte[] destroyMessage = SerializeDestroyObject(networkId);

        foreach (var ep in clientEndpoints)
        {
            try
            {
                socket.SendTo(destroyMessage, ep);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Error sending messago to {ep}: {e.Message}");
            }
        }

        pendingNetIdsToDestroy.Add(networkId);
    }

    private void ApplyTransformSync(object[] rootData)
    {
        var sceneSyncs = (List<object[]>)rootData[1];
        var ids = (List<int>)rootData[2];
        var floats = (List<float>)rootData[3];

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

            NetworkIdentity identity;
            if (networkIdentities.TryGetValue(networkId, out identity) && !identity.isLocalPlayer)
            {
                var t = identity.NetworkTransform;
                if (t != null)
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

    void ServerProcess()
    {
        byte[] buffer = new byte[2048];

        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = 0;
            try
            {
                if (socket == null || !socket.IsBound) break;

                if (socket.Available == 0)
                {
                    Thread.Sleep(1); 
                    continue;
                }

                receivedBytes = socket.ReceiveFrom(buffer, ref sender);
            }
            catch (SocketException) { break; }
            catch (System.ObjectDisposedException) { break; }

            if (receivedBytes > 0)
            {
                HandleData(buffer, receivedBytes, sender);
            }
        }
    }
    void ClientProcess()
    {
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        byte[] buffer = new byte[2048];

        while (!m_cancel)
        {
            if (socket == null) break;

            byte[] transformsData = SerializeTransformsBinary();
            try
            {
                socket.SendTo(transformsData, serverEp);
            }
            catch (SocketException) { break; }
            catch (System.ObjectDisposedException) { break; }

            if (role == NetworkRole.Client)
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int receivedBytes = 0;
                try
                {
                    if (socket.Available > 0)
                        receivedBytes = socket.ReceiveFrom(buffer, ref sender);
                }
                catch (SocketException) { break; }
                catch (System.ObjectDisposedException) { break; }

                if (receivedBytes > 0)
                {
                    HandleData(buffer, receivedBytes, sender);
                }
            }

            Thread.Sleep(33);
        }
    }

    public IEnumerator WaitAndSpawnRopes()
    {
        yield return new WaitForSeconds(1f);
        SpawnRopesForEachPlayer();
    }


    #region RopeAttachment
    [ContextMenu("Spawn Ropes For All Players")]
    public void SpawnRopesForEachPlayer()
    {
        if (role == NetworkRole.Client) return;
        if (fixedRopeAnchor == null)
        {
            Debug.LogError("FixedRopeAnchor is not set in NetworkManager.");
            return;
        }
        Debug.Log("Spawning ropes for all players.");
        var anchorIdentity = fixedRopeAnchor.GetComponent<NetworkIdentity>();
        if (anchorIdentity == null || anchorIdentity.networkId == 0)
        {
            Debug.LogError("FixedRopeAnchor must have a NetworkIdentity with a valid networkId.");
            return;
        }

        var players = networkIdentities.Values.Where(id => id.CompareTag("Player")).ToList();

        foreach (var player in players)
        {
            NetworkIdentity ropeIdentity = ServerSpawnAndBroadcast(1, transform.position, Quaternion.identity);
            if (ropeIdentity != null)
            {
                ClientRpcAttachRope(ropeIdentity.networkId, anchorIdentity.networkId, player.networkId);
            }
        }
    }

    private void ClientRpcAttachRope(int ropeNetId, int anchorNetId, int playerNetId)
    {
        object[] rpcData = new object[]
        {
            (byte)MessageType.SpawnRopeAttachments,
            ropeNetId,
            anchorNetId,
            playerNetId
        };

        byte[] rpcMessage = SerializeRpc(rpcData);

        foreach (var ep in clientEndpoints)
        {
            try
            {
                socket.SendTo(rpcMessage, ep);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Error sending RPC to {ep}: {e.Message}");
            }
        }

        if (role != NetworkRole.Client)
        {
            pendingRpcCalls.Add(rpcData);
        }
    }

    private void HandleRpc(object[] rpcData)
    {
        MessageType messageType = (MessageType)(byte)rpcData[0];
        if (messageType == MessageType.SpawnRopeAttachments)
        {
            int ropeNetId = (int)rpcData[1];
            int anchorNetId = (int)rpcData[2];
            int playerNetId = (int)rpcData[3];

            if (networkIdentities.TryGetValue(ropeNetId, out var ropeIdentity) &&
                networkIdentities.TryGetValue(anchorNetId, out var anchorIdentity) &&
                networkIdentities.TryGetValue(playerNetId, out var playerIdentity))
            {

                RopeAttach ropeAttach = ropeIdentity.GetComponentInChildren<RopeAttach>();
                if (ropeAttach != null)
                {
                    StartCoroutine(ropeAttach.AttachAndSnap(anchorIdentity.transform, 0, true));
                    StartCoroutine(ropeAttach.AttachAndSnap(playerIdentity.transform, 1, false));
                }
            }
        }
    }
    #endregion

}