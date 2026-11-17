// =====================================================================================
//  SHIFTERS - BotSenses.cs   [owned by the ENEMIES agent]
//  Sight (range + FOV + raycast line of sight to the character's Center/head, triggers ignored),
//  short-term noise memory and "most interesting target" selection for a SecurityBot.
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    public sealed class BotSenses
    {
        struct Heard { public ShifterCharacter Source; public float Time; public float Priority; }

        /// <summary>360-degree "bump" awareness radius (you can't hug a bot from behind).</summary>
        public const float TouchRadius = 1.5f;
        /// <summary>How long a noise keeps its source extra-interesting.</summary>
        public const float NoiseMemory = 4f;

        readonly SecurityBot _bot;
        readonly Heard[] _heard = new Heard[6];
        int _heardNext;
        static readonly RaycastHit[] _hits = new RaycastHit[16];

        public BotSenses(SecurityBot bot) { _bot = bot; }

        /// <summary>Characters a bot will chase/grab: alive, not carried/bridging/seated, not already held by a bot.</summary>
        public static bool IsCapturable(ShifterCharacter c)
        {
            if (c == null || !c.isActiveAndEnabled || !c.IsAlive || CaptiveBody.IsSeized(c)) return false;
            var cond = c.Condition;
            return cond != CharacterCondition.Carried && cond != CharacterCondition.Bridging && cond != CharacterCondition.Seated;
        }

        public void Remember(in NoiseEvent e)
        {
            if (e.Source == null) return;
            _heard[_heardNext] = new Heard { Source = e.Source, Time = Time.time, Priority = e.Priority };
            _heardNext = (_heardNext + 1) % _heard.Length;
        }

        /// <summary>Highest noise priority this character made within NoiseMemory seconds (0 = none).</summary>
        public float RecentNoise(ShifterCharacter c)
        {
            float best = 0f, now = Time.time;
            for (int i = 0; i < _heard.Length; i++)
                if (_heard[i].Source == c && now - _heard[i].Time < NoiseMemory && _heard[i].Priority > best) best = _heard[i].Priority;
            return best;
        }

        /// <summary>Interest bonus in meters: flopped/thrown bodies and recent noise makers are juicier.</summary>
        public float Interest(ShifterCharacter c)
        {
            float bonus = 0f;
            if (c.Condition == CharacterCondition.Flopped || c.Condition == CharacterCondition.Thrown) bonus += 5f;
            else if (c.Condition == CharacterCondition.Stunned) bonus += 2f;
            bonus += RecentNoise(c) * 3f;
            return bonus;
        }

        /// <summary>Range + FOV (or touch radius) + line of sight.</summary>
        public bool CanSee(ShifterCharacter c)
        {
            if (c == null || _bot.IsBlind) return false;
            Vector3 eye = _bot.EyePosition;
            Vector3 center = c.Center;
            Vector3 to = center - eye;
            float d = to.magnitude;
            if (d > _bot.SightRange) return false;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 fwd = _bot.transform.forward; fwd.y = 0f;
            bool touching = flat.magnitude < TouchRadius;
            if (!touching && flat.sqrMagnitude > 1e-4f && Vector3.Angle(fwd, flat) > _bot.Fov * 0.5f) return false;
            if (LineOfSight(eye, center, c)) return true;
            return LineOfSight(eye, c.HeadTop - Vector3.up * 0.12f, c);
        }

        bool LineOfSight(Vector3 from, Vector3 to, ShifterCharacter target)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.05f) return true;
            dir /= dist;
            int n = Physics.RaycastNonAlloc(from, dir, _hits, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider bestCol = null;
            Transform self = _bot.transform;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(self)) continue;
                if (h.distance < best) { best = h.distance; bestCol = h.collider; }
            }
            if (bestCol == null) return true;
            return bestCol.GetComponentInParent<ShifterCharacter>() == target;
        }

        /// <summary>Best visible capturable character inside the arena (lowest distance minus interest), or null.</summary>
        public ShifterCharacter FindTarget()
        {
            var all = ShifterCharacter.All;
            ShifterCharacter best = null;
            float bestScore = float.MaxValue;
            Vector3 pos = _bot.transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!IsCapturable(c) || !_bot.InArena(c.Feet, 0.5f)) continue;
                if (!CanSee(c)) continue;
                float score = Vector3.Distance(pos, c.Feet) - Interest(c);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }
    }
}
