// =====================================================================================
//  SHIFTERS - PerspectiveLevelTests.cs   [TEST - PERSPECTIVE & LEVEL agent]
//  DevPlay scenarios:
//    Shifters.PerspectiveLevelTests.Perspective  - reveal states per viewer, labels, pulses, events,
//                                                   hologram path + footprints, screenshots per view.
//    Shifters.PerspectiveLevelTests.LevelFlow    - LevelManager boot, checkpoints (snapshot, revive,
//                                                   ordering, restore with a Lost member), tutorials,
//                                                   hints by IQ, exit (2-press leave + all-inside).
//  Both swap GameSession for an in-memory stand-in so tests never write the user's PlayerPrefs.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class PerspectiveLevelTests
    {
        /// <summary>GameSession that keeps everything in memory (no PlayerPrefs, no scene loads).</summary>
        public class MemorySession : GameSession
        {
            public int CompleteCalls;
            public LevelResult Completed;
            protected override void Awake() { base.Awake(); ResumeLevelIndex = -1; }
            protected override void Start() { }
            protected override void Update() { }
            protected override void OnApplicationQuit() { }
            public override void NewRun(int seed = 0) { base.NewRun(seed); ResumeLevelIndex = -1; }
            public override void NotifyLevelLoaded(int index) { if (index != LevelIndex) Snapshot = null; LevelIndex = index; }
            public override void CompleteLevel(LevelResult result) { CompleteCalls++; Completed = result; Snapshot = null; }
            public override void LoadLevel(int index) { }
            public override void RestartLevel() { }
            public override void RestartFromCheckpoint() { }
            public override void SaveSettings() { }
            public void SetSnapshotState(int slot, CharacterState s) { if (Snapshot != null) Snapshot.States[slot] = s; }
            /// <summary>What RestartFromCheckpoint/RestartLevel count before reloading (the test reloads by hand).</summary>
            public void CountRestart() { LevelRestarts++; Stats.Restarts++; }
        }

        static MemorySession SwapSession(int seed)
        {
            var old = GameSession.Instance;
            var host = old != null ? old.gameObject : (GameBootstrap.Root != null ? GameBootstrap.Root : new GameObject("[TestSession]"));
            if (old != null) Object.DestroyImmediate(old);
            var s = host.AddComponent<MemorySession>();
            s.NewRun(seed);
            return s;
        }

        static Transform Arena(string name, float size = 70f)
        {
            var root = new GameObject(name).transform;
            Kit.SolidBox(root, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), MatKey.Floor, 0f);
            return root;
        }

        static void Aim(Vector3 pos, Vector3 target)
        {
            if (ShiftersCamera.Instance != null) ShiftersCamera.Instance.enabled = false; // tests frame shots manually
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = pos;
            cam.transform.LookAt(target);
        }

        static ShifterCharacter ByArchetype(Archetype a)
        {
            var cm = CharacterManager.Instance;
            if (cm == null) return null;
            for (int i = 0; i < cm.Team.Count; i++) if (cm.Team[i] != null && cm.Team[i].Archetype == a) return cm.Team[i];
            return null;
        }

        static void Put(ShifterCharacter c, Vector3 p)
        {
            if (c == null) return;
            c.ExternalControl = true;
            if (c.Motor != null) c.Motor.Teleport(p, Quaternion.identity); else c.transform.position = p;
        }

        static bool AllRevealsMatch(PerspectiveMask mask, out int count)
        {
            count = 0;
            bool ok = true;
            foreach (var r in Revealable.All)
            {
                if (r == null) continue;
                count++;
                if (r.IsRevealed != ((mask & r.Category) != 0)) ok = false;
            }
            return ok;
        }

        // =====================================================================================
        public static IEnumerator Perspective(DevPlayContext ctx)
        {
            SwapSession(4242);
            int events = 0;
            GameEvents.PerspectiveChanged += (m, v) => events++;

            var root = Arena("PerspectiveArena");
            var spawns = LevelKit.SpawnPoints(root, new Vector3(0f, 0.05f, -6f), Quaternion.identity);
            var lm = LevelKit.LevelManager(root, 0, spawns);
            lm.PlayMusic = false;

            // ---- demo content for every perspective
            var block = Kit.SolidBox(root, "HeavyBlock", new Vector3(-4f, 0.8f, 0f), Vector3.one * 1.6f, MatKey.CrateHeavy, 0.06f);
            var rBlock = PerspectiveKit.Reveal(block.gameObject, PerspectiveMask.Strength, "160 KG - YOU CAN PUSH", true, false, new Vector3(0f, 1.35f, 0f));
            var target = Kit.SolidBox(root, "Target", new Vector3(3.2f, 1.7f, 2.5f), new Vector3(1.1f, 1.1f, 0.2f), MatKey.PuzzleBody, 0.05f);
            var rTarget = PerspectiveKit.Reveal(target.gameObject, PerspectiveMask.Weapon, "SHOOT", true, false, new Vector3(0f, 1.0f, 0f));
            var hint = PerspectiveKit.ShootHint(root, new Vector3(3.2f, 1.7f, 2.3f));
            Kit.SolidBox(root, "ClueWall", new Vector3(0f, 1.5f, 6.15f), new Vector3(7f, 3f, 0.3f), MatKey.Wall, 0.03f);
            var clueA = PerspectiveKit.Clue(root, new Vector3(-1.6f, 1.7f, 6f), Quaternion.identity, "3", 0.9f);
            PerspectiveKit.Clue(root, new Vector3(1.4f, 1.7f, 6f), Quaternion.identity, "1 + 1 = ?", 0.45f);
            PerspectiveKit.Clue(root, new Vector3(0f, 0.01f, 3.2f), Quaternion.Euler(90f, 0f, 0f), "X", 0.8f);
            var tiles = PerspectiveKit.HologramPath(root, new[] { new Vector3(7f, 0.6f, -5f), new Vector3(7f, 0.6f, 3f), new Vector3(10f, 1.2f, 6f) }, 1.6f);
            var route = PerspectiveKit.SpeedRoute(root, new[] { new Vector3(-2f, 0f, -4f), new Vector3(-2f, 0f, 3f), new Vector3(2.2f, 0f, 4.4f) });

            yield return ctx.Wait(0.6f);
            var cm = CharacterManager.Instance;
            var pm = PerspectiveManager.Instance;
            ctx.Check(pm != null && pm.Labels != null, "PerspectiveManager + label pool bootstrapped");
            ctx.Check(cm != null && cm.Team.Count == 3, "LevelManager spawned a team of 3");
            ctx.Check(ShiftersCamera.Instance != null, "main camera has ShiftersCamera");
            ctx.Check(tiles.Length == 8, "hologram path built 8 tiles (got " + tiles.Length + ")");
            ctx.Check(route.Chevrons.Length >= 8, "speed route has chevrons (" + route.Chevrons.Length + ")");
            if (cm == null || pm == null || cm.Team.Count < 3) yield break;

            var brute = ByArchetype(Archetype.Brute);
            var sprinter = ByArchetype(Archetype.Sprinter);
            var brain = ByArchetype(Archetype.Brainiac);
            ShifterCharacter gunner = null;
            foreach (var c in cm.Team) if (c.Weapon != null && c.Weapon.HasWeapon) gunner = c;
            ctx.Check(gunner != null, "exactly one teammate holds the gun");
            foreach (var c in cm.Team) Put(c, c.Feet); // freeze AI/brains for deterministic checks
            Aim(new Vector3(0.5f, 8.5f, -14.5f), new Vector3(0.8f, 0.6f, 1.2f));

            var order = new[] { brute, sprinter, brain };
            foreach (var who in order)
            {
                int pulses = pm.PulseCount, ev = events;
                // switch away first so the switch event always fires
                if (cm.Active == who) { cm.SwitchTo((who.Slot + 1) % 3, true); yield return ctx.Wait(0.1f); pulses = pm.PulseCount; ev = events; }
                cm.SwitchTo(who.Slot, true);
                yield return ctx.Wait(0.6f);
                var mask = who.Perspectives;
                string tag = who.Archetype + (mask.HasFlag(PerspectiveMask.Weapon) ? "+GUN" : "");
                ctx.Check(pm.Viewer == who && pm.Current == mask, tag + ": manager viewer/mask = " + mask);
                ctx.Check(pm.PulseCount > pulses, tag + ": scan pulse fired on switch");
                ctx.Check(events > ev, tag + ": PerspectiveChanged raised");
                ctx.Check(AllRevealsMatch(mask, out int n), tag + ": all " + n + " Revealables match the mask");
                bool str = (mask & PerspectiveMask.Strength) != 0, spd = (mask & PerspectiveMask.Speed) != 0;
                bool iq = (mask & PerspectiveMask.Intellect) != 0, gun = (mask & PerspectiveMask.Weapon) != 0;
                ctx.Check(rBlock.IsRevealed == str && rTarget.IsRevealed == gun, tag + ": block(STR)=" + rBlock.IsRevealed + " target(GUN)=" + rTarget.IsRevealed);
                ctx.Check(clueA.Text.GetComponent<MeshRenderer>().enabled == iq, tag + ": IQ clue visible=" + iq);
                ctx.Check(tiles[0].IsVisible == iq, tag + ": hologram tiles visible=" + iq);
                ctx.Check(route.Chevrons[0].enabled == spd, tag + ": turbo chevrons visible=" + spd);
                ctx.Check(hint.GetComponentInChildren<MeshRenderer>(true).enabled == gun, tag + ": crosshair hint visible=" + gun);
                if (str)
                {
                    var chip = pm.Labels.ChipFor(rBlock);
                    ctx.Check(chip != null && chip.CurrentText.Contains("160 KG"), "STR: heavy block label chip shown");
                    var mate = who == brain ? brute : brain;
                    var mateTag = mate.GetComponent<TeammateWeightTag>();
                    var mateChip = mateTag != null ? pm.Labels.ChipFor(mateTag.Reveal) : null;
                    ctx.Check(mateChip != null && mateChip.CurrentText.Contains(" KG"), "STR: teammate weight label '" + (mateChip != null ? mateChip.CurrentText : "none") + "'");
                }
                else ctx.Check(pm.Labels.ChipFor(rBlock) == null, tag + ": no STR label without the perk");
                ctx.Log(tag + ": revealed=" + pm.RevealedCount + " labels=" + pm.Labels.VisibleCount);
                ctx.Shot("view_" + who.Archetype);
            }

            // ---- R pulse: focused labels for 5 s
            cm.SwitchTo(brute.Slot, true);
            yield return ctx.Wait(5.4f);
            ctx.Check(!pm.Focused, "focus expired 5 s after the switch pulse");
            int unfocused = pm.Labels.VisibleCount;
            int p0 = pm.PulseCount;
            ctx.Check(pm.TryScan(), "R scan pulses");
            ctx.Check(!pm.TryScan(), "R scan respects the 1.5 s cooldown");
            yield return ctx.Wait(0.4f);
            ctx.Check(pm.Focused && pm.PulseCount == p0 + 1, "Focused after the pulse");
            var selfTag = brute.GetComponent<TeammateWeightTag>();
            var selfChip = pm.Labels.ChipFor(selfTag.Reveal);
            ctx.Check(pm.Labels.VisibleCount > unfocused && selfChip != null && selfChip.CurrentText.Contains("KG"), "focused: labels " + unfocused + " -> " + pm.Labels.VisibleCount + " (own weight shown)");
            ctx.Shot("strength_focused");

            // ---- gun owner sees weapon content (covered above if the gunner was one of them; re-check explicitly)
            cm.SwitchTo(gunner.Slot, true);
            yield return ctx.Wait(0.5f);
            ctx.Check(rTarget.IsRevealed && hint.IsRevealed, "gun holder (" + gunner.Archetype + ") sees shoot target + crosshair");

            // ---- hidden path + footprints
            cm.SwitchTo(brute.Slot, true);
            yield return ctx.Wait(0.5f);
            bool anyVisible = false;
            foreach (var t in tiles) anyVisible |= t.IsVisible;
            ctx.Check(!anyVisible, "hologram tiles invisible to non-IQ");
            ctx.Check(tiles[0].GetComponent<BoxCollider>().enabled && !tiles[0].GetComponent<BoxCollider>().isTrigger, "hologram tile collider is solid");
            Put(brain, tiles[2].transform.position + Vector3.up * 0.3f);
            yield return ctx.Wait(0.6f);
            ctx.Check(brain.Feet.y > 0.45f, "Brainiac stands on the invisible tile (feet y=" + brain.Feet.y.ToString("0.00") + ")");
            ctx.Check(tiles[2].IsFootprinted && tiles[1].IsFootprinted && tiles[3].IsFootprinted && !tiles[6].IsFootprinted, "footprints on tile + neighbours only");
            ctx.Check(tiles[2].IsVisible && !tiles[6].IsVisible, "footprinted tiles faintly visible to the Brute");
            Aim(new Vector3(1.5f, 6.5f, -8f), new Vector3(7.5f, 0.6f, 0.5f));
            ctx.Shot("footprints_brute_view");
            cm.SwitchTo(brain.Slot, true);
            yield return ctx.Wait(0.6f);
            bool all = true;
            foreach (var t in tiles) all &= t.IsVisible;
            ctx.Check(all, "IQ view shows the whole hologram path");
            ctx.Shot("iq_view_path");
            Put(brain, new Vector3(3f, 0.1f, -6f));
            cm.SwitchTo(brute.Slot, true);
            yield return ctx.Wait(8.8f);
            ctx.Check(!tiles[2].IsVisible && !tiles[2].IsFootprinted, "footprints fade after 8 s");
            ctx.Log("perspective scenario done");
        }

        // =====================================================================================
        public static IEnumerator LevelFlow(DevPlayContext ctx)
        {
            var session = SwapSession(9001);
            int started = 0, cpActivated = 0, hides = 0;
            var reached = new List<int>();
            var toasts = new List<string>();
            var banners = new List<string>();
            var tutorials = new List<string>();
            var hints = new List<string>();
            var completed = new List<LevelResult>();
            string objective = null;
            GameEvents.LevelStarted += () => started++;
            GameEvents.CheckpointReached += i => reached.Add(i);
            GameEvents.Toast += (t, c) => toasts.Add(t);
            GameEvents.Banner += (t, s, st) => banners.Add(t + " | " + s);
            GameEvents.TutorialShow += m => tutorials.Add(m.Id);
            GameEvents.TutorialHide += () => hides++;
            GameEvents.Hint += h => hints.Add(h);
            GameEvents.LevelCompleted += r => completed.Add(r);
            GameEvents.ObjectiveChanged += o => objective = o;
            CheckpointManager.CheckpointActivated += cp => cpActivated++;

            var root = Arena("FlowArena", 90f);
            var spawns = LevelKit.SpawnPoints(root, new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            var lm = LevelKit.LevelManager(root, 0, spawns);
            lm.PlayMusic = false;
            var cp1 = LevelKit.Checkpoint(root, new Vector3(0f, 0f, 14f), Quaternion.identity, 1);
            var cp0 = LevelKit.Checkpoint(root, new Vector3(0f, 0f, 28f), Quaternion.identity, 0);
            var exitA = LevelKit.Exit(root, new Vector3(-14f, 0f, 30f), Quaternion.identity);
            var exitB = LevelKit.Exit(root, new Vector3(14f, 0f, 30f), Quaternion.identity);
            LevelKit.Tutorial(root, new Vector3(-14f, 1.5f, 0f), new Vector3(4f, 3f, 4f), "t_move", "WELCOME TO SHIFTERS", "You have three characters. Try not to lose them.", new[] { "W", "A", "S", "D" });
            var tIQ = LevelKit.Tutorial(root, new Vector3(-14f, 1.5f, 9f), new Vector3(4f, 3f, 4f), "t_iq", "BIG BRAIN", "Only smart people can read this.");
            tIQ.RequireActivePerk = PerspectiveMask.Intellect;
            var hz = LevelKit.HintZone(root, new Vector3(-14f, 2f, 0f), new Vector3(6f, 4f, 6f), "REAL ANSWER", "VAGUE ANSWER");

            yield return ctx.Wait(0.6f);
            var cm = CharacterManager.Instance;
            ctx.Check(started == 1 && lm.IsStarted, "LevelStarted raised once");
            ctx.Check(cm != null && cm.Team.Count == 3 && cm.Active != null, "team spawned with an active character");
            ctx.Check(CharacterSwitcher.Instance != null && TutorialManager.Instance != null && HintSystem.Instance != null && CheckpointManager.Instance != null, "LevelManager created switcher/tutorial/hint/checkpoint systems");
            ctx.Check(ShiftersCamera.Instance != null && ShiftersCamera.Instance.Target == cm.Active, "camera targets the active character");
            ctx.Check(objective == LevelCatalog.Get(0).DefaultObjective, "objective raised: " + objective);
            ctx.Check(banners.Exists(b => b.StartsWith(LevelCatalog.Get(0).Title)), "intro banner: " + (banners.Count > 0 ? banners[0] : "none"));
            ctx.Check(session.LevelIndex == 0, "session notified of level index");
            ctx.Check(Vector3.Distance(cm.Get(1).Feet, spawns[1].position) < 0.6f, "slot 1 at its start spawn");
            if (cm == null || cm.Team.Count < 3) yield break;
            foreach (var c in cm.Team) Put(c, c.Feet);
            var brute = ByArchetype(Archetype.Brute);
            var sprinter = ByArchetype(Archetype.Sprinter);
            var brain = ByArchetype(Archetype.Brainiac);
            Aim(new Vector3(0f, 16f, -6f), new Vector3(0f, 0f, 18f));
            ctx.Shot("flow_overview");

            // ---- checkpoint: snapshot + revive downed teammate at the spawn points
            if (cm.Active != brute) cm.SwitchTo(brute.Slot, true);
            yield return ctx.Wait(0.1f);
            var victim = brain; // not active: no auto-switch beat
            victim.KnockOut("test");
            ctx.Check(victim.State == CharacterState.Downed, "victim downed");
            Put(brute, cp1.transform.position + Vector3.up * 0.1f);
            yield return ctx.Wait(0.5f);
            var cmgr = CheckpointManager.Instance;
            ctx.Check(cmgr.CurrentIndex == 1 && cp1.IsActivated, "checkpoint 1 activated");
            ctx.Check(reached.Count == 1 && reached[0] == 1 && cpActivated == 1, "CheckpointReached + CheckpointActivated raised");
            ctx.Check(session.Snapshot != null && session.Snapshot.CheckpointIndex == 1, "snapshot saved to session");
            ctx.Check(victim.IsAlive && Vector3.Distance(victim.Feet, cp1.GetSpawnPose(victim.Slot).position) < 1.2f, "downed teammate revived at the checkpoint spawn");
            ctx.Check(toasts.Exists(t => t.StartsWith("HR retrieved " + victim.DisplayName)), "HR retrieved toast");
            ctx.Check(session.Snapshot != null && System.Array.TrueForAll(session.Snapshot.States, s => s == CharacterState.Alive), "snapshot states all Alive after revive");
            ctx.Check(session.Stats.Checkpoints == 1, "Stats.Checkpoints = 1");
            ctx.Check(session.Stats.Revives == 1, "Stats.Revives = 1 (got " + session.Stats.Revives + ")");
            ctx.Check(banners.Exists(b => b.StartsWith("CHECKPOINT 2")), "CHECKPOINT banner uses the rank (index 1 after index 0 -> 2)");
            ctx.Check(cp1.NumberTexts.Length == 2 && cp1.NumberTexts[0].text == "2" && cp0.NumberTexts[0].text == "1", "flag numbers relabelled by rank: cp1=" + (cp1.NumberTexts.Length > 0 ? cp1.NumberTexts[0].text : "?") + " cp0=" + (cp0.NumberTexts.Length > 0 ? cp0.NumberTexts[0].text : "?"));
            ctx.Check(WeaponPickup.FallbackSpawn.HasValue && Vector3.Distance(WeaponPickup.FallbackSpawn.Value, cp1.GetSpawnPose(1).position) < 0.05f, "WeaponPickup.FallbackSpawn moved to checkpoint 1");
            ctx.Check(cp1.Lamp != null && cp1.Lamp.enabled && cp0.Lamp != null && !cp0.Lamp.enabled, "light budget: only the current checkpoint's point light is on");
            yield return ctx.Wait(1.6f);
            Aim(new Vector3(-6f, 4.5f, 6.5f), new Vector3(0f, 1.5f, 14.5f));
            ctx.Shot("checkpoint_active");

            Put(brute, cp0.transform.position + Vector3.up * 0.1f);
            yield return ctx.Wait(0.5f);
            ctx.Check(cmgr.CurrentIndex == 1 && cpActivated == 1 && reached.Count == 1 && cmgr.ActivationCount == 1, "lower-index checkpoint does not activate (stays index 1)");

            // ---- restore from the checkpoint with a Lost member (scene-reload equivalent)
            session.SetSnapshotState(sprinter.Slot, CharacterState.Lost);
            session.CountRestart();
            int sprinterSlot = sprinter.Slot, bruteSlot = brute.Slot, brainSlot = brain.Slot;
            string sprinterName = sprinter.DisplayName, bruteName = brute.DisplayName;
            Object.Destroy(lm.gameObject);
            yield return ctx.Wait(0.1f);
            banners.Clear();
            ctx.Check(CharacterManager.Instance == null && TutorialManager.Instance == null && HintSystem.Instance == null, "level systems destroyed with the LevelManager");
            lm = LevelKit.LevelManager(root, 0, spawns);
            lm.PlayMusic = false;
            yield return ctx.Wait(0.6f);
            cm = CharacterManager.Instance;
            ctx.Check(lm.StartedFromCheckpoint && started == 2, "restarted from checkpoint");
            var lost = cm.Get(sprinterSlot);
            ctx.Check(lost.State == CharacterState.Lost && lost.IsHidden, "Lost member restored hidden");
            ctx.Check(Vector3.Distance(cm.Get(bruteSlot).Feet, cp1.GetSpawnPose(bruteSlot).position) < 0.6f, "team spawned at checkpoint 1");
            ctx.Check(cm.Active != null && cm.Active.IsAlive, "active slot is alive (" + cm.Active.DisplayName + ")");
            ctx.Check(cp1.IsActivated && CheckpointManager.Instance.CurrentIndex == 1, "checkpoint visuals/index restored");
            ctx.Check(cp1.Lamp.enabled && !cp0.Lamp.enabled, "restore: only the current checkpoint lamp lit");
            ctx.Check(banners.Exists(b => b.StartsWith("CHECKPOINT 2")), "restore banner: " + (banners.Count > 0 ? banners[0] : "none"));
            ctx.Check(WeaponPickup.FallbackSpawn.HasValue && Vector3.Distance(WeaponPickup.FallbackSpawn.Value, cp1.GetSpawnPose(1).position) < 0.05f, "restore: weapon fallback at checkpoint 1");
            brute = cm.Get(bruteSlot);
            brain = cm.Get(brainSlot);
            foreach (var c in cm.Team) if (c.IsAlive) Put(c, c.Feet);

            // ---- tutorials
            if (cm.Active != brute) cm.SwitchTo(bruteSlot, true);
            Put(brute, new Vector3(-14f, 0.1f, 0f));
            yield return ctx.Wait(0.4f);
            var tm = TutorialManager.Instance;
            ctx.Check(tutorials.Count == 1 && tutorials[0] == "t_move" && tm.Current != null, "tutorial fires on enter");
            tm.Hide();
            ctx.Check(tm.Current == null && hides >= 1, "skip hides the tutorial");
            Put(brute, new Vector3(-14f, 0.1f, -7f));
            yield return ctx.Wait(0.3f);
            Put(brute, new Vector3(-14f, 0.1f, 0f));
            yield return ctx.Wait(0.4f);
            ctx.Check(tutorials.Count == 1, "tutorial fires only once");
            Put(brute, new Vector3(-14f, 0.1f, 9f));
            yield return ctx.Wait(0.5f);
            ctx.Check(!tutorials.Contains("t_iq"), "IQ-only tutorial ignored for the Brute");
            cm.SwitchTo(brainSlot, true);
            Put(brain, new Vector3(-13f, 0.1f, 9f));
            yield return ctx.Wait(0.5f);
            ctx.Check(tutorials.Contains("t_iq"), "IQ-only tutorial fires for the Brainiac");

            // ---- hints by IQ
            var rng = new System.Random(1);
            ctx.Check(HintSystem.Compose(8, hz, rng) == "BIG BRAIN HINT: REAL ANSWER", "IQ 8 -> real hint");
            ctx.Check(HintSystem.Compose(5, hz, rng) == "HINT: VAGUE ANSWER", "IQ 5 -> vague hint");
            string joke = HintSystem.Compose(2, hz, rng);
            ctx.Check(System.Array.IndexOf(HintSystem.Jokes, joke) >= 0 && HintSystem.Jokes.Length >= 15, "IQ 2 -> joke: " + joke);
            ctx.Check(HintSystem.Compose(8, null, rng).StartsWith(HintSystem.RealPrefix) && HintSystem.Compose(5, null, rng).StartsWith(HintSystem.VaguePrefix), "generic tips without a zone");
            Put(brain, new Vector3(-14f, 0.1f, 0.5f));
            yield return ctx.Wait(0.2f);
            string h = HintSystem.Instance.RequestHint();
            ctx.Check(h == "BIG BRAIN HINT: REAL ANSWER" && hints.Count == 1, "H in zone with the Brainiac: " + h);
            ctx.Check(HintSystem.Instance.RequestHint() == null && hints.Count == 1, "hint cooldown");

            // ---- exit A: 1/2 alive inside, then "leave without them" (two presses)
            var zoneA = exitA.transform.TransformPoint(exitA.ZoneCenter);
            Put(brain, new Vector3(zoneA.x, 0.15f, zoneA.z));
            yield return ctx.Wait(0.6f);
            ctx.Check(!exitA.IsCompleting && exitA.AlivePresent == 1 && exitA.AliveTotal == 2, "exit waits for everyone (1/2)");
            ctx.Check(toasts.Exists(t => t == "EXIT: 1/2 PRESENT"), "EXIT: 1/2 PRESENT toast");
            var console = exitA.Console;
            ctx.Check(console.CanInteract(brain, out _), "console offers 'Leave without them'");
            console.Interact(brain);
            ctx.Check(!exitA.IsCompleting && toasts.Exists(t => t.StartsWith("Press E again to leave " + bruteName)), "first press only warns");
            yield return ctx.Wait(0.3f);
            console.Interact(brain);
            ctx.Check(exitA.IsCompleting, "second press confirms");
            Aim(new Vector3(-14f, 4f, 22f), new Vector3(-14f, 2.2f, 31f));
            yield return ctx.Wait(0.8f);
            ctx.Shot("exit_closing");
            yield return ctx.Wait(1.2f);
            ctx.Check(completed.Count == 1 && session.CompleteCalls == 1, "LevelCompleted + session.CompleteLevel");
            if (completed.Count > 0)
            {
                var r = completed[0];
                var left = new List<string>(r.LeftBehind);
                ctx.Check(r.Survivors == 1 && left.Contains(bruteName) && left.Contains(sprinterName) && left.Count == 2, "result: survivors=" + r.Survivors + " left=" + string.Join(",", r.LeftBehind));
                ctx.Check(r.LevelIndex == 0 && r.Time > 0f && r.Restarts >= 1, "result: level=" + r.LevelIndex + " time=" + r.Time.ToString("0.0") + " restarts=" + r.Restarts + " switches=" + r.Switches);
            }

            // ---- exit B (fresh start): completes only when ALL alive are inside; a downed one carried in counts
            Object.Destroy(lm.gameObject);
            yield return ctx.Wait(0.1f);
            lm = LevelKit.LevelManager(root, 0, spawns);
            lm.PlayMusic = false;
            yield return ctx.Wait(0.6f);
            cm = CharacterManager.Instance;
            ctx.Check(!lm.StartedFromCheckpoint && cm.AliveCount == 3, "fresh start after completion (snapshot cleared)");
            foreach (var c in cm.Team) Put(c, c.Feet);
            var zoneB = exitB.transform.TransformPoint(exitB.ZoneCenter);
            var ko = cm.Active == cm.Get(2) ? cm.Get(1) : cm.Get(2);
            var others = new List<ShifterCharacter>();
            foreach (var c in cm.Team) if (c != ko) others.Add(c);
            ko.KnockOut("test");
            Put(ko, new Vector3(zoneB.x, 0.15f, zoneB.z + 1f));
            Put(others[0], new Vector3(zoneB.x - 1.2f, 0.15f, zoneB.z));
            yield return ctx.Wait(0.6f);
            ctx.Check(!exitB.IsCompleting && exitB.DownedPresent == 1, "exit B: 1/2 alive inside (+1 downed) -> waits");
            Put(others[1], new Vector3(zoneB.x + 1.2f, 0.15f, zoneB.z));
            yield return ctx.Wait(0.6f);
            ctx.Check(exitB.IsCompleting, "exit B: all alive inside -> doors close");
            yield return ctx.Wait(2f);
            ctx.Check(completed.Count == 2 && completed[1].Survivors == 3 && completed[1].LeftBehind.Length == 0, "exit B: everyone saved (downed carried in counts)");
            ctx.Check(ko.IsAlive, "downed teammate revived in the elevator");

            // ---- restore a Captured teammate into its HoldingCell (enemies module) from a snapshot
            var cell = EnemyKit.HoldingCell(root, new Vector3(28f, 0f, 0f), Quaternion.identity, "pl_cell");
            int captiveSlot = (cm.ActiveIndex + 1) % 3;
            var snapC = new CheckpointSnapshot { CheckpointIndex = 1, ActiveSlot = cm.ActiveIndex };
            snapC.States[captiveSlot] = CharacterState.Captured;
            snapC.CapturedCellIds[captiveSlot] = "pl_cell";
            session.SaveCheckpoint(snapC);
            Object.Destroy(lm.gameObject);
            yield return ctx.Wait(0.1f);
            lm = LevelKit.LevelManager(root, 0, spawns);
            lm.PlayMusic = false;
            yield return ctx.Wait(0.8f);
            cm = CharacterManager.Instance;
            var captive = cm != null ? cm.Get(captiveSlot) : null;
            ctx.Check(lm.StartedFromCheckpoint && captive != null && captive.State == CharacterState.Captured && captive.CapturedCellId == "pl_cell", "captive restored as Captured (" + (captive != null ? captive.State.ToString() : "null") + ")");
            ctx.Check(cell.PrisonerCount == 1 && captive != null && cell.Contains(captive.Center), "captive locked inside HoldingCell pl_cell");
            ctx.Check(cm != null && cm.Active != null && cm.Active.IsAlive && cm.Active != captive, "active character is a free one");
            Aim(new Vector3(28f, 6f, 9f), new Vector3(28f, 1f, 0f));
            ctx.Shot("captive_restored");
            ctx.Log("level flow scenario done");
        }
    }
}
