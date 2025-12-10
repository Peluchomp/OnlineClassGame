using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkIdentity))]
public class GrabState : MonoBehaviour
{
    Rigidbody rb;
    public int currentOwnerId = -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        NetworkManager.Instance.OnServerStarted += Initialize;
    }

    private void OnDisable()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnServerStarted -= Initialize;
        }
    }

    private void Initialize()
    {
        var identity = GetComponent<NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
        {
            rb.isKinematic = true;
        }
    }

    public void OnGrabStateUpdated(bool isMine, int newOwnerId)
    {
        this.currentOwnerId = newOwnerId;

        if (!isMine)
        {
            rb.isKinematic = true;
        }
    }
}