// =====================================================================================
//  SHIFTERS - L1Builder.cs   (LEVEL 1 - THE TRAINING FACILITY, "Orientation Day")
//  Code-defined level (ILevelBuilder). The whole scene comes from here: geometry, puzzles, tutorials,
//  hints, checkpoints, decor and the void backdrop. Sections run along +Z from the spawn at the origin:
//    S0 Arrival Bay      z -12..28   spawn podium, step / foam gap / hurdle / ramp warm-up
//    S1 Switching Room   z  28..52   plate P1 in an alcove holds door D1, lever L1 latches it      -> C1
//    S2 Aptitude Tests   z  52..72   hub + STR (push block), SPD (timed run), IQ (terminal) lanes  -> C2
//    S3 Bridge Training  z  72..106  5.2 m foam gap, living bridge                                  -> C3
//    S4 Stack & Throw    z 106..146  2.5 m stack ledge (button lowers ramp), 3.6 m throw balcony
//    S5 Weapon Range     z 146..176  three targets open the shutter, bonus barrel "NICE SHOT"       -> C4
//    S6 Sacrifice Lesson z 176..222  booth plate holds D6, the hero takes the vent + duct parkour   -> C5
//    S7 Graduation       z 222..256  three plates at once open the gate, graduation elevator (exit)
//  Mark ids (for L1Solution): see the ctx.Mark / ctx.MarkPoint calls ("S<n>.<Thing>").
// =====================================================================================
using UnityEngine;

namespace Shifters
{
    /// <summary>Level 1 builder: the friendly, funny tutorial. Every pit is a FoamPit; safety nets catch everything else.</summary>
    public partial class L1Builder : ILevelBuilder
    {
        public int LevelIndex => 0;

        LevelBuildContext _ctx;

        public void Build(LevelBuildContext ctx)
        {
            _ctx = ctx;
            BuildArrivalBay();
            BuildSwitchingRoom();
            BuildAptitudeTests();
            BuildBridgeTraining();
            BuildStackAndThrow();
            BuildWeaponRange();
            BuildSacrificeLesson();
            BuildGraduation();
            BuildDressing();
        }

        // ================================================================== geometry helpers (world == level space)
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);

        /// <summary>Floor slab whose top is at 'top', spanning x0..x1 / z0..z1.</summary>
        GameObject Floor(Transform p, float x0, float x1, float z0, float z1, float top = 0f, MatKey mat = MatKey.Floor, bool trim = true, float thick = 1f)
            => EnvKit.Floor(p, V((x0 + x1) * 0.5f, top, (z0 + z1) * 0.5f), V(x1 - x0, thick, z1 - z0), mat, trim);

        /// <summary>Wall along X at z (base y0).</summary>
        GameObject WallX(Transform p, float x0, float x1, float z, float h, float y0 = 0f, bool accent = true)
            => EnvKit.Wall(p, V(x0, y0, z), V(x1, y0, z), h, 0.4f, MatKey.Wall, accent);

        /// <summary>Wall along Z at x (base y0).</summary>
        GameObject WallZ(Transform p, float x, float z0, float z1, float h, float y0 = 0f, bool accent = true)
            => EnvKit.Wall(p, V(x, y0, z0), V(x, y0, z1), h, 0.4f, MatKey.Wall, accent);

        /// <summary>Solid box from min to max corner.</summary>
        BoxCollider Box(Transform p, string name, Vector3 min, Vector3 max, MatKey mat)
            => Kit.SolidBox(p, name, (min + max) * 0.5f, max - min, mat, 0.04f);

        /// <summary>Foam pit filling the hole x0..x1 / z0..z1 between floors (floor level 0 unless 'top'): tub walls,
        /// bottom, pastel foam and a FoamPit KillZone that bounces characters back to 'respawn'.</summary>
        KillZone FoamPit(Transform p, float x0, float x1, float z0, float z1, Vector3 respawn, float top = 0f, float depth = 3.4f)
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
            var g = _ctx.Geometry;
            Kit.SolidBox(g, "PitBottom", V(cx, top - depth - 0.2f, cz), V(w + 0.8f, 0.4f, d + 0.8f), MatKey.FloorAlt, 0.04f);
            Kit.SolidBox(g, "PitWallW", V(x0 - 0.22f, top - depth * 0.5f - 0.01f, cz), V(0.4f, depth - 0.02f, d + 0.8f), MatKey.WallAlt, 0.03f);
            Kit.SolidBox(g, "PitWallE", V(x1 + 0.22f, top - depth * 0.5f - 0.01f, cz), V(0.4f, depth - 0.02f, d + 0.8f), MatKey.WallAlt, 0.03f);
            Kit.SolidBox(g, "PitWallS", V(cx, top - depth * 0.5f - 0.01f, z0 - 0.22f), V(w + 0.8f, depth - 0.02f, 0.4f), MatKey.WallAlt, 0.03f);
            Kit.SolidBox(g, "PitWallN", V(cx, top - depth * 0.5f - 0.01f, z1 + 0.22f), V(w + 0.8f, depth - 0.02f, 0.4f), MatKey.WallAlt, 0.03f);
            // safety-orange lips so the pit edges read from far away
            Kit.BoxVisual(p, "PitLipS", V(cx, top + 0.012f, z0 - 0.12f), V(w, 0.025f, 0.24f), MatKey.HazardStripes, 0.005f, null, false);
            Kit.BoxVisual(p, "PitLipN", V(cx, top + 0.012f, z1 + 0.12f), V(w, 0.025f, 0.24f), MatKey.HazardStripes, 0.005f, null, false);
            _ctx.Count("foamPits");
            return PuzzleKit.FoamPit(_ctx.Hazards, V(cx, top - 1f, cz), null, V(w, 2.6f, d), respawn);
        }

        // ================================================================== signage helpers
        /// <summary>Panel sign whose readable face points along yaw (180 = toward players walking +Z). Verified in
        /// screenshots: EnvKit text reads from the panel's -forward side, so the panel itself is turned by 180.</summary>
        GameObject Sign(Transform p, Vector3 pos, float yaw, string text, float w, float h, SignStyle style = SignStyle.Info)
        {
            _ctx.Count("signs");
            return EnvKit.Sign(p, pos, Yaw(yaw + 180f), text, new Vector2(w, h), style);
        }

        // gamepad prompts (integrator2, gamepad1): world-text templates whose key names follow the device (DevicePromptText)
        const string HowToShift = "HOW TO SHIFT:\n1. PANIC\n2. PRESS {Switcher|SHIFT}\n3. PANIC LESS";
        const string ShiftHappens = "ESPECIALLY WHEN YOU PRESS {Switcher|RIGHT SHIFT}";

        /// <summary>Corporate poster facing yaw (same convention as <see cref="Sign"/>).</summary>
        GameObject Poster(Transform p, Vector3 pos, float yaw, string title, string sub, float scale = 1f)
        {
            _ctx.Count("posters");
            return EnvKit.Poster(p, pos, Yaw(yaw + 180f), title, sub, new Vector2(1.1f * scale, 1.5f * scale));
        }

        /// <summary>Text painted on the floor, readable by someone walking toward +yaw.</summary>
        TMPro.TextMeshPro FloorText(Transform p, Vector3 pos, float yaw, string text, float h, Color c, float maxW = 8f)
            => _ctx.Label(p, pos + Vector3.up * 0.015f, Quaternion.Euler(90f, yaw, 0f), text, h, c, maxW);

        /// <summary>Overhead gantry: two pillars and a big sign readable from -Z (players walking +Z).</summary>
        void Gantry(Transform p, float z, float halfSpan, float y, string text, float w, float h, SignStyle style = SignStyle.Info)
        {
            EnvKit.Pillar(p, V(-halfSpan, 0f, z), y + h * 0.5f + 0.3f, 0.7f);
            EnvKit.Pillar(p, V(halfSpan, 0f, z), y + h * 0.5f + 0.3f, 0.7f);
            Kit.BoxVisual(p, "GantryBeam", V(0f, y + h * 0.5f + 0.15f, z), V(halfSpan * 2f, 0.3f, 0.35f), MatKey.MetalDark, 0.04f);
            Sign(p, V(0f, y, z - 0.25f), 180f, text, w, h, style);
        }

        /// <summary>Hidden IQ-only note (Brainiac sees it). Clue convention: yaw = the direction the READER looks.</summary>
        void IQNote(Transform p, Vector3 pos, float yaw, string text, float size = 0.6f)
        {
            PerspectiveKit.Clue(p, pos, Yaw(yaw), text, size);
            _ctx.Count("iqClues");
        }

        // ================================================================== mounted decor (same "faces yaw" convention as Sign)
        // EnvKit's mounted pieces (LightPanel, Decor.Whiteboard) take the direction the VIEWER looks (into the wall);
        // these helpers take the direction the lit / readable face points, like Sign and Poster above.

        /// <summary>Emissive light strip whose lit face points along faceYaw (e.g. 90 on a west wall).</summary>
        GameObject Panel(Vector3 pos, float faceYaw, float w, float h, Color color, bool pointLight = false, float intensity = 2.5f, float range = 0f)
            => EnvKit.LightPanel(_ctx.Lights, pos, Yaw(faceYaw + 180f), new Vector2(w, h), color, pointLight, intensity, range);

        /// <summary>Whiteboard on legs whose written side faces faceYaw.</summary>
        GameObject Board(Transform p, Vector3 pos, float faceYaw, string text)
            => EnvKit.Decor.Whiteboard(p, pos, Yaw(faceYaw + 180f), text);

        /// <summary>Bench whose sitters look along faceYaw (backrest on the far side, e.g. against a wall).</summary>
        GameObject BenchFacing(Transform p, Vector3 pos, float faceYaw, float length)
            => EnvKit.Decor.Bench(p, pos, Yaw(faceYaw + 180f), length);
    }
}
