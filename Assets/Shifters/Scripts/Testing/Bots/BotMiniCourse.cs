// =====================================================================================
//  SHIFTERS - BotMiniCourse.cs   [BOTS module]
//  Code-built validation course for the bot framework (used until the builder's sandbox level
//  exists). Along +Z: P0 start (terminal, shoot target, heavy block + 150 kg plate) -> 2.0 m gap ->
//  P1 (2.5 m stack ledge left, 3.5 m throw ledge right) -> 5.2 m bridge gap -> P2 with the exit.
//  Real kits (PuzzleKit / LevelKit) are used when published, else BotProbes test doubles.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public sealed class BotMiniCourse
    {
        public Transform Root;
        public GameObject Terminal, Target, Plate, Block, Exit;
        public bool RealTerminal, RealTarget, RealExit;

        // ---- key points (world space, feet height)
        public static readonly Vector3 Spawn = new Vector3(0f, 0f, -4.5f);
        public static readonly Vector3 TerminalPos = new Vector3(5.8f, 0f, 0.5f);
        public static readonly Vector3 TargetPos = new Vector3(-6.2f, 2.2f, 7.8f);
        public static readonly Vector3 BlockPos = new Vector3(-4f, 0f, -2.5f);
        public static readonly Vector3 PlatePos = new Vector3(-4f, 0f, 4.5f);
        public const float GapAStart = 9f, GapAEnd = 11f;              // 2.0 m
        public const float LedgeZ0 = 24f, LedgeZ1 = 30f;
        public const float StackHeight = 2.5f, ThrowHeight = 3.5f;
        public const float GapBStart = 33f, GapBEnd = 38.2f;           // 5.2 m
        public static readonly Vector3 StackBottom = new Vector3(-4.25f, 0f, 23.3f);
        public static readonly Vector3 StackTop = new Vector3(-4.25f, StackHeight, 26f);
        public static readonly Vector3 StackBack = new Vector3(-4.25f, StackHeight, 29.4f);
        public static readonly Vector3 StackDown = new Vector3(-4.25f, 0f, 31.6f);
        public static readonly Vector3 ThrowStage = new Vector3(4.25f, 0f, 16f);
        public static readonly Vector3 ThrowLanding = new Vector3(4.25f, ThrowHeight, 26.5f);
        public static readonly Vector3 ThrowBack = new Vector3(4.25f, ThrowHeight, 29.4f);
        public static readonly Vector3 ThrowDown = new Vector3(4.25f, 0f, 31.6f);
        public static readonly Vector3 MidLane = new Vector3(0f, 0f, 22f);
        public static readonly Vector3 BridgeSpot = new Vector3(0f, 0f, 32.6f);
        public static readonly Vector3 BridgeApproach = new Vector3(0f, 0f, 31.2f);
        public static readonly Vector3 BridgeFar = new Vector3(0f, 0f, 40f);
        public static readonly Vector3 ExitCenter = new Vector3(0f, 0f, 45f);

        public static BotMiniCourse Build(BotCapabilities caps)
        {
            var k = new BotMiniCourse();
            var root = new GameObject("BotMiniCourse").transform;
            k.Root = root;
            var geo = Kit.Make("Geometry", root, Vector3.zero).transform;
            Kit.SolidBox(geo, "P0", new Vector3(0f, -0.5f, (-6f + GapAStart) * 0.5f), new Vector3(14f, 1f, GapAStart + 6f), MatKey.Floor);
            Kit.SolidBox(geo, "P1", new Vector3(0f, -0.5f, (GapAEnd + GapBStart) * 0.5f), new Vector3(14f, 1f, GapBStart - GapAEnd), MatKey.FloorAlt);
            Kit.SolidBox(geo, "P2", new Vector3(0f, -0.5f, (GapBEnd + 50f) * 0.5f), new Vector3(14f, 1f, 50f - GapBEnd), MatKey.Floor);
            Kit.SolidBox(geo, "StackLedge", new Vector3(-4.25f, StackHeight * 0.5f, (LedgeZ0 + LedgeZ1) * 0.5f), new Vector3(5.5f, StackHeight, LedgeZ1 - LedgeZ0), MatKey.Wall);
            Kit.SolidBox(geo, "ThrowLedge", new Vector3(4.25f, ThrowHeight * 0.5f, (LedgeZ0 + LedgeZ1) * 0.5f), new Vector3(5.5f, ThrowHeight, LedgeZ1 - LedgeZ0), MatKey.WallAlt);
            Kit.SolidBox(geo, "CatchFloor", new Vector3(0f, -6.5f, 20f), new Vector3(40f, 1f, 90f), MatKey.Concrete);
            Kit.BoxVisual(geo, "GapAEdge", new Vector3(0f, 0.01f, GapAStart - 0.15f), new Vector3(14f, 0.02f, 0.3f), MatKey.HazardYellow, 0f);
            Kit.BoxVisual(geo, "GapBEdge", new Vector3(0f, 0.01f, GapBStart - 0.15f), new Vector3(14f, 0.02f, 0.3f), MatKey.HazardYellow, 0f);

            var puzzles = Kit.Make("Puzzles", root, Vector3.zero).transform;
            var faceWest = Quaternion.Euler(0f, -90f, 0f);
            // terminal
            k.Terminal = caps.KitTerminal ? BotReflect.CallKit("PuzzleKit", "Terminal", puzzles, TerminalPos, faceWest, Tuning.TerminalIQ) : null;
            k.RealTerminal = k.Terminal != null;
            if (k.Terminal == null)
            {
                var t = Kit.Make("ProbeTerminal", puzzles, TerminalPos, faceWest);
                Kit.SolidBox(t.transform, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 1.4f, 0.5f), MatKey.PuzzleBody);
                Kit.BoxVisual(t.transform, "Screen", new Vector3(0f, 1.15f, 0.26f), new Vector3(0.6f, 0.4f, 0.04f), MatKey.Screen);
                t.AddComponent<BotProbeTerminal>();
                k.Terminal = t;
            }
            // shoot target
            var faceStart = Quaternion.Euler(0f, 180f, 0f);
            k.Target = caps.KitShootTarget ? BotReflect.CallKit("PuzzleKit", "ShootTarget", puzzles, TargetPos, faceStart) : null;
            k.RealTarget = k.Target != null;
            if (k.Target == null)
            {
                var t = Kit.Make("ProbeTarget", puzzles, TargetPos, faceStart);
                Kit.SolidBox(t.transform, "Bullseye", Vector3.zero, new Vector3(1f, 1f, 0.15f), MatKey.WarningRed);
                Kit.BoxVisual(t.transform, "Post", new Vector3(0f, -TargetPos.y * 0.5f - 0.25f, 0.2f), new Vector3(0.15f, TargetPos.y - 0.5f, 0.15f), MatKey.MetalDark);
                t.AddComponent<BotProbeTarget>();
                k.Target = t;
            }
            // heavy block + 150 kg plate (real puzzles module only)
            if (caps.Push)
            {
                k.Plate = BotReflect.CallKit("PuzzleKit", "PressurePlate", puzzles, PlatePos, Quaternion.identity, Tuning.PlateHeavy);
                k.Block = BotReflect.CallKit("PuzzleKit", "HeavyBlock", puzzles, BlockPos, Quaternion.identity);
            }
            // exit
            k.Exit = caps.KitExit ? BotReflect.CallKit("LevelKit", "Exit", puzzles, ExitCenter, Quaternion.identity) : null;
            k.RealExit = k.Exit != null;
            if (k.Exit == null)
            {
                var e = Kit.Make("ProbeExit", puzzles, ExitCenter);
                Kit.BoxVisual(e.transform, "ExitPad", new Vector3(0f, 0.015f, 0f), new Vector3(7f, 0.03f, 6f), MatKey.SafetyOrange, 0f);
                e.AddComponent<BotProbeExit>();
                k.Exit = e;
            }
            return k;
        }

        /// <summary>Spawn the session team here (CharacterManager + CharacterSwitcher) and point the camera at it.</summary>
        public void SpawnTeam()
        {
            GameSession.Instance.EnsureTeam();
            var cmGo = new GameObject("CharacterManager");
            cmGo.transform.SetParent(Root, false);
            var cm = cmGo.AddComponent<CharacterManager>();
            cmGo.AddComponent<CharacterSwitcher>();
            var poses = new List<Pose>
            {
                new Pose(Spawn + new Vector3(-1.6f, 0f, 0f), Quaternion.identity),
                new Pose(Spawn, Quaternion.identity),
                new Pose(Spawn + new Vector3(1.6f, 0f, 0f), Quaternion.identity),
            };
            cm.SpawnTeam(GameSession.Instance.Team, poses, 0);
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var sc = cam.GetComponent<ShiftersCamera>();
                if (sc == null) sc = cam.gameObject.AddComponent<ShiftersCamera>();
                sc.SetTarget(cm.Active, true);
                if (!BotReflect.Declares(typeof(ShiftersCamera), "LateUpdate"))
                {
                    cam.transform.position = new Vector3(9f, 9f, -12f);
                    cam.transform.LookAt(new Vector3(0f, 0f, 8f));
                }
            }
        }

        /// <summary>Destroy the course and its team (between seeds).</summary>
        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
