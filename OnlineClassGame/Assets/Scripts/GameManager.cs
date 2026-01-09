using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartGame()
    {
        CustomerManager.Instance.SpawnCustomer();
        OrderManager.Instance.GenerateAndBroadcastOrder();
    }

    [ContextMenu("New Customer")]
    public void NewCustomer()
    {
        Debug.Log("New Customer Triggered");
        CustomerManager.Instance.SendCustomerToLeave();
        MineralManager.Instance.RestoreMinerals();
        OrderManager.Instance.StopTimer();
        
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Server)
        {
            NetworkManager.Instance.ServerBroadcastNewCustomer();

            OrderManager.Instance.GenerateAndBroadcastOrder();
        }
    }
}
