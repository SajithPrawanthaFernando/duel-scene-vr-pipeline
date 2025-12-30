using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
public class DesktopXRMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    [Header("Look Settings")]
    public float lookSpeed = 20f;
    public Transform yawRoot;
    public Transform pitchRoot;

    private CharacterController controller;
    private float yaw;
    private float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (yawRoot == null) yawRoot = transform;
        if (pitchRoot == null && Camera.main != null)
            pitchRoot = Camera.main.transform.parent;

        yawRoot.localRotation = Quaternion.identity;
        pitchRoot.localRotation = Quaternion.identity;

        yaw = yawRoot.localEulerAngles.y;
        pitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

   void Update()
    {

    if (XRSettings.isDeviceActive)
    return; 

        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;

        // ---------- 1. KEYBOARD ----------
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) moveInput.y += 1;
            if (kb.sKey.isPressed) moveInput.y -= 1;
            if (kb.aKey.isPressed) moveInput.x -= 1;
            if (kb.dKey.isPressed) moveInput.x += 1;
        }

        var mouse = Mouse.current;
        if (mouse != null)
            lookInput += mouse.delta.ReadValue();

        // ---------- 2. GAMEPAD CONTROLLER ----------
        var gp = Gamepad.current;
        if (gp != null)
        {
            // --- A. FACE BUTTONS ---
            // Y=Up, A=Down, B=Right, X=Left
            var btnY = gp.GetChildControl<ButtonControl>("button5");   // Forward
            var btnA = gp.GetChildControl<ButtonControl>("trigger");   // Back
            var btnX = gp.GetChildControl<ButtonControl>("button4");   // Left
            var btnB = gp.GetChildControl<ButtonControl>("button2");   // Right

            if (btnY != null && btnY.isPressed) moveInput.y += 1;
            if (btnA != null && btnA.isPressed) moveInput.y -= 1;
            if (btnX != null && btnX.isPressed) moveInput.x -= 1;
            if (btnB != null && btnB.isPressed) moveInput.x += 1;

            // --- B. ANALOG STICK  ---
            if (gp.leftStick != null)
                moveInput += gp.leftStick.ReadValue();
            
            // --- C. DIGITAL STICK FALLBACK  ---
        
            var sUp    = gp.GetChildControl<ButtonControl>("stick/up");
            var sDown  = gp.GetChildControl<ButtonControl>("stick/down");
            var sLeft  = gp.GetChildControl<ButtonControl>("stick/left");
            var sRight = gp.GetChildControl<ButtonControl>("stick/right");

            if (sUp != null    && sUp.isPressed)    moveInput.y += 1;
            if (sDown != null  && sDown.isPressed)  moveInput.y -= 1;
            if (sLeft != null  && sLeft.isPressed)  moveInput.x -= 1;
            if (sRight != null && sRight.isPressed) moveInput.x += 1;

            // --- D. LOOK (D-Pad / Hat) ---
            if (gp.dpad != null)
            {
                if (gp.dpad.left.isPressed)  lookInput.x -= 30f;
                if (gp.dpad.right.isPressed) lookInput.x += 30f;
                if (gp.dpad.up.isPressed)    lookInput.y -= 30f;
                if (gp.dpad.down.isPressed)  lookInput.y += 30f;
            }
        }

        // ---------- APPLY MOVEMENT ----------
        // Clamp to prevent double speed if using both Stick + Buttons
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        Vector3 forward = new Vector3(yawRoot.forward.x, 0, yawRoot.forward.z).normalized;
        Vector3 right   = new Vector3(yawRoot.right.x,   0, yawRoot.right.z).normalized;

        // Apply Movement
        Vector3 move = (forward * moveInput.y + right * moveInput.x) * moveSpeed;
        
        // Manual Gravity
        move.y = -9.81f; 

        controller.Move(move * Time.deltaTime);

        // ---------- APPLY LOOK ----------
        float maxDelta = 100f;
        lookInput.x = Mathf.Clamp(lookInput.x, -maxDelta, maxDelta);
        lookInput.y = Mathf.Clamp(lookInput.y, -maxDelta, maxDelta);

        yaw   += lookInput.x * lookSpeed * Time.deltaTime;
        pitch -= lookInput.y * lookSpeed * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, -80f, 80f);

        yawRoot.localRotation   = Quaternion.Euler(0f, yaw, 0f);
        pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}