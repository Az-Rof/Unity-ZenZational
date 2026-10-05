using System.Collections;
using KinoGlitch;
using UnityEngine;
using UnityEngine.InputSystem;

public class GlitchingManager : MonoBehaviour
{
    [Header("Glitch Controllers")]
    [SerializeField] private AnalogGlitchController analog;
    [SerializeField] private DigitalGlitchController digital;

    [Header("Test Settings")]
    [Tooltip("0 = Analog, 1 = Digital, 2 = Both")]
    [Range(0, 2)]
    [SerializeField] private int effectMode = 0;

    [Tooltip("Glitch strength as a percentage. 0 = off, 100 = maximum.")]
    [Range(0, 100)]
    [SerializeField] private int intensityPercent = 50;

    [Tooltip("How long the glitch stays active. Set to 0 to leave it on until StopGlitch is called.")]
    [Min(0f)]
    [SerializeField] private float duration = 1f;

    [SerializeField] private Key testKey = Key.G;

    private Coroutine stopRoutine;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            PlayGlitch();
    }

    [ContextMenu("Play Glitch")]
    public void PlayGlitch()
    {
        float amount = Mathf.Clamp(intensityPercent, 0, 100) / 100f;

        if (analog != null)
        {
            
            bool useAnalog = effectMode == 0 || effectMode == 2;
            analog.ScanLineJitter = useAnalog ? amount : 0f;
            analog.VerticalJump = useAnalog ? amount * 0.35f : 0f;
            analog.HorizontalShake = useAnalog ? amount * 0.35f : 0f;
            analog.ColorDrift = useAnalog ? amount * 0.7f : 0f;
            analog.HorizontalRipple = useAnalog ? amount * 0.35f : 0f;           
        }

        if (digital != null)
            digital.Intensity = effectMode == 1 || effectMode == 2 ? amount : 0f;

        if (stopRoutine != null)
            StopCoroutine(stopRoutine);

        if (duration > 0f)
            stopRoutine = StartCoroutine(StopAfterDelay());
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

    private IEnumerator StopAfterDelay()
    {
        yield return new WaitForSeconds(duration);
        stopRoutine = null;
        StopGlitch();
    }

    private void OnDisable()
    {
        StopGlitch();
    }
}
