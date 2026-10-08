using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UNSEENCUTSCENEDEATH : MonoBehaviour
{
    public charStats sts;
    public float duration = 10f;
    GlitchingManager gm;
    playerController pc;

    // Moving subscription to OnEnable/OnDisable is safest for Unity scene changes
    private void OnEnable()
    {
        if (sts != null)
        {
            // FIX: The function name no longer needs parentheses here when subscribing
            sts.OnCharacterDied += Dine;
        }
    }

    private void OnDisable()
    {
        if (sts != null)
        {
            sts.OnCharacterDied -= Dine;
        }
    }

    // FIX: Added 'GameObject deadCharacter' to match the signature of the event
    void Dine(GameObject deadCharacter)
    {
        gm = GameObject.Find("Main Camera").GetComponent<GlitchingManager>();
        pc = gm.transform.parent.GetComponent<playerController>();
        Destroy(GameObject.Find("Cursor"));
        StartCoroutine(DeathScene());
    }

    IEnumerator DeathScene()
    {
         pc.enabled = false;
        Destroy(pc.CurrentGun);
        AudioManager.Instance.PauseMusic();
        yield return new WaitForSecondsRealtime(2f);

        for (int i = 0; i < 101; i++)
        {
            gm.StartGlitchAudioLoop();
            gm.SetGlitchPitch((float)i / 100);
            Time.timeScale = Mathf.Clamp01(Time.timeScale - 0.01f);
            gm.SetWaveGlitch(i);
            yield return new WaitForSecondsRealtime(duration/100);
        }

        // Ensure it perfectly hits 0 at the end
        Time.timeScale = 0f;
        gm.SetWaveGlitch(100);

        // Give the player a brief moment at absolute freeze before loading if desired,
        // otherwise it will load immediately.
        yield return new WaitForSecondsRealtime(0.5f);

        Time.timeScale = 1;
        gm.StopGlitchAudioLoop();
        yield return gm.PlayFakePowerOff2(2.5f);
        SceneManager.LoadScene(2);
    }

}
