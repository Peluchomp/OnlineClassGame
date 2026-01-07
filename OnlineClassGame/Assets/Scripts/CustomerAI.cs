using UnityEngine;
using UnityEngine.AI;
public class CustomerAI : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Animator animator;

    bool hasArrived = true;

    void Start()
    {
       //Guarrada historica lo cambio despues
       if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client)
       {
            CustomerManager customerManager = Object.FindFirstObjectByType<CustomerManager>();
            customerManager.SetActiveInstance(GetComponent<NetworkIdentity>());
            customerManager.SendCustomerToCounter();
       }
    }

    void Update()
    {
        if (agent.pathPending) return;

        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("IsWalking", isMoving);

        if (!hasArrived && agent.remainingDistance <= agent.stoppingDistance)
        {
            Debug.Log("Customer has arrived at destination.");
            hasArrived = true;
            animator.SetTrigger("Arrive");
            animator.SetBool("IsWalking", false);
        }

        if (agent.remainingDistance > agent.stoppingDistance)
        {
            hasArrived = false;
        }
    }

    public void SetDestination(Vector3 destination)
    {
        agent.SetDestination(destination);
        hasArrived = false;
    }
}
