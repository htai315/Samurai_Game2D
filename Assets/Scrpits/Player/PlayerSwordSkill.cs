using UnityEngine;

[RequireComponent(typeof(PlayerMana))]
public class PlayerSwordSkill : MonoBehaviour
{
    public GameObject swordWavePrefab;
    public Transform spawnPoint;
    public float manaCost = 5f;
    [SerializeField] private float skillCooldown = 1.0f;
    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;
    private bool isUsingSkill;
    private float nextCastTime = 0f;
    private readonly PlayerActionAnimation action = new();

    private void Awake()
    {
        mana = GetComponent<PlayerMana>();
        controller = GetComponent<PlayerController1>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        action.Tick(controller && controller.CanAct, Time.timeScale == 0f);
        isUsingSkill = action.Active;
        if (Input.GetKeyDown(KeyCode.U)) TryUseSwordSkill();
    }

    public bool TryUseSwordSkill()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || !(Time.time >= nextCastTime) ||
            !swordWavePrefab || !spawnPoint || !swordWavePrefab.GetComponent<SwordProjectile>() ||
            !swordWavePrefab.GetComponent<Rigidbody2D>() || !PlayerActionAnimation.ValidCost(skillCooldown) ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Skill1", "doSkillWave") ||
            !mana || !mana.TrySpend(manaCost)) return false;

        isUsingSkill = true;
        nextCastTime = Time.time + skillCooldown;
        action.Begin(anim, "Player_Skill1", "doSkillWave");
        var body = GetComponent<Rigidbody2D>();
        if (body) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        return true;
    }

    // Animation event: only the accepted, still-active cast may produce one projectile.
    public void ShootSwordWave()
    {
        if (!isActiveAndEnabled || !swordWavePrefab || !spawnPoint ||
            !action.TryEmit(controller && controller.CanAct)) return;
        float direction = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 position = spawnPoint.position;
        position.x = transform.position.x + Mathf.Abs(spawnPoint.localPosition.x) * direction;
        var projectile = Instantiate(swordWavePrefab, position, Quaternion.identity).GetComponent<SwordProjectile>();
        projectile.SetDirection(direction);
        projectile.Launch();
    }

    private void OnDisable() => ResetForRespawn(false);
    public bool IsCasting => isUsingSkill;
    public float SkillCooldown => skillCooldown;
    public float SkillCooldownRemaining => Mathf.Max(0f, nextCastTime - Time.time);
    public bool IsSkillReady => !isUsingSkill && Time.time >= nextCastTime;

    public void ResetForRespawn(bool newAttempt)
    {
        action.Cancel();
        isUsingSkill = false;
        if (newAttempt) nextCastTime = 0f;
    }
}
