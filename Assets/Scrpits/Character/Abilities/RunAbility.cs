using UnityEngine;

public sealed class RunAbility : MonoBehaviour
{
    [SerializeField] private CharacterConfig cfg;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private CharacterState state;

    public void FixedTick(float moveX)
    {
        if (state.IsDead || state.IsHurting || state.IsDashing) return;
        motor.SetHorizontal(moveX, cfg.moveSpeed);
    }
}
