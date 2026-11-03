using System.Collections.Generic;
using UnityEngine;

namespace WoLPricklyBallet
{
    // The dash is the game's own (BaseDashState: same distance, speed, evade, and the dash relics
    // count). Where it started, a cactus dancer pops up, at most one every Every seconds.
    public class PricklyBalletState : Player.BaseDashState
    {
        public new static string staticID = "PricklyBallet";

        // When each wizard may leave the next dancer (game time).
        private static readonly Dictionary<int, float> nextDancer = new Dictionary<int, float>();

        public PricklyBalletState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            // As the game's own dashes: one charge. Nothing else here; anything that throws in a
            // constructor stops the wizard spawning.
            InitChargeSkillSettings(1, 0f, skillData, this);
        }

        public override void OnEnter()
        {
            Vector2 start = parent.transform.position;
            base.OnEnter();
            if (!cooldownReady)
                return;
            ApplyStopElement();
            int key = parent.GetInstanceID();
            float ready;
            if (nextDancer.TryGetValue(key, out ready) && Time.time < ready)
                return;
            bool enhanced = IsEmpowered;
            nextDancer[key] = Time.time + (enhanced ? PricklyBalletPlugin.EnhancedEvery : PricklyBalletPlugin.Every);
            Dancer.Spawn(parent, skillID, start, enhanced ? PricklyBalletPlugin.EnhancedLifetime : PricklyBalletPlugin.Lifetime);
        }
    }
}
