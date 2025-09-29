using UnityEngine;

public sealed class VfxDashDust : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterEvents eventsBus;
    [SerializeField] private CharacterState state;

    [Header("VFX")]
    [SerializeField] private GameObject dashDustPrefab;
    [SerializeField] private Transform dustSpawnPoint;
    [SerializeField] private float dustBackOffset = 0.2f;

    [Header("Sorting")]
    [SerializeField] private SpriteRenderer playerSR;

    void OnEnable()
    {
        if (eventsBus != null) eventsBus.DashStarted += SpawnDust;
    }

    void OnDisable()
    {
        if (eventsBus != null) eventsBus.DashStarted -= SpawnDust;
    }

    private void SpawnDust()
    {
        if (!dashDustPrefab) return;

        Vector3 basePos = dustSpawnPoint ? dustSpawnPoint.position : transform.position;
        float dir = state.Facing >= 0 ? 1f : -1f;
        Vector3 spawnPos = basePos + new Vector3(-dustBackOffset * dir, 0f, 0f);

        var dust = Instantiate(dashDustPrefab, spawnPos, Quaternion.identity);

        if (playerSR && dust.TryGetComponent<SpriteRenderer>(out var dustSR))
        {
            dustSR.sortingLayerID = playerSR.sortingLayerID;
            dustSR.sortingOrder = playerSR.sortingOrder - 1;
            dustSR.flipX = (dir < 0);
        }
    }
}
