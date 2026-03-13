using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float verticalSpeed = 5f;

    [Header("Rotation Settings")]
    [SerializeField] private float turnSpeed = 720f; // Degrees per second

    private CharacterController controller;
    private PlayerInput playerInput;
    private InputAction ascendAction;
    private InputAction descendAction;
    private Vector2 moveInput;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        CacheFlightActions();
    }

    void OnEnable()
    {
        CacheFlightActions();
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        if (ascendAction == null || descendAction == null)
        {
            CacheFlightActions();
        }

        Vector3 horizontalMove = new Vector3(moveInput.x, 0f, moveInput.y);
        if (horizontalMove != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalMove);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        float verticalInput = 0f;

        if (ascendAction != null && ascendAction.IsPressed())
        {
            verticalInput += 1f;
        }

        if (descendAction != null && descendAction.IsPressed())
        {
            verticalInput -= 1f;
        }

        Vector3 move = (horizontalMove * speed) + (Vector3.up * (verticalInput * verticalSpeed));
        controller.Move(move * Time.deltaTime);
    }

    private void CacheFlightActions()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null || playerInput.actions == null)
        {
            return;
        }

        ascendAction = playerInput.actions.FindAction("Ascend");
        descendAction = playerInput.actions.FindAction("Descend");
    }
}