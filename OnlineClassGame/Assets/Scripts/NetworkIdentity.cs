using UnityEngine;


public class NetworkIdentity : MonoBehaviour
{
    public string sceneId;

    public int networkId;
    public bool isLocalPlayer;

    private NetworkTransform networkTransform;
    public NetworkTransform NetworkTransform => networkTransform;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
    }

    private void Start()
    {
        NetworkManager.Instance.RegisterIdentity(this);
    }

    public void SetNetworkId(int id)
    {
        networkId = id;
    }

    public void SetIsLocalPlayer(bool isLocalPlayer)
    {
        this.isLocalPlayer = isLocalPlayer;
    }
}