// =====================================================================================
//  SHIFTERS - L8Solution.cs   [SHIFT 07 THE FEDERAL COLOR RESERVE - TeamBot solution, no debug assists]
//  S0 the gun owner takes the SWAP-O-MATIC from the locker (skipped with -l8NoTool 1: tool-less path)
//  S1 Sprinter presses the TIMED button, TURBO-runs the laser curtains, pulls LASER POWER: OFF -> C1
//  S2 Lock A: Brute pushes the bullion pallet onto I, Brainiac drops a crate on II, Sprinter stands on III
//     Lock B: Brute K1, Brainiac K2, Sprinter ramp -> turbo jump -> K3; E, SHIFT, E, SHIFT, E (< 5 s)
//     Lock C: the gun owner shoots CROWN, MONOCLE, MUSTACHE, DRUM from the teller's podium -> door -> C2
//  S3 Sprinter on the pressure pedestal, Brute carries the 60 kg prism into the MASTER VALVE -> exit.
//  Run: run_level_test.sh <proj> 8 1,2,3,MIN,MAX   (fallback: ... 8 1 -l8NoTool 1)
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Shifters
{
    public static class L8Solution
    {
        public static IEnumerator Run(DevPlayContext ctx) => TeamBot.RunSeeds(ctx, BotArgs.Seeds(1, 2, 3), Solve, null, Options(), "L8");

        public static BotOptions Options() => new BotOptions { StrictPlayerThrows = true };

        /// <summary>DEV ONLY (never in the proof runs): start at section N (1 = start, 2 = C1, 3 = C2) by debug-teleporting.</summary>
        public static int DevStartSection = 1;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 P(string id) => LevelMark.Position(id);

        public static IEnumerator Solve(TeamBot bot)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bool useTool = BotArgs.Int("-l8NoTool", 0) == 0;
            bot.Log("L8 team: " + bot.TeamSummary() + (useTool ? "" : "  [TOOL-LESS FALLBACK]"));
            if (DevStartSection > 1)
            {
                Vector3 c = DevStartSection == 2 ? P("checkpoint.1") : P("checkpoint.2");
                yield return bot.DebugTeleport(brute, c + V(-1.4f, 0.1f, 0f));
                yield return bot.DebugTeleport(spr, c + V(0f, 0.1f, 0f));
                yield return bot.DebugTeleport(brain, c + V(1.4f, 0.1f, 0f));
                yield return bot.Wait(0.5f);
            }
            // story probe: proves what the story did during this run (CAMPAIGN 22 item 3: story-ON run)
            int lines = 0, cuts = 0; var beats = new System.Collections.Generic.List<string>();
            System.Action<StoryLine> onLine = _ => lines++;
            System.Action<bool> onCut = on => { if (on) cuts++; };
            System.Action<string> onBeat = id => { if (id != null && id.StartsWith("ch07")) beats.Add(id); };
            GameEvents.StoryLineShown += onLine; GameEvents.CutsceneChanged += onCut; GameEvents.StoryBeat += onBeat;
            bot.Log("L8 story: AutoSkip=" + BotStory.AutoSkip + " BotMode=" + BotStory.BotMode + " -botStory=" + BotStory.CommandLineStoryOn);
            if (DevStartSection <= 1) yield return S0S1(bot, brute, spr, brain, useTool);
            if (!bot.Failed && DevStartSection <= 2) yield return S2(bot, brute, spr, brain);
            if (!bot.Failed) yield return S3(bot, brute, spr, brain);
            GameEvents.StoryLineShown -= onLine; GameEvents.CutsceneChanged -= onCut; GameEvents.StoryBeat -= onBeat;
            bot.Log("L8 story: lines shown " + lines + ", blocking cutscenes " + cuts + ", beats " + string.Join(",", beats));
        }

        // ------------------------------------------------------------------ S0 + S1
        static IEnumerator S0S1(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain, bool useTool)
        {
            if (useTool)
            {
                var owner = bot.GunOwner;
                yield return bot.Interact(owner, "s0.locker", (BotTarget)"s0.lockerStand");
                if (bot.Failed) yield break;
                yield return bot.Expect(() => { foreach (var t in ToolSource.All) if (t != null && t.IsOut) return true; return false; }, "SWAP-O-MATIC is out of the locker", 2f);
                if (bot.Failed) yield break;
            }
            // the others walk up to the first curtain while the Sprinter runs
            yield return bot.Parallel(
                SprinterCurtainRun(bot, spr),
                bot.WalkPath(brute, false, V(-1.3f, 0f, 9f), V(-1.3f, 0f, 21.2f)),
                bot.WalkPath(brain, false, V(-0.2f, 0f, 9f), V(-1.6f, 0f, 14.5f), V(1.2f, 0f, 21.0f)));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s1.laserToggle", true, 1f);
            if (bot.Failed) yield break;
            yield return bot.Parallel(
                bot.WalkPath(brute, false, V(-1.3f, 0f, 44.2f), V(-1.4f, 0f, 47.5f)),
                bot.WalkPath(brain, false, V(-1.6f, 0f, 22.5f), V(-1.6f, 0f, 43.8f), V(1.4f, 0f, 47.5f)),
                bot.WalkPath(spr, false, V(-1.3f, 0f, 42.6f), V(-1.2f, 0f, 44.6f), V(0f, 0f, 47.2f)));
            if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.1");
        }

        static IEnumerator SprinterCurtainRun(TeamBot bot, ShifterCharacter spr)
        {
            yield return bot.Interact(spr, "s1.timed", (BotTarget)"s1.timedStand");
            if (bot.Failed) yield break;
            yield return bot.WalkPath(spr, true, "s1.run.1", "s1.run.2", "s1.leverStand");
            if (bot.Failed) yield break;
            yield return bot.Interact(spr, "s1.lever");
        }

        // ------------------------------------------------------------------ S2
        static IEnumerator S2(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.Parallel(bot.WalkTo(brute, V(-1f, 0f, 55.6f)), bot.WalkTo(brain, V(0.4f, 0f, 55.8f)), bot.WalkTo(spr, V(1.6f, 0f, 55.6f)));
            if (bot.Failed) yield break;
            // ---- Lock A
            yield return bot.Parallel(
                PushPallet(bot, brute),
                CrateOnPlateII(bot, brain),
                bot.WalkPath(spr, false, V(-3f, 0f, 62f), P("s2.plateIII.center")));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.lockA", true, 5f);
            if (bot.Failed) yield break;
            // ---- Lock C first (the gun owner shoots the code from the podium) while the others park on key pads
            var gun = bot.GunOwner;
            if (gun == null) { bot.Fail("no gun owner"); yield break; }
            ShifterCharacter k1, k2;
            if (gun == spr) { k1 = brute; k2 = brain; }
            else { k2 = gun; k1 = gun == brute ? brain : brute; }
            yield return bot.Parallel(GunnerRoute(bot, gun, brute, spr, brain), gun == spr ? bot.Wait(0.1f) : SprinterToK3(bot, spr),
                                      ToK1(bot, k1, brute), gun == k2 ? bot.Wait(0.1f) : ToK2(bot, k2, brute));
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s2.lockC", true, 1f);
            if (bot.Failed) yield break;
            // ---- Lock B: E, SHIFT, E, SHIFT, E
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return bot.Interact(k1, "s2.K1.key");
                yield return bot.Interact(k2, "s2.K2.key");
                yield return bot.Interact(spr, "s2.K3.key");
                if (bot.Failed) yield break;
                var lb = LevelMark.Find<L8Node>("s2.lockB");
                float t0 = Time.time;
                while (Time.time - t0 < 0.6f && (lb == null || !lb.IsOn)) yield return null;
                if (lb != null && lb.IsOn) break;
                bot.Warn("three-key turn missed the 5 s window (attempt " + (attempt + 1) + "), retrying");
                yield return bot.Wait(5.2f);
            }
            yield return bot.ExpectPowered("s2.lockB", true, 1f);
            yield return bot.ExpectPowered("s2.vaultDoor", true, 1f);
            if (bot.Failed) yield break;
            var door = LevelMark.Find<L8VaultDoor>("s2.vaultDoor");
            yield return bot.WaitUntil(() => door == null || door.Progress01 >= 1f, 6f, "vault door fully open");
            if (bot.Failed) yield break;
            yield return bot.Parallel(Stage(bot, brute, -1.6f), Stage(bot, brain, 0f), Stage(bot, spr, 1.6f));
            if (bot.Failed) yield break;
            yield return bot.GatherAt("checkpoint.2");
        }

        static IEnumerator PushPallet(TeamBot bot, ShifterCharacter brute)
        {
            yield return bot.PushBlock(brute, "s2.pallet", "s2.plateI.center", 0.3f, 60f);
            if (bot.Failed) yield break;
            yield return bot.WalkTo(brute, V(-6.2f, 0f, 63.2f));
        }

        static IEnumerator CrateOnPlateII(TeamBot bot, ShifterCharacter brain)
        {
            var plate = LevelMark.Find<WeightRangePlate>("s2.plateII");
            var crate = LevelMark.Find("s2.crateA");
            Vector3 pc = P("s2.plateII.drop");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return bot.Grab(brain, crate);
                if (bot.Failed) yield break;
                yield return bot.WalkTo(brain, pc + V(0.55f, 0f, 0.1f * attempt), false, 0.35f);
                if (bot.Failed) yield break;
                yield return bot.Face(brain, Vector3.left);
                yield return bot.Drop(brain);
                if (bot.Failed) yield break;
                yield return bot.Wait(0.6f);
                yield return bot.WalkTo(brain, pc + V(2.4f, 0f, 0.4f));
                if (bot.Failed) yield break;
                yield return bot.Wait(0.4f);
                var cp = crate.transform.position;
                if (Mathf.Abs(cp.x - pc.x) < 0.95f && Mathf.Abs(cp.z - pc.z) < 0.95f && (plate == null || plate.IsOn)) yield break;
                bot.Warn("crate missed plate II at " + cp + ", retrying");
            }
        }

        static IEnumerator SprinterToK3(TeamBot bot, ShifterCharacter spr, bool fromPodium = false)
        {
            if (fromPodium) yield return bot.WalkPath(spr, false, V(-4.5f, 0.4f, 69.6f), V(-4.5f, 0f, 68.6f), V(-0.6f, 0f, 57.2f), P("s2.rampBottom"), P("s2.rampTop"), P("s2.rampLip"));
            else yield return bot.WalkPath(spr, false, V(-4.2f, 0f, 62f), V(-0.6f, 0f, 57.2f), P("s2.rampBottom"), P("s2.rampTop"), P("s2.rampLip"));
            if (bot.Failed) yield break;
            yield return bot.JumpGap(spr, "s2.lipEdge", "s2.galleryLanding", true, 2.6f);
            if (bot.Failed) yield break;
            yield return bot.WalkTo(spr, "s2.K3");
        }

        static IEnumerator SprinterDown(TeamBot bot, ShifterCharacter spr)
        {
            yield return bot.WalkPath(spr, false, V(9.6f, 3f, 70.9f), V(9.6f, 0f, 68.4f), V(5.4f, 0f, 67.6f));
        }

        static IEnumerator ToK1(TeamBot bot, ShifterCharacter c, ShifterCharacter brute)
        {
            if (c == brute) yield return bot.WalkPath(c, false, V(-1.2f, 0f, 57.0f), V(3.6f, 0f, 57.2f), P("s2.K1"));
            else yield return bot.WalkPath(c, false, V(-6.6f, 0f, 69.4f), V(-5.9f, 0f, 63.5f), V(-1.2f, 0f, 57.0f), V(3.6f, 0f, 57.2f), P("s2.K1"));
        }

        static IEnumerator ToK2(TeamBot bot, ShifterCharacter c, ShifterCharacter brute)
        {
            if (c == brute) yield return bot.WalkPath(c, false, V(-6.6f, 0f, 68f), V(-6.8f, 0f, 76f), P("s2.K2"));
            else yield return bot.WalkPath(c, false, V(-6.8f, 0f, 76f), P("s2.K2"));
        }

        /// <summary>Gun owner: podium, CROWN -> MONOCLE -> MUSTACHE -> DRUM, then to its key pad (K2, or K3 for the Sprinter).</summary>
        static IEnumerator GunnerRoute(TeamBot bot, ShifterCharacter gun, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            if (gun == brain) yield return bot.WalkPath(gun, false, V(-6.6f, 0f, 68.8f), V(-4.5f, 0f, 68.8f), P("s2.podium"));
            else if (gun == brute) yield return bot.WalkPath(gun, false, V(-6.2f, 0f, 66.5f), V(-4.5f, 0f, 68.8f), P("s2.podium"));
            else yield return bot.WalkPath(gun, false, V(-6.6f, 0f, 70.5f), V(-4.5f, 0f, 68.8f), P("s2.podium"));
            if (bot.Failed) yield break;
            var seq = LevelMark.Find<SequenceLock>("s2.sequence");
            string[] order = { "CROWN", "MONOCLE", "MUSTACHE", "DRUM" };
            for (int i = 0; i < order.Length; i++)
            {
                int need = i + 1;
                yield return bot.ShootAt(gun, "s2.dial." + order[i], () => seq == null || seq.Progress >= need || seq.IsOn, 8, 14f);
                if (bot.Failed) yield break;
            }
            if (gun == spr) yield return SprinterToK3(bot, spr, true);
            else yield return bot.WalkPath(gun, false, V(-4.5f, 1f, 73.4f), V(-4.3f, 0f, 76.6f), P("s2.K2"));
        }

        static IEnumerator Stage(TeamBot bot, ShifterCharacter c, float x)
        {
            var f = c.Feet;
            if (f.y > 2f) yield return bot.WalkPath(c, false, V(9.6f, 3f, 70.9f), V(9.6f, 0f, 68.4f), V(5.4f, 0f, 67.6f));
            f = c.Feet;
            if (f.x < -3.4f && f.z < 74f) yield return bot.WalkTo(c, V(-3.6f, 0f, 76.5f));   // podium -> past the lip
            else if (f.x > 3f && f.z < 78f) yield return bot.WalkTo(c, V(4.2f, 0f, 78.5f));   // east side -> between lip and gallery
            if (bot.Failed) yield break;
            yield return bot.WalkTo(c, V(x, 0f, 83.6f));
        }

        // ------------------------------------------------------------------ S3
        static IEnumerator S3(TeamBot bot, ShifterCharacter brute, ShifterCharacter spr, ShifterCharacter brain)
        {
            yield return bot.Parallel(
                bot.WalkPath(spr, false, V(-1.35f, 0f, 97.6f), P("s3.pedestalStep"), P("s3.pedestalTop")),
                bot.WalkPath(brute, false, V(2.6f, 0f, 97.5f), V(1.8f, 0f, 101.9f)),
                bot.WalkTo(brain, V(-3.6f, 0f, 103.5f)));
            if (bot.Failed) yield break;
            yield return bot.WaitSettled(spr, 2f);
            yield return bot.Grab(brute, "s3.prism");
            if (bot.Failed) yield break;
            var alarm = LevelMark.Find<MinWeightAlarm>("s3.pedestal");
            yield return bot.Wait(0.5f);
            if (alarm != null && alarm.IsOn) { bot.Fail("pedestal alarm went off (the swap failed)"); yield break; }
            yield return bot.WalkTo(brute, "s3.socketDrop");
            if (bot.Failed) yield break;
            yield return bot.Drop(brute);
            yield return bot.ExpectPowered("s3.valve", true, 3f);
            if (bot.Failed) yield break;
            yield return bot.ExpectPowered("s3.exitDoor", true, 2f);
            yield return bot.Wait(1.2f);
            yield return bot.Parallel(
                bot.WalkPath(spr, false, V(-2.1f, 0f, 100.2f), V(-2.9f, 0f, 104f), V(-2.9f, 0f, 112.5f), V(-1.0f, 0f, 113.7f)),
                bot.WalkPath(brain, false, V(-3.5f, 0f, 106f), V(-3.3f, 0f, 112.6f), V(-0.2f, 0f, 114.4f)),
                bot.WalkPath(brute, false, V(-1.2f, 0f, 107.1f), V(-3.1f, 0f, 107.6f), V(-3.1f, 0f, 112.5f), V(0.9f, 0f, 114.0f)));
            if (bot.Failed) yield break;
            yield return bot.EnterExit();
        }
    }
}
