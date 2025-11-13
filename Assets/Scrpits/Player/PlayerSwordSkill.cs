using UnityEngine;

[RequireComponent(typeof(PlayerMana))]
public class PlayerSwordSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    public GameObject swordWavePrefab;   // prefab kiếm khí
    public Transform spawnPoint;         // điểm spawn trước mặt
    public float manaCost = 5f;

    [Header("Cooldown")]
    [SerializeField] private float skillCooldown = 1.0f;   // thời gian chờ giữa 2 lần U

    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;

    private bool isUsingSkill = false;
    private float nextCastTime = 0f;     // thời điểm có thể cast lần tiếp theo

    private void Start()
    {
        mana = GetComponent<PlayerMana>();
        controller = GetComponent<PlayerController1>();
        anim = GetComponentInChildren<Animator>();
        nextCastTime = 0f;               // cast được ngay
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
            TryUseSwordSkill();
    }

    private void TryUseSwordSkill()
    {
        // Đang dùng skill hoặc chưa hết cooldown → bỏ
        if (isUsingSkill) return;
        if (Time.time < nextCastTime) return;

        // (tuỳ chọn) chặn khi đang chết/hurt/dash
        if (controller != null &&
            (controller.IsDead || controller.IsHurting || controller.IsDashing))
            return;

        // Kiểm tra mana
        if (!mana.TrySpend(manaCost))
        {
            Debug.Log("❌ Không đủ mana!");
            return;
        }

        // Đặt cooldown mới
        nextCastTime = Time.time + skillCooldown;

        isUsingSkill = true;
        anim.SetTrigger("doSkillWave"); // trigger animation có event
    }

    // 🧩 HÀM NÀY SẼ ĐƯỢC GỌI BỞI ANIMATION EVENT
    public void ShootSwordWave()
    {
        float direction = Mathf.Sign(controller.transform.localScale.x);
        if (direction == 0) direction = 1f;

        // Spawn trước mặt
        Vector3 spawnPos = spawnPoint.position;
        spawnPos.x = transform.position.x + Mathf.Abs(spawnPoint.localPosition.x) * direction;

        GameObject wave = Instantiate(swordWavePrefab, spawnPos, Quaternion.identity);
        var proj = wave.GetComponent<SwordProjectile>();
        if (proj != null)
        {
            proj.SetDirection(direction);
            proj.Launch(); // nếu bạn dùng bản có Launch()
        }

        Debug.Log("💨 Bắn kiếm khí từ animation!");
        isUsingSkill = false;
    }

    // ===== API cho UI cooldown =====
    public float SkillCooldown => skillCooldown;

    public float SkillCooldownRemaining
        => Mathf.Max(0f, nextCastTime - Time.time);

    public bool IsSkillReady
        => !isUsingSkill && Time.time >= nextCastTime;
}
