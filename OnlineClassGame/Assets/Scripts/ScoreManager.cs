using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI scoreText;

    public int score;

    public static ScoreManager Instance { get; private set; }

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        NetworkManager.Instance.OnIntValueReceived += UpdateScoreUI;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnIntValueReceived -= UpdateScoreUI;
        }
    }

    public void ModifyScore(int amount)
    {
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client)
        {
            Debug.LogWarning("Clients cannot modify the score directly.");
            return;
        }

        score += amount;
        NetworkManager.Instance.ServerBroadcastIntegerValue(score);
        UpdateScoreUI(score);
    }

    void UpdateScoreUI(int newValue)
    {
        scoreText.text = "Customer happiness: " + newValue;
    }

}
