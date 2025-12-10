using UnityEngine;

public class PlayerGrabber : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float grabRange = 3.0f;
    [SerializeField] private LayerMask grabLayer;
    [SerializeField] private Transform holdPoint;

    private NetworkIdentity currentHeldObject;
    private Rigidbody heldRb;
    private Camera playerCamera;

    private bool waitingForServerConfirmation = false;

    private void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        var myIdentity = GetComponent<NetworkIdentity>();
        if (myIdentity != null && !myIdentity.isLocalPlayer) return;

        if (currentHeldObject != null)
        {
            if (currentHeldObject.isLocalPlayer || waitingForServerConfirmation)
            {
                if (currentHeldObject.isLocalPlayer) waitingForServerConfirmation = false;

                MoveObjectToHand();
            }
            else
            {
                ForceDrop();
            }
        }
    }

    public void HandleInput()
    {
        if (currentHeldObject == null) AttemptGrab();
        else DropObject();
    }

    private void AttemptGrab()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, grabRange, grabLayer))
        {
            NetworkIdentity targetIdentity = hit.collider.GetComponent<NetworkIdentity>();
            if (targetIdentity != null)
            {
                waitingForServerConfirmation = true;

                NetworkManager.Instance.ClientRequestGrab(targetIdentity.networkId);

                AttachObjectLocally(targetIdentity);
            }
        }
    }

    public void AttachObjectLocally(NetworkIdentity identity)
    {
        currentHeldObject = identity;
        heldRb = identity.GetComponent<Rigidbody>();
        if (heldRb != null)
        {
            heldRb.isKinematic = true;
            heldRb.useGravity = false;
            heldRb.GetComponent<Collider>().enabled = false;
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

        Vector3 throwVelocity = playerCamera.transform.forward * 5f;

        NetworkManager.Instance.ClientRequestRelease(currentHeldObject.networkId, throwVelocity);

        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
            heldRb.GetComponent<Collider>().enabled = true;

            heldRb.linearVelocity = throwVelocity;
        }

        currentHeldObject = null;
        heldRb = null;
        waitingForServerConfirmation = false;
    }

    private void ForceDrop()
    {
        currentHeldObject = null;
        heldRb = null;
        waitingForServerConfirmation = false;
    }
}