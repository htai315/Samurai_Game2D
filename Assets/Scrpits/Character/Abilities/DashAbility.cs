using UnityEngine;

public sealed class DashAbility : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterConfig cfg;
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private CharacterState state;
    [SerializeField] private CharacterEvents eventsBus;

    [Header("Collision")]
    [SerializeField] private LayerMask dashBlockerMask;

    private ContactFilter2D dashFilter;
    private readonly RaycastHit2D[] buf = new RaycastHit2D[4];
    private float remain, speed, dir;

    void Awake()
    {
        dashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = dashBlockerMask,
            useTriggers = false
        };
    }

    public bool TryStart(float facing)
    {
        if (state.IsDead || state.IsHurting || state.IsDashing) return false;

        dir = Mathf.Sign(facing) == 0 ? 1f : Mathf.Sign(facing);
        speed = cfg.dashDistance / Mathf.Max(0.01f, cfg.dashDuration);
        remain = cfg.dashDistance;

        // Ngắt chuyển động thường ở frame đầu (giống code cũ)
        motor.StopImmediatelyX();

        state.IsDashing = true;
        eventsBus?.InvokeDashStarted();
        return true;
    }

    public void FixedTick()
    {
        if (!state.IsDashing) return;

        float step = speed * Time.fixedDeltaTime;
        float move = Mathf.Min(step, remain);

        bool hit = motor.CastHorizontal(dir, move, cfg.dashSkin, dashFilter, buf, out float allowed);

        if (hit && allowed <= 0f)
        {
            End();
            return;
        }

        float delta = (hit && allowed < move) ? allowed : move;
        motor.MoveHorizontal(dir * delta);

        remain -= move;
        if (remain <= 0f) End();
    }

    private void End()
    {
        state.IsDashing = false;
        eventsBus?.InvokeDashEnded();
    }
}
