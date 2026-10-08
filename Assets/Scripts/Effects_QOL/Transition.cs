using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Transition : MonoBehaviour
{

    [Header("Transition Settings")]
    public float duration = 2f;
    public Image img;
    public void inFade()
    {
        StartCoroutine(Fade(true));
    }
    public void outFade()
    {
        StartCoroutine(Fade(false));
    }

    IEnumerator Fade(bool visible)
    {
        if (visible)
        {
            img.fillAmount = 0;
            for (int i = 0; i < 100; i++)
            {
                print("in" + img.fillAmount);
                img.fillAmount = ((float)i / 100);
                yield return new WaitForSecondsRealtime(duration / 100);
            }
            img.fillAmount = 1;
        }
        else
        {
            img.fillAmount = 1;
            for (int i = 0; i < 100; i++)
            {
                img.fillAmount = (1f-((float)i / 100));
                yield return new WaitForSecondsRealtime(duration / 100);
            }
            img.fillAmount = 0;
        }
    }
}
