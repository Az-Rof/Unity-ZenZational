using System.Collections;
using KinoGlitch;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GlitchingManager : MonoBehaviour
{
    private enum GlitchMode
    {
        Analog = 0,
        Digital = 1,
        Both = 2
    }

    [Header("Glitch Controllers")]
    [SerializeField] private AnalogGlitchController analog;
    [SerializeField] private DigitalGlitchController digital;

    [Header("Test Settings")]
    [Tooltip("Select which glitch controller(s) to use for test and story effects.")]
    [SerializeField] private GlitchMode effectMode = GlitchMode.Analog;

    [Tooltip("Glitch strength as a percentage. 0 = off, 100 = maximum.")]
    [Range(0, 100)]
    [SerializeField] private int intensityPercent = 50;

    [Tooltip("How long the glitch stays active. Set to 0 to leave it on until StopGlitch is called.")]
    [Min(0f)]
    [SerializeField] private float duration = 1f;

    [Header("Final Boss Reveal")]
    [Tooltip("Assign a scream audio clip for the face reveal. Leave empty to keep the reveal silent.")]
    [SerializeField] private AudioClip faceRevealScream;

    [SerializeField] private Key testKey = Key.G;

    private Coroutine stopRoutine;
    private Image powerOffOverlay;
    private Image faceRevealOverlay;
    private float savedAudioListenerVolume = 1f;
    private bool audioMutedForFaceReveal;

    private void Awake()
    {
        powerOffOverlay = GetComponentInChildren<Image>(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            PlayGlitch();
    }

    [ContextMenu("Play Glitch")]
    public void PlayGlitch()
    {
        PlayGlitch((int)effectMode, intensityPercent, duration);
    }

    /// <summary>Plays a glitch pulse with parameters supplied by a gameplay sequence.</summary>
    public void PlayGlitch(int mode, int strengthPercent, float effectDuration)
    {
        mode = Mathf.Clamp(mode, (int)GlitchMode.Analog, (int)GlitchMode.Both);
        float amount = Mathf.Clamp(strengthPercent, 0, 100) / 100f;

        if (analog != null)
        {
            bool useAnalog = mode == 0 || mode == 2;
            analog.ScanLineJitter = useAnalog ? amount : 0f;
            analog.VerticalJump = useAnalog ? amount * 0.35f : 0f;
            analog.HorizontalShake = useAnalog ? amount * 0.35f : 0f;
            analog.ColorDrift = useAnalog ? amount * 0.7f : 0f;
            analog.HorizontalRipple = useAnalog ? amount * 0.35f : 0f;
        }

        if (digital != null)
            digital.Intensity = mode == 1 || mode == 2 ? amount : 0f;

        if (stopRoutine != null)
            StopCoroutine(stopRoutine);

        if (effectDuration > 0f)
            stopRoutine = StartCoroutine(StopAfterDelay(effectDuration));
    }

    /// <summary>Short story beat used when entering a more corrupted wave.</summary>
    public void PlayWaveGlitch(int strengthPercent, float effectDuration)
    {
        PlayGlitch((int)effectMode, strengthPercent, effectDuration);
    }

    /// <summary>Sets persistent corruption using the selected Inspector mode.</summary>
    public void SetWaveGlitch(int strengthPercent)
    {
        PlayGlitch((int)effectMode, strengthPercent, 0f);
    }

    /// <summary>Plays the selected glitch effect before the final reveal blackout.</summary>
    public void PlayRevealGlitch(int strengthPercent, float effectDuration)
    {
        PlayGlitch((int)effectMode, strengthPercent, effectDuration);
    }

    public void ClearWaveGlitch()
    {
        StopGlitch();
    }

    /// <summary>
    /// Briefly flickers into a fake power-off, holds on black, then restores the view.
    /// Uses unscaled time so it still runs if a menu or transition has paused gameplay.
    /// </summary>
    public IEnumerator PlayFakePowerOff(float blackHoldSeconds = 1.25f)
    {
        Image overlay = GetPowerOffOverlay();
        SetOverlayAlpha(overlay, 0f);

        float[] flickerAlpha = { 0.12f, 1f, 0.05f, 1f };
        foreach (float alpha in flickerAlpha)
        {
            SetOverlayAlpha(overlay, alpha);
            PlayGlitch((int)effectMode, 85, 0.12f);
            yield return new WaitForSecondsRealtime(0.07f);
        }

        StopGlitch();
        SetOverlayAlpha(overlay, 1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, blackHoldSeconds));

        const float fadeDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(overlay, 1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        SetOverlayAlpha(overlay, 0f);
        StopGlitch();
    }

    /// <summary>Blackout, silent face lunge, stare, scream, then a quick cut back.</summary>
    public IEnumerator PlayFaceReveal(
        Sprite faceSprite,
        float blackHoldSeconds = 2f,
        float faceRevealSeconds = 0.12f,
        float stareSeconds = 1f,
        float faceScale = 1.5f,
        System.Action onScream = null,
        System.Action onCutBack = null)
    {
        Image blackOverlay = GetPowerOffOverlay();
        Image faceOverlay = GetFaceRevealOverlay(blackOverlay);
        RectTransform faceRect = faceOverlay.rectTransform;

        StopGlitch();
        savedAudioListenerVolume = AudioListener.volume;
        audioMutedForFaceReveal = true;
        AudioListener.volume = 0f;
        blackOverlay.sprite = null;
        blackOverlay.color = Color.black;
        blackOverlay.raycastTarget = false;
        SetOverlayAlpha(blackOverlay, 1f);

        faceOverlay.sprite = faceSprite;
        faceOverlay.preserveAspect = true;
        faceOverlay.color = Color.white;
        faceOverlay.enabled = faceSprite != null;
        faceRect.localScale = Vector3.one * 0.01f;
        SetOverlayAlpha(faceOverlay, 0f);

        // Remain silent while the screen is black, then reveal and lunge the face.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, blackHoldSeconds));
        if (faceSprite != null)
        {
            SetOverlayAlpha(faceOverlay, 1f);
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, faceRevealSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                faceRect.localScale = Vector3.one * Mathf.Lerp(0.01f, Mathf.Max(0.01f, faceScale), easedT);
                yield return null;
            }
            faceRect.localScale = Vector3.one * Mathf.Max(0.01f, faceScale);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, stareSeconds));
        }

        AudioListener.volume = savedAudioListenerVolume;
        audioMutedForFaceReveal = false;
        if (faceRevealScream != null)
            AudioSource.PlayClipAtPoint(faceRevealScream, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        else
            Debug.LogWarning("[GlitchingManager] Assign Face Reveal Scream to play the final boss scream.", this);
        onScream?.Invoke();

        // Hold the face under darkness while the scream begins, then reveal the
        // boss opening attack at the cut back to gameplay.
        yield return new WaitForSecondsRealtime(0.1f);
        onCutBack?.Invoke();
        faceOverlay.enabled = false;
        faceOverlay.sprite = null;
        faceOverlay.color = Color.white;
        faceRect.localScale = Vector3.one;
        SetOverlayAlpha(blackOverlay, 0f);
    }

    [ContextMenu("Stop Glitch")]
    public void StopGlitch()
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
        }

        if (analog != null)
        {
            analog.ScanLineJitter = 0f;
            analog.VerticalJump = 0f;
            analog.HorizontalShake = 0f;
            analog.ColorDrift = 0f;
            analog.HorizontalRipple = 0f;
        }

        if (digital != null)
            digital.Intensity = 0f;
    }

    private IEnumerator StopAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        stopRoutine = null;
        StopGlitch();
    }

    private Image GetPowerOffOverlay()
    {
        if (powerOffOverlay != null) return powerOffOverlay;

        Image existingOverlay = GetComponentInChildren<Image>(true);
        if (existingOverlay != null && existingOverlay.GetComponentInParent<Canvas>() != null)
        {
            powerOffOverlay = existingOverlay;
            powerOffOverlay.color = Color.black;
            powerOffOverlay.raycastTarget = false;
            return powerOffOverlay;
        }

        GameObject overlayObject = new GameObject(
            "Glitch Power-Off Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(Image));

        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        powerOffOverlay = overlayObject.GetComponent<Image>();
        powerOffOverlay.color = Color.black;
        powerOffOverlay.raycastTarget = false;
        return powerOffOverlay;
    }

    private Image GetFaceRevealOverlay(Image blackOverlay)
    {
        if (faceRevealOverlay != null) return faceRevealOverlay;

        GameObject faceObject = new GameObject("Final Boss Face Reveal", typeof(RectTransform), typeof(Image));
        faceObject.transform.SetParent(blackOverlay.transform, false);
        faceRevealOverlay = faceObject.GetComponent<Image>();
        faceRevealOverlay.raycastTarget = false;
        faceRevealOverlay.preserveAspect = true;

        RectTransform faceRect = faceRevealOverlay.rectTransform;
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = Vector2.zero;
        faceRect.offsetMax = Vector2.zero;
        faceRect.localScale = Vector3.one * 0.01f;
        faceRevealOverlay.enabled = false;
        return faceRevealOverlay;
    }

    private static void SetOverlayAlpha(Image overlay, float alpha)
    {
        if (overlay == null) return;
        Color color = overlay.color;
        color.a = Mathf.Clamp01(alpha);
        overlay.color = color;
    }

    private void OnDisable()
    {
        if (audioMutedForFaceReveal)
        {
            AudioListener.volume = savedAudioListenerVolume;
            audioMutedForFaceReveal = false;
        }
        StopGlitch();
        SetOverlayAlpha(powerOffOverlay, 0f);
        if (faceRevealOverlay != null)
        {
            faceRevealOverlay.enabled = false;
            faceRevealOverlay.sprite = null;
            faceRevealOverlay.color = Color.white;
            faceRevealOverlay.rectTransform.localScale = Vector3.one;
        }
    }
}
