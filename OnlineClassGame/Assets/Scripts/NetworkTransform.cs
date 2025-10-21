using UnityEngine;

public class NetworkTransform : MonoBehaviour
{
    public int networkId { get; private set; }
    public bool isLocalPlayer = true;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    // Networked values
    public Vector3 netwPos;
    public Quaternion netwRot;
    public Vector3 netwScale;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetScale = transform.localScale;

        netwPos = transform.position;
        netwRot = transform.rotation;
        netwScale = transform.localScale;
    }

    void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RegisterTransform(this);
        }
        else
        {
            Debug.LogError("NetworkManager.Instance is null. Cannot register NetworkTransform.");
        }
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 10); 
        }
        else
        {
            netwPos = transform.position;
            netwRot = transform.rotation;
            netwScale = transform.localScale;
        }
    }

    public void SetNetworkId(int id)
    {
        networkId = id;
    }

    public void UpdateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        netwPos = position;
        netwRot = rotation;
        netwScale = scale;

        targetPosition = position;
        targetRotation = rotation;
        targetScale = scale;

    }
}