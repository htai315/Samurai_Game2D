using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class CharacterMotor2D : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    public bool Grounded { get; private set; }
    public Rigidbody2D RB { get; private set; }
    public Collider2D Col { get; private set; }

    void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        Col = GetComponent<Collider2D>();

        RB.freezeRotation = true;
        RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void TickGrounded()
    {
        if (!groundCheck) return;
        Grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
    }

    public void SetHorizontal(float x, float speed)
    {
        var v = RB.linearVelocity;
        v.x = x * speed;
        RB.linearVelocity = v;
    }

    public void StopImmediatelyX()
    {
        var v = RB.linearVelocity;
        v.x = 0f;
        RB.linearVelocity = v;
    }

    public void Jump(float force)
    {
        var v = RB.linearVelocity;
        v.y = force;
        RB.linearVelocity = v;
    }

    // Cast theo trục X để chống xuyên khi dash
    public bool CastHorizontal(float dir, float dist, float skin, ContactFilter2D filter, RaycastHit2D[] hits, out float allowed)
    {
        int n = RB.Cast(new Vector2(dir, 0f), filter, hits, dist + skin);
        if (n > 0)
        {
            allowed = Mathf.Max(0f, hits[0].distance - skin);
            return true; // có va chạm
        }
        allowed = dist;
        return false; // không va chạm
    }

    public void MoveHorizontal(float delta)
    {
        RB.MovePosition(RB.position + new Vector2(delta, 0f));
    }

    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
