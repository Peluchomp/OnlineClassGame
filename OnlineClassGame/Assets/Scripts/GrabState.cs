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

    public void OnGrabStateUpdated(bool isMine, int newOwnerId)
    {

        this.currentOwnerId = newOwnerId;

        if (!isMine)
        {
           rb.isKinematic = true;
        }
        

    }

}
