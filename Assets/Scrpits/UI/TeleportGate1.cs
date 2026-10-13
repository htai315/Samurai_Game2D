using UnityEngine;

// The assigned canvas enables the shared black fade; it is never moved or modified.
[RequireComponent(typeof(Collider2D))]
public class TeleportGate1 : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string targetScene = "Level2";
    [SerializeField] private string targetSpawnId = "A";
    [Header("Transition (optional)")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float delayBeforeTeleport = 0.2f;

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!SceneTransition.CanEnterGate(other)) return;
        SceneTransition.TryLoad(targetScene,
            () => SpawnLocator.NextSpawnId = targetSpawnId,
            fadeDuration: fadeCanvas ? fadeDuration : 0f,
            delay: fadeCanvas ? 0f : delayBeforeTeleport);
    }
}
