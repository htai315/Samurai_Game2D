using UnityEngine;

// Spawn adapter shared by ordinary ranged enemies and the optional boss abilities.
public static class EnemyRangedAttack
{
    public static void Launch(Vector3 spawnPosition, float direction, GameObject prefab,
        float damage, BossAbilityController bossAbilities)
    {
        CombatVFX.SpawnPerilousAttackTell(spawnPosition);
        if (bossAbilities != null && bossAbilities.TryLaunchGroundWave(direction)) return;
        GameObject projectile;
        if (prefab) projectile = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
        else
        {
            projectile = new GameObject("EnemyRangedProjectile");
            projectile.transform.position = spawnPosition;
            projectile.AddComponent<SpriteRenderer>().sortingOrder = 5;
            var collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.22f;
            projectile.AddComponent<Rigidbody2D>();
            projectile.AddComponent<EnemyProjectile>();
        }
        var shot = projectile.GetComponent<EnemyProjectile>();
        if (!shot) return;
        shot.damage = damage;
        shot.Launch(direction);
    }
}
