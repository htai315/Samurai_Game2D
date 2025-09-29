using UnityEngine;

public sealed class JumpAbility : MonoBehaviour
{
    [SerializeField] private CharacterConfig cfg;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private CharacterState state;
    [SerializeField] private CharacterEvents eventsBus;

    public bool TryJump()
    {
        if (state.IsDead || state.IsHurting || state.IsDashing) return false;

        bool canJump = motor.Grounded || state.JumpsLeft > 0;
        if (!canJump) return false;

        motor.Jump(cfg.jumpForce);

        if (motor.Grounded)
            state.JumpsLeft = Mathf.Max(0, cfg.maxJumps - 1);
        else
            state.ConsumeJump();

        eventsBus?.InvokeJumped();
        return true;
    }
}
