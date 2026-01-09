using UnityEngine;

public class MineralManager : MonoBehaviour
{
    [SerializeField] GameObject[] minerals;

    public static MineralManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RestoreMinerals()
    {
        foreach (GameObject mineral in minerals)
        {
            mineral.SetActive(true);
        }
    }

}
