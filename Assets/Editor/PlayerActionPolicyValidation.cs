using System;
using Samurai.Core.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerActionPolicyValidation
{
    private const string Running = "PlayerActionPolicyValidation.Running";
    private static int assertions;
    private static double until, deadline;
    static PlayerActionPolicyValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        try
        {
            CheckPurePolicy();
            CheckOwnership();
            CheckInputArbitration();
            SessionState.SetInt(Running + ".Assertions", assertions);
            PlayerSettings.companyName = "SamuraiPlayerPolicyValidation";
            PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void CheckPurePolicy()
    {
        int snapshots = 0;
        for (int g = 0; g < 64; g++)
        for (int a = 0; a < 128; a++)
        for (int animation = 0; animation < 4; animation++)
        {
            bool active = (g & 1) == 0, dead = (g & 2) != 0, hurt = (g & 4) != 0,
                paused = (g & 8) != 0, transition = (g & 16) != 0, respawn = (g & 32) != 0;
            bool dash = (a & 1) != 0, melee = (a & 2) != 0, cast = (a & 4) != 0,
                parry = (a & 8) != 0, focus = (a & 16) != 0, ultimate = (a & 32) != 0, recovery = (a & 64) != 0;
            bool actionAnimation = (animation & 1) != 0, dashAnimation = (animation & 2) != 0;
            bool continuing = active && !dead && !hurt && !transition && !respawn;
            bool act = continuing && !paused && !ultimate;
            var state = new PlayerActionState((PlayerGate)g, (PlayerAction)a, actionAnimation, dashAnimation);
            Check(state.CanContinueAction == continuing && state.CanAct == act &&
                state.CanCollectPickups == (active && !dead && !paused && !transition && !respawn) &&
                state.CanStartAction == (act && !dash && !melee && !cast && !parry && !recovery && !focus && !actionAnimation) &&
                state.CanStartDash == (act && !dash && !cast && !parry && !focus && !recovery && !dashAnimation) &&
                state.CanLocomote == (act && !dash && !melee && !cast && !parry && !focus),
                $"legacy gate parity at gates={g}, actions={a}, animation={animation}");
            snapshots++;
        }
        Check(snapshots == 32768, "complete known-state truth table");
        var idle = new PlayerActionState(PlayerGate.None, PlayerAction.None);
        var blocked = idle.WithAnimationBlocks(true, true);
        Check(idle.CanStartAction && idle.CanStartDash && !blocked.CanStartAction && !blocked.CanStartDash,
            "snapshot is immutable; animator blocks only start gates");
        Debug.Log("[PlayerActionPolicyValidation] PURE PARITY PASSED: 32768 snapshots, six eligibility predicates");
    }
    private static void CheckOwnership()
    {
        var owner = new PlayerActionOwnership();
        Check(!owner.TryReserve(PlayerActionKind.None, out _) &&
            !owner.TryReserve(PlayerActionKind.ParryRecovery, out _), "non-startable owner kinds rejected");
        Check(owner.TryReserve(PlayerActionKind.Melee, out var melee) && owner.HasReservation &&
            owner.CurrentAction == PlayerActionKind.None && !owner.IsOwner(melee), "reservation is not active execution");
        Check(!owner.TryReserve(PlayerActionKind.Fire, out _), "pending startup excludes a second command");
        Check(owner.Commit(melee) && owner.IsOwner(melee) && !owner.HasReservation, "reservation commits to one owner");
        Check(owner.TryReserve(PlayerActionKind.Dash, out var dash) && owner.IsOwner(melee),
            "dash reserves without canceling current melee");
        Check(owner.Release(dash) && owner.IsOwner(melee) && !owner.HasReservation, "failed dash startup leaves melee owner intact");
        Check(owner.TryReserve(PlayerActionKind.Dash, out dash) && owner.Commit(dash) &&
            !owner.Release(melee) && owner.IsOwner(dash), "committed dash replaces melee; stale release cannot clear dash");
        owner.CancelAll();
        Check(!owner.IsOwner(dash) && !owner.Release(dash) && owner.CurrentAction == PlayerActionKind.None,
            "lifecycle cancellation invalidates owner");
        Check(owner.TryReserve(PlayerActionKind.Fire, out var fire) && fire.Generation > dash.Generation && owner.Commit(fire),
            "generation never reused across resets");
        var foreign = new PlayerActionOwnership();
        foreign.TryReserve(PlayerActionKind.Fire, out var foreignToken); foreign.Commit(foreignToken);
        Check(!owner.Release(foreignToken) && !owner.IsOwner(foreignToken) && owner.IsOwner(fire), "tokens scoped to actor identity");
        Check(!owner.Commit(melee) && owner.IsOwner(fire), "stale commit cannot overwrite owner");
        owner.CancelAll();
        owner.TryReserve(PlayerActionKind.Dive, out var dive); owner.Commit(dive);
        Check(owner.TryReserve(PlayerActionKind.Dash, out dash) && owner.Commit(dash) && !owner.Release(dive),
            "dive preserves melee-to-dash cancellation");
        owner.CancelAll();
        owner.TryReserve(PlayerActionKind.Parry, out var parry); owner.Commit(parry);
        Check(owner.EnterParryRecovery(parry) && owner.IsOwner(parry) &&
            owner.CurrentAction == PlayerActionKind.ParryRecovery && !owner.TryReserve(PlayerActionKind.Dash, out _),
            "parry recovery retains identity and excludes dash");
        Check(owner.Release(parry) && owner.CurrentAction == PlayerActionKind.None, "recovery completes with original token");
        owner.TryReserve(PlayerActionKind.Focus, out var pending); owner.CancelAll();
        Check(!owner.Commit(pending) && !owner.HasReservation, "lifecycle cancellation invalidates pending startup");
        typeof(PlayerActionOwnership).GetField("sequence", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(owner, ulong.MaxValue);
        Check(!owner.TryReserve(PlayerActionKind.Fire, out _), "generation overflow fails closed");
        var reserved = new PlayerActionState(PlayerGate.ActionReservation, PlayerAction.None);
        Check(reserved.CanAct && reserved.CanContinueAction && reserved.CanCollectPickups &&
            !reserved.CanStartAction && !reserved.CanStartDash && !reserved.CanLocomote,
            "reservation blocks new commands/motor without interrupting life or current action");
    }
    private sealed class InputProbe : IPlayerInputSink
    {
        public PlayerInputCommands Allowed, Attempted;
        public int Moves, Jumps, FocusCalls, Accepted;
        public bool JumpPressed, JumpReleased;
        public void Clear(PlayerInputCommands allowed)
        { Allowed = allowed; Attempted = 0; Moves = Jumps = FocusCalls = Accepted = 0; }
        private bool Accept(PlayerInputCommands command)
        { Attempted |= command; if ((Allowed & command) == 0) return false; Accepted++; return true; }
        public void Move(float axis) { Moves++; }
        public bool Focus(bool held) { FocusCalls++; return held && Accept(PlayerInputCommands.FocusHeld); }
        public bool Parry() => Accept(PlayerInputCommands.Parry);
        public bool Ultimate() => Accept(PlayerInputCommands.Ultimate);
        public bool Dash() => Accept(PlayerInputCommands.Dash);
        public void Jump(bool pressed, bool released) { Jumps++; JumpPressed = pressed; JumpReleased = released; }
        public bool Attack(bool up, bool down) => Accept(PlayerInputCommands.Attack);
        public bool Fire() => Accept(PlayerInputCommands.Fire);
        public bool Sword() => Accept(PlayerInputCommands.Sword);
    }
    private static void CheckInputArbitration()
    {
        var priority = new[] { PlayerInputCommands.FocusHeld, PlayerInputCommands.Parry, PlayerInputCommands.Ultimate,
            PlayerInputCommands.Dash, PlayerInputCommands.Attack, PlayerInputCommands.Fire, PlayerInputCommands.Sword };
        var allowed = new PlayerInputCommands[128];
        for (int bits = 0; bits < 128; bits++)
            for (int i = 0; i < priority.Length; i++) if ((bits & (1 << i)) != 0) allowed[bits] |= priority[i];
        var probe = new InputProbe(); int cases = 0;
        for (int mask = 0; mask < 2048; mask++)
        for (int acceptance = 0; acceptance < 128; acceptance++)
        {
            var frame = new PlayerInputFrame(.75f, (PlayerInputCommands)mask);
            probe.Clear(allowed[acceptance]);
            var winner = PlayerInputArbitration.Dispatch(frame, probe);
            PlayerInputCommands expected = 0, attempts = 0;
            foreach (var request in priority)
            {
                if (!frame.Has(request)) continue;
                attempts |= request;
                if ((allowed[acceptance] & request) == 0) continue;
                expected = request; break;
            }
            Check(winner == expected && probe.Attempted == attempts && probe.Accepted <= 1 &&
                probe.Moves == 1 && probe.Jumps == 1 && probe.FocusCalls == 1 &&
                probe.JumpPressed == frame.Has(PlayerInputCommands.JumpPressed) &&
                probe.JumpReleased == frame.Has(PlayerInputCommands.JumpReleased),
                $"input priority/fallback/single-dispatch mask={mask}, acceptance={acceptance}");
            cases++;
        }
        Check(cases == 262144, "complete input/acceptance matrix");
        Check(new PlayerInputFrame(float.NaN, 0).Movement == 0 && new PlayerInputFrame(float.PositiveInfinity, 0).Movement == 0 &&
            new PlayerInputFrame(3, 0).Movement == 1 && new PlayerInputFrame(-3, 0).Movement == -1,
            "synthetic movement frame rejects nonfinite axis and clamps bounds");
        Debug.Log("[PlayerActionPolicyValidation] INPUT MATRIX PASSED: 262144 cases");
    }
    private static void CheckRealInput(PlayerController1 player)
    {
        var mana = player.GetComponent<PlayerMana>();
        var fire = player.GetComponent<PlayerFireSkill>();
        var sword = player.GetComponent<PlayerSwordSkill>();
        var jump = player.GetComponent<PlayerJump>();
        var combat = player.GetComponent<PlayerCombat>();
        Reset(player);
        var ownership = (PlayerActionOwnership)typeof(PlayerController1).GetField("actionOwnership",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
        ownership.TryReserve(PlayerActionKind.Melee, out var reservation);
        Check(player.ProcessInputFrame(new PlayerInputFrame(1, PlayerInputCommands.Fire | PlayerInputCommands.JumpPressed)) == 0 &&
            player.HasActionReservation && mana.Current == 100 &&
            (float)typeof(PlayerJump).GetField("jumpBufferCounter", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).GetValue(jump) == 0, "reentrant frame during startup cannot move, buffer or spend");
        player.ReleaseAction(reservation);
        var cleanup = typeof(PlayerController1).GetField("actionCleanupDepth",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        cleanup.SetValue(player, 1);
        try
        {
            Check(player.ProcessInputFrame(new PlayerInputFrame(1, PlayerInputCommands.Sword | PlayerInputCommands.JumpPressed)) == 0 &&
                player.OwnedAction == PlayerActionKind.None && mana.Current == 100 &&
                (float)typeof(PlayerJump).GetField("jumpBufferCounter", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(jump) == 0, "reentrant frame during cleanup cannot buffer or start");
        }
        finally { cleanup.SetValue(player, 0); }
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Fire | PlayerInputCommands.Sword)) ==
            PlayerInputCommands.Fire && player.OwnedAction == PlayerActionKind.Fire && mana.Current == 100 - fire.manaCost &&
            !sword.IsCasting, "synthetic simultaneous I/U deterministically casts fire and spends once");
        Reset(player);
        var prefab = fire.fireballPrefab; fire.fireballPrefab = null;
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Fire | PlayerInputCommands.Sword)) ==
            PlayerInputCommands.Sword && player.OwnedAction == PlayerActionKind.Sword && mana.Current == 100 - sword.manaCost,
            "invalid higher-priority fire falls through to sword without double spending");
        fire.fireballPrefab = prefab; Reset(player);
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Attack | PlayerInputCommands.Fire)) ==
            PlayerInputCommands.Attack && player.OwnedAction == PlayerActionKind.Melee && mana.Current == 100,
            "melee preserves controller priority over standalone cast");
        Reset(player);
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Parry | PlayerInputCommands.Ultimate |
            PlayerInputCommands.Dash | PlayerInputCommands.Attack | PlayerInputCommands.Fire)) == PlayerInputCommands.Parry &&
            player.OwnedAction == PlayerActionKind.Parry && mana.Current == 100, "parry wins defensive/offensive same-frame chord");
        Reset(player);
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Ultimate | PlayerInputCommands.Dash)) ==
            PlayerInputCommands.Ultimate && player.OwnedAction == PlayerActionKind.Ultimate &&
            mana.Current == 100 - player.GetComponent<PlayerUltimateSkill>().ManaCost, "ultimate preserves priority over dash");
        Reset(player);
        player.GetComponent<PlayerHealth>().SetCurrentHealth(player.GetComponent<PlayerHealth>().MaxHealth - 1);
        typeof(PlayerJump).GetField("grounded", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(jump, true);
        Check(player.ProcessInputFrame(new PlayerInputFrame(-1, PlayerInputCommands.FocusHeld | PlayerInputCommands.Parry |
            PlayerInputCommands.Ultimate)) == PlayerInputCommands.FocusHeld && player.OwnedAction == PlayerActionKind.Focus &&
            player.transform.localScale.x < 0, "movement facing precedes eligible focus at original controller priority");
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, 0)) == 0 && player.OwnedAction == PlayerActionKind.None,
            "released focus cancels even without a requested exclusive command");
        Reset(player);
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Dash | PlayerInputCommands.JumpPressed)) ==
            PlayerInputCommands.Dash && player.OwnedAction == PlayerActionKind.Dash &&
            (float)typeof(PlayerJump).GetField("jumpBufferCounter", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).GetValue(jump) > 0, "dash wins while jump press still buffers once");
        Reset(player);
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, PlayerInputCommands.JumpPressed | PlayerInputCommands.Attack |
            PlayerInputCommands.AttackUpHeld)) == PlayerInputCommands.Attack &&
            player.GetComponent<Rigidbody2D>().linearVelocity.y > 0 &&
            (int)typeof(PlayerCombat).GetField("currentAttackSlot", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).GetValue(combat) == 2, "jump precedes directional melee without losing upward attack modifier");
        Reset(player);
        Time.timeScale = 0;
        Check(player.ProcessInputFrame(new PlayerInputFrame(1, PlayerInputCommands.Fire | PlayerInputCommands.JumpPressed)) == 0 &&
            player.OwnedAction == PlayerActionKind.None && mana.Current == 100, "paused synthetic input cannot start/spend");
        Time.timeScale = 1;
        Check(player.ProcessInputFrame(new PlayerInputFrame(0, 0)) == 0 && !fire.IsCasting && player.OwnedAction == PlayerActionKind.None,
            "resume does not replay discarded input frame");
        player.enabled = false;
        Check(player.ProcessInputFrame(new PlayerInputFrame(1, PlayerInputCommands.Sword)) == 0 && mana.Current == 100,
            "disabled controller rejects synthetic command batch");
        player.enabled = true; Reset(player);
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 40; until = now + 2; assertions = SessionState.GetInt(Running + ".Assertions", 0); }
            if (now > deadline) throw new TimeoutException("Player policy timeout");
            if (now < until || SceneTransition.IsBusy) return;
            var player = Object.FindFirstObjectByType<PlayerController1>();
            foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
            player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
            var body = player.GetComponent<Rigidbody2D>(); body.simulated = false;
            player.transform.position = new Vector3(1000, 1000);
            Reset(player);
            Check(player.CanAct && player.CanContinueAction && player.CanCollectPickups &&
                player.CanStartAction && player.CanStartDash && player.CanLocomote, "real idle actor gates");
            Check(player.OwnedAction == PlayerActionKind.None && !player.HasActionReservation,
                "respawn clears all authority");
            Check(!player.TryStartOwnedAction(PlayerActionKind.Fire, null, 0, out _) &&
                !player.TryStartOwnedAction(PlayerActionKind.Melee, null, float.NaN, out _),
                "owned startup rejects missing mana service and invalid costs");
            Check(player.TryStartOwnedAction(PlayerActionKind.Melee, out var token) && !player.IsAttacking &&
                !player.CanStartAction && player.CanStartDash && !player.CanLocomote,
                "authority blocks startup even before compatibility module flags are set");
            player.ReleaseAction(token);
            Time.timeScale = 0;
            Check(player.CanContinueAction && !player.CanAct && !player.CanCollectPickups &&
                !player.CanStartAction && !player.CanStartDash && !player.CanLocomote, "pause suspends action without life interruption");
            Time.timeScale = 1; player.SetHurting(true);
            Check(!player.CanAct && !player.CanContinueAction && player.CanCollectPickups &&
                !player.CanStartAction && !player.CanStartDash && !player.CanLocomote, "hurt preserves existing pickup exception");
            player.SetDead(true);
            Check(!player.CanCollectPickups && !player.CanAct, "death dominates hurt and pickup permissions");
            Reset(player);
            var fire = player.GetComponent<PlayerFireSkill>(); var sword = player.GetComponent<PlayerSwordSkill>();
            player.GetComponent<PlayerMana>().SetCurrentMana(0);
            Check(!fire.TryCastFire() && player.OwnedAction == PlayerActionKind.None && !player.HasActionReservation &&
                fire.SkillCooldownRemaining == 0, "failed mana spend rolls back reservation without cooldown");
            player.GetComponent<PlayerMana>().SetCurrentMana(100);
            Check(fire.TryCastFire() && player.IsCasting && !player.CanLocomote &&
                !sword.TryUseSwordSkill() && !player.CanStartDash, "same-frame cast excludes sword/dash/locomotion");
            player.ReleaseAction(token);
            Check(player.OwnedAction == PlayerActionKind.Fire, "stale actor release cannot clear current fire owner");
            fire.enabled = false;
            Check(player.OwnedAction == PlayerActionKind.None && !player.HasActionReservation, "module disable releases ownership");
            fire.enabled = true;
            Reset(player);
            Check(sword.TryUseSwordSkill() && !fire.TryCastFire(), "reverse same-frame cast order also excludes second command");
            Check(player.OwnedAction == PlayerActionKind.Sword, "real sword module owns actor");
            Reset(player);
            var combat = player.GetComponent<PlayerCombat>();
            Check(combat.TryAttack(1) && !player.CanStartAction && player.CanStartDash && !player.CanLocomote,
                "melee preserves explicit dash-cancel permission");
            player.GetComponent<PlayerMana>().SetCurrentMana(0);
            Check(!player.GetComponent<PlayerDash>().TryDash() && player.IsAttacking &&
                player.OwnedAction == PlayerActionKind.Melee && !player.HasActionReservation,
                "unfunded dash cannot cancel melee or leak reservation");
            player.GetComponent<PlayerMana>().SetCurrentMana(100);
            Check(player.GetComponent<PlayerDash>().TryDash() && player.IsDashing && !player.IsAttacking,
                "dash cancels melee through the existing execution path");
            Check(player.OwnedAction == PlayerActionKind.Dash, "melee cleanup cannot release newly transferred dash token");
            Reset(player);
            Check(combat.TryDiveAttack() && player.OwnedAction == PlayerActionKind.Dive, "real dive owns actor");
            Check(player.GetComponent<PlayerDash>().TryDash() && !combat.IsDiving &&
                player.OwnedAction == PlayerActionKind.Dash, "dash transfer cancels dive coroutine without releasing dash");
            player.enabled = false;
            Check(!player.CanAct && !player.CanContinueAction && !player.CanCollectPickups &&
                !player.CanLocomote && !player.IsDashing && !player.IsCasting, "disabled controller releases actions and gates");
            player.enabled = true; Reset(player);
            var parry = player.GetComponent<PlayerParry>();
            Check(parry.TryStartParry() && !player.CanStartAction && !player.CanStartDash && !player.CanLocomote,
                "active parry excludes other actions and locomotion");
            player.OnParrySuccess(); HitStop.Cancel(); Time.timeScale = 1;
            Check(parry.IsRecovering && !player.IsParrying && !player.CanStartAction && !player.CanStartDash &&
                player.CanAct && player.CanLocomote, "recovery blocks new actions but preserves walking");
            Check(player.OwnedAction == PlayerActionKind.ParryRecovery, "real parry retains recovery phase ownership");
            Reset(player);
            var focus = player.GetComponent<PlayerFocusHeal>();
            bool focused = (bool)typeof(PlayerFocusHeal).GetMethod("StartFocus", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(focus, null);
            Check(focused && focus.IsFocusing && player.OwnedAction == PlayerActionKind.Focus &&
                !player.CanLocomote && !player.CanStartDash, "real focus startup owns actor");
            focus.CancelFocus();
            Check(player.OwnedAction == PlayerActionKind.None && !focus.IsFocusing, "focus cancellation releases owner");
            Reset(player);
            Check(player.GetComponent<PlayerUltimateSkill>().TryExecuteUltimate() && !player.CanAct &&
                player.CanContinueAction && player.CanCollectPickups && !player.CanLocomote,
                "ultimate owns presentation/motion without changing pickup contract");
            Check(player.OwnedAction == PlayerActionKind.Ultimate, "real ultimate owns actor");
            player.InterruptActions(); HitStop.Cancel(); Time.timeScale = 1;
            Check(!player.IsUltimateExecuting && player.CanAct && player.OwnedAction == PlayerActionKind.None &&
                !player.HasActionReservation, "interruption removes ultimate authority and reservation");
            Reset(player);
            player.SetHurting(true);
            var movement = player.GetComponent<PlayerMovement>();
            typeof(PlayerMovement).GetField("xInput", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(movement, 1f);
            body.linearVelocity = new Vector2(123, 0);
            float vx = body.linearVelocity.x;
            movement.FixedUpdateMovement();
            Check(body.linearVelocity.x == vx, "direct motor invocation cannot bypass lifecycle gate");
            Reset(player);
            CheckRealInput(player);
            Debug.Log($"[PlayerActionPolicyValidation] ALL CHECKS PASSED ({assertions} assertions)");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void Reset(PlayerController1 player)
    {
        HitStop.Cancel(); Time.timeScale = 1; player.ResetForRespawn(true);
        player.GetComponent<PlayerMana>().SetCurrentMana(100);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("FAIL: " + message); assertions++; }
    private static void Finish(int code)
    { SessionState.SetBool(Running, false); HitStop.Cancel(); Time.timeScale = 1; EditorApplication.Exit(code); }
}
