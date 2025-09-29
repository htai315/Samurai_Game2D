using UnityEngine;

[RequireComponent(typeof(CharacterMotor2D))]
[RequireComponent(typeof(CharacterState))]
public sealed class PlayerFacade : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private CharacterConfig cfg;

    [Header("Core")]
    [SerializeField] private CharacterMotor2D motor;
    [SerializeField] private CharacterState state;
    [SerializeField] private CharacterEvents eventsBus;

    [Header("Abilities")]
    [SerializeField] private RunAbility run;
    [SerializeField] private JumpAbility jump;
    [SerializeField] private DashAbility dash;

    [Header("IO")]
    [SerializeField] private InputReader input;

    private bool prevGrounded;

    void Reset()
    {
        motor = GetComponent<CharacterMotor2D>();
        state = GetComponent<CharacterState>();
        eventsBus = GetComponent<CharacterEvents>();
        input = GetComponent<InputReader>();
    }

    void Start()
    {
        state.ResetJumps(cfg.maxJumps);
        state.Facing = 1f;
        prevGrounded = false;
    }

    void Update()
    {
        // Đọc input mỗi frame
        input.Read();

        // Cập nhật hướng nhìn khi không dash
        if (!state.IsDashing && Mathf.Abs(input.MoveX) > 0.01f)
            state.Facing = input.MoveX > 0 ? 1f : -1f;

        // Thả phím ngang -> dừng ngay (giống code cũ)
        if (!state.IsDashing && input.ReleasedHorizontal)
            motor.StopImmediatelyX();

        // Xử lý ý định hành động
        if (input.DashPressed)
            dash.TryStart(state.Facing);

        if (input.JumpPressed)
            jump.TryJump();
    }

    void FixedUpdate()
    {
        // Ground check đầu FixedUpdate
        prevGrounded = motor.Grounded;
        motor.TickGrounded();

        if (motor.Grounded && !prevGrounded)
        {
            state.ResetJumps(cfg.maxJumps);
            eventsBus?.InvokeLanded();
        }

        // Vật lý dash ưu tiên trước
        dash.FixedTick();

        // Nếu không dash thì chạy thường
        if (!state.IsDashing)
            run.FixedTick(input.MoveX);
    }
}
