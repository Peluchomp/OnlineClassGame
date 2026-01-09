using System.Collections;
using UnityEngine;
using static NetworkManager;

public class CustomerManager : MonoBehaviour
{
   [SerializeField] Transform customerSpawn;

   [SerializeField] Transform counterPosition;

    NetworkIdentity customerActiveInstance;

    bool isCustomerActive = false;
    public static CustomerManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SpawnCustomer()
    {

        if (NetworkManager.Instance.role != NetworkRole.Server) return;
        isCustomerActive = true;
        Debug.Log("Spawning Customer");
        customerActiveInstance = NetworkManager.Instance.ServerSpawnAndBroadcast(Random.Range(2,2),customerSpawn.position,Quaternion.identity);

        SendCustomerToCounter();
    }

    public void DespawnCustomer()
    {
        if (NetworkManager.Instance.role != NetworkRole.Server) return;
        Debug.Log("Despawning Customer");
        NetworkManager.Instance.BroadcastDestroyObject(customerActiveInstance.networkId);
    }

    public void SetActiveInstance(NetworkIdentity networkIdentity)
    {
        customerActiveInstance = networkIdentity;
    }

    public void SendCustomerToCounter()
    {
        CustomerAI customerAI = customerActiveInstance.GetComponent<CustomerAI>();
        customerAI.SetDestination(counterPosition.position);
    }

    public void SendCustomerToLeave()
    {
        CustomerAI customerAI = customerActiveInstance.GetComponent<CustomerAI>();
        customerAI.SetDestination(customerSpawn.position);

        if (NetworkManager.Instance.role == NetworkRole.Server && isCustomerActive)
        {
            isCustomerActive = false;
            StartCoroutine(SendNewCustomerAfterDelay(1.5f));
        }
    }

    IEnumerator SendNewCustomerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DespawnCustomer();
        yield return new WaitForSeconds(delay/2);
        SpawnCustomer();
    }
}
