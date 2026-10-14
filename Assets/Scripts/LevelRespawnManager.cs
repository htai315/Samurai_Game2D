using System.Collections;
using UnityEngine;

public class LevelRespawnManager : MonoBehaviour
{
    public static LevelRespawnManager Instance { get; private set; }
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Animator fadeAnimator;
    [SerializeField] private float fadeDuration = 1f;
    public bool IsRespawning { get; private set; }
    public Transform CurrentCheckpoint => respawnPoint;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        IsRespawning = false;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RespawnPlayer() => BeginRespawn(true);
    public void RespawnPlayerSkipRestore() => BeginRespawn(false);
    public void SetCheckpoint(Transform point) { if (point) respawnPoint = point; }

    private void BeginRespawn(bool restoreHealth)
    {
        if (IsRespawning || (RunSession.Instance && RunSession.Instance.IsTransitioningScene)) return;
        if (!GameObject.FindGameObjectWithTag("Player") || !respawnPoint)
        {
            Debug.LogWarning("[Respawn] Thiếu nhân vật hoặc checkpoint; không thay đổi trạng thái.");
            return;
        }
        IsRespawning = true;
        StartCoroutine(RespawnRoutine(restoreHealth));
    }

    private IEnumerator RespawnRoutine(bool restoreHealth)
    {
        if (fadeAnimator)
        {
            fadeAnimator.SetTrigger("FadeOut");
            yield return new WaitForSeconds(fadeDuration);
        }
        if (RunSession.Instance && RunSession.Instance.IsTransitioningScene)
        {
            IsRespawning = false;
            yield break;
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player || !respawnPoint) { IsRespawning = false; yield break; }

        player.transform.position = respawnPoint.position;
        var health = player.GetComponent<PlayerHealth>();
        if (restoreHealth && health) health.SetCurrentHealth(health.MaxHealth);
        player.GetComponent<PlayerController1>()?.ResetForRespawn();
        Physics2D.SyncTransforms();
        if (fadeAnimator)
        {
            foreach (var parameter in fadeAnimator.parameters)
                if (parameter.name == "FadeIn" && parameter.type == AnimatorControllerParameterType.Trigger)
                    fadeAnimator.SetTrigger("FadeIn");
        }
        IsRespawning = false;
    }
}
