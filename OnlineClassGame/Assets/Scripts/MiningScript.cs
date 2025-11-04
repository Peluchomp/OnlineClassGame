using UnityEngine;

public class MiningScript : MonoBehaviour
{
    [SerializeField] private string targetTag = "Destructible";
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private Camera playerCamera;

    private CrosshairController crosshairController;

    void Start()
    {
        crosshairController = FindFirstObjectByType<CrosshairController>();
    }

    void Update()
    {
        if (!GetComponent<NetworkIdentity>().isLocalPlayer) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        bool isPointingAtTarget = false;
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, targetLayer.value))
        {
            if (hit.collider.CompareTag(targetTag))
            {
                isPointingAtTarget = true;

                if (Input.GetMouseButtonDown(0))
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

        if (crosshairController != null)
        {
            if (isPointingAtTarget)
            {
                crosshairController.ActivateSpecialCrosshair();
            }
            else
            {
                crosshairController.ActivateNormalCrosshair();
            }
        }
    }
}