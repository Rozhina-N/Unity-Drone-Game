using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LineRenderer))]
public class PlayerDrone : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float liftSpeed = 3f;
    public float acceleration = 5f;
    private bool hoverMode = false;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera;
    private float pitch;

    [Header("Lowering Settings")]
    public float loweredHeightOffset = 2f;
    public float loweringSpeed = 2f;
    private bool isLowered = false;
    private float originalY;

    [Header("Tractor Beam")]
    public float beamLength = 2f;
    public Color beamColor = Color.cyan;
    private bool beamActive = false;
    private LineRenderer beamLine;
    private Transform beamAnchor;
    private Items attachedItem;

    [Header("Crash Settings")]
    public float crashRotationThreshold = 60f;
    public float crashFallSpeed = 2f;
    public float crashResetDelay = 2f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool liftUp;
    private bool liftDown;
    private Vector3 currentVelocity;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool isCrashed = false;
    private float crashTimer;
    public float maxLookUpAngle = 55f;
    public float maxLookDownAngle = 90f;

    private DroneControls controls;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;

        controls = new DroneControls();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();

        controls.Player.BeamToggle.performed += ctx => ToggleBeam();
        controls.Player.LowerToggle.performed += ctx => ToggleLowering();
        controls.Player.Reset.performed += ctx => StartCoroutine(SmoothReset());

        controls.Player.HoverToggle.performed += ctx => hoverMode = !hoverMode;


        controls.Player.LiftUp.started += ctx => liftUp = true;
        controls.Player.LiftUp.canceled += ctx => liftUp = false;

        controls.Player.LiftDown.started += ctx => liftDown = true;
        controls.Player.LiftDown.canceled += ctx => liftDown = false;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Start()
    {
        controls.Disable();
        
        startPosition = transform.position;
        startRotation = transform.rotation;
        originalY = startPosition.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        beamLine = GetComponent<LineRenderer>();
        beamLine.positionCount = 2;
        beamLine.startWidth = 0.05f;
        beamLine.endWidth = 0.05f;
        beamLine.material = new Material(Shader.Find("Sprites/Default"));
        beamLine.startColor = beamColor;
        beamLine.endColor = beamColor;
        beamLine.enabled = false;

        beamAnchor = new GameObject("BeamAnchor").transform;
        beamAnchor.parent = transform;
        beamAnchor.localPosition = new Vector3(0, -beamLength, 0);
    }

    private void Update()
    {
        if (controls == null || !controls.asset.enabled)
            return;
        
        if (!isCrashed)
        {
            HandleLowering();
            MovePlayer();
            RotateCamera();
            HandleBeam();
            CheckForCrash();
        }
        else
        {
            CrashFall();
        }
    }

    Vector3 liftVelocity;
    float verticalSmoothing = 4f;

    void MovePlayer()
    {
        if (hoverMode)
        {
            rb.linearVelocity = Vector3.zero;
            ApplyTiltVisual(Vector3.zero);
            return;
        }


        // Calculate horizontal input direction
        Vector3 moveDir = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 targetHorizontal = moveDir * moveSpeed;
        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 smoothHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontal, Time.fixedDeltaTime * acceleration);

        // Calculate vertical (lift) component
        float targetLift = 0f;
        if (liftUp) targetLift += liftSpeed;
        if (liftDown) targetLift -= liftSpeed;
        float smoothLift = Mathf.Lerp(rb.linearVelocity.y, targetLift, Time.fixedDeltaTime * verticalSmoothing);

        // Apply final velocity
        rb.linearVelocity = new Vector3(smoothHorizontal.x, smoothLift, smoothHorizontal.z);

        // Optional: tilt drone for realism
        ApplyTiltVisual(moveDir);
    }

    void ApplyTiltVisual(Vector3 direction)
    {
        if (direction == Vector3.zero) return;

        float tiltAmountX = -moveInput.y * 10f; // Forward/Back
        float tiltAmountZ = moveInput.x * 10f;  // Left/Right

        Quaternion targetTilt = Quaternion.Euler(tiltAmountX, transform.eulerAngles.y, tiltAmountZ);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetTilt, Time.deltaTime * 5f);
    }


    void RotateCamera()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookUpAngle, maxLookDownAngle); // Correct angle clamping

        Quaternion targetCamRot = Quaternion.Euler(pitch, 0f, 0f);
        playerCamera.localRotation = Quaternion.Slerp(playerCamera.localRotation, targetCamRot, Time.deltaTime * 10f);

        Quaternion targetRot = Quaternion.Euler(0f, transform.eulerAngles.y + mouseX, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
    }


    void ToggleLowering() => isLowered = !isLowered;

    void HandleLowering()
    {
        float targetY = isLowered ? loweredHeightOffset : originalY;
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * loweringSpeed);
        rb.MovePosition(pos);
    }

    void ToggleBeam()
    {
        beamActive = !beamActive;
        beamLine.enabled = beamActive;

        if (!beamActive && attachedItem != null)
        {
            attachedItem.DetachFromBeam();
            attachedItem = null;
        }
    }

    void HandleBeam()
    {
        if (beamActive)
        {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * beamLength;

            beamLine.SetPosition(0, start);
            beamLine.SetPosition(1, end);

            beamAnchor.localPosition = new Vector3(0, -beamLength, 0);

            if (attachedItem == null)
            {
                Collider[] hits = Physics.OverlapSphere(end, 0.5f);
                foreach (Collider col in hits)
                {
                    Items item = col.GetComponent<Items>();
                    if (item != null && item.canBeAttracted)
                    {
                        attachedItem = item;
                        item.AttachToBeam(beamAnchor);
                        break;
                    }
                }
            }
        }
    }

    void CheckForCrash()
    {
        float tilt = Vector3.Angle(Vector3.up, transform.up);

        if (tilt > crashRotationThreshold)
        {
            isCrashed = true;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void CrashFall()
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = new Vector3(currentPos.x, 0f, currentPos.z);
        Vector3 newPos = Vector3.MoveTowards(currentPos, targetPos, crashFallSpeed * Time.deltaTime);
        rb.MovePosition(newPos);

        crashTimer += Time.deltaTime;
        if (crashTimer >= crashResetDelay || Vector3.Distance(newPos, targetPos) < 0.05f)
        {
            StartCoroutine(SmoothReset());
        }
    }

    System.Collections.IEnumerator SmoothReset()
    {
        Vector3 start = transform.position;
        Quaternion fromRot = transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, startPosition, t);
            transform.rotation = Quaternion.Slerp(fromRot, startRotation, t);
            yield return null;
        }

        FinalizeReset();
    }

    void FinalizeReset()
    {
        isCrashed = false;
        crashTimer = 0f;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        pitch = 0f;
    }
}
