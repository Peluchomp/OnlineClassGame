using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkTransform : MonoBehaviour
{
    [HideInInspector]
    public NetworkIdentity networkIdentity;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    // Networked values
    public Vector3 netwPos;
    public Quaternion netwRot;
    public Vector3 netwScale;

    // Thresholds
    private float positionThreshold = 0.01f;
    private float rotationThreshold = 0.5f;
    private float scaleThreshold = 0.01f;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;

    public bool sendData { get; private set; }

    void Awake()
    {
        networkIdentity = GetComponent<NetworkIdentity>();

        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetScale = transform.localScale;

        netwPos = transform.position;
        netwRot = transform.rotation;
        netwScale = transform.localScale;

        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.localScale;
    }

    void Update()
    {
        if (!networkIdentity.isLocalPlayer)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 10);
        }
        else
        {
            bool positionChanged = Vector3.Distance(transform.position, lastPosition) > positionThreshold;
            bool rotationChanged = Quaternion.Angle(transform.rotation, lastRotation) > rotationThreshold;
            bool scaleChanged = Vector3.Distance(transform.localScale, lastScale) > scaleThreshold;

            sendData = positionChanged || rotationChanged || scaleChanged;
            Debug.Log($"[NetworkTransform] sendData: {sendData} (PosChanged: {positionChanged}, RotChanged: {rotationChanged}, ScaleChanged: {scaleChanged})");
            if (sendData)
            {
                netwPos = transform.position;
                netwRot = transform.rotation;
                netwScale = transform.localScale;

                lastPosition = transform.position;
                lastRotation = transform.rotation;
                lastScale = transform.localScale;
            }
        }
    }

    public void UpdateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (networkIdentity.isLocalPlayer) return;

        netwPos = position;
        netwRot = rotation;
        netwScale = scale;

        targetPosition = position;
        targetRotation = rotation;
        targetScale = scale;
    }
}