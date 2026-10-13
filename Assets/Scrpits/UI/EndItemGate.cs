using System.Collections;
using UnityEngine;

public class EndItemGate : MonoBehaviour
{
    [Header("Ending")]
    [SerializeField] private string endingSceneName = "Ending";
    [SerializeField] private bool destroyPersistentRoot = true;
    [Header("Transition (optional)")]
    [SerializeField] private Animator fadeAnimator;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float smallDelay = 0.15f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!SceneTransition.CanEnterGate(other)) return;
        SceneTransition.TryLoad(endingSceneName,
            () => { if (destroyPersistentRoot) SaveRuntime.Clear(); else SpawnLocator.NextSpawnId = "A"; }, destroyPersistentRoot,
            fadeAnimator ? fadeDuration : 0f, fadeAnimator ? 0f : smallDelay);
    }
}
