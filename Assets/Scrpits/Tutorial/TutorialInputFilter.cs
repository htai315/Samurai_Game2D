using UnityEngine;

public class TutorialInputFilter : MonoBehaviour
{
    [Header("Allowed in current stage")]
    public bool allowMove;
    public bool allowJump;
    public bool allowAttack;
    public bool allowDash;

    // Helper đổi nhanh theo stage
    public void Set(bool move, bool jump, bool attack, bool dash)
    {
        allowMove = move;
        allowJump = jump;
        allowAttack = attack;
        allowDash = dash;
    }
}
