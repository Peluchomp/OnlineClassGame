using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkIdentity))]
public class GrabState : MonoBehaviour
{
    NetworkIdentity networkIdentity;
    public int currentOwnerId = -1;

    private void Awake()
    {
    }

    public void OnGrabStateUpdated(bool isMine, int newOwnerId)
    {

        this.currentOwnerId = newOwnerId;

       
    }

}
