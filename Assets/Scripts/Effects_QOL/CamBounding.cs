using UnityEngine;

public class CamBounding : MonoBehaviour
{
    public Vector2 mapBounds;

    [Tooltip("Size of the camera's visible area")]
    public Camera cam;

    private void Update()
    {
        transform.parent.position = new Vector3(
            Mathf.Clamp(transform.parent.position.x, -mapBounds.x / 2f, mapBounds.x / 2f),
            Mathf.Clamp(transform.parent.position.y, -mapBounds.y / 2f, mapBounds.y / 2f),
            transform.parent.position.z
        );
    }

    void LateUpdate()
    {
        Vector3 pos = transform.parent.position - Vector3.forward * 10f;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // Map boundaries
        float left = -mapBounds.x / 2f;
        float right = mapBounds.x / 2f;
        float bottom = -mapBounds.y / 2f;
        float top = mapBounds.y / 2f;

        // Camera boundaries
        pos.x = Mathf.Clamp(
            pos.x,
            left + halfWidth,
            right - halfWidth
        );

        pos.y = Mathf.Clamp(
            pos.y,
            bottom + halfHeight,
            top - halfHeight
        );

        transform.position = pos;
    }
}