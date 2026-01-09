using TMPro;
using UnityEngine;

public class CrosshairController : MonoBehaviour
{
    [SerializeField] private GameObject normalCrosshair;
    [SerializeField] private GameObject specialCrosshair;
    [SerializeField] public TextMeshProUGUI uiText;

    void Start()
    {
        ActivateNormalCrosshair();
    }

    public void ActivateNormalCrosshair()
    {
        if (normalCrosshair != null)
        {
            normalCrosshair.SetActive(true);
        }
           
        if (specialCrosshair != null)
        {
            specialCrosshair.SetActive(false);
        }
        uiText.enabled = false;
    }

    public void ActivateSpecialCrosshair()
    {
        if (normalCrosshair != null)
        {
            normalCrosshair.SetActive(false);
        }
        
        if (specialCrosshair != null)
        {
            specialCrosshair.SetActive(true);
        }
        uiText.enabled = true;
    }
}