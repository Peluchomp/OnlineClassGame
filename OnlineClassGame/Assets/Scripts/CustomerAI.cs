using UnityEngine;
using UnityEngine.AI;
public class CustomerAI : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Animator animator;

    bool hasArrived = true;

    void Start()
    {
       if (NetworkManager.Instance.role == NetworkManager.NetworkRole.Client)
       {
            CustomerManager.Instance.SetActiveInstance(GetComponent<NetworkIdentity>());
            CustomerManager.Instance.SendCustomerToCounter();
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
            OrderManager.Instance.ActivateOrderDisplay();
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
        OrderManager.Instance.DeactivateOrderDisplay();
    }
}
