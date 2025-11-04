using UnityEngine;


public class MiningScript : MonoBehaviour
{
    [SerializeField] private string targetTag = "Destructible";
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private Camera playerCamera;

    
    void Update()
    {
        if (!GetComponent<NetworkIdentity>().isLocalPlayer) return;

        if (Input.GetMouseButtonDown(0)) 
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, targetLayer.value))
            {
                if (hit.collider.CompareTag(targetTag))
                {
                    if (hit.collider.GetComponent<NetworkIdentity>() != null)
                    {
                        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Server)
                        {
                            NetworkManager.Instance.BroadcastDestroyObject(hit.collider.GetComponent<NetworkIdentity>().networkId);
                        }
                        else
                        {
                            NetworkManager.Instance.ClientRequestDestroy(hit.collider.GetComponent<NetworkIdentity>().networkId);
                        }
                    }
                }
            }
        }
    }

}
