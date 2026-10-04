using System;
using System.Collections;
using UnityEngine;

public class ExplosiveBarrel : MonoBehaviour
{

    public Vector2 landingPos;
    [SerializeField] private ParticleSystem efx;
    [SerializeField] private GameObject circ;
    charStats stats;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stats = GameObject.Find("Player").GetComponent<charStats>();
        StartCoroutine(Fling());
    }

    IEnumerator Fling()
    {
        while (landingPos == Vector2.zero)
        {
            yield return null; // Wait until landingPos is set
        }
        Vector2 DefaultSize = transform.localScale;
        Vector2 HeightSize = transform.localScale*2;
        Vector2 startPos = transform.position;
        float duration = Vector2.Distance(transform.position, landingPos) / 10f; // Adjust the divisor for desired fling speed
        float elapsedTime = 0f;
        GameObject aC = Instantiate(circ, landingPos, Quaternion.identity);
        SpriteRenderer acSR = aC.GetComponent<SpriteRenderer>();
        while (elapsedTime < duration)
        {
            transform.Rotate(Vector3.forward, 360 * Time.deltaTime); // Rotate the barrel
            transform.localScale = elapsedTime < (duration/2) ? Vector2.Lerp(transform.localScale, HeightSize, Time.deltaTime) : Vector2.Lerp(transform.localScale, DefaultSize, Time.deltaTime);
            acSR.color = new Color(acSR.color.r, acSR.color.g, acSR.color.b, Mathf.Lerp(0f, 0.5f, elapsedTime / duration));
            transform.position = Vector2.Lerp(startPos, landingPos, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        Destroy(aC);
        GetComponent<SpriteRenderer>().enabled = false; // Hide the barrel sprite>
        efx.Play();
        if (Vector2.Distance(stats.transform.position, landingPos) < circ.transform.localScale.y/2)
        {
            stats.TakeDamage(20); // Adjust damage value as needed
        }
        Destroy(gameObject, efx.main.duration); // Destroy the barrel after the particle effect duration)
    }
}
