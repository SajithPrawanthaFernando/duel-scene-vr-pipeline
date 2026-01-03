using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TransitionController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup fadeGroup;
    public float fadeDuration = 1f;
    public BootstrapLoader loader;

    [Header("Geo Sync")]
    public GeoSyncController geoSync;                 
    public InputActionReference toggleGeoSyncAction; 

    [Header("Optional: Player movement to disable during transition")]
    public Behaviour[] disableDuringTransition;

    [Header("Inputs")]
    public InputActionReference toggleEraAction;

    [Header("Debug")]
    public bool skipFade = false;

    private bool isTransitioning;

    void Awake()
    {
        if (fadeGroup != null) fadeGroup.alpha = 0f;
    }

    void OnEnable()
    {
        if (toggleEraAction != null)
        {
            toggleEraAction.action.Enable();
            toggleEraAction.action.performed += OnToggleEra;
        }

        if (toggleGeoSyncAction != null)
        {
            toggleGeoSyncAction.action.Enable();
            toggleGeoSyncAction.action.performed += OnToggleGeoSync;
        }
    }

    void OnDisable()
    {
        if (toggleEraAction != null)
        {
            toggleEraAction.action.performed -= OnToggleEra;
            toggleEraAction.action.Disable();
        }

        if (toggleGeoSyncAction != null)
        {
            toggleGeoSyncAction.action.performed -= OnToggleGeoSync;
            toggleGeoSyncAction.action.Disable();
        }
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            // PC fallback
            if (!isTransitioning && loader != null && !loader.IsBusy)
            {
                if (Keyboard.current.hKey.wasPressedThisFrame)
                    StartCoroutine(TransitionTo(EraScene.Historical));

                if (Keyboard.current.mKey.wasPressedThisFrame)
                    StartCoroutine(TransitionTo(EraScene.Modern));
            }

            // GeoSync toggle fallback key (G)
            if (Keyboard.current.gKey.wasPressedThisFrame)
                ToggleGeoSyncNow();
        }
    }

    void OnToggleEra(InputAction.CallbackContext ctx)
    {
        if (isTransitioning) return;
        if (loader == null || loader.IsBusy) return;

        var current = loader.CurrentEra ?? EraScene.Modern;
        var next = (current == EraScene.Modern) ? EraScene.Historical : EraScene.Modern;

        StartCoroutine(TransitionTo(next));
    }

    void OnToggleGeoSync(InputAction.CallbackContext ctx)
    {
        ToggleGeoSyncNow();
    }

    void ToggleGeoSyncNow()
    {
        if (geoSync == null)
        {
            Debug.LogWarning("GeoSync not assigned in TransitionController.");
            return;
        }

        geoSync.ToggleGeoSync();
        Debug.Log("GeoSync is now: " + (geoSync.geoSyncEnabled ? "ON" : "OFF"));
    }

    IEnumerator TransitionTo(EraScene targetEra)
    {
        if (loader == null) yield break;
        if (loader.CurrentEra.HasValue && loader.CurrentEra.Value == targetEra) yield break;

        isTransitioning = true;

        SetMovementEnabled(false);

        if (!skipFade)
        {
            yield return Fade(0f, 1f);
            yield return new WaitForEndOfFrame();
        }
        else
        {
            if (fadeGroup != null) fadeGroup.alpha = 1f;
        }

        yield return loader.SwitchTo(targetEra);

        yield return null;

        if (!skipFade)
        {
            yield return Fade(1f, 0f);
        }
        else
        {
            if (fadeGroup != null) fadeGroup.alpha = 0f;
        }

        SetMovementEnabled(true);

        isTransitioning = false;
    }

    void SetMovementEnabled(bool enabled)
    {
        if (disableDuringTransition == null) return;

        for (int i = 0; i < disableDuringTransition.Length; i++)
        {
            if (disableDuringTransition[i] != null)
                disableDuringTransition[i].enabled = enabled;
        }
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = to;
    }
}
