using System.Collections;
using TMPro;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{

    [SerializeField] TextMeshProUGUI playersConnectedText;
    [SerializeField] TextMeshProUGUI clientText;
    [SerializeField] GameObject startHostGo;
    [SerializeField] GameObject startServerGo;
    [SerializeField] GameObject lobbyCam;

    public static LobbyManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void StartClient()
    {
        NetworkManager.Instance.DiscoverServer();
        NetworkManager.Instance.StartClient();
        startHostGo.SetActive(false);
        clientText.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (playersConnectedText.IsActive())
        {
            playersConnectedText.text = "Clients connected: " + NetworkManager.Instance.connectedClientsCount;
        }
    }

    public void StartHost()
    {
        NetworkManager.Instance.StartServer();
        startHostGo.SetActive(false);
        startServerGo.SetActive(true);
    }

    public void StartGame()
    {
        NetworkManager.Instance.SpawnPlayersContextMenu();

        lobbyCam.SetActive(false);
        gameObject.SetActive(false);
    }


    public void ClientStart()
    {
        lobbyCam.SetActive(false);
        gameObject.SetActive(false);
    }

}
