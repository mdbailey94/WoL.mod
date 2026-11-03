using System.Collections.Generic;
using UnityEngine;

namespace WoLPricklyBallet
{
    // Cactus Waltz. The dash is the game's own (BaseDashState: same distance, speed, evade, and the
    // dash relics count). Where it started, the game's cactus pops up, at most one every Every
    // seconds. The skill ID stays "PricklyBallet" from 0.1.0: a save holding an ID the game no
    // longer knows has its whole loadout reset (Player.InitSkills).
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
            Cactus.Spawn(parent, skillID, start, enhanced ? PricklyBalletPlugin.EnhancedLifetime : PricklyBalletPlugin.Lifetime, enhanced);
        }
    }
}
