using UnityEngine;


public class NetworkIdentity : MonoBehaviour
{
    [SerializeField]
    private string m_sceneId; 

    public string sceneId => m_sceneId;

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