using UnityEngine;

public sealed class InputReader : MonoBehaviour
{
    public float MoveX { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool ReleasedHorizontal { get; private set; }

    public void Read()
    {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        MoveX = (left == right) ? 0f : (right ? 1f : -1f);

        JumpPressed = Input.GetButtonDown("Jump");     // mapping "Jump" mặc định = Space
        DashPressed = Input.GetKeyDown(KeyCode.L);     // giống code cũ

        ReleasedHorizontal =
               Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.LeftArrow)
            || Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.RightArrow);
    }
}
