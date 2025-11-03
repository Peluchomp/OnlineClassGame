using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkIdentity))]
public class PlayerMovement : MonoBehaviour
{
    NetworkIdentity networkIdentity;
    InputAction moveAction;

    void Start()
    {
        
    }

    void Update()
    {
        if (networkIdentity != null && networkIdentity.isLocalPlayer)
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 movement = new Vector3(input.x, 0, input.y) * Time.deltaTime * 5f;
            transform.Translate(movement, Space.World);
        }
    }
}
