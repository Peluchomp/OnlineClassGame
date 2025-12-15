using System.Security.Principal;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkIdentity))]
public class GrabState : MonoBehaviour
{
    Rigidbody rb;
    public int currentOwnerId = -1;
    NetworkIdentity identity;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        identity = GetComponent<NetworkIdentity>();
    }

    private void Start()
    {
        NetworkManager.Instance.OnServerStarted += Initialize;
        if (identity != null && identity.isLocalPlayer)
        {
            rb.isKinematic = false;
        }
        else
        {
            rb.isKinematic = true;
        }
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
        if (identity != null && identity.isLocalPlayer)
        {
            rb.isKinematic = false;
        }else
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