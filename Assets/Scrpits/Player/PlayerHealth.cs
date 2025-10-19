using System.Collections;
using UnityEngine;

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

        luongMauHienTai -= damage;
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

        if (luongMauHienTai <= 0)
        {
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
        anim.SetTrigger(DoDie);
        controller.SetDead(true);

        // Dừng hoàn toàn mọi chuyển động
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // Khoá Rigidbody để không bị di chuyển nữa
        rb.bodyType = RigidbodyType2D.Static;

        controller.enabled = false;
    }

    private IEnumerator HurtStun()
    {
        controller.SetHurting(true);
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(hurtStunDuration);
        controller.SetHurting(false);
    }

    // Test damage (xóa hoặc thay bằng hệ thống collision)
    private void OnMouseDown()
    {
        TakeDamage(1f);
    }

    public void Heal(float amount)
    {
        luongMauHienTai = Mathf.Min(luongMauHienTai + amount, luongMauToiDa);
        if (thanhMau)
            thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

    public float CurrentHealth => luongMauHienTai;
    public float MaxHealth => luongMauToiDa;
}