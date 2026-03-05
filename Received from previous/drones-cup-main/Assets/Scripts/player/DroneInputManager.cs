using UnityEngine;
using UnityEngine.InputSystem; // Vergeet deze namespace niet!

public class DroneInputManager : MonoBehaviour
{
    public static DroneInputManager Instance;

    [Header("Input Setup")]
    // Sleep hier je Input Action Asset in via de Inspector (of gebruik een gegenereerde C# class)
    public InputActionAsset inputAsset;

    // Referenties naar de specifieke acties
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction liftAction;
    private InputAction toggleLoweredAction;
    private InputAction toggleBeamAction;
    private InputAction resetDroneAction;

    [Header("Settings")]
    public float lookSensitivity = 1.0f; // Het nieuwe systeem geeft vaak andere waarden voor muis, dus handig om te scalen

    [Header("Axes (Read Only)")]
    public float moveX;
    public float moveZ;
    public float lookX;
    public float lookY;
    public float liftY;

    [Header("Buttons (Read Only)")]
    public bool toggleLowered;
    public bool toggleBeam;
    public bool resetDrone;
    public bool liftUp;
    public bool liftDown;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeActions();
    }

    void OnEnable()
    {
        // Activeer de acties zodat ze luisteren naar input
        inputAsset?.Enable();
    }

    void OnDisable()
    {
        inputAsset?.Disable();
    }

    private void InitializeActions()
    {
        if (inputAsset == null)
        {
            Debug.LogError("DroneInputManager: Geen Input Action Asset toegewezen in de Inspector!");
            return;
        }

        // Zorg dat je Action Map in Unity 'Drone' heet, of pas de string hieronder aan
        var droneMap = inputAsset.FindActionMap("Drone");

        moveAction = droneMap.FindAction("Move");
        lookAction = droneMap.FindAction("Look");
        liftAction = droneMap.FindAction("Lift"); // Maak dit een 1D Axis in je asset

        toggleLoweredAction = droneMap.FindAction("ToggleLowered");
        toggleBeamAction = droneMap.FindAction("ToggleBeam");
        resetDroneAction = droneMap.FindAction("ResetDrone");
    }

    void Update()
    {
        if (inputAsset == null) return;

        // 1. Movement (WASD / Left Stick)
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        moveX = moveInput.x;
        moveZ = moveInput.y;

        // 2. Look (Mouse Delta / Right Stick)
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        lookX = lookInput.x * lookSensitivity;
        lookY = lookInput.y * lookSensitivity;

        // 3. Lift (Space/Ctrl / Triggers)
        // Tip: Maak in je Input Action Asset een "1D Axis" binding.
        // Positive: Space, Negative: Left Ctrl
        liftY = liftAction.ReadValue<float>();

        // Update de bools voor backward compatibility met andere scripts
        liftUp = liftY > 0.1f;
        liftDown = liftY < -0.1f;

        // 4. Buttons (Triggers)
        // wasPressedThisFrame vervangt GetButtonDown / GetKeyDown
        //toggleLowered = toggleLoweredAction.wasPressedThisFrame;
        //toggleBeam = toggleBeamAction.wasPressedThisFrame;
        //resetDrone = resetDroneAction.wasPressedThisFrame;
    }
}