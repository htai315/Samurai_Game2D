using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class PlayerOutOfBoundsKiller : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private Collider2D bounds;
    [SerializeField] private Transform checkPoint;
    [SerializeField] private float margin = 0.02f;

    [Header("Kill Mode")]
    [SerializeField] private bool killByDamage = true;

    private Collider2D playerCol;
    private PlayerHealth health;

    void Awake()
    {
        playerCol = GetComponent<Collider2D>();
        health = GetComponent<PlayerHealth>();
        FindBounds(); // 🔹 Tìm ngay khi khởi tạo

        // 🔹 Đăng ký callback khi scene đổi
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 🔹 Khi scene mới load, tìm lại CameraBounds
        FindBounds();
    }

    private void FindBounds()
    {
        var go = GameObject.Find("CameraBounds");
        if (go)
        {
            bounds = go.GetComponent<Collider2D>();
            Debug.Log($"[Bounds] Gắn lại bounds: {bounds.name}");
        }
        else
        {
            bounds = null;
            Debug.LogWarning("⚠️ Không tìm thấy CameraBounds trong scene này!");
        }
    }

    void Update()
    {
        if (!bounds) return;

        Vector2 p = checkPoint ? (Vector2)checkPoint.position : (Vector2)playerCol.bounds.center;

        bool inside = bounds.OverlapPoint(p);
        Vector2 cp = bounds.ClosestPoint(p);
        float sqrDist = (p - cp).sqrMagnitude;

        if (!inside && sqrDist > margin * margin)
            KillNow();
    }

    private void KillNow()
    {
        if (killByDamage && health)
            health.TakeDamage(9999f);
        else
            Destroy(gameObject);
    }
}
