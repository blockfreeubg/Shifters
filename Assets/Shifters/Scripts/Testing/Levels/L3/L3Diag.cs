// =====================================================================================
//  SHIFTERS - L3Diag.cs   [LEVEL 3]  lure-stability diagnostics (development tool, not a solution run)
//  Puts the Brainiac on a lure pulpit (S1 / S4), taunts like a player and measures for 30 s how well the
//  security bots stay locked on: frames in Chase on the lure, frames they can't see it ("blind"), frames
//  the lure is outside their FOV, target switches, and how often each sampled point of the crossing
//  routes would be visible to a bot (exposure). Uses Motor.Teleport for setup (diagnostic only).
//    devplay.sh <proj> l3diag -devSetup Shifters.L3Diag.Lure -devSeconds 1 [-l3Seeds 3,1,22] [-l3Secs 32]
//  (Runtime-built level: no -devScene needed.) Used to pick the PROW pulpit shape over the old curb design.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shifters
{
    public static class L3Diag
    {
        public static IEnumerator Lure(DevPlayContext ctx)
        {
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            foreach (var ss in BotArgs.Str("-l3Seeds", "3").Split(','))
            {
                int seed = int.Parse(ss.Trim());
                yield return TeamBot.BuildRuntimeLevel(ctx, 2, seed);
                var bot = TeamBot.Attach(ctx);
                bot.Log("[L3DIAG] ===== seed " + seed + " team: " + bot.TeamSummary());
                yield return bot.Wait(seed % 5);   // vary where the patrolling bots are when the taunting starts
                yield return Measure(ctx, bot, "S4s" + seed, "S4.Pulpit", Vector3.right, new[] { "S4.BotA", "S4.BotB" },
                    new[] { "S4.Entry", "S4.Entry2" }, Routes4());
                yield return Measure(ctx, bot, "S1s" + seed, "S1.Pulpit", Vector3.left, new[] { "S1.Bot" },
                    new[] { "S1.Entry", "S1.Entry2" }, Routes1());
                bot.Dispose();
            }
            driver.ReleaseHarness(ctx);
        }

        static Dictionary<string, Vector3[]> Routes4() => new Dictionary<string, Vector3[]>
        {
            { "mid", Line(new Vector3(4.6f, 0f, 150f), new Vector3(2.6f, 0f, 168.2f), 1.5f) },
            { "east", Poly(new Vector3(5.4f, 0f, 152.5f), new Vector3(5.2f, 0f, 160f), new Vector3(5.5f, 0f, 166.4f), new Vector3(4.6f, 0f, 170.4f)) },
        };

        static Dictionary<string, Vector3[]> Routes1() => new Dictionary<string, Vector3[]>
        {
            { "west", Line(new Vector3(-3.4f, 0f, 22f), new Vector3(-3.4f, 0f, 40f), 1.5f) },
            { "wall", Line(new Vector3(-6.8f, 0f, 20f), new Vector3(-6.8f, 0f, 30f), 1.5f) },
        };

        static Vector3[] Poly(params Vector3[] corners)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < corners.Length; i++)
            {
                var seg = Line(corners[i], corners[i + 1], 1.5f);
                for (int k = i == 0 ? 0 : 1; k < seg.Length; k++) pts.Add(seg[k]);
            }
            return pts.ToArray();
        }

        static Vector3[] Line(Vector3 a, Vector3 b, float step)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = Vector3.Lerp(a, b, i / (float)n);
            return pts;
        }

        static IEnumerator Measure(DevPlayContext ctx, TeamBot bot, string tag, string perchMark, Vector3 face, string[] botMarks,
                                   string[] parkMarks, Dictionary<string, Vector3[]> routes)
        {
            var lure = bot.Brainiac;
            var others = bot.Others(lure);
            for (int i = 0; i < others.Count && i < parkMarks.Length; i++)
                others[i].Motor.Teleport(LevelMark.Position(parkMarks[i]) + Vector3.up * 0.05f, Quaternion.identity);
            lure.Motor.Teleport(LevelMark.Position(perchMark) + Vector3.up * 0.05f, Quaternion.LookRotation(face));
            var bots = new List<SecurityBot>();
            foreach (var m in botMarks) bots.Add(LevelMark.Find<SecurityBot>(m));
            yield return bot.Wait(0.5f);

            int n = bots.Count;
            var chase = new int[n]; var blind = new int[n]; var outFov = new int[n]; var waiting = new int[n]; var switches = new int[n];
            var lastTarget = new ShifterCharacter[n];
            var exposure = new Dictionary<string, int[]>();
            foreach (var kv in routes) exposure[kv.Key] = new int[kv.Value.Length];
            int samples = 0, frames = 0;
            float t0 = bot.Now, lastTaunt = -99f, settledAt = -1f, maxFacingErr = 0f;
            float Duration = BotArgs.Int("-l3Secs", 32);
            while (bot.Now - t0 < Duration)
            {
                if (bot.Now - lastTaunt > 2.2f) { lastTaunt = bot.Now; var pb = lure.GetComponent<PlayerBrain>(); if (pb != null) pb.TryTaunt(); }
                bool allOn = true;
                for (int i = 0; i < n; i++) if (!(bots[i].State == BotState.Chase && bots[i].Target == lure)) allOn = false;
                if (settledAt < 0f && allOn) settledAt = bot.Now;
                if (settledAt > 0f && bot.Now - settledAt > 2f)
                {
                    frames++;
                    bool sample = frames % 6 == 0;
                    if (sample) samples++;
                    for (int i = 0; i < n; i++)
                    {
                        var b = bots[i];
                        if (b.Target != lastTarget[i]) { if (lastTarget[i] == lure) switches[i]++; lastTarget[i] = b.Target; }
                        bool onLure = b.State == BotState.Chase && b.Target == lure;
                        if (onLure) chase[i]++;
                        if (b.IsWaitingBelow) waiting[i]++;
                        if (onLure && !b.CanSee(lure)) blind[i]++;
                        Vector3 to = lure.Feet - b.transform.position; to.y = 0f;
                        float err = Vector3.Angle(Flat(b.transform.forward), to);
                        if (err > b.Fov * 0.5f) outFov[i]++;
                        if (onLure) maxFacingErr = Mathf.Max(maxFacingErr, err);
                        if (!sample) continue;
                        foreach (var kv in routes)
                        {
                            var pts = kv.Value; var ex = exposure[kv.Key];
                            for (int k = 0; k < pts.Length; k++) if (L3BotSight.WouldSee(b, pts[k])) ex[k]++;
                        }
                    }
                }
                if (((int)((bot.Now - t0) * 60f)) % 300 == 0)
                {
                    string st = "";
                    foreach (var b in bots) st += b.name + "=" + b.State + "/" + (b.Target != null ? b.Target.DisplayName : "-") + (b.IsWaitingBelow ? "/WAIT" : "") + " @" + BotFmt.V(b.transform.position) + " fwd" + BotFmt.V(b.transform.forward) + "  ";
                    bot.Log("[L3DIAG] " + tag + " t=" + (bot.Now - t0).ToString("0.0") + " " + st);
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                float f = Mathf.Max(1, frames);
                bot.Log("[L3DIAG] " + tag + " " + bots[i].name + " settle=" + (settledAt > 0f ? (settledAt - t0).ToString("0.0") : "NEVER")
                        + " chaseLure=" + (chase[i] / f * 100f).ToString("0") + "% waiting=" + (waiting[i] / f * 100f).ToString("0")
                        + "% blind=" + (blind[i] / f * 100f).ToString("0.0") + "% lureOutOfFov=" + (outFov[i] / f * 100f).ToString("0.0")
                        + "% switchesAway=" + switches[i] + " pos=" + BotFmt.V(bots[i].transform.position));
            }
            bot.Log("[L3DIAG] " + tag + " maxFacingErr=" + maxFacingErr.ToString("0") + " deg, frames=" + frames);
            foreach (var kv in routes)
            {
                var ex = exposure[kv.Key];
                string s = ""; int worst = 0;
                for (int k = 0; k < ex.Length; k++) { s += (ex[k] * 100 / Mathf.Max(1, samples * n)) + " "; worst = Mathf.Max(worst, ex[k]); }
                bot.Log("[L3DIAG] " + tag + " exposure route '" + kv.Key + "' (% of bot-samples per point): " + s + " worst=" + (worst * 100 / Mathf.Max(1, samples * n)) + "%");
            }
            ShotFromAbove(ctx, tag, LevelMark.Position(perchMark));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void ShotFromAbove(DevPlayContext ctx, string tag, Vector3 perch)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var sc = cam.GetComponent<ShiftersCamera>();
            bool was = sc != null && sc.enabled;
            if (sc != null) sc.enabled = false;
            var p0 = cam.transform.position; var r0 = cam.transform.rotation;
            Vector3 eye = perch + new Vector3(0f, 13f, -7f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(perch + new Vector3(0f, 0f, 1.5f) - eye));
            ctx.Shot("l3diag_" + tag, cam);
            cam.transform.SetPositionAndRotation(p0, r0);
            if (sc != null) sc.enabled = was;
        }
    }
}
