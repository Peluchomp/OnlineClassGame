using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour
{

    private NetworkIdentity networkIdentity;
    private Rigidbody rb;
    private Collider playerCollider;


    private Transform cameraTransform; 
    private float rotationX = 0f; 

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float jumpForce = 1000.0f;

    [Header("Control de Cámara")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float lookXLimit = 80.0f; 

    [Header("Detección de Suelo")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;


    void Awake()
    {
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
    }

    void Update()
    {

        if (networkIdentity == null || !networkIdentity.isLocalPlayer)
        {
            return;
        }

        HandleMovementInput();
        HandleJumpInput();
        HandleLookInput(); 

        IsGrounded(true);
    }


    private void HandleMovementInput()
    {
        float horizontalInput = 0f;
        float verticalInput = 0f;


        if (Input.GetKey(KeyCode.W))
        {
            verticalInput += 1f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            verticalInput -= 1f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            horizontalInput -= 1f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            horizontalInput += 1f;
        }

        // El movimiento usa Space.Self, que es relativo a la rotación del cuerpo
        Vector3 movement = new Vector3(horizontalInput, 0, verticalInput).normalized * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.Self);
    }

    private void HandleJumpInput()
    {
        if (rb != null && Input.GetKeyDown(KeyCode.Space))
        {
            if (IsGrounded())
            {
                Debug.Log("Salto Ejecutado");
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }
    }


    private void HandleLookInput()
    {
        if (cameraTransform == null) return;

        // Obtener movimiento del ratón
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        rotationX -= mouseY;

        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }
    // ------------------------------------

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