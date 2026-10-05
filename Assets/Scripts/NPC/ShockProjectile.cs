using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShockProjectile : MonoBehaviour
{
    bool hit = false;
    public ParticleSystem hitEffect;
    public float speed = 5f;
    public LayerMask pMask;

    private void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
        if (!hit) {
            Collider2D player = Physics2D.OverlapCircle(
            transform.position,
            5,
            pMask
        );
            if (player != null)
            {
                charStats stats = player.GetComponentInParent<charStats>();
                if (stats != null)
                {
                    stats.TakeDamage(10);
                    hit = true;
                }
            }
    }
    }
}
