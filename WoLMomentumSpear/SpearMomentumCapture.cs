using HarmonyLib;

namespace WoLMomentumSpear
{
    // RunState.OnExit stops the rigidbody before the new state enters. Capture at the
    // transition boundary, rather than estimating speed from stats or a stale frame cache.
    [HarmonyPatch(typeof(FSM), "ChangeState", new[] { typeof(string), typeof(bool) })]
    internal static class SpearMomentumCapture
    {
        [HarmonyPrefix]
        internal static void Capture(FSM __instance, [HarmonyArgument(0)] string targetName,
            [HarmonyArgument(1)] bool allowSelfTransition)
        {
            if (targetName != SpearState.staticID || __instance == null) return;
            if (!allowSelfTransition && __instance.currentStateName == targetName) return;
            State<Player> current = __instance.currentState as State<Player>;
            Player player = current != null ? current.parent : null;
            if (player == null || player.skillsDict == null) return;
            Player.SkillState skill;
            if (!player.skillsDict.TryGetValue(SpearState.staticID, out skill)) return;
            SpearState incoming = skill as SpearState;
            if (incoming == null || !object.ReferenceEquals(incoming.fsm, __instance)) return;
            incoming.CaptureMomentum();
        }
    }
}
