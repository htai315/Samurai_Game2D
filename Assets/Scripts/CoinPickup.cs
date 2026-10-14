using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinPickup : MonoBehaviour
{
    [Header("ID duy nhất cho coin này")]
    [SerializeField] private string pickupId;   // Editor assigns a unique scene-instance ID on save.
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private int value = 1;

    private string FullId => $"{gameObject.scene.name}:{pickupId}";
    private bool consumed;
    private Transform playerTransform;
    private PlayerStats playerStats;
    private PlayerController1 pickupPlayer;
    private float nextSearchTime;

    private void OnEnable()
    {
        if (SaveRuntime.IsCollected(FullId))
            gameObject.SetActive(false);
    }

    private void Update()
    {
        if (consumed || Time.timeScale <= 0f) return;

        if (playerTransform == null || playerStats == null)
        {
            if (Time.time < nextSearchTime) return;
            nextSearchTime = Time.time + 1f;
            var player = PlayerContext.Current?.Player;
            if (player)
            {
                playerTransform = player.transform;
                playerStats = player.GetComponent<PlayerStats>();
                pickupPlayer = player.GetComponent<PlayerController1>();
            }
        }

        if (playerTransform != null && pickupPlayer && pickupPlayer.CanCollectPickups)
        {
            float magnetRadius = (playerStats != null && playerStats.hasMagnetTalisman) ? 6.5f : 3.0f;
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist <= magnetRadius)
            {
                float flySpeed = (playerStats != null && playerStats.hasMagnetTalisman) ? 12f : 7.5f;
                // Bay mượt mà hướng về phía ngực Samurai
                Vector3 targetPos = playerTransform.position + Vector3.up * 0.35f;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, flySpeed * Time.deltaTime);

                // Nếu bay sát vào người thì tự động thu thập luôn
                if (dist < 0.45f)
                {
                    CollectCoin();
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || !other) return;
        var player = other.GetComponentInParent<PlayerController1>();
        if (!player || !player.CompareTag("Player")) return;
        pickupPlayer = player;
        CollectCoin();
    }

    private void CollectCoin()
    {
        if (consumed || !isActiveAndEnabled || value <= 0 || !pickupPlayer || !pickupPlayer.CanCollectPickups) return;
        var wallet = PlayerContext.Resolve(pickupPlayer)?.Wallet;
        string id = FullId;
        if (wallet == null || SaveRuntime.IsCollected(id)) return;
        consumed = true;
        SaveRuntime.MarkCollected(id);
        if (!wallet.TryCredit(value))
        {
            consumed = false;
            SaveRuntime.UnmarkCollected(id);
            return;
        }
        SoundManager.PlayCoin();

        // 🌟 Hiệu ứng nhặt vàng: Lóe sáng nhẹ ngôi sao vàng
        CombatVFX.SpawnBladeGlint(transform.position, 0.22f);


        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
