using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float luongMauToiDa = 10f;
    [SerializeField] private float hurtStunDuration = 0.3f;

    private float luongMauHienTai;
    private ThanhMau thanhMau;
    private PlayerController1 controller;
    private Animator anim;
    private Rigidbody2D rb;

    // Animator hashes
    private static readonly int DoHurt = Animator.StringToHash("doHurt");
    private static readonly int DoDie = Animator.StringToHash("doDie");

    public void Initialize(PlayerController1 ctrl, Animator animator, Rigidbody2D rigidbody, ThanhMau healthBar)
    {
        controller = ctrl;
        anim = animator;
        rb = rigidbody;
        thanhMau = healthBar;
    }

    public void Start()
    {
        luongMauHienTai = luongMauToiDa;
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

    public void TakeDamage(float damage)
    {
        if (controller.IsDead || controller.IsHurting)
            return;

        // 🔍 LOG trước khi trừ
        Debug.Log($"[HP] Nhận sát thương: {damage}. Trước khi trừ: {luongMauHienTai}/{luongMauToiDa}");

        luongMauHienTai -= damage;

        // Cập nhật UI
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

        // 🔍 LOG sau khi trừ
        Debug.Log($"[HP] Sau khi trừ: {luongMauHienTai}/{luongMauToiDa}");

        if (luongMauHienTai <= 0)
        {
            Debug.Log("[HP] Player chết.");
            Die();
        }
        else
        {
            anim.SetTrigger(DoHurt);
            StartCoroutine(HurtStun());
        }
    }


    private void Die()
    {
        if (RunSession.Instance != null && RunSession.Instance.IsTransitioningScene)
            return;
        anim.SetTrigger(DoDie);
        controller.SetDead(true);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Static;

        // Hỏi hệ thống mạng
        bool canRespawn = false;
        if (PlayerLives.Instance != null)
        {
            canRespawn = PlayerLives.Instance.UseLife();
            // true  = còn mạng → respawn
            // false = hết mạng → không respawn
        }

        if (canRespawn && LevelRespawnManager.Instance != null)
        {
            // Chết nhưng vẫn còn mạng → hồi sinh
            StartCoroutine(RespawnAfterDelay(1.2f));
        }
        else
        {
            // Hết mạng hoặc không có manager → chết hẳn
            StartCoroutine(FinalDeath(1.2f));
        }
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LevelRespawnManager.Instance.RespawnPlayer();
    }

    private IEnumerator FinalDeath(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Cách 1: Destroy player luôn (game over im lặng)
        RunSession.Instance?.OpenGameOver();

        // Cách 2 (khuyên dùng): Load sang GameOver scene
        // SceneManager.LoadScene("GameOverScene");

        // Cách 3: Hiện UI Game Over ngay tại màn hình hiện tại (cần thêm script khác)
    }



    private IEnumerator HurtStun()
    {
        controller.SetHurting(true);
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(hurtStunDuration);
        controller.SetHurting(false);
    }



    public void Heal(float amount)
    {
        Debug.Log($"[HP] Hồi máu: +{amount}. Trước khi hồi: {luongMauHienTai}/{luongMauToiDa}");

        luongMauHienTai = Mathf.Min(luongMauHienTai + amount, luongMauToiDa);
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

        Debug.Log($"[HP] Sau khi hồi: {luongMauHienTai}/{luongMauToiDa}");
    }


    public float CurrentHealth => luongMauHienTai;
    public float MaxHealth => luongMauToiDa;
    // PlayerHealth.cs (bổ sung 2 API bên dưới trong class)
    public void ApplyNewMaxHealth(float newMax, bool keepRatio)
    {
        float oldMax = luongMauToiDa;
        float oldCur = luongMauHienTai;

        luongMauToiDa = Mathf.Max(0.01f, newMax);

        float targetCur = keepRatio && oldMax > 0.0001f
            ? Mathf.Clamp01(oldCur / oldMax) * luongMauToiDa
            : Mathf.Min(oldCur, luongMauToiDa);

        luongMauHienTai = targetCur;
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

    public void SetCurrentHealth(float value)
    {
        luongMauHienTai = Mathf.Clamp(value, 0, luongMauToiDa);
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

}