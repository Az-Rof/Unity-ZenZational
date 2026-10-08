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
    [Tooltip("SFX name registered in AudioController for the final boss face reveal.")]
    [SerializeField] private string faceRevealScreamSfxName = "scream";

    [Tooltip("Optional fallback clip used only when no AudioManager is available.")]
    [SerializeField] private AudioClip faceRevealScream;

    [Header("Glitch Audio")]
    [SerializeField] private string glitchSfxName = "glitch";

    [SerializeField] private Key testKey = Key.G;

    private Coroutine stopRoutine;
    private AudioSource glitchLoopSource;
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
        ApplyGlitch(mode, strengthPercent, effectDuration, true, true);
    }

    /// <summary>Plays the visual glitch without its SFX, for boss combat readability.</summary>
    public void PlayGlitchSilently(int mode, int strengthPercent, float effectDuration)
    {
        ApplyGlitch(mode, strengthPercent, effectDuration, false, true);
    }

    private void ApplyGlitch(
        int mode,
        int strengthPercent,
        float effectDuration,
        bool playGlitchAudio,
        bool stopAudioWhenSilent)
    {
        mode = Mathf.Clamp(mode, (int)GlitchMode.Analog, (int)GlitchMode.Both);
        float amount = Mathf.Clamp(strengthPercent, 0, 100) / 100f;

        if (amount <= 0f || (!playGlitchAudio && stopAudioWhenSilent))
            StopGlitchAudio();
        else if (playGlitchAudio)
            StartGlitchAudio();

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
        // Wave corruption is visual only; reserve the loop SFX for the final reveal transition.
        ApplyGlitch((int)effectMode, strengthPercent, effectDuration, false, true);
    }

    /// <summary>Sets persistent corruption using the selected Inspector mode.</summary>
    public void SetWaveGlitch(int strengthPercent)
    {
        // The wave pulse already played its one-shot SFX; this call keeps the
        // visual distortion active without triggering the sound a second time.
        // Keep an already-running loop alive while extending the visual glitch.
        ApplyGlitch((int)effectMode, strengthPercent, 0f, false, false);
    }

    /// <summary>Plays the selected glitch effect before the final reveal blackout.</summary>
    public void PlayRevealGlitch(int strengthPercent, float effectDuration)
    {
        // The transition loop begins after the final face reveal, not during this visual lead-in.
        ApplyGlitch((int)effectMode, strengthPercent, effectDuration, false, true);
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
            ApplyGlitch((int)effectMode, 85, 0.12f, true, true);
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

    public IEnumerator PlayFakePowerOff2(float blackHoldSeconds = 1.25f)
    {
        Image overlay = GetPowerOffOverlay();
        SetOverlayAlpha(overlay, 0f);

        float[] flickerAlpha = { 0.12f, 1f, 0.05f, 1f };
        foreach (float alpha in flickerAlpha)
        {
            SetOverlayAlpha(overlay, alpha);
            ApplyGlitch((int)effectMode, 85, 0.12f, true, true);
            yield return new WaitForSecondsRealtime(0.07f);
        }

        StopGlitch();
        SetOverlayAlpha(overlay, 1f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, blackHoldSeconds));
    }

    /// <summary>Blackout, silent face lunge, stare, scream, then a quick cut back.</summary>
    public IEnumerator PlayFaceReveal(
        Sprite faceSprite,
        float blackHoldSeconds = 2f,
        float faceRevealSeconds = 0.12f,
        float stareSeconds = 1f,
        float faceScale = 1.5f,
        System.Action onScream = null,
        System.Action onCutBack = null,
        bool playScream = true)
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
            if (playScream)
            {
                PlayFaceRevealScream();
                onScream?.Invoke();
            }
            else
            {
                RestoreRevealAudio();
            }

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

        // Also restore audio if the reveal sprite was not assigned.
        RestoreRevealAudio();

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

    void cutsceneGlitch()
    {
        digital.Intensity = Mathf.Lerp(digital.Intensity, 1f, Time.deltaTime * 2f);
        analog.ScanLineJitter = Mathf.Lerp(analog.ScanLineJitter, 1f, Time.deltaTime * 2f);
        analog.ColorDrift = Mathf.Lerp(analog.ColorDrift, 1f, Time.deltaTime * 2f);
        analog.VerticalJump = Mathf.Lerp(analog.VerticalJump, 1f, Time.deltaTime * 2f);
        analog.HorizontalShake = Mathf.Lerp(analog.HorizontalShake, 1f, Time.deltaTime * 2f);
        analog.HorizontalRipple = Mathf.Lerp(analog.HorizontalRipple, 1f, Time.deltaTime * 2f);
    }

    void restGlitch()
    {
        digital.Intensity = 0;
        analog.ScanLineJitter = 0;
        analog.ColorDrift = 0;
        analog.VerticalJump = 0;
        analog.HorizontalShake = 0;
        analog.HorizontalRipple = 0;
    }

    public IEnumerator PlayFaceReveal2(
       Sprite faceSprite1, Sprite faceSprite2,
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

        // The transition loop starts after the first reveal and should continue through this second reveal.
        StopGlitch(false);
        savedAudioListenerVolume = AudioListener.volume;
        audioMutedForFaceReveal = true;
        AudioListener.volume = 0f;
        blackOverlay.sprite = null;
        blackOverlay.color = Color.black;
        blackOverlay.raycastTarget = false;
        SetOverlayAlpha(blackOverlay, 1f);

        faceOverlay.sprite = faceSprite1;
        faceOverlay.preserveAspect = true;
        faceOverlay.color = Color.white;
        faceOverlay.enabled = faceSprite1 != null;
        faceRect.localScale = Vector3.one * 0.01f;
        SetOverlayAlpha(faceOverlay, 0f);

        // Remain silent while the screen is black, then reveal and lunge the face.
        if (faceSprite1 != null)
        {
            SetOverlayAlpha(faceOverlay, 1f);
            PlayFaceRevealScream();
            onScream?.Invoke();

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
        }

        int oldIntensityPercent = intensityPercent;
        intensityPercent = 100;

        for (int i = 0; i < 80; i++)
        {
            faceOverlay.sprite = (i % 2 == 0) ? faceSprite2 : faceSprite1;
            cutsceneGlitch();
            yield return new WaitForSecondsRealtime(0.05f);
        }
        intensityPercent = oldIntensityPercent;
        restGlitch();
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
        StopGlitch(true);
    }

    private void StopGlitch(bool stopAudio)
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

        if (stopAudio)
            StopGlitchAudio();
    }

    private void StartGlitchAudio()
    {
        if (glitchLoopSource != null || AudioManager.Instance == null || string.IsNullOrEmpty(glitchSfxName))
            return;

        glitchLoopSource = AudioManager.Instance.PlaySFXLoop(glitchSfxName);
    }

    /// <summary>Starts the glitch loop for the final reveal-to-boss transition.</summary>
    public void StartGlitchAudioLoop()
    {
        StartGlitchAudio();
    }

    public void SetGlitchPitch(float scl)
    {
        glitchLoopSource.pitch = 1f + scl;
    }

    /// <summary>Stops the glitch loop without changing the visual effect.</summary>
    public void StopGlitchAudioLoop()
    {
        StopGlitchAudio();
    }

    private void StopGlitchAudio()
    {
        if (glitchLoopSource == null) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopSFX(glitchLoopSource);
        else
            Destroy(glitchLoopSource.gameObject);

        glitchLoopSource = null;
    }

    private void PlayFaceRevealScream()
    {
        RestoreRevealAudio();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(faceRevealScreamSfxName);
            return;
        }

        if (faceRevealScream != null)
            AudioSource.PlayClipAtPoint(
                faceRevealScream,
                Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        else
            Debug.LogWarning(
                $"[GlitchingManager] No AudioManager is available to play '{faceRevealScreamSfxName}', and no fallback scream clip is assigned.",
                this);
    }

    private void RestoreRevealAudio()
    {
        if (!audioMutedForFaceReveal) return;

        AudioListener.volume = savedAudioListenerVolume;
        audioMutedForFaceReveal = false;
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
