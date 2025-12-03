using UnityEngine;
using UnityEngine.InputSystem;

public class MinerScript : MonoBehaviour
{
    string targetTag = "Mineral";
    LayerMask targetLayer;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private Camera playerCamera;

    [SerializeField] Animator animator;

    private PlayerInput playerInput;

    bool minerActionPressed = false;

    private CrosshairController crosshairController;

    private void Awake()
    {
        targetLayer = LayerMask.GetMask("Mineral");

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Debug.LogError("PlayerInput no encontrado en el objeto del jugador. La entrada del jugador fallará.");
            }
        }
    }

    void Start()
    {
        crosshairController = FindFirstObjectByType<CrosshairController>();
    }

    public void HandleMining()
    {
        minerActionPressed = true;
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

                if (minerActionPressed)
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
                        if (animator != null)
                        {
                            animator.SetTrigger("Mine");
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
        minerActionPressed = false;
    }
}