using UnityEngine;

public class MineralState : MonoBehaviour
{
    public enum MineralType
    {
        Iron,
        Copper,
        Silver,
        Gold,
        Lead,
        Sulfur,
        Zinc,
        Manganese,
        None = 99
    }

    public MineralType mineralType;
    public void Mine()
    {
        switch (mineralType)
        {
            case MineralType.Iron:
                NetworkManager.Instance.ServerSpawnAndBroadcast(1, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Copper:
                NetworkManager.Instance.ServerSpawnAndBroadcast(3, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Silver:
                NetworkManager.Instance.ServerSpawnAndBroadcast(6, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Gold:
                NetworkManager.Instance.ServerSpawnAndBroadcast(4, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Lead:
                NetworkManager.Instance.ServerSpawnAndBroadcast(5, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Sulfur:
                NetworkManager.Instance.ServerSpawnAndBroadcast(7, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Zinc:
                NetworkManager.Instance.ServerSpawnAndBroadcast(9, transform.position + Vector3.up, Quaternion.identity);
                break;
            case MineralType.Manganese:
                NetworkManager.Instance.ServerSpawnAndBroadcast(8, transform.position + Vector3.up, Quaternion.identity);
                break;
        }
    }
}
