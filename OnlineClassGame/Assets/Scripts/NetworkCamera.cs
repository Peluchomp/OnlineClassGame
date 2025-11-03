using UnityEngine;

public class NetworkCamera : MonoBehaviour
{
    void Start()
    {
        if ( !GetComponentInParent<NetworkIdentity>().isLocalPlayer) 
        {
            GetComponent<Camera>().enabled = false;
            GetComponent<AudioListener>().enabled = false;
        }
    }
}
