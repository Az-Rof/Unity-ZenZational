using UnityEngine;

public class UNSEENFACE : MonoBehaviour
{
    Quaternion rot;
    Vector2 pos;
    public float intensity = 0.125f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rot = transform.rotation;
        pos = transform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = rot;
        float rX = Random.Range(-intensity, intensity);
        float rY = Random.Range(-intensity, intensity);
        transform.localPosition = pos + new Vector2(rX, rY); 
    }
}
