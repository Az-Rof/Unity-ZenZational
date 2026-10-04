using UnityEngine;
using UnityEngine.InputSystem;

public class CustomCursor : MonoBehaviour
{
    RectTransform rt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
         rt = GetComponent<RectTransform>();

    }

    // Update is called once per frame
    void Update()
    {
        if (Time.timeScale != 0)
        {
            Cursor.visible = false;
            rt.position = Mouse.current.position.ReadValue();
        }
        else
        {
            Cursor.visible = true;
        }
    }
}
