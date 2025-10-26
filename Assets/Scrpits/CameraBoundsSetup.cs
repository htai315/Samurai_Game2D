using UnityEngine;

[ExecuteInEditMode]
public class CameraBoundsSetup : MonoBehaviour
{
    public float minX = -100f;
    public float maxX = 100f;
    public float minY = -5f;
    public float maxY = 100f;

    void OnValidate()
    {
        var poly = GetComponent<PolygonCollider2D>();
        if (!poly) poly = gameObject.AddComponent<PolygonCollider2D>();

        Vector2[] points = new Vector2[]
        {
            new Vector2(minX, minY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY),
            new Vector2(minX, maxY)
        };
        poly.SetPath(0, points);
        poly.isTrigger = true;
    }
}
