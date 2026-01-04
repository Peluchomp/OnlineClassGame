using UnityEngine;

public class ClientConnection
{
    public int ConnectionId;
    public System.Net.EndPoint EndPoint;
    public NetworkIdentity PlayerIdentity;

    public float LastMessageTime;

    public ClientConnection(int id, System.Net.EndPoint ep, float initialTime)
    {
        ConnectionId = id;
        EndPoint = ep;
        LastMessageTime = initialTime;
    }
}