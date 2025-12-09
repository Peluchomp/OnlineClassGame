using UnityEngine;

public class PlayerGrabber : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float grabRange = 3.0f;
    [SerializeField] private LayerMask grabLayer;
    [SerializeField] private Transform holdPoint; // Assign an empty child object here

    private NetworkIdentity currentHeldObject;
    private Rigidbody heldRb;
    private Camera playerCamera;

    private void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void Update()
    {
        var myIdentity = GetComponent<NetworkIdentity>();
        if (myIdentity != null && !myIdentity.isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentHeldObject == null)
            {
                AttemptGrab();
            }
            else
            {
                DropObject();
            }
        }

        if (currentHeldObject != null)
        {
            if (currentHeldObject.isLocalPlayer)
            {
                MoveObjectToHand();
            }
            else
            {
                ForceDrop();
            }
        }
    }

    private void AttemptGrab()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * grabRange, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, grabRange, grabLayer))
        {
            NetworkIdentity targetIdentity = hit.collider.GetComponent<NetworkIdentity>();

            if (targetIdentity != null)
            {
                NetworkManager.Instance.ClientRequestGrab(targetIdentity.networkId);

                AttachObjectLocally(targetIdentity);
            }
        }
    }

    private void AttachObjectLocally(NetworkIdentity identity)
    {
        currentHeldObject = identity;
        heldRb = identity.GetComponent<Rigidbody>();

        if (heldRb != null)
        {
            heldRb.isKinematic = true; 
            heldRb.useGravity = false;
        }
    }

    private void MoveObjectToHand()
    {
        currentHeldObject.transform.position = Vector3.Lerp(currentHeldObject.transform.position, holdPoint.position, Time.deltaTime * 10f);
        currentHeldObject.transform.rotation = Quaternion.Lerp(currentHeldObject.transform.rotation, holdPoint.rotation, Time.deltaTime * 10f);
    }

    private void DropObject()
    {
        if (currentHeldObject == null) return;

        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
            heldRb.AddForce(playerCamera.transform.forward * 5f, ForceMode.Impulse);
        }

        currentHeldObject = null;
        heldRb = null;
    }

    private void ForceDrop()
    {
        currentHeldObject = null;
        heldRb = null;
    }
}