using System;
using WoLMomentumSpear;
using UnityEngine;

internal static class Behavior
{
    private static int count;
    private static void Check(bool good, string name) { count++; if (!good) throw new Exception(name); }
    private static void Equal(float actual, float expected, string name) { Check(Math.Abs(actual - expected) < .0001f, name + ": " + actual + " != " + expected); }
    private static void Exit(SpearState s) { s.OnExit(); }

    public static int Main()
    {
        try
        {
            Equal(SpearRules.MomentumMultiplier(0, 0, .1f, 2), 1, "stationary baseline");
            Equal(SpearRules.MomentumMultiplier(3, 4, .1f, 2), 1.5f, "actual diagonal speed five");
            Equal(SpearRules.MomentumMultiplier(-3, -4, .1f, 2), 1.5f, "speed magnitude independent of direction");
            Equal(SpearRules.MomentumMultiplier(5, 0, .1f, 2), 1.5f, "same axis speed");
            Equal(SpearRules.MomentumMultiplier(6, 8, .1f, 2), 2, "default cap boundary");
            Equal(SpearRules.MomentumMultiplier(100, 0, .1f, 2), 2, "dash speed capped");
            Equal(SpearRules.MomentumMultiplier(float.MaxValue, float.MaxValue, .1f, 2), 2, "finite huge speed does not overflow");
            Equal(SpearRules.MomentumMultiplier(float.NaN, 4, .1f, 2), 1, "NaN velocity neutral");
            Equal(SpearRules.MomentumMultiplier(4, float.PositiveInfinity, .1f, 2), 1, "infinite velocity neutral");
            Equal(SpearRules.MomentumMultiplier(5, 0, 0, 2), 1, "zero coefficient disables bonus");
            Equal(SpearRules.MomentumMultiplier(5, 0, -1, 2), 1, "negative coefficient bounded");
            Equal(SpearRules.MomentumMultiplier(5, 0, float.NaN, 2), 1.5f, "invalid coefficient defaults");
            Equal(SpearRules.MomentumMultiplier(5, 0, .1f, float.NaN), 1.5f, "invalid cap defaults");
            Equal(SpearRules.MomentumMultiplier(5, 0, .1f, .5f), 1, "cap never reduces base damage");
            Equal(SpearRules.MomentumMultiplier(5, 0, 10f, 100f), 4, "custom settings hard bounds");
            Check(SpearRules.Step(-1) == 1 && SpearRules.Step(2) == 2 && SpearRules.Step(20) == 3, "three visual steps bounded");

            var plugin = new SpearPlugin(); Simulation.Call(plugin, "Awake");
            var registered = LegendAPI.Skills.Registered;
            Check(registered.ID == "MomentumSpearBasic" && registered.stateType == typeof(SpearState), "registered basic ID/state");
            Check(registered.skillStats.damage.Length == 3, "three damage levels");
            Check(registered.skillStats.damage[0] == 8 && registered.skillStats.damage[1] == 10 && registered.skillStats.damage[2] == 16, "default combo damage");
            Equal(registered.skillStats.cooldown[0], 0, "basic has no standard-arcana cooldown");
            Check(registered.icon != null && registered.unlockCondition(), "icon and offering");

            var player = new Player(); var fsm = new FSM(); var state = new SpearState(fsm, player);
            Check(state.isBasic && state.isMeleeSkill && state.maxComboCount == 3, "native melee/basic flags and combo count");
            Check(state.autoHoldStat.BaseValue, "native hold-to-combo enabled");
            Check(state.destroyAttackObjectOnExit, "native exit owns hitbox cleanup");
            player.movement.moveSpeedStat.BaseValue = 100;
            player.rigidbody2D.velocity = Vector2.zero;
            state.OnEnter(); state.ExecuteAttack();
            Check(state.currentAttackObject.attack.DamageForTarget() == 8, "high move stat without movement gives no bonus");
            Equal(state.currentAttackObject.attack.damageModifier, 1, "standing opening neutral");
            Check(state.currentAttackObject.GetComponentsInChildren<LineRenderer>(true).Length == 2, "spear shaft and head drawn");
            Check(!state.currentAttackObject.GetComponentsInChildren<SpriteRenderer>(true)[0].enabled, "native dagger visual replaced only after drawing");
            var standingAttack = state.currentAttackObject; Exit(state);
            Check(standingAttack.gameObject.Destroyed, "exiting destroys owned hitbox");
            Check(state.comboCounter == 2, "native progression to second hit");
            Check(!player.movement.moveSpeedStat.Mods.ContainsKey(state.atkMoveModStr), "attack movement modifier removed");

            fsm.previousStateName = "Run"; player.rigidbody2D.velocity = new Vector2(3, 4);
            state.OnEnter();
            Equal(player.rigidbody2D.velocity.x, 0, "native entry ends horizontal movement");
            Equal(player.rigidbody2D.velocity.y, 0, "native entry ends vertical movement");
            player.rigidbody2D.velocity = new Vector2(100, 0); // Post-entry acceleration must not change the sample.
            state.ExecuteAttack(); var first = state.currentAttackObject;
            Equal(first.attack.damageModifier, 1.5f, "pre-stop velocity captured, not post-entry velocity");
            Check(first.attack.DamageForTarget() == 12, "opening damage from five units per second");
            Equal(state.EventDamageModifier, 1.5f, "bonus prepared before native execute event");
            Check(first.attack.DamageForTarget() == 12 && first.attack.DamageForTarget() == 12, "same bonus for multiple targets without compounding");
            int events = state.ExecuteEvents, attacks = state.BaseAttacks;
            state.ExecuteAttack(); state.HandleComboCount(1);
            Check(state.ExecuteEvents == events && state.BaseAttacks == attacks && state.currentAttackObject == first, "duplicate execution does not spawn or announce again");
            Equal(first.attack.damageModifier, 1.5f, "repeated combo hook does not compound bonus");
            Equal(first.transform.localScale.x, .65f, "narrow spear hitbox");
            Equal(first.transform.localScale.y, 1.8f, "native forward-Y stretch");
            Exit(state);

            player.rigidbody2D.velocity = new Vector2(100, 0); state.OnEnter(); state.ExecuteAttack();
            Check(state.comboCounter == 2 && state.currentLevel == 2, "second combo level retained");
            Equal(state.currentAttackObject.attack.damageModifier, 1, "second hit ignores all momentum");
            Check(state.currentAttackObject.attack.DamageForTarget() == 10, "second damage unchanged");
            Exit(state);
            player.rigidbody2D.velocity = new Vector2(100, 0); state.OnEnter(); state.ExecuteAttack();
            Check(state.comboCounter == 3 && state.currentLevel == 3, "third combo level");
            Equal(state.currentAttackObject.attack.damageModifier, 1, "finisher ignores momentum");
            Check(state.currentAttackObject.attack.DamageForTarget() == 16, "finisher damage");
            Equal(state.currentAttackObject.transform.localScale.y, 2.25f, "finisher extended reach");
            Exit(state); Check(state.comboCounter == 1, "three-hit chain wraps");
            player.rigidbody2D.velocity = Vector2.zero; state.OnEnter(); state.ExecuteAttack();
            Equal(state.currentAttackObject.attack.damageModifier, 1, "next chain cannot reuse old momentum"); Exit(state);

            // Interrupted non-self transitions reset the next attack to a fresh opening.
            fsm.previousStateName = "Dash"; player.rigidbody2D.velocity = new Vector2(30, 0); state.OnEnter();
            state.ExecuteAttack(); Check(state.comboCounter == 1, "dash into attack starts new chain");
            Check(state.currentAttackObject.attack.DamageForTarget() == 16, "fast opening respects double cap"); Exit(state);

            MeleeAttackObject.PrefabDamageMultiplier = 1.25f; fsm.previousStateName = "Run";
            player.rigidbody2D.velocity = new Vector2(5, 0); state.OnEnter(); state.ExecuteAttack();
            Equal(state.currentAttackObject.attack.damageModifier, 1.875f, "existing prefab multiplier preserved");
            Check(state.currentAttackObject.attack.DamageForTarget() == 15, "native multiplier and momentum combine"); Exit(state); MeleeAttackObject.PrefabDamageMultiplier = 1;
            player.DamageMultiplier = 1.5f; fsm.previousStateName = "Run";
            player.rigidbody2D.velocity = new Vector2(5, 0); state.OnEnter(); state.ExecuteAttack();
            Check(state.currentAttackObject.attack.DamageForTarget() == 18, "registered/player damage pipeline preserved"); Exit(state); player.DamageMultiplier = 1;

            var otherPlayer = new Player(); var otherState = new SpearState(new FSM(), otherPlayer);
            fsm.previousStateName = "Run"; player.rigidbody2D.velocity = new Vector2(5, 0);
            state.OnEnter(); otherState.OnEnter(); otherState.ExecuteAttack(); state.ExecuteAttack();
            Check(state.currentAttackObject.attack.DamageForTarget() == 12 && otherState.currentAttackObject.attack.DamageForTarget() == 8, "co-op momentum snapshots isolated");
            Exit(state); Exit(otherState);

            fsm.previousStateName = "Run"; state.IsEmpowered = true; state.OnEnter(); state.ExecuteAttack();
            Equal(state.currentAttackObject.transform.localScale.y, 2.07f, "enhanced reach fifteen percent");
            Check(state.maxComboCount == 3, "enhancement retains three-hit combo"); Exit(state); state.IsEmpowered = false;
            Simulation.Set(plugin, "Momentum/MaximumMultiplier", 1f); fsm.previousStateName = "Run";
            player.rigidbody2D.velocity = new Vector2(100, 0); state.OnEnter(); state.ExecuteAttack();
            Equal(state.currentAttackObject.attack.damageModifier, 1, "config disables momentum bonus"); Exit(state);
            Simulation.Set(plugin, "Momentum/MaximumMultiplier", 2f);
            Simulation.Set(plugin, "Spear/ReachScale", float.NaN); Equal(SpearPlugin.ReachScale, 1.8f, "invalid reach safe default");
            Simulation.Set(plugin, "Spear/WidthScale", 100f); Equal(SpearPlugin.WidthScale, 1.5f, "width hard cap");
            Simulation.Set(plugin, "Spear/WidthScale", .65f);

            // Critical regression: the outgoing RunState stops velocity BEFORE spear.OnEnter.
            // This dispatcher explicitly simulates the prefix; actual Harmony/Unity runtime
            // behavior is reserved for gameplay testing, not claimed by this harness.
            var charging = new Player(); var transition = new FSM();
            var run = new SimRunState(transition, charging);
            var chargedSpear = new SpearState(transition, charging);
            charging.skillsDict.Add(SpearState.staticID, chargedSpear);
            transition.currentState = run; transition.currentStateName = "Run";
            Time.frameCount = 10; charging.rigidbody2D.velocity = new Vector2(3, 4);
            transition.ChangeState(SpearState.staticID);
            Equal(charging.rigidbody2D.velocity.x, 0, "outgoing run exit erased horizontal speed");
            Equal(charging.rigidbody2D.velocity.y, 0, "outgoing run exit erased vertical speed");
            chargedSpear.ExecuteAttack();
            Check(chargedSpear.currentAttackObject.attack.DamageForTarget() == 12, "pre-transition snapshot survives outgoing run stop");
            var original = chargedSpear.currentAttackObject;
            charging.rigidbody2D.velocity = new Vector2(100, 0);
            transition.ChangeState(SpearState.staticID, false); chargedSpear.ExecuteAttack();
            Check(chargedSpear.currentAttackObject == original, "rejected self-transition cannot restart attack");
            Equal(original.attack.damageModifier, 1.5f, "rejected self-transition cannot change current bonus");
            transition.ChangeState(SpearState.staticID, true); chargedSpear.ExecuteAttack();
            Check(chargedSpear.comboCounter == 2 && chargedSpear.currentAttackObject.attack.DamageForTarget() == 10, "second transition ignores captured momentum");
            charging.rigidbody2D.velocity = new Vector2(100, 0);
            transition.ChangeState(SpearState.staticID, true); chargedSpear.ExecuteAttack();
            Check(chargedSpear.comboCounter == 3 && chargedSpear.currentAttackObject.attack.DamageForTarget() == 16, "third transition unchanged");
            transition.ChangeState(SpearState.staticID, true); chargedSpear.ExecuteAttack();
            Check(chargedSpear.comboCounter == 1 && chargedSpear.currentAttackObject.attack.DamageForTarget() == 8, "new chain captures zero instead of previous chain speed");
            chargedSpear.OnExit();
            transition.previousStateName = "Run"; charging.rigidbody2D.velocity = new Vector2(100, 0);
            chargedSpear.CaptureMomentum(); Time.frameCount++; charging.rigidbody2D.velocity = Vector2.zero;
            chargedSpear.OnEnter(); chargedSpear.ExecuteAttack();
            Check(chargedSpear.currentAttackObject.attack.DamageForTarget() == 8, "stale previous-frame capture rejected");
            chargedSpear.OnExit();
            var foreignFSM = new FSM(); foreignFSM.currentState = new SimRunState(foreignFSM, charging);
            foreignFSM.currentStateName = "Run"; charging.rigidbody2D.velocity = new Vector2(100, 0);
            SpearMomentumCapture.Capture(foreignFSM, SpearState.staticID, true);
            charging.rigidbody2D.velocity = Vector2.zero; transition.previousStateName = "Run";
            chargedSpear.OnEnter(); chargedSpear.ExecuteAttack();
            Check(chargedSpear.currentAttackObject.attack.DamageForTarget() == 8, "unrelated FSM cannot seed this player skill");
            chargedSpear.OnExit();

            LineRenderer.Fail = true; fsm.previousStateName = "Run"; player.rigidbody2D.velocity = new Vector2(5, 0);
            state.OnEnter(); state.ExecuteAttack();
            Check(state.currentAttackObject.GetComponentsInChildren<SpriteRenderer>(true)[0].enabled, "failed custom drawing preserves native sprites");
            Check(state.currentAttackObject.attack.DamageForTarget() == 12, "visual failure does not alter damage");
            var partial = state.currentAttackObject; Exit(state); LineRenderer.Fail = false;
            Check(partial.gameObject.Destroyed, "failed visual lifetime owned by native hitbox");
            Simulation.Set(plugin, "General/Enabled", false); Check(!registered.unlockCondition(), "config hides new offers");
            Simulation.Call(plugin, "OnDestroy"); Check(SpearPlugin.Material == null, "shared material cleaned");
            Check(!Simulation.Patched, "plugin destruction removes its transition patch");
            fsm.previousStateName = "Run"; state.OnEnter(); state.ExecuteAttack();
            Check(state.currentAttackObject.GetComponentsInChildren<SpriteRenderer>(true)[0].enabled, "missing material preserves native look");
            Exit(state);
            Console.WriteLine(count + " checks passed (simulation; no gameplay testing)."); return 0;
        }
        catch (Exception e) { Console.WriteLine("FAILED after " + count + ": " + e); return 1; }
    }
}
