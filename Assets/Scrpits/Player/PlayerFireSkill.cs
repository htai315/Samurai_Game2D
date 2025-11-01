using UnityEngine;

[RequireComponent(typeof(PlayerMana))]
public class PlayerFireSkill : MonoBehaviour
{
    [Header("Skill Settings")]
    public GameObject fireballPrefab;     // Prefab của đốm lửa
    public Transform castPoint;           // Điểm spawn (trước người)
    public float manaCost = 10f;          // Mana tiêu hao mỗi lần cast
    public float cooldown = 0.8f;         // Thời gian hồi chiêu (giây)

    private PlayerMana mana;
    private Animator anim;
    private float lastCastTime = -999f;
    private bool isCasting = false;

    void Start()
    {
        mana = GetComponent<PlayerMana>();
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Ấn phím I để cast chiêu lửa
        if (Input.GetKeyDown(KeyCode.I))
            TryCastFire();
    }

    void TryCastFire()
    {
        if (isCasting) return;
        if (Time.time < lastCastTime + cooldown) return;

        if (!mana.TrySpend(manaCost))
        {
            Debug.Log("❌ Không đủ mana để cast chiêu lửa!");
            return;
        }

        // Phát trigger animation cast
        isCasting = true;
        lastCastTime = Time.time;
        anim.SetTrigger("doCastFire"); // nhớ tạo trigger này trong Animator Player
    }

    // 🔥 GỌI TỪ ANIMATION EVENT trong clip cast (frame vung tay)
    public void SpawnFireball()
    {
        if (!fireballPrefab || !castPoint) return;

        // Tính hướng theo hướng Player đang nhìn
        float dir = Mathf.Sign(transform.localScale.x);
        if (dir == 0) dir = 1f;

        Vector3 pos = castPoint.position;

        // Sinh ra Fireball
        GameObject fb = Instantiate(fireballPrefab, pos, Quaternion.identity);
        var ctrl = fb.GetComponent<FireballController>();
        if (ctrl != null)
            ctrl.SetDirection(dir);

        Debug.Log("🔥 Fireball casted!");
        isCasting = false;
    }
}
