using UnityEngine;
using System.Text;
using static MineralState;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;


public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;
    private int orderIdCounter = 0;

    [SerializeField] private TextMeshProUGUI orderDisplayText;
    [SerializeField] private GameObject orderDisplayImage;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private GameObject sliderVisuals;
    private Image sliderFillImage;

    private MineralType currentType1;
    private int currentAmount1;
    private MineralType currentType2;
    private int currentAmount2;

    private int minAmount = 1;
    private int maxAmount = 1;

    private List<GameObject> mineralsInZone = new List<GameObject>();

    private double serverStartTime;
    private float timerDuration;
    private bool isTimerRunning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        DeactivateOrderDisplay();

        if (timerSlider != null)
        {
            timerSlider.value = 0;
            if (timerSlider.fillRect != null)
            {
                sliderFillImage = timerSlider.fillRect.GetComponent<Image>();
            }
        }
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            UpdateTimerVisuals();
        }
    }

    [ContextMenu("Generate Random Order")]
    public void GenerateAndBroadcastOrder()
    {
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client)
        {
            Debug.LogWarning("Only the Server can generate orders!");
            return;
        }

        orderIdCounter++;

        bool twoMinerals = Random.value > 0.5f;

        MineralType type1 = GetRandomMineral();
        int amount1 = Random.Range(minAmount, maxAmount + 1);

        MineralType type2 = MineralType.None;
        int amount2 = 0;

        if (twoMinerals)
        {
            do
            {
                type2 = GetRandomMineral();
            } while (type2 == type1);

            amount2 = Random.Range(minAmount, maxAmount + 1);
        }

        currentType1 = type1;
        currentAmount1 = amount1;
        currentType2 = type2;
        currentAmount2 = amount2;

        CheckOrderCompletion();

        string orderText = FormatOrderString(type1, amount1, type2, amount2);
        Debug.Log($"[Server] Generated Order #{orderIdCounter}: {orderText}");

        NetworkManager.Instance.ServerBroadcastOrder(orderIdCounter, (int)type1, amount1, (int)type2, amount2);
    }

    public void ReceiveOrder(int id, MineralType m1, int a1, MineralType m2, int a2)
    {
        string orderText = FormatOrderString(m1, a1, m2, a2);

        orderDisplayText.text = orderText;

        currentType1 = m1;
        currentAmount1 = a1;
        currentType2 = m2;
        currentAmount2 = a2;

        CheckOrderCompletion();
    }

    private MineralType GetRandomMineral()
    {
        System.Array values = System.Enum.GetValues(typeof(MineralType));
       
        return (MineralType)values.GetValue(Random.Range(0, values.Length - 1));
    }

    private string FormatOrderString(MineralType m1, int a1, MineralType m2, int a2)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Request: {a1} {m1}");

        if (m2 != MineralType.None && a2 > 0)
        {
            sb.Append($" and {a2} {m2}");
        }

        return sb.ToString();
    }

    public void DeactivateOrderDisplay()
    {
        orderDisplayImage.GetComponent<Image>().enabled = false;
        orderDisplayText.alpha = 0;
        sliderVisuals.SetActive(false);
    }

    public void ActivateOrderDisplay()
    {
        orderDisplayImage.GetComponent<Image>().enabled = true;
        orderDisplayText.alpha = 255;
        ServerStartTimer(10f);
        sliderVisuals.SetActive(true);
    }

    public void ServerStartTimer(float durationSeconds)
    {
        if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client) return;

        NetworkManager.Instance.ServerBroadcastTimer(durationSeconds);
    }

    public void StartTimer(double startTime, float duration)
    {
        serverStartTime = startTime;
        timerDuration = duration;
        isTimerRunning = true;

        Debug.Log($"Timer Started. Duration: {duration}");
    }

    private void UpdateTimerVisuals()
    {
        double currentNetworkTime = NetworkManager.Instance.GetNetworkTime();

       float timeElapsed = (float)(currentNetworkTime - serverStartTime);

        float timeRemaining = timerDuration - timeElapsed;

        float normalizedTime = Mathf.Clamp01(timeRemaining / timerDuration);

        if (timerSlider != null)
        {
            timerSlider.normalizedValue = normalizedTime;

            if (sliderFillImage != null)
            {
                sliderFillImage.color = Color.Lerp(Color.red, Color.green, normalizedTime);
            }
        }

        if (timeRemaining <= 0)
        {
            isTimerRunning = false;
            OnTimerFinished();
        }
    }

    private void OnTimerFinished()
    {
        if (timerSlider != null) timerSlider.value = 0;

        ScoreManager.Instance.ModifyScore(-1);
        GameManager.Instance.NewCustomer();
        ServerStartTimer(10f);
    }

    public void StopTimer() 
    {
        isTimerRunning = false;
        if (timerSlider != null) timerSlider.value = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Untagged")) return;

        mineralsInZone.Add(other.gameObject);
        CheckOrderCompletion();
    }

    private void OnTriggerExit(Collider other)
    {
        if (mineralsInZone.Contains(other.gameObject))
        {
            mineralsInZone.Remove(other.gameObject);
            CheckOrderCompletion();
        }
    }

    private void CheckOrderCompletion()
    {
        if (NetworkManager.Instance.role != NetworkManager.NetworkRole.Server) return;

        mineralsInZone.RemoveAll(item => item == null);

        int count1 = 0;
        int count2 = 0;

        foreach (var item in mineralsInZone)
        {
            if (item.tag == currentType1.ToString())
            {
                count1++;
            }
            else if (currentType2 != MineralType.None && item.tag == currentType2.ToString())
            {
                count2++;
            }
        }

        if (count1 >= currentAmount1 && (currentType2 == MineralType.None || count2 >= currentAmount2))
        {
            foreach (var item in mineralsInZone)
            {
                NetworkManager.Instance.BroadcastDestroyObject(item.GetComponent<NetworkIdentity>().networkId);
            }

            isTimerRunning = false;
            ScoreManager.Instance.ModifyScore(1);
            GameManager.Instance.NewCustomer();

        }
    }

}