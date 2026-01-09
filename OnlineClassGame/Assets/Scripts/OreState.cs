using UnityEngine;

public class OreState : MonoBehaviour
{
    string furnaceTag = "Furnace";
    float timer;
    float cookTime = 5;

    GameObject oreObject;

    public MineralState.MineralType mineralType;

    [SerializeField] ParticleSystem cookParticles;
    bool oreCooked;

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == furnaceTag && !oreCooked)
        {
            timer += Time.deltaTime * 1;
            cookParticles.Play();
            if (timer > cookTime)
            {
                oreCooked = true;
                if (NetworkManager.Instance.role != NetworkManager.NetworkRole.Server)
                    return;

                switch(mineralType)
                {
                    case MineralState.MineralType.Iron:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(12, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Copper:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(10, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Silver:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(15, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Gold:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(11, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Lead:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(13, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Sulfur:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(16, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Zinc:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(17, transform.position + Vector3.up, Quaternion.identity);
                        break;
                    case MineralState.MineralType.Manganese:
                        NetworkManager.Instance.ServerSpawnAndBroadcast(14, transform.position + Vector3.up, Quaternion.identity);
                        break;
                }

                NetworkManager.Instance.BroadcastDestroyObject(GetComponent<NetworkIdentity>().networkId);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == furnaceTag)
        {
            cookParticles.Stop();
            timer = 0;
        }
    }
}
