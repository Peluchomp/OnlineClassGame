using UnityEngine;

public class OreState : MonoBehaviour
{
    string furnaceTag = "Furnace";
    float timer;
    float cookTime = 5;

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
                GetComponent<Renderer>().enabled = false;
                cookParticles.Stop();
                oreCooked = true;
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
