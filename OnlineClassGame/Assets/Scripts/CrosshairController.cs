using UnityEngine;

public class CrosshairController : MonoBehaviour
{
    [SerializeField] private GameObject normalCrosshair;
    [SerializeField] private GameObject specialCrosshair;

    void Start()
    {
        ActivateNormalCrosshair();
    }

    public void ActivateNormalCrosshair()
    {
        if (normalCrosshair != null)
            normalCrosshair.SetActive(true);
        if (specialCrosshair != null)
            specialCrosshair.SetActive(false);
    }

    public void ActivateSpecialCrosshair()
    {
        if (normalCrosshair != null)
            normalCrosshair.SetActive(false);
        if (specialCrosshair != null)
            specialCrosshair.SetActive(true);
    }
}