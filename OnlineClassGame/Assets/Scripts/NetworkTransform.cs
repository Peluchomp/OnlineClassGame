// NetworkTransform.cs
using UnityEngine;

public class NetworkTransform : MonoBehaviour
{
    public int networkId { get; private set; }
    public bool isLocalPlayer = true; // Set this to false for remote players/objects

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    public Vector3 netwPos;
    public Quaternion netwRot;

    void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void Start()
    {
        NetworkManager.Instance.RegisterTransform(this);
    }

    void Update()
    {
        if (!isLocalPlayer)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10);
        }

        if (isLocalPlayer)
        {
            netwPos = transform.position;
            netwRot = transform.rotation;
        }
    }

    public void SetNetworkId(int id)
    {
        networkId = id;
    }

    public void UpdateTransform(Vector3 position, Quaternion rotation)
    {
        netwPos = position;
        netwRot = rotation;
        // Aplica directamente a la posición y rotación del objeto
        targetPosition = position;
        targetRotation = rotation;
        Debug.Log("Im didac and im a nerd im updating transform");
    }
}