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
    private void Reset()
    {
        m_sceneId = System.Guid.NewGuid().ToString();
    }

    void OnEnable()
    {
       
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnServerStarted += RegisterSelf;
            NetworkManager.Instance.OnClientStarted += RegisterSelf;
        }
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnServerStarted += RegisterSelf;
            NetworkManager.Instance.OnClientStarted += RegisterSelf;
        }
    }

    void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnServerStarted -= RegisterSelf;
            NetworkManager.Instance.OnClientStarted -= RegisterSelf;
        }
    }

    private void RegisterSelf()
    {
        Debug.Log($"Registering NetworkIdentity with SceneID: {m_sceneId}");
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