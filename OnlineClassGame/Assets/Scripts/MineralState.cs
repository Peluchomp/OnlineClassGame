using UnityEngine;

public class MineralState : MonoBehaviour
{
    bool isQuitting;
    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (!isQuitting)
        {
            NetworkManager.Instance.ServerSpawnAndBroadcast(1, transform.position + Vector3.up, Quaternion.identity);
        }
    }
}
