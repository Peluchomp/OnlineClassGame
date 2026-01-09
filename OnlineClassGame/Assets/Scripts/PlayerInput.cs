using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    private InputSystem_Actions _gameControls;

    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] MinerScript minerScript;
    [SerializeField] PlayerGrabber grabScript;

    private void Awake()
    {
        _gameControls = new InputSystem_Actions();
    }

    private void Update()
    {
        if (!GetComponent<NetworkIdentity>().isLocalPlayer) return;

        PlayerJumpedInput();
        PlayerMovementInput();
        PlayerLookInput();
        PlayerMineInput();
        PlayerGrabInput();
        QuitGame();
    }

    private void OnEnable()
    {
        _gameControls.Enable();
    }

    private void OnDisable()
    {
        _gameControls.Disable();
    }

    public void PlayerJumpedInput()
    {
        if (_gameControls.Player.Jump.triggered)
        {
            playerMovement.HandleJump();
        }
    }

    public void PlayerMovementInput()
    {
        Vector2 moveInput = _gameControls.Player.Move.ReadValue<Vector2>();

        playerMovement.HandleMovement(moveInput);

    }

    public void PlayerGrabInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Interact pressed");
            grabScript.HandleInput();
        }
    }
    public void PlayerLookInput()
    {
        Vector2 lookInput = _gameControls.Player.Look.ReadValue<Vector2>();

        playerMovement.HandleLook(lookInput);
    }
    public void PlayerMineInput()
    {
        if (_gameControls.Player.Attack.triggered)
        {
            minerScript.HandleMining();
        }
    }
    public bool PlayerIsHoldingSprint()
    {
        return _gameControls.Player.Sprint.IsPressed();
    }

    public void QuitGame()
    {
        if (_gameControls.UI.Cancel.triggered)
        {
            Application.Quit();
        }
    }

}
