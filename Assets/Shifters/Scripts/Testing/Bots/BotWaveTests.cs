// =====================================================================================
//  SHIFTERS - BotWaveTests.cs   [BOTS module - campaign-wave self tests]
//  DevPlay:  devplay.sh <proj> bot_wave -devSetup Shifters.BotWaveTests.Run -devSeconds 1
//    * BotArgs team tokens (MIN / MAX / numbers), TeamKey, ApplyExtreme stats + masses + gun owner (both teams legal)
//    * BotPrefsGuard: nested Push/Pop restores string / int / float keys and deletions exactly
//    * BotStory null-safety (story/campaign modules may be absent) and AutoSkip/BotMode while a run is attached
//    * verbs on the bots' mini course: FaceAndHold, Chase (moving TAG), Race, Guard / WithGuard (fake stalkers),
//      WaitCameraClear (BotFakeCamera), RideUntil (PuzzleKit.MovingPlatform), tool-aware ShootAt helpers
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class BotWaveTests
    {
        public static IEnumerator Run(DevPlayContext ctx)
        {
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            Pure(ctx);
            Prefs(ctx);
            yield return Verbs(ctx);
            driver.ReleaseHarness(ctx);
        }

        static void Pure(DevPlayContext ctx)
        {
            ctx.Check(BotArgs.ParseTeam("MIN") == TeamBot.SeedMin && BotArgs.ParseTeam("max") == TeamBot.SeedMax && BotArgs.ParseTeam(" 7 ") == 7 && BotArgs.ParseTeam("x") == 0,
                      "[BOTW] team tokens MIN/max/7/x parse");
            ctx.Check(TeamBot.TeamKey(TeamBot.SeedMin) == "MIN" && TeamBot.TeamKey(TeamBot.SeedMax) == "MAX" && TeamBot.TeamKey(3) == "3", "[BOTW] TeamKey");
            foreach (var e in new[] { ExtremeTeam.Min, ExtremeTeam.Max })
            {
                var team = CharacterGenerator.GenerateTeam(e == ExtremeTeam.Min ? 9001 : 9002);
                TeamBot.ApplyExtreme(team, e);
                bool max = e == ExtremeTeam.Max;
                int guns = 0;
                bool ok = true;
                foreach (var m in team.Members)
                {
                    if (m.HasWeapon) { guns++; ok &= m.Archetype == (max ? Archetype.Brainiac : Archetype.Brute); }
                    int prim = m.Stats.Get(CharacterGenerator.PrimaryOf(m.Archetype)), total = m.Stats.Strength + m.Stats.Speed + m.Stats.IQ;
                    float kg = m.Archetype == Archetype.Brute ? (max ? 120 : 100) : m.Archetype == Archetype.Sprinter ? (max ? 65 : 55) : (max ? 75 : 65);
                    ok &= prim == (max ? 10 : 7) && Mathf.Approximately(m.Mass, kg) && total >= CharacterGenerator.TotalStatMin && total <= CharacterGenerator.TotalStatMax;
                }
                ctx.Check(ok && guns == 1, "[BOTW] extreme " + e + ": " + TeamBot.DescribeTeam(team));
            }
        }

        static void Prefs(DevPlayContext ctx)
        {
            var keys = BotPrefsGuard.Keys();
            ctx.Check(keys.Contains("shifters_settings") && keys.Contains("shifters_run") && keys.Contains("shifters_campaign") && keys.Contains("shifters_best_21"),
                      "[BOTW] guarded keys include settings/run/campaign/best_21 (" + keys.Count + " keys)");
            PlayerPrefs.SetString("shifters_run", "{\"orig\":1}"); PlayerPrefs.SetInt("shifters_completed", 5); PlayerPrefs.SetFloat("shifters_best_2", 42.5f);
            PlayerPrefs.DeleteKey("shifters_best_3");
            BotPrefsGuard.Push(ctx);
            BotPrefsGuard.Push(ctx);
            PlayerPrefs.SetString("shifters_run", "garbage"); PlayerPrefs.SetInt("shifters_completed", 31); PlayerPrefs.DeleteKey("shifters_best_2");
            PlayerPrefs.SetFloat("shifters_best_3", 1f);
            BotPrefsGuard.Pop(ctx);
            bool midUntouched = PlayerPrefs.GetString("shifters_run", "") == "garbage";
            bool ok = BotPrefsGuard.Pop(ctx);
            ctx.Check(midUntouched, "[BOTW] inner Pop does not restore (nesting)");
            ctx.Check(ok && PlayerPrefs.GetString("shifters_run", "") == "{\"orig\":1}" && PlayerPrefs.GetInt("shifters_completed", 0) == 5
                      && Mathf.Approximately(PlayerPrefs.GetFloat("shifters_best_2", 0f), 42.5f) && !PlayerPrefs.HasKey("shifters_best_3"),
                      "[BOTW] outer Pop restored string/int/float/deleted keys");
            ctx.Check(BotPrefsGuard.Pop(null) && !BotPrefsGuard.Active, "[BOTW] unbalanced Pop is harmless");
        }

        static IEnumerator Verbs(DevPlayContext ctx)
        {
            var caps = BotCapabilities.Probe();
            TeamBot.ForceTeam(101);
            var course = BotMiniCourse.Build(caps);
            course.SpawnTeam();
            yield return null;
            var stalkers = new List<Component>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("FakeStalker" + i);
                go.transform.SetParent(course.Root, false);
                go.transform.position = new Vector3(i == 0 ? 6f : -6f, 1f, i == 0 ? -1f : 8f);
                stalkers.Add(go.transform);
            }
            var camGo = new GameObject("FakeCamera");
            camGo.transform.SetParent(course.Root, false);
            camGo.transform.position = new Vector3(0f, 3f, 2f);
            var cam = camGo.AddComponent<BotFakeCamera>();
            var plat = PuzzleKit.MovingPlatform(course.Root, new Vector3(-3.5f, 0.25f, 7.4f), null, new[] { Vector3.zero, new Vector3(5f, 0f, 0f) },
                                                new Vector3(2.6f, 0.25f, 2.6f), 0.7f, 1.5f, PlatformLoop.PingPong);
            yield return TeamBot.Run(ctx, bot => Solve(bot, ctx, stalkers, cam, plat), new BotOptions { DiagnosticShots = true }, "BotWave");
            ctx.Check(TeamBot.LastResult != null && TeamBot.LastResult.Passed, "[BOTW] verbs run passed");
            course.Destroy();
        }

        static IEnumerator Solve(TeamBot bot, DevPlayContext ctx, List<Component> stalkers, BotFakeCamera cam, MovingPlatform plat)
        {
            var brute = bot.Brute; var spr = bot.Sprinter; var brain = bot.Brainiac;
            bool? skip = BotStory.AutoSkip;
            ctx.Check(skip == null || skip == true, "[BOTW] StoryPlayer.AutoSkip on during a bot run (" + (skip.HasValue ? skip.ToString() : "module absent") + ")");
            // FaceAndHold a teammate that walks past
            yield return bot.Parallel(bot.WalkTo(brute, new Vector3(4f, 0f, -1f)), bot.FaceAndHold(brain, brute, 2.5f));
            float ang = Vector3.Angle(Flat(brain.transform.forward), Flat(brute.Feet - brain.Feet));
            ctx.Check(ang < 15f, "[BOTW] FaceAndHold keeps facing the moving teammate (" + ang.ToString("0") + " deg off)");
            // Chase: the Brute walks away, the Sprinter tags him
            yield return bot.Parallel(bot.WalkPath(brute, false, new Vector3(-4f, 0f, 1f), new Vector3(-5f, 0f, 6f)), bot.Chase(spr, brute, true, 1.3f, 15f));
            ctx.Check(!bot.Failed, "[BOTW] Chase tagged the walking Brute");
            // Race: the short one ends it
            float t0 = bot.Now;
            yield return bot.Race(bot.Wait(0.5f), bot.Wait(5f));
            ctx.Check(bot.Now - t0 < 1.5f, "[BOTW] Race finishes with the first routine (" + (bot.Now - t0).ToString("0.0") + " s)");
            // Guard: nearest fake stalker within 12 m
            yield return bot.WithGuard(bot.Wait(1.5f), brain, stalkers, 12f);
            Vector3 nearest = (stalkers[0].transform.position - brain.Center).sqrMagnitude < (stalkers[1].transform.position - brain.Center).sqrMagnitude ? stalkers[0].transform.position : stalkers[1].transform.position;
            ang = Vector3.Angle(Flat(brain.transform.forward), Flat(nearest - brain.Feet));
            ctx.Check(!bot.Failed && ang < 15f && CharacterManager.Instance.Active == brain, "[BOTW] Guard made the Brainiac active and faced the nearest stalker (" + ang.ToString("0") + " deg)");
            // WaitCameraClear
            cam.ClearAt = bot.Now + 1.2f;
            t0 = bot.Now;
            yield return bot.WaitCameraClear(cam, brute, 1f, 2f, 5f);
            ctx.Check(!bot.Failed && bot.Now - t0 > 1.0f && bot.Now - t0 < 2.5f, "[BOTW] WaitCameraClear waited for the camera (" + (bot.Now - t0).ToString("0.0") + " s)");
            ctx.Check(!TeamBot.CameraCouldSee(cam, brute.Center, 1f), "[BOTW] CameraCouldSee via reflection");
            // RideUntil on a real MovingPlatform
            var deck = plat.Body != null ? plat.Body.transform : plat.transform;
            yield return bot.WalkTo(spr, deck, false, 0.4f, 15f);
            yield return bot.WaitUntil(() => TeamBot.OnDeck(plat, spr), 4f, "Sprinter on the deck");
            float r0 = bot.Now; Vector3 p0 = deck.position;
            yield return bot.RideUntil(plat, () => bot.Now - r0 > 4f, 10f, new[] { spr });
            float moved = (deck.position - p0).magnitude;
            ctx.Check(!bot.Failed && TeamBot.OnDeck(plat, spr) && moved > 0.5f, "[BOTW] RideUntil kept the Sprinter on the moving platform (moved " + moved.ToString("0.0") + " m)");
            // tool helpers
            var tool = new WeaponData { Type = WeaponType.Experimental, Variant = ExperimentalVariant.SwapOMatic, Name = "SWAP" };
            var blower = new WeaponData { Type = WeaponType.Experimental, Variant = ExperimentalVariant.LeafBlower, Name = "BLOW" };
            ctx.Check(TeamBot.IsTool(tool) && !TeamBot.IsTool(blower) && TeamBot.IsContinuous(blower) && TeamBot.ShotInterval(tool) >= 1.5f, "[BOTW] tool classification");
            // classic ShootAt still works on the course target
            var gun = bot.GunOwner;
            if (gun != null && bot.Driver != null) yield return bot.ShootAt(gun, BotMiniCourse.TargetPos, null, 3);
            // REQ level14-1: something else (StoryPlayer.RestoreBlocking) hands a bot-owned character back -> re-taken next frame;
            // a character the bot released itself stays released
            spr.ExternalControl = false;
            yield return null; yield return null;
            ctx.Check(spr.ExternalControl, "[BOTW] RetakeControl re-took a character handed back by a story script");
            float w0 = bot.Now;
            yield return bot.WalkTo(spr, spr.Feet + new Vector3(1.5f, 0f, 0f), false, 0.5f, 6f);
            ctx.Check(!bot.Failed, "[BOTW] re-taken character walks (" + (bot.Now - w0).ToString("0.0") + " s)");
            bot.ReleaseControl(brain);
            yield return null; yield return null;
            ctx.Check(!brain.ExternalControl, "[BOTW] RetakeControl leaves a released character alone");
            bot.TakeControl(brain);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
