using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterState))]
public sealed class HealthComponent : MonoBehaviour
{
    [SerializeField] private CharacterConfig cfg;
    [SerializeField] private CharacterState state;
    [SerializeField] private CharacterEvents eventsBus;

    [Header("UI Thanh Máu")]
    [SerializeField] private ThanhMau thanhMau;

    [Header("Runtime")]
    [SerializeField] private float luongMauHienTai;

    void Start()
    {
        luongMauHienTai = cfg.luongMauToiDa;
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, cfg.luongMauToiDa);
    }

    public void TakeDamage(float amount)
    {
        if (state.IsDead) return;

        luongMauHienTai = Mathf.Max(0, luongMauHienTai - Mathf.Abs(amount));
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, cfg.luongMauToiDa);

        if (luongMauHienTai <= 0)
        {
            state.IsDead = true;
            eventsBus?.InvokeDied();
            // Không tắt component vội; AnimatorSync sẽ bắn trigger die.
        }
        else
        {
            StartCoroutine(HurtStun(cfg.hurtStunDuration));
        }
    }

    private IEnumerator HurtStun(float duration)
    {
        state.IsHurting = true;
        eventsBus?.InvokeHurt();
        yield return new WaitForSeconds(duration);
        state.IsHurting = false;
    }

    // Giữ hành vi test "click để trừ máu" như code cũ
    void OnMouseDown()
    {
        if (state.IsDead || state.IsHurting) return;
        TakeDamage(1f);
    }

    // Gọi từ Animation Event
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}
