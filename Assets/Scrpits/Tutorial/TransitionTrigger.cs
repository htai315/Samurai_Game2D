using UnityEngine;

public class TransitionTrigger : MonoBehaviour
{
    public TutorialManager tutorialManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // (Tuỳ chọn) chỉ cho qua nếu người chơi đang đi sang phải
        var rb = other.attachedRigidbody;
        if (rb && rb.linearVelocity.x < 0.2f) return;

        tutorialManager?.TryAdvanceAtRightEdge();
    }
}
