using UnityEngine;

public class TestMover : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * 5);
        }

        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(Vector3.back * Time.deltaTime * 5);
        }
        if (Input.GetKeyDown(KeyCode.F) && NetworkManager.Instance.role == NetworkManager.NetworkRole.Server)
        {
            Debug.Log("Spawning object from TestMover");
            NetworkManager.Instance.ServerSpawnAndBroadcast(0, transform.position + Vector3.up * 2, Quaternion.identity);
        }
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client && Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("Requesting spawn from TestMover");
            NetworkManager.Instance.ClientRequestSpawn(0, transform.position + Vector3.up * 2, Quaternion.identity);
        }
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Server && Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("Broadcasting despawn from TestMover");
            NetworkManager.Instance.BroadcastDestroyObject(1);
        }
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client && Input.GetKeyDown(KeyCode.J))
        {
            Debug.Log("Requesting despawn from TestMover");
            NetworkManager.Instance.ClientRequestDestroy(1);
        }
    }
}
