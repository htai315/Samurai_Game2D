using UnityEngine;

[RequireComponent(typeof(PlayerMana))]
public class PlayerSwordSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    public GameObject swordWavePrefab;   // prefab kiếm khí
    public Transform spawnPoint;         // điểm spawn trước mặt
    public float manaCost = 5f;

    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;
    private bool isUsingSkill = false;

    void Start()
    {
        mana = GetComponent<PlayerMana>();
        controller = GetComponent<PlayerController1>();
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U) && !isUsingSkill)
            TryUseSwordSkill();
    }

    void TryUseSwordSkill()
    {
        if (!mana.TrySpend(manaCost))
        {
            Debug.Log("❌ Không đủ mana!");
            return;
        }

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
}
