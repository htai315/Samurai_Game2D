using UnityEngine;

[RequireComponent(typeof(PlayerMana))]
public class PlayerFireSkill : MonoBehaviour
{
    public GameObject fireballPrefab;
    public Transform castPoint;
    public float manaCost = 10f;
    public float cooldown = 0.8f;
    private PlayerMana mana;
    private PlayerController1 controller;
    private Animator anim;
    private bool isCasting;
    private float lastCastTime = -999f;
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
        isCasting = action.Active;
        if (Input.GetKeyDown(KeyCode.I)) TryCastFire();
    }

    public bool TryCastFire()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || !(Time.time >= lastCastTime + cooldown) ||
            !fireballPrefab || !castPoint || !fireballPrefab.GetComponent<FireballController>() ||
            !fireballPrefab.GetComponent<Rigidbody2D>() || !PlayerActionAnimation.ValidCost(cooldown) ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Fire_Skill", "doCastFire") ||
            !mana || !mana.TrySpend(manaCost)) return false;

        isCasting = true;
        lastCastTime = Time.time;
        action.Begin(anim, "Player_Fire_Skill", "doCastFire");
        var body = GetComponent<Rigidbody2D>();
        if (body) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        return true;
    }

    // Animation event: only the accepted, still-active cast may produce one projectile.
    public void SpawnFireball()
    {
        if (!isActiveAndEnabled || !fireballPrefab || !castPoint ||
            !action.TryEmit(controller && controller.CanAct)) return;
        float direction = transform.localScale.x < 0f ? -1f : 1f;
        Vector3 position = castPoint.position;
        var projectile = Instantiate(fireballPrefab, position, Quaternion.identity).GetComponent<FireballController>();
        projectile.SetDirection(direction);
    }

    private void OnDisable() => ResetForRespawn(false);
    public bool IsCasting => isCasting;
    public float SkillCooldown => cooldown;
    public float SkillCooldownRemaining => Mathf.Max(0f, lastCastTime + cooldown - Time.time);
    public bool IsSkillReady => !isCasting && Time.time >= lastCastTime + cooldown;

    public void ResetForRespawn(bool newAttempt)
    {
        action.Cancel();
        isCasting = false;
        if (newAttempt) lastCastTime = -999f;
    }
}
