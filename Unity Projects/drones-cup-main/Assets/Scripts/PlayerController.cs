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
    [SerializeField] private float turnSpeed = 720f;

    private CharacterController controller;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction ascendAction;
    private InputAction descendAction;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        CacheActions();
    }

    void OnEnable()
    {
        CacheActions();
    }

    void CacheActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        moveAction = playerInput.actions["Move"];
        ascendAction = playerInput.actions["Ascend"];
        descendAction = playerInput.actions["Descend"];
    }

    void Update()
    {
        if (moveAction == null)
            CacheActions();

        // --- HORIZONTAL MOVEMENT ---
        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

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

        // --- VERTICAL MOVEMENT ---
        float verticalInput = 0f;

        if (ascendAction != null && ascendAction.IsPressed())
            verticalInput += 1f;

        if (descendAction != null && descendAction.IsPressed())
            verticalInput -= 1f;

        // --- APPLY MOVEMENT ---
        Vector3 move =
            (horizontalMove * speed) +
            (Vector3.up * (verticalInput * verticalSpeed));

        controller.Move(move * Time.deltaTime);
    }
}