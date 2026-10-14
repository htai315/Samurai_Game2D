using System;
using Samurai.Core.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DamageContractValidation
{
    private const string Running = "DamageContractValidation.Running";
    private static double deadline, until;
    private static int assertions;
    static DamageContractValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        PlayerSettings.companyName = "SamuraiDamageValidation";
        PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 40; until = now + 2; }
            if (now > deadline) throw new TimeoutException("Damage contract timeout");
            if (now < until || SceneTransition.IsBusy) return;
            foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
            HitStop.Cancel(); Time.timeScale = 1;
            CheckCore();
            CheckEnemyAttackWindow();
            CheckSpatialSensor();
            CheckEnemyAcquisitionAndMotor();
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Skeleton.prefab"))
                .GetComponent<EnemyHealth1>();
            var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss1.prefab"))
                .GetComponent<BossHealth>();
            enemy.GetComponent<GhostController>().enabled = false;
            boss.GetComponent<GhostController>().enabled = false;
            CheckReceiver(enemy, () => enemy.CurrentHealth, value => enemy.enabled = value);
            CheckReceiver(boss, () => boss.CurrentHealth, value => boss.enabled = value);
            CheckFeedback();
            CheckProps();
            CheckPlayer();
            Debug.Log($"[DamageContractValidation] ALL CHECKS PASSED ({assertions} assertions)");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void CheckEnemyAttackWindow()
    {
        var window = new EnemyAttackWindow();
        Check(!window.Active && !window.TryConsumeDamage(true, true, false), "idle enemy cannot spend a hit");
        Check(window.TryBegin(10) && window.Active && !window.DamageSpent && !window.EnteredAnimation,
            "commit initializes one owned enemy window");
        Check(!window.TryBegin(20) && !window.ShouldCancel(10.5f, false, true, false),
            "overlapping begin cannot reset original startup deadline");
        Check(!window.ShouldCancel(100, true, false, true) && !window.EnteredAnimation && window.Active,
            "paused update changes neither animation entry nor attack lifetime");
        Check(window.ShouldCancel(10.501f, false, true, false), "missing animation cancels strictly after half second");
        window.Cancel();
        Check(!window.Active && window.DamageSpent && !window.TryConsumeDamage(true, true, false),
            "cancel closes window before late callbacks");
        window.Cancel();
        Check(!window.ShouldCancel(100, false, false, true), "repeated cancel and idle ticking are harmless");
        Check(window.TryBegin(20) && !window.ShouldCancel(20.1f, false, true, true) && window.EnteredAnimation,
            "new swing resets canceled flags and tracks animation entry");
        Check(window.ShouldCancel(20.2f, false, true, false), "leaving attack animation ends committed window");
        window.Cancel(); window.TryBegin(30);
        Check(!window.ShouldCancel(35, false, true, true) && window.ShouldCancel(35.001f, false, true, true),
            "active animation timeout retains strict five second boundary");
        Check(window.ShouldCancel(30.1f, false, false, true), "death transition or target loss requests cancellation");

        for (int flags = 0; flags < 8; flags++)
        {
            bool eligible = (flags & 1) != 0, inAnimation = (flags & 2) != 0, outgoing = (flags & 4) != 0;
            var candidate = new EnemyAttackWindow(); candidate.TryBegin(0);
            bool expected = eligible && inAnimation && !outgoing;
            Check(candidate.TryConsumeDamage(eligible, inAnimation, outgoing) == expected && candidate.DamageSpent == expected,
                "enemy damage eligibility matrix " + flags);
            Check(candidate.TryConsumeDamage(true, true, false) == !expected && candidate.DamageSpent,
                "rejected event leaves window available; accepted event cannot repeat " + flags);
            Check(!candidate.TryConsumeDamage(true, true, false), "miss or reentrant callback cannot spend twice " + flags);
        }
        var other = new EnemyAttackWindow(); other.TryBegin(0);
        window.Cancel();
        Check(other.Active && other.TryConsumeDamage(true, true, false), "attack ownership is isolated per enemy");
        window.TryBegin(40);
        Check(!window.TryConsumeDamage(false, true, false) && window.Active && !window.DamageSpent &&
            window.TryConsumeDamage(true, true, false), "temporary stun suppression preserves boss committed window");
    }
    private static void CheckSpatialSensor()
    {
        var bodyObject = new GameObject("SensorBody", typeof(Rigidbody2D), typeof(BoxCollider2D));
        var body = bodyObject.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        bodyObject.transform.position = new Vector3(5000, 5000);
        var collider = bodyObject.GetComponent<BoxCollider2D>(); collider.offset = new Vector2(.4f, .3f); collider.size = Vector2.one;
        var targetObject = new GameObject("SensorTarget", typeof(BoxCollider2D));
        targetObject.transform.position = new Vector3(5001.4f, 5000);
        var target = targetObject.GetComponent<BoxCollider2D>(); target.offset = Vector2.up; target.size = Vector2.one * .4f;
        var child = new GameObject("SensorChild", typeof(BoxCollider2D)); child.transform.SetParent(targetObject.transform, false);
        child.transform.localPosition = Vector3.up; child.GetComponent<BoxCollider2D>().isTrigger = true;
        var hits = new System.Collections.Generic.List<Collider2D>(8);
        var sensor = new EnemySpatialSensor(body, collider, hits);
        Physics2D.SyncTransforms();
        Check(sensor.BodyCenter == (Vector2)collider.bounds.center && sensor.BodyCenter != body.position,
            "sensor preserves body bounds rather than pivot");
        Check(sensor.TargetPoint(targetObject.transform, target) == (Vector2)target.bounds.center &&
            sensor.TargetPoint(targetObject.transform, target) != (Vector2)targetObject.transform.position,
            "sensor target uses collider center with offset pivot");
        Check(sensor.CanHit(target, true, .05f, 0) && !sensor.CanHit(target, false, .05f, 0), "sensor preserves facing gate");
        target.enabled = false;
        Check(sensor.TargetPoint(targetObject.transform, target) == (Vector2)targetObject.transform.position,
            "disabled target collider preserves pivot fallback");
        target.enabled = true;
        var point = new GameObject("SensorAttackPoint"); point.transform.position = target.bounds.center;
        Physics2D.SyncTransforms();
        sensor.QueryAttackHits(point.transform, .1f, 1);
        Check(hits.Contains(target) && hits.Contains(child.GetComponent<Collider2D>()), "sensor queries body and child triggers");
        var legacy = new System.Collections.Generic.List<Collider2D>();
        Physics2D.OverlapCircle(point.transform.position, .1f,
            new ContactFilter2D { useLayerMask = true, layerMask = 1, useTriggers = true }, legacy);
        Check(hits.Count == legacy.Count && hits.TrueForAll(legacy.Contains), "sensor overlap parity with original query");
        sensor.QueryAttackHits(point.transform, 0, 1); Check(hits.Count == 0, "zero attack range clears prior results");
        sensor.QueryAttackHits(null, .1f, 1); Check(hits.Count == 0, "missing attack point clears prior results");
        sensor.QueryAttackHits(point.transform, .1f, 0); Check(hits.Count == 0, "changed player mask is applied immediately");
        var wall = new GameObject("SensorWall", typeof(BoxCollider2D)); wall.layer = 6;
        wall.transform.position = (sensor.BodyCenter + (Vector2)target.bounds.center) * .5f;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 3);
        Physics2D.SyncTransforms();
        Check(!sensor.HasLineOfSight(target.bounds.center, 1 << 6) &&
            !sensor.CanHit(target, true, .05f, 1 << 6), "solid wall blocks both perception and attack geometry");
        wall.GetComponent<BoxCollider2D>().isTrigger = true; Physics2D.SyncTransforms();
        Check(sensor.HasLineOfSight(target.bounds.center, 1 << 6), "trigger wall cannot occlude perception");
        Check(sensor.HasLineOfSight(target.bounds.center, 0), "zero obstacle mask bypasses sight query");
        Object.Destroy(bodyObject); Object.Destroy(targetObject); Object.Destroy(point); Object.Destroy(wall);
    }
    private static void CheckEnemyAcquisitionAndMotor()
    {
        int searches = 0; GameObject found = null; float next = 0;
        var acquisition = new EnemyTargetAcquisition(() => { searches++; return found; });
        Check(!acquisition.TryAcquire(null, 0, ref next, out _) && searches == 1 && next == .5f,
            "missing target advances original search clock");
        Check(!acquisition.TryAcquire(null, .49f, ref next, out _) && searches == 1, "target search throttled before deadline");
        found = new GameObject("AcquisitionFixture", typeof(BoxCollider2D));
        found.transform.position = new Vector3(6200, 6200);
        Check(acquisition.TryAcquire(null, .5f, ref next, out var target) && searches == 2 &&
            target.Transform == found.transform && target.Collider == found.GetComponent<Collider2D>() && !target.Controller,
            "deadline search returns exact target component snapshot");
        Check(!acquisition.TryAcquire(target.Transform, 20, ref next, out _) && searches == 2,
            "valid cached reference prevents repeated lookup");
        found.SetActive(false);
        Check(!EnemyTargetAcquisition.IsAlive(target.Transform, target.Controller) &&
            !acquisition.TryAcquire(target.Transform, 20, ref next, out _) && searches == 2,
            "inactive cached target remains cached but not alive");
        found.SetActive(true);
        Check(EnemyTargetAcquisition.IsAlive(target.Transform, target.Controller), "reactivated target is reused");
        found.GetComponent<Collider2D>().enabled = false;
        Check(EnemyTargetAcquisition.IsAlive(target.Transform, target.Controller), "collider disable alone does not change target life");
        Object.DestroyImmediate(found); // Generated fixture only, never a scene-authored object/asset.
        found = new GameObject("ReplacementFixture", typeof(BoxCollider2D)); found.transform.position = new Vector3(6200, 6200);
        Check(acquisition.TryAcquire(target.Transform, 21, ref next, out target) && searches == 3 &&
            target.Transform == found.transform, "destroyed Unity reference permits replacement lookup");
        var player = Object.FindFirstObjectByType<PlayerController1>();
        player.SetDead(true);
        Check(!EnemyTargetAcquisition.IsAlive(player.transform, player), "dead real player is not a live target");
        player.ResetForRespawn(true);
        Check(EnemyTargetAcquisition.IsAlive(player.transform, player), "respawn restores real target life");
        Object.Destroy(found);

        var go = new GameObject("MotorBody", typeof(Rigidbody2D), typeof(BoxCollider2D));
        go.transform.position = new Vector3(6500, 6501);
        var rb = go.GetComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
        var box = go.GetComponent<BoxCollider2D>();
        var floor = new GameObject("MotorFloor", typeof(BoxCollider2D)); floor.layer = 6;
        floor.transform.position = new Vector3(6500, 6500); floor.GetComponent<BoxCollider2D>().size = new Vector2(4, 1);
        var motor = new EnemyNavigationMotor(rb, box);
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        var hits = new RaycastHit2D[8];
        var ground = new EnemyNavigationSettings(1 << 6, true, .5f, .02f);
        var flying = new EnemyNavigationSettings(1 << 6, false, .5f, .02f);
        Physics2D.SyncTransforms();
        Check(motor.CanMove(1, .1f, ground, ref filter, hits) && motor.CanMove(-1, .1f, ground, ref filter, hits),
            "motor accepts supported travel in both directions");
        var wall = new GameObject("MotorWall", typeof(BoxCollider2D)); wall.layer = 6;
        wall.transform.position = new Vector3(6500.6f, 6501); wall.GetComponent<BoxCollider2D>().size = new Vector2(.15f, 2);
        Physics2D.SyncTransforms();
        Check(!motor.CanMove(1, .1f, ground, ref filter, hits), "motor body sweep blocks wall");
        wall.GetComponent<BoxCollider2D>().size = new Vector2(.15f, .2f);
        wall.transform.position = new Vector3(6500.6f, 6500.62f); Physics2D.SyncTransforms();
        Check(!motor.CanMove(1, .1f, ground, ref filter, hits), "motor body sweep blocks low obstacle");
        wall.GetComponent<BoxCollider2D>().isTrigger = true; Physics2D.SyncTransforms();
        Check(motor.CanMove(1, .1f, ground, ref filter, hits), "motor ignores trigger terrain");
        floor.GetComponent<BoxCollider2D>().enabled = false; Physics2D.SyncTransforms();
        Check(!motor.CanMove(1, .1f, ground, ref filter, hits) && motor.CanMove(1, .1f, flying, ref filter, hits),
            "unsupported ground enemy stops; configured flyer may travel");
        rb.linearVelocity = new Vector2(1, -2);
        motor.ApplyHorizontalVelocity(3, .02f, ground, ref filter, hits);
        Check(rb.linearVelocity == new Vector2(0, -2), "blocked motor preserves vertical velocity");
        motor.ApplyHorizontalVelocity(3, .02f, flying, ref filter, hits);
        Check(rb.linearVelocity == new Vector2(3, -2), "flyer horizontal command preserves aerial vertical velocity");
        var noTerrain = new EnemyNavigationSettings(0, true, .5f, .02f);
        Check(motor.CanMove(1, .1f, noTerrain, ref filter, hits), "zero terrain mask retains original navigation bypass");
        var noCollider = new EnemyNavigationMotor(rb, null);
        Check(noCollider.CanMove(1, .1f, ground, ref filter, hits), "missing body collider retains original bypass");
        rb.bodyType = RigidbodyType2D.Static; var before = rb.linearVelocity;
        motor.ApplyHorizontalVelocity(99, .02f, ground, ref filter, hits);
        Check(rb.linearVelocity == before, "static body is never assigned motor velocity");
        Object.Destroy(go); Object.Destroy(floor); Object.Destroy(wall);
    }
    private static void CheckCore()
    {
        Check(!new DamageRequest(double.NaN).IsValid && !new DamageRequest(double.PositiveInfinity).IsValid &&
            !new DamageRequest(0).IsValid && !new DamageRequest(-1).IsValid, "invalid amounts");
        Check(new DamageRequest(int.MaxValue).IsInteger && !new DamageRequest(1.5).IsInteger &&
            !new DamageRequest((double)int.MaxValue + 1).IsInteger, "integer range and precision");
        var signal = new DamageSignal(error => faults++);
        faults = 0; int calls = 0;
        Action<DamageResult> bad = result => throw new Exception("Expected observer failure");
        Action<DamageResult> good = result => calls++;
        signal.Resolved += bad; signal.Resolved += good;
        var applied = new DamageResult(DamageStatus.Applied, new DamageRequest(100), 3, 0);
        Check(applied.AppliedAmount == 3 && applied.Killed, "overkill reports actual health loss");
        signal.Publish(applied);
        Check(faults == 1 && calls == 1, "faulty observer cannot suppress next observer");
        signal.Resolved -= bad; signal.Resolved -= good; signal.Publish(applied);
        Check(faults == 1 && calls == 1, "subscriber removal");
        signal.Resolved += good;
        signal.Publish(DamageResult.Rejected(DamageStatus.Paused, new DamageRequest(1), 3));
        Check(calls == 1, "rejection cannot emit confirmed damage");
    }
    private static int faults;
    private static void CheckFeedback()
    {
        int rewards = 0, events = 0; bool nested = true;
        var channel = new CombatFeedbackChannel(null, hit => rewards++, null);
        Action<ConfirmedCombatHit> observer = hit =>
        { events++; nested = channel.Publish(hit); };
        channel.Confirmed += observer;
        Check(!channel.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Melee, Vector3.zero, 1, 0)) &&
            rewards == 0 && events == 0, "unconfirmed hit cannot reward or notify");
        Check(channel.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Melee, Vector3.zero, 1, 5)) &&
            rewards == 1 && events == 1 && !nested, "multi-target window rewards once; observer cannot republish recursively");
        channel.Confirmed -= observer;
        channel.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Melee, Vector3.zero, 1));
        Check(rewards == 2 && events == 1, "feedback consumer unsubscribe");
        Check(!new ConfirmedCombatHit(CombatFeedbackKind.ReflectedBoss, Vector3.zero, 2).AwardsCombo &&
            !new ConfirmedCombatHit(CombatFeedbackKind.ReflectedEnemy, Vector3.zero, 2).AwardsCombo &&
            new ConfirmedCombatHit(CombatFeedbackKind.Parry, Vector3.zero, 0).AwardsCombo, "reflection/parry reward policy");
        Check(!new ConfirmedCombatHit(CombatFeedbackKind.Deflection, Vector3.zero, 1, awardCombo: false).AwardsCombo,
            "deflection presentation independent from reward");
        var melee = new ConfirmedCombatHit(CombatFeedbackKind.Melee, Vector3.zero, 3, 2, 3, true);
        Check(melee.AttackSlot == 3 && melee.FireTalisman && melee.AcceptedTargets == 2, "immutable melee feedback settings");
        string order = "";
        var staged = new CombatFeedbackChannel(hit => order += "impact,", hit => order += "reward,", hit => order += "after");
        staged.PresentImpact(new ConfirmedCombatHit(CombatFeedbackKind.Ultimate, Vector3.zero, 3));
        order += "stagger,";
        staged.PublishAfterImpact(new ConfirmedCombatHit(CombatFeedbackKind.Ultimate, Vector3.zero, 3));
        Check(order == "impact,stagger,reward,after", "ultimate staged presentation preserves physical stagger ordering");

        var a = new GameObject("FeedbackTargetA", typeof(BoxCollider2D), typeof(EnemyHealth1));
        var b = new GameObject("FeedbackTargetB", typeof(BoxCollider2D), typeof(EnemyHealth1));
        a.transform.position = new Vector3(2000, 2000); b.transform.position = new Vector3(2000.2f, 2000);
        var child = new GameObject("DuplicateHitbox", typeof(BoxCollider2D));
        child.transform.SetParent(a.transform, false);
        var swordObject = new GameObject("FeedbackSword", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SwordProjectile));
        var sword = swordObject.GetComponent<SwordProjectile>(); sword.damage = 1;
        int before = ComboSystem.Instance.CurrentCombo, swordEvents = 0;
        sword.CombatFeedback.Confirmed += hit =>
        {
            swordEvents++;
            sword.SendMessage("OnTriggerEnter2D", child.GetComponent<Collider2D>());
        };
        Time.timeScale = 0; sword.SendMessage("OnTriggerEnter2D", a.GetComponent<Collider2D>());
        Check(swordEvents == 0 && ComboSystem.Instance.CurrentCombo == before, "paused sword cannot confirm");
        Time.timeScale = 1; sword.SendMessage("OnTriggerEnter2D", a.GetComponent<Collider2D>());
        Check(swordEvents == 1 && ComboSystem.Instance.CurrentCombo == before + 1 &&
            a.GetComponent<EnemyHealth1>().CurrentHealth == 2, "sword consumes before event and rewards once");
        HitStop.Cancel(); Time.timeScale = 1;
        var fireObject = new GameObject("FeedbackFire", typeof(Rigidbody2D), typeof(FireballController));
        fireObject.transform.position = a.transform.position;
        var fire = fireObject.GetComponent<FireballController>(); fire.burstDamage = 1; fire.enemyMask = ~0;
        int fireEvents = 0, targets = 0;
        fire.CombatFeedback.Confirmed += hit => { fireEvents++; targets = hit.AcceptedTargets; fire.DoBurst(); };
        Physics2D.SyncTransforms(); fire.DoBurst();
        Check(fireEvents == 1 && targets == 2 && ComboSystem.Instance.CurrentCombo == before + 2 &&
            a.GetComponent<EnemyHealth1>().CurrentHealth == 1 && b.GetComponent<EnemyHealth1>().CurrentHealth == 2,
            "fire multi-collider/two-target burst confirms and rewards once");
        HitStop.Cancel(); Time.timeScale = 1;
        var reflectedObject = new GameObject("FeedbackReflection", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(EnemyProjectile));
        var reflected = reflectedObject.GetComponent<EnemyProjectile>();
        float speed = reflected.speed;
        Check(reflected.ReceiveDeflection(new DeflectionRequest(double.NaN)).Status == DeflectionStatus.Invalid &&
            reflected.ReceiveDeflection(new DeflectionRequest(0)).Status == DeflectionStatus.Invalid &&
            !reflected.IsDeflected && reflected.speed == speed, "invalid deflection cannot mutate projectile");
        Time.timeScale = 0;
        Check(reflected.ReceiveDeflection(new DeflectionRequest(-1)).Status == DeflectionStatus.Paused, "paused deflection result");
        Time.timeScale = 1; reflected.enabled = false;
        Check(reflected.ReceiveDeflection(new DeflectionRequest(-1)).Status == DeflectionStatus.Unavailable, "disabled deflection result");
        reflected.enabled = true;
        int deflectionEvents = 0; DeflectionResult nestedDeflection = default;
        reflected.CombatFeedback.Confirmed += hit =>
        {
            if (hit.Kind != CombatFeedbackKind.Deflection) return;
            deflectionEvents++;
            nestedDeflection = reflected.ReceiveDeflection(new DeflectionRequest(1));
        };
        var deflection = reflected.ReceiveDeflection(new DeflectionRequest(-1, false));
        Check(deflection.Applied && !deflection.AwardsCombo && deflection.Direction == -1 &&
            Mathf.Approximately(reflected.speed, speed * 1.6f) && deflectionEvents == 1 &&
            ComboSystem.Instance.CurrentCombo == before + 2, "no-reward deflection still presents and notifies once");
        Check(nestedDeflection.Status == DeflectionStatus.AlreadyDeflected &&
            reflected.ReceiveDeflection(new DeflectionRequest(1)).Status == DeflectionStatus.AlreadyDeflected,
            "deflection consumes state before recursive observer");
        HitStop.Cancel(); Time.timeScale = 1;
        int reflectedEvents = 0;
        reflected.CombatFeedback.Confirmed += hit => reflectedEvents++;
        reflected.SendMessage("OnTriggerEnter2D", b.GetComponent<Collider2D>());
        Check(reflectedEvents == 1 && ComboSystem.Instance.CurrentCombo == before + 2,
            "reflected accepted damage notifies without combo");
        reflected.SendMessage("OnTriggerEnter2D", b.GetComponent<Collider2D>());
        Check(reflectedEvents == 1, "reflected projectile consumes once");
        Check(reflected.ReceiveDeflection(new DeflectionRequest(1)).Status == DeflectionStatus.Consumed, "consumed projectile result");
        HitStop.Cancel(); Time.timeScale = 1;
        Object.Destroy(a); Object.Destroy(b); Object.Destroy(fireObject); Object.Destroy(swordObject); Object.Destroy(reflectedObject);
    }
    private static void CheckProps()
    {
        var player = Object.FindFirstObjectByType<PlayerController1>();
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false; player.ResetForRespawn(true);
        player.GetComponent<Rigidbody2D>().simulated = false;
        var wallet = PlayerContext.Current.Wallet;
        var mana = player.GetComponent<PlayerMana>();
        int oldGold = wallet.Balance; float oldMana = mana.Current;
        var go = new GameObject("DamageContractProp"); go.SetActive(false);
        go.transform.position = new Vector3(4000, 4000);
        var prop = go.AddComponent<BreakableProp>();
        Set(prop, "pickupId", "damage-contract-" + Guid.NewGuid().ToString("N"));
        Set(prop, "maxHealth", 3); Set(prop, "coinRewardMin", 2); Set(prop, "coinRewardMax", 2);
        go.SetActive(true);
        int events = 0; DamageResult nested = default;
        prop.DamageResolved += result => { events++; nested = prop.ReceiveDamage(new DamageRequest(1)); };
        Check(prop.ReceiveDamage(new DamageRequest(1.5)).Status == DamageStatus.Invalid && events == 0, "prop rejects fractional damage");
        Time.timeScale = 0;
        Check(prop.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Paused, "prop pause result");
        Time.timeScale = 1;
        var partial = prop.ReceiveDamage(new DamageRequest(1));
        Check(partial.Applied && partial.AppliedAmount == 1 && !partial.Killed && prop.CurrentHealth == 2 &&
            events == 1 && nested.Status == DamageStatus.Reentrant, "prop partial hit commits and prevents observer reentry");
        wallet.TrySetBalance(int.MaxValue); mana.SetCurrentMana(0);
        var failed = prop.ReceiveDamage(new DamageRequest(int.MaxValue));
        Check(failed.Status == DamageStatus.RewardRejected && !failed.Applied && failed.HealthBefore == 2 &&
            failed.HealthAfter == 2 && !prop.IsBroken && events == 1 && !SaveRuntime.IsCollected(prop.FullId) &&
            mana.Current == 0 && wallet.Balance == int.MaxValue, "failed prop credit rolls back health/mana/marker and emits no accepted hit");
        wallet.TrySetBalance(0);
        var fatal = prop.ReceiveDamage(new DamageRequest(int.MaxValue));
        Check(fatal.Applied && fatal.Killed && fatal.AppliedAmount == 2 && prop.IsBroken && prop.CurrentHealth == 0 &&
            events == 2 && wallet.Balance == 2 && SaveRuntime.IsCollected(prop.FullId), "fatal prop result after complete reward commit");
        Check(prop.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Dead && events == 2, "broken prop cannot reward twice");
        wallet.TrySetBalance(oldGold); mana.SetCurrentMana(oldMana); Object.Destroy(go);
    }
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    private static void CheckReceiver(IDamageReceiver receiver, Func<int> health, Action<bool> enable)
    {
        int start = health(), events = 0;
        DamageResult nested = default;
        Action<DamageResult> observer = result =>
        {
            events++;
            Check(result.HealthAfter == health(), "observer sees committed health");
            nested = receiver.ReceiveDamage(new DamageRequest(1));
        };
        receiver.DamageResolved += observer;
        Check(receiver.ReceiveDamage(new DamageRequest(1.5)).Status == DamageStatus.Invalid, "integer receiver rejects fraction");
        Time.timeScale = 0;
        Check(receiver.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Paused && health() == start, "pause rejects damage");
        Time.timeScale = 1; enable(false);
        Check(receiver.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Unavailable, "disabled receiver rejects damage");
        enable(true);
        var result = receiver.ReceiveDamage(new DamageRequest(1));
        Check(result.Applied && result.AppliedAmount == 1 && health() == start - 1 && events == 1, "applied damage exactly once");
        Check(nested.Status == DamageStatus.Reentrant, "same-target observer reentry rejected");
        receiver.DamageResolved -= observer;
        result = receiver.ReceiveDamage(new DamageRequest(int.MaxValue));
        Check(result.Killed && result.HealthAfter == 0 && result.AppliedAmount == start - 1, "fatal result clamps overkill");
        Check(receiver.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Dead && events == 1, "dead target cannot emit another hit");
        HitStop.Cancel(); Time.timeScale = 1;
    }
    private static void CheckPlayer()
    {
        var player = Object.FindFirstObjectByType<PlayerController1>();
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
        player.ResetForRespawn(true);
        var body = player.GetComponent<Rigidbody2D>(); body.position = new Vector2(1000, 1000); body.gravityScale = 0;
        var hp = player.GetComponent<PlayerHealth>(); float start = hp.CurrentHealth; int events = 0;
        Action<DamageResult> observer = result => events++;
        hp.DamageResolved += observer;
        int parryConfirmations = 0;
        var parrySkill = player.GetComponent<PlayerParry>();
        bool parryLockHeld = false;
        Action<ConfirmedCombatHit> parryObserver = hit =>
        { parryConfirmations++; parryLockHeld = player.IsParrying; parrySkill.HandleParrySuccess(); };
        parrySkill.CombatFeedback.Confirmed += parryObserver;
        Check(hp.ReceiveDamage(new DamageRequest(double.MaxValue)).Status == DamageStatus.Invalid, "player rejects overflowing float");
        Check(player.GetComponent<PlayerDash>().TryDash(), "real player dash begins");
        Check(hp.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Invulnerable && events == 0, "dash rejected without hit event");
        player.ResetForRespawn(true);
        Check(player.GetComponent<PlayerParry>().TryStartParry(), "real player parry begins");
        var parry = hp.ReceiveDamage(new DamageRequest(1));
        Check(parry.Status == DamageStatus.Parried && !parry.Applied && parry.AppliedAmount == 0 &&
            hp.CurrentHealth == start && events == 1, "parry is distinct from damage");
        Check(parryConfirmations == 1 && parryLockHeld && !player.IsParrying,
            "parry consumes reward window before reentry but holds action lock through notification");
        parrySkill.CombatFeedback.Confirmed -= parryObserver;
        HitStop.Cancel(); Time.timeScale = 1; player.ResetForRespawn(true);
        var result = hp.ReceiveDamage(new DamageRequest(.5));
        Check(result.Applied && result.AppliedAmount == .5 && events == 2, "fractional player damage preserved");
        Check(hp.ReceiveDamage(new DamageRequest(1)).Status == DamageStatus.Invulnerable && events == 2, "hurt window rejects repeat");
        player.ResetForRespawn(true);
        result = hp.ReceiveDamage(new DamageRequest(float.MaxValue));
        Check(result.Killed && player.IsDead && hp.CurrentHealth == 0 && events == 3, "player fatal result and life state committed");
        hp.DamageResolved -= observer;
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("FAIL: " + message); assertions++; }
    private static void Finish(int code)
    { Time.timeScale = 1; SessionState.SetBool(Running, false); EditorApplication.Exit(code); }
}
