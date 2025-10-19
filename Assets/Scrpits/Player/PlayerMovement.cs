using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;
    private float xInput;

    // Animator hashes
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsJumping = Animator.StringToHash("isJumping");
    private static readonly int IsDashing = Animator.StringToHash("isDashing");

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
    }

    public void HandleInput()
    {
        ReadInput();
        HandleFlip();
        HandleStopOnRelease();
    }

    private void ReadInput()
    {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        xInput = (left == right) ? 0f : (right ? 1f : -1f);
    }

    private void HandleFlip()
    {
        // KHÔNG đổi mặt khi đang dash hoặc đang attack
        if (controller.IsDashing || controller.IsAttacking)
            return;

        if (xInput != 0f)
        {
            transform.localScale = new Vector3(xInput > 0 ? 1 : -1, 1, 1);
        }
    }

    private void HandleStopOnRelease()
    {
        // KHÔNG dừng cưỡng bức khi đang attack/dash
        if (controller.IsDashing || controller.IsAttacking)
            return;

        if (Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.LeftArrow) ||
            Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.RightArrow))
        {
            var v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
            xInput = 0f;
        }
    }

    public void FixedUpdateMovement()
    {
        var v = rb.linearVelocity;
        v.x = xInput * moveSpeed;
        rb.linearVelocity = v;
    }

    public void ApplyAnimator()
    {
        anim.SetBool(IsDashing, controller.IsDashing);

        float vx = rb.linearVelocity.x;
        // KHÔNG chạy khi đang attack hoặc dash
        bool isRunning = !controller.IsDashing && !controller.IsAttacking && Mathf.Abs(vx) > 0.01f;
        anim.SetBool(IsRunning, isRunning);
    }

    public float CurrentSpeed => rb.linearVelocity.x;
}