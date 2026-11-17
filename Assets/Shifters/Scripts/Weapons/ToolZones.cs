// =====================================================================================
//  SHIFTERS - ToolZones.cs   [owned by the WEAPONS agent]
//  W7 anti-cheese query: is a tool blocked at a point / across a segment by a purple-striped
//  NO-SWAP / NO-SIZE volume (puzzles' PuzzleKit.NoToolZone)? The tools fizzle with "HR SAYS NO.".
//  Sources: puzzles' NoToolZone (wired when its static query is published) + the PointQuery /
//  SegmentQuery hooks (tests, level-local zones). Null-safe, allocation-free.
// =====================================================================================
using System;
using UnityEngine;

namespace Shifters
{
    public static class ToolZones
    {
        /// <summary>Fizzle text of every tool blocked by a zone.</summary>
        public const string Refusal = "HR SAYS NO.";

        /// <summary>Extra point query (tests / level-local zones): true = blocked.</summary>
        public static Func<Vector3, ToolMask, bool> PointQuery;
        /// <summary>Extra segment query (a -> b crosses a zone): true = blocked.</summary>
        public static Func<Vector3, Vector3, ToolMask, bool> SegmentQuery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PointQuery = null; SegmentQuery = null; }

        /// <summary>True if 'tool' is not allowed at p.</summary>
        public static bool Blocks(Vector3 p, ToolMask tool)
        {
            if (tool == ToolMask.None) return false;
            try
            {
                if (PointQuery != null && PointQuery(p, tool)) return true;
                if (PuzzleZones(p, tool)) return true;
            }
            catch (Exception e) { Debug.LogException(e); }
            return false;
        }

        /// <summary>True if either end is blocked or the segment a -> b crosses a zone of 'tool'.</summary>
        public static bool BlocksSegment(Vector3 a, Vector3 b, ToolMask tool)
        {
            if (tool == ToolMask.None) return false;
            if (Blocks(a, tool) || Blocks(b, tool)) return true;
            try
            {
                if (SegmentQuery != null && SegmentQuery(a, b, tool)) return true;
                if (PuzzleZonesSegment(a, b, tool)) return true;
            }
            catch (Exception e) { Debug.LogException(e); }
            return false;
        }

        /// <summary>Kind version (guns without a ToolMask are never blocked).</summary>
        public static bool Blocks(Vector3 p, WeaponKind kind) => Blocks(p, WeaponStyle.ToolMaskOf(kind));

        // ---- puzzles bridge (PuzzleKit.NoToolZone). Wired to puzzles' static NoToolZone.Blocks / BlocksSegment.
        static bool PuzzleZones(Vector3 p, ToolMask tool) => NoToolZone.Blocks(p, tool);
        static bool PuzzleZonesSegment(Vector3 a, Vector3 b, ToolMask tool) => NoToolZone.BlocksSegment(a, b, tool);
    }
}
