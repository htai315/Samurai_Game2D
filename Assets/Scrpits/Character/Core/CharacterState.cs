using UnityEngine;

public sealed class CharacterState : MonoBehaviour
{
    [Header("State")]
    public bool IsDead;
    public bool IsHurting;
    public bool IsDashing;

    [Header("Jump State")]
    public int JumpsLeft;

    [Header("Facing")]
    [Tooltip("-1 = trái, 1 = phải")]
    public float Facing = 1f;

    public void ResetJumps(int maxJumps) => JumpsLeft = Mathf.Max(0, maxJumps);
    public void ConsumeJump() => JumpsLeft = Mathf.Max(0, JumpsLeft - 1);
}
