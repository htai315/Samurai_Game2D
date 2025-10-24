using UnityEngine;

[DisallowMultipleComponent]
public class BoundsLimiter : MonoBehaviour
{
    [Header("Assigned at runtime")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("Tuning (optional)")]
    public float epsilon = 0.05f;      // biên an toàn
    public float halfWidth = -1f;      // -1 = tự đo theo Collider2D
    public bool hardStopXVelocity = true;

    Rigidbody2D rb;
    Collider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (halfWidth < 0f && col != null)
            halfWidth = col.bounds.extents.x;  // tự đo nửa bề ngang từ collider
    }

    public void SetBounds(Transform left, Transform right)
    {
        leftBound = left;
        rightBound = right;
    }

    void LateUpdate()
    {
        if (!leftBound || !rightBound) return;
        var p = transform.position;

        float minX = leftBound.position.x + halfWidth + epsilon;
        float maxX = rightBound.position.x - halfWidth - epsilon;

        float clampedX = Mathf.Clamp(p.x, minX, maxX);
        if (Mathf.Abs(clampedX - p.x) > 1e-4f)
        {
            transform.position = new Vector3(clampedX, p.y, p.z);
            if (hardStopXVelocity && rb) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }
}

