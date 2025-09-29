using UnityEngine;

public sealed class FacingFlipper : MonoBehaviour
{
    [SerializeField] private CharacterState state;

    void LateUpdate()
    {
        if (state.IsDashing) return;
        float face = state.Facing >= 0 ? 1f : -1f;
        var s = transform.localScale;
        s.x = face;
        transform.localScale = s;
    }
}
