using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class AnimatorSync : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private CharacterState state;
    [SerializeField] private CharacterEvents eventsBus;

    static readonly int IsRunning = Animator.StringToHash("isRunning");
    static readonly int IsJumping = Animator.StringToHash("isJumping");
    static readonly int IsDashing = Animator.StringToHash("isDashing");
    static readonly int DoJump = Animator.StringToHash("doJump");
    static readonly int DoDash = Animator.StringToHash("doDash");
    static readonly int DoHurt = Animator.StringToHash("doHurt");
    static readonly int DoDie = Animator.StringToHash("doDie");

    void Reset()
    {
        anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (eventsBus == null) return;
        eventsBus.Jumped += OnJumped;
        eventsBus.DashStarted += OnDashStarted;
        eventsBus.Hurt += OnHurt;
        eventsBus.Died += OnDied;
    }

    void OnDisable()
    {
        if (eventsBus == null) return;
        eventsBus.Jumped -= OnJumped;
        eventsBus.DashStarted -= OnDashStarted;
        eventsBus.Hurt -= OnHurt;
        eventsBus.Died -= OnDied;
    }

    void Update()
    {
        anim.SetBool(IsDashing, state.IsDashing);
        // Dựa vào vận tốc thật để tránh lệch nhịp
        float vx = motor.RB.linearVelocity.x;
        anim.SetBool(IsRunning, !state.IsDashing && Mathf.Abs(vx) > 0.01f);
        anim.SetBool(IsJumping, !motor.Grounded);
    }

    void OnJumped() => anim.SetTrigger(DoJump);
    void OnDashStarted() => anim.SetTrigger(DoDash);
    void OnHurt() => anim.SetTrigger(DoHurt);
    void OnDied() => anim.SetTrigger(DoDie);
}
