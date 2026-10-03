using UnityEngine;

public class BuildingScroller : MonoBehaviour
{
    public float Length;
    public float ScrollSpeed;
    Vector2 pos;
    RectTransform tran;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tran = GetComponent<RectTransform>();
        pos = tran.anchoredPosition;
    }

    // Update is called once per frame
    void Update()
    {
        tran.anchoredPosition += new Vector2(-ScrollSpeed * Time.deltaTime, 0f);

        if (tran.anchoredPosition.x < Length)
        {
            tran.anchoredPosition = pos;
        }
    }
}
