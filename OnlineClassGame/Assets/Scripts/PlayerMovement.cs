using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour
{

    private NetworkIdentity networkIdentity;
    private Rigidbody rb;
    private Collider playerCollider;

    private PlayerInput playerInput;

    private Transform cameraTransform; 
    private float rotationX = 0f; 

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float jumpForce = 1000.0f;

    [Header("Control de Cámara")]
    [SerializeField] private float mouseSensitivity = 1;
    [SerializeField] private float lookXLimit = 80.0f; 

    [Header("Detección de Suelo")]
    private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;

    void Awake()
    {
        groundLayer = LayerMask.GetMask("Ground");
        networkIdentity = GetComponent<NetworkIdentity>();
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody no encontrado. El salto no funcionará.");
        }
        if (playerCollider == null)
        {
            Debug.LogError("Collider no encontrado. La detección de suelo fallará.");
        }

        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client)
        {
            LobbyManager.Instance.ClientStart();
        }

    }

    void Start()
    {
        if (networkIdentity.isLocalPlayer)
        {

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Camera cameraComponent = GetComponentInChildren<Camera>();
            if (cameraComponent != null)
            {
                cameraTransform = cameraComponent.transform;
            }
            else
            {
                Debug.LogError("Cámara no encontrada como hijo del objeto del jugador. La rotación de la vista fallará.");
            }
        }
        else {

            GetComponent<Rigidbody>().isKinematic = true;
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Debug.LogError("PlayerInput no encontrado en el objeto del jugador. La entrada del jugador fallará.");
            }
        }
    }

    void Update()
    {
        if (networkIdentity == null || !networkIdentity.isLocalPlayer)
        {
            return;
        }

        IsGrounded(true);
    }

    public void HandleMovement(Vector2 moveInput)
    {
        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.Self);
    }

    public void HandleJump()
    {
        if (rb != null)
        {
            if (IsGrounded())
            {
                Debug.Log("Salto Ejecutado");
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }
    }

    public void HandleLook(Vector2 lookDelta)
    {
        if (cameraTransform == null) return;

        rotationX -= lookDelta.y;

        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

        transform.Rotate(Vector3.up * lookDelta.x);
    }

    private bool IsGrounded(bool debug = false)
    {
        if (playerCollider == null) return false;

        Vector3 center = playerCollider.bounds.center;
        Vector3 extents = playerCollider.bounds.extents;

        Vector3 rayOrigin = new Vector3(center.x, center.y - extents.y + 0.01f, center.z);

        float checkDistance = groundCheckDistance;

        bool isGrounded = Physics.Raycast(rayOrigin, Vector3.down, checkDistance, groundLayer);

        if (debug)
        {
            Debug.DrawRay(rayOrigin, Vector3.down * checkDistance, isGrounded ? Color.green : Color.red);
        }

        return isGrounded;
    }
}