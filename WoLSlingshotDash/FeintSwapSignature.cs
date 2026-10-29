using System.Collections;
using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap's signature (cast from the signature slot with the gauge full, isUltimate):
    // 1. Fan throw: on release, nine ice feints (six up to 0.26.1) fly out at once in a fan over FanSpread degrees
    //    centred on your aim, FanDistanceScale of the normal throw's distance at that charge (each
    //    stopping short of walls), each bursting with ice through the enemies on its way (level 3),
    //    with a frosty flash, the stage dimming a little and the camera pulling back to show the fan.
    // 2. Wait: they stand the normal 2 s, or until a re-press or a recast (the same rules as the
    //    normal feint: the press that cast it never counts, the button has to come up first).
    // 3. Chained swaps: you swap onto the feints one after another, SwapInterval apart, in a
    //    random order that ends on the feint thrown along your aim (SwapOrder). Each
    //    feint you land on is consumed in an icicle spin: a ring of icicles spins out from you and
    //    bursts in a Frost Nova (level 4, a sure freeze), with a hit-stop and a shake that rise
    //    with each swap. The last one is bigger (level 5): a wider ring, a longer hit-stop, a big
    //    shake, a flash and the stage lighting up. Feints broken by enemies before their turn are
    //    skipped (no gap is left for them).
    // Enhanced as well (the usual case): the charge is full in half the time, and a feint is left
    // where you stood when the chain began, exploding 2 s later, as the enhanced normal cast does.
    // Every hit is marked as a signature cast's (isUltimate), so it doesn't refill the gauge.
    // The camera, time and light are FeintCinematic's, which always puts them back.
    public partial class FeintSwapState
    {
        // 0.26.2 (Matt: more clones): nine feints (was six), the fan widened to keep them as far
        // apart as six were across 52 degrees.
        private const int FanCount = 9;
        private const float FanSpread = 72f;            // degrees across the fan, centred on the aim (0.24.0: 110, too spread out; 0.25.1: 70; 0.25.5: 52 for six)
        private const float FanDistanceScale = 1f;      // the feints fly this share of the normal throw's distance (0.25.1-0.25.4: 0.75; 0.25.5 is back to the
                                                        // full throw, as the owner asked, with the fan narrowed so its ends sit as far apart as 70 degrees at 0.75 did)
        private const float SigPathHitInterval = 0.075f; // each feint bursts this often in flight (the normal one every 0.05 s)...
        private const float SigPathHitScale = 1f;       // ...a little smaller than the normal one's (1.1), six at once
        // 0.26.1 (Matt: more cinema, a marker on each feint, swap more rapidly, every burst a large
        // AoE): 0.08 s between swaps (was 0.2), bursts of radius 3 (was 1.75) and 4.2 at the finish
        // (was 2.4), a marker ring under each feint (FeintMarker), a held beat as the chain
        // begins, and FeintCinematic's beats heavier.
        private const float SwapInterval = 0.08f;       // between one swap and the next
        private const float SpinTime = 0.12f;           // each icicle ring spins out for this long, then bursts
        private const float SpinRadiusScale = 3f;       // the spin's burst radius, times FrostFreezeRadius (1 by default)
        private const float FinaleRadiusScale = 4.2f;   // the last one's
        private const int ChainStartHitStop = 4;        // frames held as the chain begins
        private const int SpinIcicles = 8;              // icicles in each ring
        private const int FinaleIcicles = 12;           // and in the last one
        private const float HitCountDelay = 0.1f;       // hits and freezes are counted this long after a burst
        private const int SigPathLevel = 3;             // skill levels (SlingshotDashPlugin's SkillStats)
        private const int SigSpinLevel = 4;
        private const int SigFinaleLevel = 5;
        private const int SigLeftBehindLevel = 1;       // the enhanced feint left behind: the normal explosion, marked as a signature's

        // One feint of the fan.
        private class FanShot
        {
            public int index;          // 0 = the fan's left end
            public float angle;        // degrees off the aim, anticlockwise
            public float distance;
            public bool clipped;       // stopped short of a wall
            public Vector2 end;
            public GameObject decoy;
            public bool brokenLogged;
            public FeintMarker marker;
        }

        // What the chain did, for the finish line.
        private class ChainStats
        {
            public float startedAt;
            public int swaps, skipped, hits, frozen;
        }

        private void ThrowFan(Vector2 start, Vector2 aim, float distance, float power)
        {
            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            Effects.Splash(start, 1.2f, 4);
            Effects.Spark(start + aim * 0.5f, HitSparkType.Medium);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 1.3f, false);

            var feint = new FrostSlingshotState.Feint { thrownAt = Time.time };
            outFeint = feint;
            var shots = new List<FanShot>();
            var spread = new List<string>();
            for (int i = 0; i < FanCount; i++)
            {
                // From the fan's left end (anticlockwise of the aim) to its right.
                float angle = FanSpread * 0.5f - FanSpread * i / (FanCount - 1);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * (Vector3)aim;
                float d = distance * FanDistanceScale;
                bool clipped = false;
                // Stop short of walls so you never swap into one.
                RaycastHit2D hit = Physics2D.Raycast(start, direction, d, ChaosCollisions.layerAllWallAndObst);
                if (hit.collider != null)
                {
                    d = Mathf.Max(0f, hit.distance - FrostSlingshotState.WallMargin);
                    clipped = true;
                }
                shots.Add(new FanShot
                {
                    index = i,
                    angle = angle,
                    distance = d,
                    clipped = clipped,
                    end = start + direction * d,
                    // Its own attack (if enemies break it) is the spin's, marked as a signature's.
                    decoy = SpawnDecoy(start, SigSpinLevel, true)
                });
                spread.Add($"{d:0.0}{(clipped ? "*" : "")}");
            }

            FeintCinematic cine = FeintCinematic.Begin();
            string fx = cine != null ? cine.Throw() : "no cinematic";
            bool empowered = IsEmpowered;
            SlingshotDashPlugin.Log($"Feint Swap: signature fan thrown after a {held:0.00} s hold (power {power:0.00}" +
                $"{(empowered ? ", enhanced" : "")}): {FanCount} feints over {FanSpread:0} degrees round the aim, " +
                $"distances {string.Join(", ", spread.ToArray())} (left to right, * = stopped short of a wall); " +
                $"they swap on a re-press, a recast, or by themselves after {HoverTime:0} s; cinematic: {fx}");
            SlingshotDashPlugin.Run(FlyFan(this, feint, shots, start, power, empowered, cine));
            SlingshotDashPlugin.Run(WatchForSwap(parent, skillSlot, feint));
        }

        // The fan's flight, its wait and the chain of swaps. Runs on the plugin, since this state
        // ends long before the swaps.
        private static IEnumerator FlyFan(FeintSwapState state, FrostSlingshotState.Feint feint, List<FanShot> shots,
            Vector2 start, float power, bool empowered, FeintCinematic cine)
        {
            Player player = state.parent;
            string category = player.skillCategory;
            string id = state.skillID;

            // 1. Fly out together, bursting with ice along the way (they can't be hurt until they land).
            float t = 0f;
            float nextHit = 0f;
            while (t < FrostSlingshotState.FlightTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / FrostSlingshotState.FlightTime);
                bool burst = t >= nextHit;
                if (burst)
                    nextHit = t + SigPathHitInterval;
                foreach (FanShot shot in shots)
                {
                    Vector2 position = Vector2.Lerp(start, shot.end, k);
                    FrostSlingshotState.MoveDecoy(shot.decoy, position);
                    if (burst && shot.decoy != null)
                    {
                        SignatureBurst(position, category, id, SigPathLevel, SigPathHitScale);
                        Effects.Shimmer(position, 1);
                    }
                }
                Keep(cine);
                yield return null;
            }
            // Landed: each feint gets its marker, the size of the burst it will go off in.
            float markRadius = SlingshotDashPlugin.FrostFreezeRadius * SpinRadiusScale;
            foreach (FanShot shot in shots)
            {
                SetVulnerable(shot.decoy, true);
                if (shot.decoy != null)
                    shot.marker = FeintMarker.Place(shot.end, markRadius, player.spriteRenderer);
            }

            // 2. Stand there with a slight bob until you press again or the time is up (a moment
            // at least, as the normal feint). If enemies break them all first, that's the end of it.
            t = 0f;
            while (t < SettleTime || (!feint.swapRequested && t < HoverTime))
            {
                t += Time.deltaTime;
                int standing = 0;
                foreach (FanShot shot in shots)
                {
                    if (Broken(shot.decoy))
                    {
                        if (!shot.brokenLogged)
                        {
                            shot.brokenLogged = true;
                            FeintMarker.Remove(shot.marker);
                            shot.marker = null;
                            SlingshotDashPlugin.Log($"Feint Swap: signature feint {shot.index + 1} of {FanCount} broken by enemies " +
                                $"{Time.time - feint.thrownAt:0.00} s after the throw");
                        }
                        continue;
                    }
                    standing++;
                    FrostSlingshotState.MoveDecoy(shot.decoy, shot.end + new Vector2(0f, Mathf.Sin(t * 6f + shot.index) * 0.08f));
                }
                if (standing == 0)
                {
                    feint.swapped = true;
                    state.ClearFeint(feint);
                    SlingshotDashPlugin.Log($"Feint Swap: signature: all {FanCount} feints were broken before the swaps, no chain");
                    if (cine != null)
                        cine.Finish("all feints broken");
                    yield break;
                }
                Keep(cine);
                yield return null;
            }

            bool forced = !feint.swapRequested;
            feint.swapped = true;
            state.ClearFeint(feint);
            if (Gone(player))
            {
                ConsumeAll(shots, 0);
                SlingshotDashPlugin.Log("Feint Swap: signature: wizard gone or dead before the chain, the feints vanished without swaps");
                if (cine != null)
                    cine.Finish("wizard gone before the chain");
                yield break;
            }

            // 3. The chain.
            int left = 0;
            foreach (FanShot shot in shots)
            {
                if (!Broken(shot.decoy))
                    left++;
            }
            var stats = new ChainStats { startedAt = Time.time };
            Vector2 origin = player.transform.position;
            List<FanShot> order = SwapOrder(shots);
            var orderText = new List<string>();
            foreach (FanShot shot in order)
                orderText.Add((shot.index + 1).ToString());
            SlingshotDashPlugin.Log($"Feint Swap: signature chain begins ({(forced ? "time's up" : "on your press")}) " +
                $"{Time.time - feint.thrownAt:0.00} s after the throw: {left} of {FanCount} feints standing, " +
                $"a swap every {SwapInterval:0.0} s in random order, finishing on the one thrown along your aim " +
                $"(order {string.Join(", ", orderText.ToArray())}, numbered left to right)");
            if (empowered)
                SlingshotDashPlugin.Run(LeftBehindSignature(state, origin, power, category, id));
            // The chain begins: every marker flares, a held beat, a flash and a shake.
            foreach (FanShot shot in order)
                if (shot.marker != null)
                    shot.marker.Flare();
            string startFx = cine != null ? cine.ChainStart(ChainStartHitStop) : "no cinematic";
            SlingshotDashPlugin.Log($"Feint Swap: signature chain start beat: {startFx}");
            {
                float w = 0f;
                while (w < ChainStartHitStop * 0.0167f + 0.05f)
                {
                    w += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float lastArrival = Time.time;
            bool waited = false;    // the gap before the next swap has been waited out
            bool finaleDone = false;
            shots = order;
            for (int i = 0; i < shots.Count; i++)
            {
                FanShot shot = shots[i];
                if (Broken(shot.decoy))
                {
                    stats.skipped++;
                    FeintMarker.Remove(shot.marker);
                    shot.marker = null;
                    SlingshotDashPlugin.Log($"Feint Swap: signature feint {shot.index + 1} of {FanCount} skipped (broken by enemies before its turn)");
                    continue;
                }
                if (stats.swaps > 0 && !waited)
                {
                    float w = 0f;
                    while (w < SwapInterval)
                    {
                        w += Time.deltaTime;
                        Keep(cine);
                        yield return null;
                    }
                    waited = true;
                }
                if (Gone(player))
                {
                    ConsumeAll(shots, i);
                    SlingshotDashPlugin.Log($"Feint Swap: signature: wizard gone or dead after {stats.swaps} swaps, the rest of the feints vanished");
                    if (cine != null)
                        cine.Finish("wizard gone mid-chain");
                    yield break;
                }
                if (Broken(shot.decoy))
                {
                    stats.skipped++;
                    FeintMarker.Remove(shot.marker);
                    shot.marker = null;
                    SlingshotDashPlugin.Log($"Feint Swap: signature feint {shot.index + 1} of {FanCount} skipped (broken by enemies while waiting its turn)");
                    continue;
                }

                // The last one standing gets the big finish.
                bool finale = true;
                for (int j = i + 1; j < shots.Count; j++)
                {
                    if (!Broken(shots[j].decoy))
                        finale = false;
                }
                float delay = Time.time - lastArrival;
                lastArrival = Time.time;
                waited = false;
                stats.swaps++;

                Vector2 here = player.transform.position;
                Teleport(player, shot.end);
                Consume(shot.decoy);
                shot.decoy = null;
                if (shot.marker != null)
                {
                    shot.marker.Pop();
                    shot.marker = null;
                }
                // Flair only: a frosty streak along the swap.
                for (int s = 1; s < 4; s++)
                    Effects.Shimmer(Vector2.Lerp(here, shot.end, s / 4f), 1);
                SoundManager.PlayAudioWithDistance("IceDaggerBasic", new Vector2?(shot.end), null, 24f, -1f,
                    0.9f + 0.06f * stats.swaps, false);
                float radius = SlingshotDashPlugin.FrostFreezeRadius * (finale ? FinaleRadiusScale : SpinRadiusScale);
                IcicleSpin.Create(shot.end, finale ? FinaleIcicles : SpinIcicles, radius, SpinTime, player, stats.swaps % 2 == 0);
                SlingshotDashPlugin.Run(SpinBurst(player, category, id, shot, stats.swaps, delay, radius, finale, cine, stats));
                if (finale)
                {
                    finaleDone = true;
                    break;
                }
            }
            if (!finaleDone)
            {
                // The feints after the last swap were broken in the meantime: no big finish.
                SlingshotDashPlugin.Log($"Feint Swap: signature chain ended without its finish ({stats.swaps} swaps, " +
                    $"{stats.skipped} skipped: the last feints were broken)");
                float w = 0f;
                while (w < SpinTime + HitCountDelay)
                {
                    w += Time.deltaTime;
                    yield return null;
                }
                if (cine != null)
                    cine.Finish(stats.swaps > 0 ? "chain ended early" : "no feint left to swap to");
            }
        }

        // The order of the swaps: the feints in a random order, ending on the one thrown along your
        // aim (the fan has an even count, so that's one of the two either side of it, at random).
        // If that one is broken before its turn, the last one still standing gets the finish.
        private static List<FanShot> SwapOrder(List<FanShot> shots)
        {
            FanShot last = null;
            foreach (FanShot shot in shots)
            {
                if (last == null || Mathf.Abs(shot.angle) < Mathf.Abs(last.angle) - 0.01f ||
                    (Mathf.Abs(Mathf.Abs(shot.angle) - Mathf.Abs(last.angle)) <= 0.01f && Random.value < 0.5f))
                    last = shot;
            }
            var order = new List<FanShot>();
            foreach (FanShot shot in shots)
            {
                if (shot != last)
                    order.Insert(Random.Range(0, order.Count + 1), shot);
            }
            order.Add(last);
            return order;
        }

        // A spin bursting SpinTime after you land: the Frost Nova (the spin's or the finish's
        // level), shards, a splash and the game's ice sounds, the cinematic's beat, and the log
        // line once the hits are in.
        private static IEnumerator SpinBurst(Player player, string category, string id, FanShot shot, int swap,
            float delay, float radius, bool finale, FeintCinematic cine, ChainStats stats)
        {
            float t = 0f;
            while (t < SpinTime)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Vector2 at = shot.end;
            int level = finale ? SigFinaleLevel : SigSpinLevel;
            var hit = new HashSet<Entity>();
            SignatureNova(at, radius, level, category, id, hit);
            Effects.Dust(at, finale ? 40 : 20, radius * 0.7f);
            Effects.Splash(at, radius, finale ? 5 : 3);
            Effects.Shimmer(at, finale ? 8 : 4);
            Effects.Spark(at, finale ? HitSparkType.Large : HitSparkType.Medium);
            SoundManager.PlayAudioWithDistance("IceExplode", new Vector2?(at), null, 24f, -1f, 0.95f + 0.05f * swap, false);
            if (finale)
            {
                SoundManager.PlayAudioWithDistance("IceBlast", new Vector2?(at), null, 24f, -1f, 0.85f, false);
                SoundManager.PlayAudioWithDistance("Shatter", new Vector2?(at), null, 24f, -1f, 1f, false);
            }
            string fx = cine != null ? cine.Burst(swap, finale) : "no cinematic";

            t = 0f;
            while (t < HitCountDelay)
            {
                t += Time.deltaTime;
                yield return null;
            }
            int frozen = 0;
            foreach (Entity e in hit)
            {
                if (Frozen(e))
                    frozen++;
            }
            stats.hits += hit.Count;
            stats.frozen += frozen;
            SlingshotDashPlugin.Log($"Feint Swap: signature swap {swap}{(finale ? " (the finish)" : "")} onto feint {shot.index + 1} " +
                $"of {FanCount}, {delay:0.00} s after {(swap == 1 ? "the chain began" : "the last swap")}: icicle spin " +
                $"(radius {radius:0.0}, level {level}) hit {hit.Count}, froze {frozen}; {fx}");
            if (finale)
            {
                SlingshotDashPlugin.Log($"Feint Swap: signature finish: {stats.swaps} swaps, {stats.skipped} skipped, " +
                    $"{Time.time - stats.startedAt:0.00} s from the first swap to the last burst; {stats.hits} hits, " +
                    $"{stats.frozen} frozen in all");
                if (cine != null)
                    cine.Finish("finished");
            }
        }

        // Enhanced as well: a feint where you stood when the chain began, drawing enemies in,
        // exploding 2 s later in the normal explosion (marked as a signature's).
        private static IEnumerator LeftBehindSignature(FeintSwapState state, Vector2 here, float power, string category, string id)
        {
            GameObject left = null;
            try
            {
                left = state.SpawnDecoy(here, SigLeftBehindLevel, true);
                SetVulnerable(left, true);
            }
            catch
            {
            }
            Effects.Splash(here, 0.8f, 2);
            float fuse = 0f;
            while (fuse < LeftBehindFuse && !Broken(left))
            {
                fuse += Time.deltaTime;
                // A quickening glint as it's about to go.
                if (Random.value < 0.05f + 0.25f * (fuse / LeftBehindFuse))
                    Effects.Shimmer(here + Random.insideUnitCircle * 0.4f, 1);
                yield return null;
            }
            if (Broken(left))
            {
                SlingshotDashPlugin.Log("Feint Swap: signature + enhanced: the feint left behind was broken before its fuse ran out");
                yield break;
            }
            Consume(left);
            float radius = SlingshotDashPlugin.FrostFreezeRadius * (1f + 0.5f * power);
            var hit = new HashSet<Entity>();
            SignatureNova(here, radius, SigLeftBehindLevel, category, id, hit);
            Effects.Splash(here, radius, 3);
            Effects.Shimmer(here, 6);
            Effects.Spark(here, HitSparkType.Medium);
            Effects.Shake(0.7f);
            float t = 0f;
            while (t < HitCountDelay)
            {
                t += Time.deltaTime;
                yield return null;
            }
            SlingshotDashPlugin.Log($"Feint Swap: signature + enhanced: the feint left behind exploded {LeftBehindFuse:0} s after " +
                $"the chain began: hit {hit.Count}");
        }

        // A Frost Nova marked as a signature cast's (the game's CreateFrostNova has no flag for
        // it, so it's made the same way by hand), counting what it hits.
        private static void SignatureNova(Vector2 position, float radius, int level, string category, string id, HashSet<Entity> hit)
        {
            try
            {
                FrostNova nova = Globals.ChaosInst<FrostNova>(FrostNova.Prefab);
                nova.attack.SetAttackInfo(category, id, level, true);
                if (hit != null)
                    nova.attack.entityCollisionEventHandlers += e =>
                    {
                        if (e != null)
                            hit.Add(e);
                    };
                nova.Activate(position, radius);
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: couldn't make the signature explosion: {e.Message}");
            }
        }

        // An ice burst marked as a signature cast's.
        private static void SignatureBurst(Vector2 position, string category, string id, int level, float scale)
        {
            try
            {
                IceBurst burst = IceBurst.CreateBurst(position, category, id, level, scale);
                burst.attack.SetAttackInfo(category, id, level, true);
            }
            catch
            {
            }
        }

        // A feint swapped onto (or vanishing when the cast is cut short): gone in the game's icicle
        // shards, without the game's own break (whose blast would hit again on top of the spin).
        private static void Consume(GameObject decoy)
        {
            if (decoy == null)
                return;
            try
            {
                IceDecoy ice = decoy.GetComponent<IceDecoy>();
                Vector3 at = ice != null && ice.hurtBoxTransform != null ? ice.hurtBoxTransform.position : decoy.transform.position;
                // Off the allies' list first, as the game's own break does, so enemies stop going after it.
                if (ice != null && ice.parentGroup != null)
                    ice.parentGroup.Remove(decoy);
                PoolManager.GetPoolItem<ParticleEffect>("IcicleBreakEffect").Emit(30, at);
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Feint Swap: couldn't break the feint up: {e.Message}");
            }
            Object.Destroy(decoy);
        }

        private static void ConsumeAll(List<FanShot> shots, int from)
        {
            for (int i = from; i < shots.Count; i++)
            {
                if (!Broken(shots[i].decoy))
                    Consume(shots[i].decoy);
                shots[i].decoy = null;
                FeintMarker.Remove(shots[i].marker);
                shots[i].marker = null;
            }
        }

        private static void Teleport(Player player, Vector2 to)
        {
            player.transform.position = to;
            if (player.rigidbody2D != null)
            {
                player.rigidbody2D.position = to;
                player.rigidbody2D.velocity = Vector2.zero;
            }
        }

        // Gone (cancel, level change) or dead.
        private static bool Gone(Player player)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                return true;
            try
            {
                if (player.health != null && player.health.CurrentHealthValue <= 0)
                    return true;
                return player.fsm != null && player.fsm.currentStateName == "Dead";
            }
            catch
            {
                return false;
            }
        }

        private static bool Frozen(Entity entity)
        {
            try
            {
                return entity != null && entity.fsm != null && entity.fsm.currentStateName != null
                    && entity.fsm.currentStateName.Contains("Frozen");
            }
            catch
            {
                return false;
            }
        }

        private static void Keep(FeintCinematic cine)
        {
            if (cine != null)
                cine.KeepAlive();
        }
    }
}
