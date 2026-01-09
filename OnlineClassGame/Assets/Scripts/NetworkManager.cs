using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using static MineralState;

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
        SpawnRopeAttachments = 7,
        GrabObjectRequest = 8, 
        GrabObjectUpdate = 9,
        ReleaseObjectRequest = 10, 
        ReleaseObjectBroadcast = 11,
        Ack = 12,
        TimeSyncRequest = 13,
        TimeSyncResponse = 14,
        NewOrder = 15,
        MineObjectRequest = 16,
        MineObjectBroadcast = 17,
        RestoreMineralsBroadcast = 18,
        NewCustomerBroadcast = 19,
        SyncIntegerValue = 20,
        TimerSync = 21
    }
    public enum NetworkRole { Server, Client, Host }
    public NetworkRole role = NetworkRole.Host;

    public static NetworkManager Instance;

    public int port = 9050;
    public string serverAddress = "127.0.0.1";

    private Thread discoveryThread;
    private const int discoveryPort = 9051;

    private double clientTimeOffset = 0.0;

    // will move it away from networkManager on a later delivery
    public Transform fixedRopeAnchor;

    public List<GameObject> spawnablePrefabs = new List<GameObject>();

    private List<object[]> pendingSpawnRequests = new List<object[]>();
    private Dictionary<object[],EndPoint> pendingServerSpawnRequests = new Dictionary<object[], EndPoint>();
    private List<int> pendingNetIdsToDestroy = new List<int>();
    private List<object[]> pendingRpcCalls = new List<object[]>();
    private List<object[]> pendingGrabUpdates = new List<object[]>();
    private List<(int objectNetId, EndPoint requester)> pendingServerGrabRequests = new List<(int, EndPoint)>();
    private List<int> pendingReleaseNetIds = new List<int>();
    private List<(int objectNetId, EndPoint requester, Vector3 velocity)> pendingServerReleaseRequests = new List<(int, EndPoint, Vector3)>();
    private List<object[]> pendingOrders = new List<object[]>();
    private List<int> pendingMineRequests = new List<int>();
    private List<int> pendingMineBroadcasts = new List<int>();
    private List<bool> pendingNewCustomers = new List<bool>();
    private List<int> pendingIntValues = new List<int>();
    private bool pendingRestoreMinerals = false;

    Dictionary<int, NetworkIdentity> networkIdentities = new Dictionary<int, NetworkIdentity>();
    Dictionary<string, NetworkIdentity> sceneIdentities = new Dictionary<string, NetworkIdentity>();
    private int nextNetworkId = 1;

    private Dictionary<int, EndPoint> serverObjectOwnership = new Dictionary<int, EndPoint>();

    private int localSequenceId = 0;
    private float resendTimeout = 1.0f;
    private List<PendingPacket> pendingAckPackets = new List<PendingPacket>();

    private Dictionary<System.Net.EndPoint, int> latestSequenceReceived = new Dictionary<System.Net.EndPoint, int>();

    private Socket socket;
    private Thread serverThread;
    private Thread clientThread;
    private volatile bool m_cancel = false;
    private object sequenceLock = new object();

    private Dictionary<EndPoint, ClientConnection> clientConnections = new Dictionary<EndPoint, ClientConnection>();
    private int nextConnectionId = 1;

    [HideInInspector]
    public int connectedClientsCount => clientConnections.Count;

    public event Action OnServerStarted;
    public event Action OnClientStarted;
    public event Action<int> OnIntValueReceived;

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
        //Debug.Log("Registering NetworkIdentity: " + identity.gameObject.name);
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
                networkIdentities[newId].isLocalPlayer = true;
            }

            if (role == NetworkRole.Client)
            {
                identity.isLocalPlayer = false;
            }
        }
        else 
        {
            if (role == NetworkRole.Server || role == NetworkRole.Host)
            {
                int newId = nextNetworkId++;
                identity.SetNetworkId(newId);
                networkIdentities[newId] = identity;
                networkIdentities[newId].isLocalPlayer = true;
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

        discoveryThread = new Thread(ServerDiscoveryProcess);
        discoveryThread.Start();

        Debug.Log("Servidor UDP (Binario) iniciado en el puerto " + port);

        OnServerStarted?.Invoke();
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

        OnClientStarted?.Invoke();

        StartCoroutine(SyncClockLoop());
    }

    private IEnumerator SyncClockLoop()
    {
        while (!m_cancel)
        {
            if (socket != null)
            {
                SendTimeSyncRequest();
            }

            yield return new WaitForSeconds(5.0f);
        }
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

            discoverySocket.SendTo(discoveryMsg, broadcastEp);

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
                Debug.LogWarning("No se encontr� ning�n servidor en la red local.");
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

        if ((role == NetworkRole.Host || role == NetworkRole.Server) && pendingServerGrabRequests.Count > 0)
        {
            lock (pendingServerGrabRequests)
            {
                foreach (var (objectNetId, requester) in pendingServerGrabRequests)
                {
                    HandleServerGrabRequest(objectNetId, requester);
                }
                pendingServerGrabRequests.Clear();
            }
        }

        if (pendingNetIdsToDestroy.Count > 0)
        {
            Debug.Log("Pending destroys: " + pendingNetIdsToDestroy.Count);
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

        if (pendingGrabUpdates.Count > 0)
        {
            lock (pendingGrabUpdates)
            {
                foreach (var grabData in pendingGrabUpdates)
                {
                    ProcessGrabUpdate(grabData);
                }
                pendingGrabUpdates.Clear();
            }
        }

        if (pendingReleaseNetIds.Count > 0)
        {
            lock (pendingReleaseNetIds)
            {
                foreach (var netId in pendingReleaseNetIds)
                {
                    HandleClientReleaseBroadcast(netId);
                }
                pendingReleaseNetIds.Clear();
            }
        }

        if ((role == NetworkRole.Host || role == NetworkRole.Server) && pendingServerReleaseRequests.Count > 0)
        {
            lock (pendingServerReleaseRequests)
            {
                foreach (var (objectNetId, requester, velocity) in pendingServerReleaseRequests)
                {
                    HandleServerReleaseRequest(objectNetId, requester, velocity);
                }
                pendingServerReleaseRequests.Clear();
            }
        }

        if ((role == NetworkRole.Host || role == NetworkRole.Server) && pendingMineRequests.Count > 0)
        {
            lock (pendingMineRequests)
            {
                foreach (var netId in pendingMineRequests)
                {
                    PerformMineLogic(netId);
                }
                pendingMineRequests.Clear();
            }
        }

        if (pendingMineBroadcasts.Count > 0)
        {
            lock (pendingMineBroadcasts)
            {
                foreach (var netId in pendingMineBroadcasts)
                {
                    DisableMineralLocally(netId);
                }
                pendingMineBroadcasts.Clear();
            }
        }

        if (pendingNewCustomers.Count > 0)
        {
            lock (pendingNewCustomers)
            {
                foreach (var pendingCustomer in pendingNewCustomers)
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.NewCustomer();
                    }
                }
                pendingNewCustomers.Clear();
            }
        }

        if (pendingIntValues.Count > 0)
        {
            lock (pendingIntValues)
            {
                foreach (var value in pendingIntValues)
                {
                    OnIntValueReceived?.Invoke(value);
                }
                pendingIntValues.Clear();
            }
        }

        if (pendingRestoreMinerals)
        {
            if (MineralManager.Instance != null)
            {
                MineralManager.Instance.RestoreMinerals();
            }
            pendingRestoreMinerals = false;
        }

        lock (pendingAckPackets)
        {
            for (int i = pendingAckPackets.Count - 1; i >= 0; i--)
            {
                var packet = pendingAckPackets[i];

                if ((float)NetTimer.GetTime() - packet.sendTime > resendTimeout)
                {
                    if (packet.retryCount > 5)
                    {
                        Debug.LogWarning($"Packet {packet.sequenceId} dropped after 5 retries.");
                        pendingAckPackets.RemoveAt(i);
                        continue;
                    }

                    try
                    {
                        socket.SendTo(packet.serializedData, packet.target);
                        Debug.Log($"Resending packet {packet.sequenceId} to {packet.target}");
                    }
                    catch { }

                    packet.sendTime = (float)NetTimer.GetTime();
                    packet.retryCount++;
                }
            }
        }

        if (pendingOrders.Count > 0)
        {
            lock (pendingOrders)
            {
                foreach (var orderData in pendingOrders)
                {
                    HandleOrderBroadcast(orderData);
                }
                pendingOrders.Clear();
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

    private object[] GetTimerSyncData(double startTime, float duration)
    {
        return new object[]
        {
            (byte)MessageType.TimerSync,
            startTime,
            duration
        };
    }

    private object[] GetNewCustomerBroadcastData()
    {
        return new object[]
        {
            (byte)MessageType.NewCustomerBroadcast
        };
    }

    private object[] GetSyncIntData(int value)
    {
        return new object[]
        {
            (byte)MessageType.SyncIntegerValue,
            value
        };
    }

    private object[] GetSpawnBroadcastData(int prefabId, int networkId, Vector3 pos, Quaternion rot, MessageType messageType)
    {
        return new object[]
        {
            (byte)messageType,
            prefabId,
            networkId,
            pos.x, pos.y, pos.z,
            rot.x, rot.y, rot.z, rot.w
        };
    }

    private object[] GetTransformsData()
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

        foreach (int id in ids)
        {
            Debug.Log("Syncing transform for NetworkId: " + id);
        }

        if (sceneSyncs.Count == 0 && ids.Count == 0)
        {
            return null;
        }

        return new object[] { (byte)MessageType.TransformSync, sceneSyncs, ids, floats };
    }

    private object[] GetSpawnRequestData(int prefabId, Vector3 pos, Quaternion rot)
    {
        return new object[]
        {
            (byte)MessageType.SpawnObjectRequest,
            prefabId,
            pos.x, pos.y, pos.z,
            rot.x, rot.y, rot.z, rot.w
        };
    }

    private object[] GetDestroyObjectData(int networkId)
    {
        return new object[]
        {
        (byte)MessageType.DestroyObject,
        networkId
        };
    }

    private object[] GetDestroyRequestData(int networkId)
    {
        return new object[]
        {
        (byte)MessageType.DestroyObject,
        networkId
        };
    }

    private object[] GetSceneObjectsSyncData()
    {
        var sceneSyncs = new List<object[]>();
        foreach (var kvp in sceneIdentities)
        {
            if (kvp.Value.networkId != 0)
            {
                sceneSyncs.Add(new object[] { kvp.Key, kvp.Value.networkId });
            }
        }
        return new object[] { (byte)MessageType.SceneObjectSync, sceneSyncs };
    }

    private object[] GetGrabRequestData(int objectNetId)
    {
        return new object[]
        {
            (byte)MessageType.GrabObjectRequest,
            objectNetId
        };
    }

    private object[] GetGrabUpdateData(int objectNetId, int newOwnerId, bool isNowOwner)
    {
        return new object[]
        {
            (byte)MessageType.GrabObjectUpdate,
            objectNetId,
            newOwnerId,
            isNowOwner 
        };
    }

    private object[] GetReleaseRequestData(int objectNetId, Vector3 velocity)
    {
        return new object[]
        {
        (byte)MessageType.ReleaseObjectRequest,
        objectNetId,
        velocity.x, velocity.y, velocity.z
        };
    }

    private object[] GetReleaseBroadcastData(int objectNetId)
    {
        return new object[]
        {
            (byte)MessageType.ReleaseObjectBroadcast,
            objectNetId
        };
    }

    private object[] GetOrderBroadcastData(int orderId, int min1, int amt1, int min2, int amt2)
    {
        return new object[]
        {
        (byte)MessageType.NewOrder,
        orderId,
        min1,
        amt1,
        min2,
        amt2
        };
    }

    public void ServerBroadcastOrder(int orderId, int min1, int amt1, int min2, int amt2)
    {
        if (role == NetworkRole.Client) return;

        object[] packetData = GetOrderBroadcastData(orderId, min1, amt1, min2, amt2);

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(packetData, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to send order to {clientProxy.EndPoint}: {e.Message}");
            }
        }

            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.ReceiveOrder(orderId, (MineralType)min1, amt1, (MineralType)min2, amt2);
            }
    }

    private void HandleOrderBroadcast(object[] rootData)
    {
        if (OrderManager.Instance == null) return;

        int orderId = (int)rootData[1];
        int m1 = (int)rootData[2];
        int a1 = (int)rootData[3];
        int m2 = (int)rootData[4];
        int a2 = (int)rootData[5];

        OrderManager.Instance.ReceiveOrder(orderId, (MineralType)m1, a1, (MineralType)m2, a2);
    }

    private object[] GetMineRequestData(int networkId)
    {
        return new object[]
        {
            (byte)MessageType.MineObjectRequest,
            networkId
        };
    }

    private object[] GetMineBroadcastData(int networkId)
    {
        return new object[]
        {
            (byte)MessageType.MineObjectBroadcast,
            networkId
        };
    }

    private object[] GetRestoreMineralsData()
    {
        return new object[]
        {
            (byte)MessageType.RestoreMineralsBroadcast
        };
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

        foreach (var clientProxy in clientConnections.Values)
        {
            if (clientProxy.PlayerIdentity != null) continue;

            Debug.Log($"Spawning player for client {clientProxy.ConnectionId}.");

            NetworkIdentity newPlayer = ServerSpawnAndBroadcast(playerPrefabId, spawnPosition, Quaternion.identity, clientProxy.EndPoint);

            clientProxy.PlayerIdentity = newPlayer;
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

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                if (owner != null && clientProxy.EndPoint.Equals(owner))
                {
                    object[] rootData = GetSpawnBroadcastData(prefabId, identity.networkId, position, rotation, MessageType.SpawnObjectBroadcastOwned);
                    SendNetworkMessage(rootData, clientProxy.EndPoint, true);
                }
                else
                {
                    object[] rootData = GetSpawnBroadcastData(prefabId, identity.networkId, position, rotation, MessageType.SpawnObjectBroadcast);
                    SendNetworkMessage(rootData, clientProxy.EndPoint, true);
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Failed to send spawn broadcast to {clientProxy.EndPoint}: {e.Message}");
            }
        }
        //Debug.Log($"Spawned and broadcasted object {prefab.name} with NetworkId {identity.networkId}. Owner: {(owner == null ? "Server" : owner.ToString())}");
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

        object[] requestMessage = GetSpawnRequestData(prefabId, position, rotation);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        SendNetworkMessage(requestMessage, serverEp, true);
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

    public void ServerBroadcastTimer(float duration)
    {
        if (role == NetworkRole.Client) return;

        double startTime = GetNetworkTime();

        object[] data = GetTimerSyncData(startTime, duration);

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(data, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to broadcast Timer: {e.Message}");
            }
        }

        if (OrderManager.Instance != null)
        {
            OrderManager.Instance.StartTimer(startTime, duration);
        }
    }

    public void ServerBroadcastNewCustomer()
    {
        if (role == NetworkRole.Client) return;

        object[] data = GetNewCustomerBroadcastData();

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(data, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to broadcast New Customer: {e.Message}");
            }
        }
    }

    public void ServerBroadcastIntegerValue(int value)
    {
        if (role == NetworkRole.Client) return;

        object[] data = GetSyncIntData(value);

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                Debug.Log($"Broadcasting Int Value: {value} to {clientProxy.EndPoint}");
                SendNetworkMessage(data, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to broadcast Int Value: {e.Message}");
            }
        }

        OnIntValueReceived?.Invoke(value);
    }

    private void HandleData(byte[] data, int length, EndPoint sender)
    {
        try
        {
            object[] wrapper = DeserializePacketWrapper(data, length);

            int sequenceId = (int)wrapper[0];
            float serverTime = (float)wrapper[1];
            bool isReliable = (bool)wrapper[2];
            object[] payload = (object[])wrapper[3];

            if (isReliable)
            {
                SendAck(sequenceId, sender);
            }

            MessageType msgType = (MessageType)(byte)payload[0];

            //Debug.Log($"Data received from {sender}, length: {length} bytes, message Type: {msgType}");
            if (msgType == MessageType.Ack)
            {
                int ackSeqId = (int)payload[1];
                lock (pendingAckPackets)
                {
                    Debug.Log($"Received ACK for packet {ackSeqId} from {sender}");

                    pendingAckPackets.RemoveAll(p => p.sequenceId == ackSeqId);
                }
                return; 
            }

            if (!latestSequenceReceived.ContainsKey(sender))
            {
                latestSequenceReceived[sender] = 0;
            }

            if (sequenceId <= latestSequenceReceived[sender])
            {

            }
            latestSequenceReceived[sender] = sequenceId;

            if (!clientConnections.ContainsKey(sender))
            {
                ClientConnection newClient = new ClientConnection(nextConnectionId++, sender, (float)NetTimer.GetTime());

                clientConnections.Add(sender, newClient);

                Debug.Log($"New Client Connected: ID {newClient.ConnectionId} [{sender}]");

                object[] sceneSyncMsg = GetSceneObjectsSyncData();
                SendNetworkMessage(sceneSyncMsg, sender, true);
            }
            else
            {
                clientConnections[sender].LastMessageTime = (float)NetTimer.GetTime();
            }

            ProcessGameData(payload, sender);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error deserializando datos: {e.Message} from {sender}");
        }
    }

    public double GetNetworkTime()
    {
        return NetTimer.GetTime() + clientTimeOffset;
    }

    private void ProcessGameData(object[] rootData, EndPoint sender)
    {
        MessageType messageType = (MessageType)(byte)rootData[0];

        switch (messageType)
        {
            case MessageType.TransformSync:
               ApplyTransformSync(rootData);
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
                    bool objectExists = networkIdentities.ContainsKey(networkIdToDestroy);

                    bool alreadyPending = false;
                    lock (pendingNetIdsToDestroy)
                    {
                        alreadyPending = pendingNetIdsToDestroy.Contains(networkIdToDestroy);
                    }

                    if (objectExists && !alreadyPending)
                    {
                        BroadcastDestroyObject(networkIdToDestroy);
                    }
                    else
                    {
                        Debug.Log($"Ignored duplicate destroy request for {networkIdToDestroy}");
                    }
                }
                else if (role == NetworkRole.Client)
                {
                    lock (pendingNetIdsToDestroy)
                    {
                        if (!pendingNetIdsToDestroy.Contains(networkIdToDestroy))
                        {
                            pendingNetIdsToDestroy.Add(networkIdToDestroy);
                        }
                    }
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
            case MessageType.GrabObjectRequest:
                if (role == NetworkRole.Server || role == NetworkRole.Host)
                {
                    int objNetId = (int)rootData[1];
                    lock (pendingServerGrabRequests)
                    {
                        pendingServerGrabRequests.Add((objNetId, sender));
                    }
                }
                break;

            case MessageType.GrabObjectUpdate:
                if (role == NetworkRole.Client || role == NetworkRole.Host || role == NetworkRole.Server)
                {
                    lock (pendingGrabUpdates)
                    {
                        pendingGrabUpdates.Add(rootData);
                    }
                }
                break;
            case MessageType.ReleaseObjectRequest:
                if (role == NetworkRole.Server || role == NetworkRole.Host)
                {
                    int objNetId = (int)rootData[1];

                    Vector3 throwVel = new Vector3((float)rootData[2], (float)rootData[3], (float)rootData[4]);

                    lock (pendingServerReleaseRequests)
                    {
                        pendingServerReleaseRequests.Add((objNetId, sender, throwVel));
                    }
                }
                break;
            case MessageType.ReleaseObjectBroadcast:
                if (role == NetworkRole.Client || role == NetworkRole.Host || role == NetworkRole.Server)
                {
                    int objNetId = (int)rootData[1];
                    lock (pendingReleaseNetIds)
                    {
                        pendingReleaseNetIds.Add(objNetId);
                    }
                }
                break;
            case MessageType.TimeSyncRequest:
                if (role == NetworkRole.Server || role == NetworkRole.Host)
                {
                    HandleTimeSyncRequest(rootData, sender);
                }
                break;

            case MessageType.TimeSyncResponse:
                if (role == NetworkRole.Client)
                {
                    HandleTimeSyncResponse(rootData);
                }
                break;
            case MessageType.SpawnRopeAttachments:
                lock (pendingRpcCalls)
                {
                    pendingRpcCalls.Add(rootData);
                }
                break;
            case MessageType.NewOrder:
                lock (pendingOrders)
                {
                    pendingOrders.Add(rootData);
                }
                break;
            case MessageType.MineObjectRequest:
                if (role == NetworkRole.Server || role == NetworkRole.Host)
                {
                    int netId = (int)rootData[1];
                    lock (pendingMineRequests)
                    {
                        pendingMineRequests.Add(netId);
                    }
                }
                break;

            case MessageType.MineObjectBroadcast:
                if (role == NetworkRole.Client)
                {
                    int netId = (int)rootData[1];
                    lock (pendingMineBroadcasts)
                    {
                        pendingMineBroadcasts.Add(netId);
                    }
                }
                break;

            case MessageType.RestoreMineralsBroadcast:
                pendingRestoreMinerals = true;
                break;
            case MessageType.NewCustomerBroadcast:
                lock (pendingNewCustomers)
                {
                    pendingNewCustomers.Add(true);
                }
                break;
            case MessageType.SyncIntegerValue:
                int receivedValue = (int)rootData[1];
                lock (pendingIntValues)
                {
                    pendingIntValues.Add(receivedValue);
                }
                break;
            case MessageType.TimerSync:
                double startTime = (double)rootData[1];
                float duration = (float)rootData[2];
                if (OrderManager.Instance != null)
                {
                    OrderManager.Instance.StartTimer(startTime, duration);
                }
                break;
        }
    }

    #region Mining Logic

    public void ClientRequestMine(int networkId)
    {
        if (role == NetworkRole.Server || role == NetworkRole.Host)
        {
            ServerHandleMine(networkId);
            return;
        }

        object[] data = GetMineRequestData(networkId);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        SendNetworkMessage(data, serverEp, true);
    }

    public void ServerHandleMine(int networkId)
    {
        lock (pendingMineRequests)
        {
            pendingMineRequests.Add(networkId);
        }
    }

    private void PerformMineLogic(int networkId)
    {
        Debug.Log($"Performing mine logic for mineral {networkId}.");
        if (networkIdentities.TryGetValue(networkId, out NetworkIdentity identity))
        {
            Debug.Log($"Found mineral identity for {networkId}, proceeding to mine.");
            MineralState mineralState = identity.GetComponent<MineralState>();
            if (mineralState != null)
            {
                mineralState.Mine();
            }

            BroadcastMineDisable(networkId);

            DisableMineralLocally(networkId);
        }
    }

    private void BroadcastMineDisable(int networkId)
    {
        object[] data = GetMineBroadcastData(networkId);

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(data, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to broadcast mine disable: {e.Message}");
            }
        }
    }

    private void DisableMineralLocally(int networkId)
    {
        if (networkIdentities.TryGetValue(networkId, out NetworkIdentity identity))
        {
            identity.gameObject.SetActive(false);
            Debug.Log($"Mineral {networkId} mined and disabled.");
        }
    }

    #endregion

    #region Restore Minerals Logic

    [ContextMenu("Restore All Minerals")]
    public void ServerBroadcastRestoreMinerals()
    {
        if (role == NetworkRole.Client) return;

        if (MineralManager.Instance != null)
        {
            Debug.Log("Restoring all minerals on server.");
            MineralManager.Instance.RestoreMinerals();
        }

        object[] data = GetRestoreMineralsData();

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(data, clientProxy.EndPoint, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to broadcast restore minerals: {e.Message}");
            }
        }
    }

    #endregion

    #region AcknowledgmentsLogic

    private byte[] SerializePacket(object[] originalData, bool isReliable, int seqId)
    {
        double networkTime = NetTimer.GetTime() + clientTimeOffset;

        object[] packetWrapper = new object[]
        {
        seqId,
        (float)NetTimer.GetTime(),
        isReliable,
        originalData
        };

        using (var memoryStream = new MemoryStream())
        {
            var binaryFormatter = new BinaryFormatter();
            binaryFormatter.Serialize(memoryStream, packetWrapper);
            return memoryStream.ToArray();
        }
    }

    private object[] DeserializePacketWrapper(byte[] data, int length)
    {
        using (var memoryStream = new MemoryStream(data, 0, length))
        {
            var binaryFormatter = new BinaryFormatter();
            return (object[])binaryFormatter.Deserialize(memoryStream);
        }
    }
    private void SendAck(int sequenceId, EndPoint target)
    {
        object[] ackPayload = new object[] { (byte)MessageType.Ack, sequenceId };

        SendNetworkMessage(ackPayload, target, false);
    }

    #endregion  

    public void SendNetworkMessage(object[] data, System.Net.EndPoint target, bool isReliable)
    {
        int seqId;
        lock (sequenceLock)
        {
            localSequenceId++;
            seqId = localSequenceId;
        }

        byte[] packetBytes = SerializePacket(data, isReliable, seqId);

        try
        {
            socket.SendTo(packetBytes, target);

            if (isReliable)
            {
                lock (pendingAckPackets)
                {
                    pendingAckPackets.Add(new PendingPacket(seqId, (float)NetTimer.GetTime(), packetBytes, target));
                }
            }
        }
        catch (SocketException e)
        {
            Debug.LogWarning($"Send Error: {e.Message}");
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

        object[] requestMessage = GetDestroyRequestData(networkId);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        SendNetworkMessage(requestMessage, serverEp, true);
    }

    public void SendTimeSyncRequest()
    {
        object[] data = new object[]
        {
        (byte)MessageType.TimeSyncRequest,
        NetTimer.GetTime()
        };

        SendNetworkMessage(data, new IPEndPoint(IPAddress.Parse(serverAddress), port), false);
    }

    private void HandleTimeSyncRequest(object[] payload, EndPoint sender)
    {
        double clientSentTime = (double)payload[1];

        object[] response = new object[]
        {
        (byte)MessageType.TimeSyncResponse,
        clientSentTime,
        NetTimer.GetTime()
        };

        SendNetworkMessage(response, sender, false);
    }

    private void HandleTimeSyncResponse(object[] payload)
    {
        double clientSentTime = (double)payload[1];
        double serverTimeStep = (double)payload[2];
        double now = NetTimer.GetTime();

        double rtt = now - clientSentTime;

        double latency = rtt / 2.0;

        double expectedServerTime = serverTimeStep + latency;

        clientTimeOffset = expectedServerTime - now;

        Debug.Log($"[Clock Sync] RTT: {rtt * 1000:0}ms | Offset: {clientTimeOffset:0.00}s");
    }

    public void BroadcastDestroyObject(int networkId)
    {
        if (role == NetworkRole.Client)
        {
            Debug.LogWarning("Clients cannot broadcast destroy messages.");
            return;
        }
        
        object[] destroyMessage = GetDestroyObjectData(networkId);

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(destroyMessage, clientProxy.EndPoint, true);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Error sending messago to {clientProxy.EndPoint}: {e.Message}");
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
        byte[] buffer = new byte[65536];

        while (!m_cancel)
        {
            if (socket != null && socket.IsBound)
            {
                object[] transformsData = GetTransformsData();

                if (transformsData != null && clientConnections.Count > 0)
                {
                    foreach (var client in clientConnections.Values)
                    {
                        try
                        {
                            SendNetworkMessage(transformsData, client.EndPoint, false);
                        }
                        catch (SocketException) { }
                    }
                }
            }

            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = 0;
            try
            {
                if (socket.Available == 0)
                {
                    Thread.Sleep(3);
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
        byte[] buffer = new byte[65536];

        while (!m_cancel)
        {
            if (socket == null) break;

            object[] transformsData = GetTransformsData();
            try
            {
                if (transformsData != null)
                    SendNetworkMessage(transformsData, serverEp, false);
            }
            catch (SocketException) { break; }
            catch (System.ObjectDisposedException) { break; }

            if (socket.Available == 0)
            {
                Thread.Sleep(33);
                continue;
            }

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
                    Debug.Log($"Server received {receivedBytes} bytes from {sender}. Buffer is this large:{buffer}");

                    HandleData(buffer, receivedBytes, sender);
                }
            }
        }
    }

    public IEnumerator WaitAndSpawnRopes()
    {
        yield return new WaitForSeconds(1f);
        SpawnRopesForEachPlayer();
    }
   
    #region GrabLogic

    public void ClientRequestGrab(int objectNetworkId)
    {
        if (networkIdentities.TryGetValue(objectNetworkId, out NetworkIdentity identity))
        {
            identity.SetIsLocalPlayer(true);
            var grabState = identity.GetComponent<GrabState>();
            if (grabState != null) grabState.OnGrabStateUpdated(true, -1);
        }

        object[] requestMsg = GetGrabRequestData(objectNetworkId);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);
        SendNetworkMessage(requestMsg, serverEp, true);
    }

    private void HandleServerGrabRequest(int objectNetId, EndPoint requester)
    {
        bool isServerRequest = false;
        if (requester is IPEndPoint requesterIp)
        {
            isServerRequest = (requesterIp.Port == this.port);
        }

        if (serverObjectOwnership.ContainsKey(objectNetId))
        {
            EndPoint currentOwner = serverObjectOwnership[objectNetId];

            if (!currentOwner.Equals(requester))
            {
                //if (isServerRequest)
                //{
                //    Debug.Log($"Server is stealing object {objectNetId} from {currentOwner}");

                //    object[] forceDropMsg = GetGrabUpdateData(objectNetId, -1, false);
                //    SendNetworkMessage(forceDropMsg, currentOwner, true);
                //}
                //else
               // {
                    Debug.Log($"Grab denied. Object {objectNetId} is held by {currentOwner}.");

                    object[] denyMsg = GetGrabUpdateData(objectNetId, -1, false);
                    SendNetworkMessage(denyMsg, requester, true);
                    return;
              //  }
            }
        }

        serverObjectOwnership[objectNetId] = requester;

        if (networkIdentities.TryGetValue(objectNetId, out NetworkIdentity identity))
        {
            identity.SetIsLocalPlayer(isServerRequest);

            Rigidbody rb = identity.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = !isServerRequest;
            }
        }

        foreach (var clientProxy in clientConnections.Values)
        {
            bool isNowOwner = clientProxy.EndPoint.Equals(requester);
        
            object[] msg = GetGrabUpdateData(objectNetId, -1, isNowOwner);

            try { SendNetworkMessage(msg, clientProxy.EndPoint, true); } catch { }
        }
    }

    private void ProcessGrabUpdate(object[] rootData)
    {
        int objectNetId = (int)rootData[1];
        int newOwnerPlayerId = (int)rootData[2];
        bool amIOwner = (bool)rootData[3];

        if (networkIdentities.TryGetValue(objectNetId, out NetworkIdentity identity))
        {
            identity.SetIsLocalPlayer(amIOwner);

            var grabState = identity.GetComponent<GrabState>();
            if (grabState != null)
            {
                grabState.OnGrabStateUpdated(amIOwner, newOwnerPlayerId);
            }

            if (!amIOwner)
            {
                Rigidbody rb = identity.GetComponent<Rigidbody>();
                Collider col = identity.GetComponent<Collider>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                if (col != null)
                {
                    col.enabled = true;
                }

                if (identity.transform.parent != null)
                {
                    identity.transform.SetParent(null);
                    DontDestroyOnLoad(identity.gameObject);
                }
            }
        }
    }
    public void ClientRequestRelease(int objectNetworkId, Vector3 throwVelocity)
    {
        object[] requestMsg = GetReleaseRequestData(objectNetworkId, throwVelocity);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Parse(serverAddress), port);

        SendNetworkMessage(requestMsg, serverEp, true);
    }

    private void HandleServerReleaseRequest(int objectNetId, EndPoint requester, Vector3 velocity)
    {
        if (serverObjectOwnership.ContainsKey(objectNetId))
        {
            EndPoint currentOwner = serverObjectOwnership[objectNetId];

            if (currentOwner.Equals(requester))
            {
                serverObjectOwnership.Remove(objectNetId);
                Debug.Log($"Object {objectNetId} released by {requester}. Reverting to Server Authority with velocity {velocity}.");

                if (networkIdentities.TryGetValue(objectNetId, out NetworkIdentity identity))
                {
                    identity.SetIsLocalPlayer(true);

                    Rigidbody rb = identity.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.linearVelocity = velocity;
                    }
                }

                BroadcastRelease(objectNetId);
            }
        }
    }

    private void BroadcastRelease(int objectNetId)
    {
        object[] releaseMsg = GetReleaseBroadcastData(objectNetId);
        foreach (var clientProxy in clientConnections.Values)
        {
            try { SendNetworkMessage(releaseMsg, clientProxy.EndPoint, true); } catch { }
        }

        if (role == NetworkRole.Host || role == NetworkRole.Server)
        {
            lock (pendingReleaseNetIds)
            {
                pendingReleaseNetIds.Add(objectNetId);
            }
        }
    }
    private void HandleClientReleaseBroadcast(int objectNetId)
    {
        if (networkIdentities.TryGetValue(objectNetId, out NetworkIdentity identity))
        {
           
            if (role == NetworkRole.Server || role == NetworkRole.Host)
            {
                identity.SetIsLocalPlayer(true);

                var rb = identity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
            else
            {
                identity.SetIsLocalPlayer(false);
                Debug.Log("Releasing object on client, setting isLocalPlayer to false.");
                var rb = identity.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }

            var grabState = identity.GetComponent<GrabState>();
            if (grabState != null) grabState.OnGrabStateUpdated(identity.isLocalPlayer, -1);
        }
    }

    #endregion

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

        foreach (var clientProxy in clientConnections.Values)
        {
            try
            {
                SendNetworkMessage(rpcData, clientProxy.EndPoint, true);
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Error sending RPC to {clientProxy.EndPoint}: {e.Message}");
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