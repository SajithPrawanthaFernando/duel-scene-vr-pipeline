using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class GamepadMappingDebugger : MonoBehaviour
{
    void Update()
    {
        // Print all connected input devices once
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            Debug.Log("---- INPUT DEVICES ----");
            foreach (var d in InputSystem.devices)
                Debug.Log($"Device: {d.displayName} | {d.layout} | {d.description.interfaceName}");
        }

        // Log any button press from ANY device 
        foreach (var device in InputSystem.devices)
        {
            foreach (var c in device.allControls)
            {
                if (c is ButtonControl b && b.wasPressedThisFrame)
                    Debug.Log($"[{device.displayName}] Pressed: {b.path}");
            }
        }

        // log stick/axis values 
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            foreach (var d in InputSystem.devices)
            {
                foreach (var c in d.allControls)
                {
                    if (c is AxisControl a)
                    {
                        float v = a.ReadValue();
                        if (Mathf.Abs(v) > 0.2f)
                            Debug.Log($"[{d.displayName}] Axis: {a.path} = {v}");
                    }
                }
            }
        }
    }
}
