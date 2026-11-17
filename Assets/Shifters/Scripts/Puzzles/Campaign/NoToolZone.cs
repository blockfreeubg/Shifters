// =====================================================================================
//  SHIFTERS - NoToolZone.cs   [PUZZLES - campaign wave, CAMPAIGN §13.1 W7]
//  Purple-striped NO-SWAP / NO-SIZE / ... volume. Tools ask the static queries before acting:
//    NoToolZone.Blocks(point, ToolMask.SwapOMatic)            - point inside a zone that bans that tool
//    NoToolZone.BlocksSegment(a, b, ToolMask.SwapOMatic)      - either end inside, or the segment crosses a zone
//    NoToolZone.Fizzle(point)                                 - the shared "HR SAYS NO." feedback (rate-limited toast)
//  Built by PuzzleKit.NoToolZone (box = local Center/Size in this transform's space).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public class NoToolZone : MonoBehaviour
    {
        public const string Message = "HR SAYS NO.";
        public static readonly Color Purple = new Color(0.55f, 0.18f, 0.95f);
        public static readonly List<NoToolZone> All = new List<NoToolZone>();
        static float _lastToast = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); _lastToast = -10f; }

        public Vector3 Center = new Vector3(0f, 2f, 0f);
        public Vector3 Size = new Vector3(4f, 4f, 4f);
        public ToolMask Mask = ToolMask.All;

        public int Fizzles { get; private set; }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public bool Bans(ToolMask tool) => (Mask & tool) != 0;

        public bool Contains(Vector3 world) => PuzzleUtil.InBox(transform, Center, Size * 0.5f, world);

        /// <summary>Does the world segment a-b touch this box (slab test in local space)?</summary>
        public bool Crosses(Vector3 a, Vector3 b)
        {
            var la = transform.InverseTransformPoint(a) - Center;
            var lb = transform.InverseTransformPoint(b) - Center;
            var h = Size * 0.5f;
            var d = lb - la;
            float t0 = 0f, t1 = 1f;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-6f) { if (la[i] < -h[i] || la[i] > h[i]) return false; continue; }
                float inv = 1f / d[i];
                float ta = (-h[i] - la[i]) * inv, tb = (h[i] - la[i]) * inv;
                if (ta > tb) { var tmp = ta; ta = tb; tb = tmp; }
                t0 = Mathf.Max(t0, ta); t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ static queries (tools)
        /// <summary>The first enabled zone banning 'tool' that contains 'world' (null = allowed).</summary>
        public static NoToolZone At(Vector3 world, ToolMask tool)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var z = All[i];
                if (z != null && z.isActiveAndEnabled && z.Bans(tool) && z.Contains(world)) return z;
            }
            return null;
        }

        public static bool Blocks(Vector3 world, ToolMask tool) => At(world, tool) != null;

        /// <summary>True if either end is inside, or the straight path a-b passes through, a zone banning 'tool'
        /// (SWAP-O-MATIC: "never across NO-SWAP planes").</summary>
        public static bool BlocksSegment(Vector3 a, Vector3 b, ToolMask tool)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var z = All[i];
                if (z != null && z.isActiveAndEnabled && z.Bans(tool) && z.Crosses(a, b)) return true;
            }
            return false;
        }

        /// <summary>Convenience: false + reason "HR SAYS NO." when blocked.</summary>
        public static bool Allowed(Vector3 world, ToolMask tool, out string reason)
        {
            reason = Blocks(world, tool) ? Message : null;
            return reason == null;
        }

        /// <summary>Shared fizzle feedback: purple sparks + "HR SAYS NO." floating text + denied sound + (rate-limited) toast.</summary>
        public static void Fizzle(Vector3 at)
        {
            var z = At(at, ToolMask.All);
            if (z != null) z.Fizzles++;
            Fx.Sparks(at, Vector3.up, 10, Purple);
            Fx.FloatingText(at + Vector3.up * 0.4f, Message, Purple, 1.1f);
            Sfx.At(SfxId.Denied, at);
            if (Time.time - _lastToast > 2f) { _lastToast = Time.time; GameEvents.RaiseToast(Message + " (TOOLS ARE NOT PERMITTED IN THIS AREA)", Purple); }
        }

        public static string LabelFor(ToolMask mask)
        {
            if (mask == ToolMask.SwapOMatic) return "NO-SWAP ZONE";
            if (mask == ToolMask.BitShifter) return "NO-SIZE ZONE";
            if (mask == ToolMask.CapsLock) return "NO-LOCK ZONE";
            if (mask == ToolMask.PartyPopper) return "NO-PARTY ZONE";
            if (mask == (ToolMask.SwapOMatic | ToolMask.BitShifter)) return "NO-SWAP / NO-SIZE";
            return "NO TOOLS ZONE";
        }
    }
}
