namespace WoLTrailblazer
{
    // Trailblazer (Fire dash arcana): the dash itself is the game's own plain dash. What the arcana
    // does is FlameTrail, which follows the wizard while Trailblazer is equipped: running, dashing
    // and every movement arcana leave a trail of fire that scorches whoever the wizard runs into.
    public class TrailblazerState : Player.BaseDashState
    {
        public new static string staticID = "Trailblazer";

        public TrailblazerState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
            applyStopElementStatus = true;
            // Nothing else here: the game builds every dash state while the wizard spawns, and
            // anything that throws in a constructor stops the wizard spawning at all.
        }
    }
}
