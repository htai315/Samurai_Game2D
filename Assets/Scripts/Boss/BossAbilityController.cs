using System;
using System.Collections;
using UnityEngine;

// Managed adapter owned by one enemy actor; not another MonoBehaviour or AI loop.
public sealed class BossAbilityController
{
    private readonly MonoBehaviour owner;
    private readonly Rigidbody2D body;
    private readonly Collider2D bodyCollider;
    private readonly BossHealth health;
    private readonly Func<Transform> target;
    private readonly Func<bool> canContinue, attacking;
    private readonly Action<float> face;
    private readonly Func<bool> attack;
    private readonly Func<LayerMask> terrain;
    private readonly Func<float> skin;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly WaitForSeconds tellWait = new WaitForSeconds(0.24f);
    private readonly WaitForSeconds recoveryWait = new WaitForSeconds(0.08f);
    private Coroutine shadowDash;
    private float nextShadowDashTime;
    public bool HasPendingShadowDash => shadowDash != null;
    public float RangedCooldownMultiplier => health && health.IsEnraged ? 0.7f : 1f;

    public BossAbilityController(MonoBehaviour owner, Rigidbody2D body, Collider2D bodyCollider, BossHealth health,
        Func<Transform> target, Func<bool> canContinue, Func<bool> attacking,
        Action<float> face, Func<bool> attack, Func<LayerMask> terrain, Func<float> skin)
    {
        this.owner = owner; this.body = body; this.bodyCollider = bodyCollider; this.health = health;
        this.target = target; this.canContinue = canContinue; this.attacking = attacking;
        this.face = face; this.attack = attack; this.terrain = terrain; this.skin = skin;
    }

    public void CheckShadowDash()
    {
        if (!health || !health.IsEnraged || HasPendingShadowDash || !canContinue() ||
            Time.timeScale <= 0 || attacking() || Time.time < nextShadowDashTime) return;
        float distance = Mathf.Abs(target().position.x - owner.transform.position.x);
        if (distance < 2f || distance > 7f) return;
        nextShadowDashTime = Time.time + UnityEngine.Random.Range(5.5f, 7.5f);
        shadowDash = owner.StartCoroutine(ShadowDashRoutine());
    }

    public void CancelShadowDash()
    {
        var pending = shadowDash;
        shadowDash = null;
        if (pending != null && owner) owner.StopCoroutine(pending);
        // Interruption does not refund the already scheduled cooldown.
    }

    private IEnumerator ShadowDashRoutine()
    {
        try
        {
            CombatVFX.SpawnPerilousAttackTell(owner.transform.position + Vector3.up * 0.6f);
            yield return tellWait;
            if (!canContinue()) yield break;
            var player = target();
            float direction = Mathf.Sign(player.position.x - owner.transform.position.x);
            Vector2 destination = player.position - new Vector3(direction * 1.4f, 0f, 0f);
            var trail = owner.GetComponent<GhostTrail>() ?? owner.gameObject.AddComponent<GhostTrail>();
            trail.SpawnSingleGhost(new Color(1f, 0.2f, 0.2f, 0.8f));
            CombatVFX.SpawnParryVFX(owner.transform.position);
            SoundManager.PlayDash();
            Vector2 travel = destination - body.position;
            float allowed = travel.magnitude;
            float clearance = skin();
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = terrain(), useTriggers = false };
            int count = body.Cast(travel.normalized, filter, castHits, allowed + clearance);
            for (int i = 0; i < count; i++)
                allowed = Mathf.Min(allowed, Mathf.Max(0f, castHits[i].distance - clearance));
            // Boss1's authored body is a trigger. Explicit bounds clearance is
            // also required: its Rigidbody cast did not detect solid terrain in
            // the real-prefab Unity 6 regression. Do not change prefab physics.
            if (bodyCollider && bodyCollider.enabled && bodyCollider.isTrigger)
            {
                var bounds = bodyCollider.bounds;
                float inset = Mathf.Clamp(clearance, .001f, Mathf.Min(bounds.extents.x, bounds.extents.y) * .25f);
                count = Physics2D.BoxCast(bounds.center, (Vector2)bounds.size - Vector2.one * (inset * 2),
                    0, travel.normalized, filter, castHits, allowed + inset);
                for (int i = 0; i < count; i++)
                    allowed = Mathf.Min(allowed, Mathf.Max(0f, castHits[i].distance - inset));
            }
            body.position += travel.normalized * allowed;
            face(player.position.x - owner.transform.position.x);
            trail.SpawnSingleGhost(new Color(1f, 0.2f, 0.2f, 0.8f));
            yield return recoveryWait;
            if (canContinue()) attack();
        }
        finally { shadowDash = null; }
    }

    public bool TryLaunchGroundWave(float direction)
    {
        if (!health || UnityEngine.Random.value >= 0.5f) return false;
        var waveObject = new GameObject("BossGroundWave");
        waveObject.transform.position = owner.transform.position + new Vector3(direction * 0.8f, -0.2f, 0f);
        waveObject.AddComponent<BossGroundWave>().Launch(direction);
        return true;
    }

    public void PresentSlash(Vector3 position, bool facingRight)
    {
        if (!health || !health.IsEnraged) return;
        CombatVFX.SpawnBloodSlashArc(position, facingRight ? 1f : -1f);
        CombatVFX.SpawnFireSparks(position);
        CameraShaker.Shake(0.12f, 0.18f);
    }
}
