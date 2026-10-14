using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Samurai.Core.Combat;

public class PlayerHealth : MonoBehaviour, IDamageReceiver
{
    private readonly DamageSignal damageSignal = new DamageSignal(error => Debug.LogException(error));
    private bool resolvingDamage;
    public event System.Action<DamageResult> DamageResolved
    { add { damageSignal.Resolved += value; } remove { damageSignal.Resolved -= value; } }
    [Header("Health Settings")]
    [SerializeField] private float luongMauToiDa = 10f;
    [SerializeField] private float hurtStunDuration = 0.3f;

    private float luongMauHienTai;
    private ThanhMau thanhMau;
    private PlayerController1 controller;
    private Animator anim;
    private Rigidbody2D rb;
    private bool initialized;

    // Animator hashes
    private static readonly int DoHurt = Animator.StringToHash("doHurt");
    private static readonly int DoDie = Animator.StringToHash("doDie");

    public void Initialize(PlayerController1 ctrl, Animator animator, Rigidbody2D rigidbody, ThanhMau healthBar)
    {
        controller = ctrl;
        anim = animator;
        rb = rigidbody;
        thanhMau = healthBar;
        if (!initialized)
        {
            luongMauHienTai = luongMauToiDa;
            initialized = true;
        }
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

    public void TakeDamage(float damage)
    {
        ReceiveDamage(new DamageRequest(damage));
    }

    public DamageResult ReceiveDamage(DamageRequest request)
    {
        if (resolvingDamage) return DamageResult.Rejected(DamageStatus.Reentrant, request, luongMauHienTai);
        if (!request.IsValid || request.Amount > float.MaxValue || (float)request.Amount <= 0)
            return DamageResult.Rejected(DamageStatus.Invalid, request, luongMauHienTai);
        if (controller && controller.IsDead) return DamageResult.Rejected(DamageStatus.Dead, request, luongMauHienTai);
        if (Time.timeScale <= 0) return DamageResult.Rejected(DamageStatus.Paused, request, luongMauHienTai);
        if (!CanChangeHealth) return DamageResult.Rejected(DamageStatus.Unavailable, request, luongMauHienTai);
        if (controller.IsHurting || controller.IsDashing)
            return DamageResult.Rejected(DamageStatus.Invulnerable, request, luongMauHienTai);
        float damage = (float)request.Amount;
        float before = luongMauHienTai;
        resolvingDamage = true;
        try
        {

            // 🛡️ PARRY: Nếu đang trong cửa sổ đỡ đòn hoàn hảo
            if (controller && controller.IsParrying)
            {
                controller.OnParrySuccess();
                var parry = DamageResult.Rejected(DamageStatus.Parried, request, before);
                damageSignal.Publish(parry);
                return parry;
            }

            // 🔍 LOG trước khi trừ
            Debug.Log($"[HP] Nhận sát thương: {damage}. Trước khi trừ: {luongMauHienTai}/{luongMauToiDa}");

            luongMauHienTai = Mathf.Max(0, luongMauHienTai - damage);

            // Cập nhật UI
            if (thanhMau)
                thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

            // 🔍 LOG sau khi trừ
            Debug.Log($"[HP] Sau khi trừ: {luongMauHienTai}/{luongMauToiDa}");

            if (luongMauHienTai <= 0)
            {
                Debug.Log("[HP] Player chết.");
                CameraShaker.Shake(0.25f, 0.35f);
                SoundManager.PlayHurt();
                Die();
            }
            else
            {
                CameraShaker.Shake(0.12f, 0.2f);
                SoundManager.PlayHurt();
                anim.SetTrigger(DoHurt);
                StartCoroutine(HurtStun());
            }
            var result = new DamageResult(DamageStatus.Applied, request, before, luongMauHienTai);
            damageSignal.Publish(result);
            return result;
        }
        finally { resolvingDamage = false; }
    }


    private bool CanChangeHealth => isActiveAndEnabled && controller && !controller.IsDead &&
        Time.timeScale > 0 && !SceneTransition.IsBusy &&
        !(RunSession.Instance && (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver)) &&
        !(LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning);

    private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    // Environmental death bypasses hurt invulnerability, never pause/transition/death guards.
    public void KillOutOfBounds()
    {
        if (!CanChangeHealth) return;
        luongMauHienTai = 0;
        if (thanhMau) thanhMau.capNhatMau(0, luongMauToiDa);
        Die();
    }

    private void Die()
    {
        if (RunSession.Instance != null && RunSession.Instance.IsTransitioningScene)
            return;
        if (controller.IsDead) return;
        StopAllCoroutines();
        controller.SetHurting(false);
        anim.ResetTrigger(DoHurt);
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

    private void OnDisable()
    {
        // Inactive GameObjects stop coroutines; release the transient lock explicitly.
        StopAllCoroutines();
        if (controller) controller.SetHurting(false);
    }



    public void Heal(float amount)
    {
        TryHeal(amount);
    }

    public bool TryHeal(float amount)
    {
        if (!CanChangeHealth || !PositiveFinite(amount) || luongMauHienTai >= luongMauToiDa) return false;
        Debug.Log($"[HP] Hồi máu: +{amount}. Trước khi hồi: {luongMauHienTai}/{luongMauToiDa}");

        luongMauHienTai = Mathf.Min(luongMauHienTai + amount, luongMauToiDa);
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

        Debug.Log($"[HP] Sau khi hồi: {luongMauHienTai}/{luongMauToiDa}");
        return true;
    }


    public float CurrentHealth => luongMauHienTai;
    public float MaxHealth => luongMauToiDa;
    // PlayerHealth.cs (bổ sung 2 API bên dưới trong class)
    public void ApplyNewMaxHealth(float newMax, bool keepRatio)
    {
        if (!PositiveFinite(newMax)) return;
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
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        luongMauHienTai = Mathf.Clamp(value, 0, luongMauToiDa);
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

}
