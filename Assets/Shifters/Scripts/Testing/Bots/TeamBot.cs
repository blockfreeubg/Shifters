// =====================================================================================
//  SHIFTERS - TeamBot.cs   [BOTS module - automated playtesting]
//  Drives the REAL characters (ExternalControl + CharacterMotor.SetCommand + public ability
//  methods) from DevPlay scenarios. No teleport cheats (except DebugTeleport, disabled by default).
//
//  A level solution (Scripts/LevelDesigns/L1/L1Solution.cs or Testing/<Module>/...):
//      public static class L1Solution
//      {
//          public static IEnumerator Run(DevPlayContext ctx) => TeamBot.Run(ctx, Solve);
//          static IEnumerator Solve(TeamBot bot)
//          {
//              yield return bot.WalkTo(bot.Brute, "S1.P1");                  // LevelMark id / Vector3 / Transform / Component
//              yield return bot.JumpGap(bot.Sprinter, "S0.Takeoff", "S0.Landing");
//              yield return bot.ExpectPowered("S1.D1", within: 2f);
//              yield return bot.GatherAt("Exit");
//              yield return bot.ExpectLevelComplete(10f);
//          }
//      }
//  Every action is a coroutine (yield return it). Characters are referenced by ARCHETYPE (never by slot).
//  The first failure records a failed ctx.Check, dumps team + puzzle state, takes diagnostic
//  screenshots and aborts the run (TeamBot.Run stops the solution).
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Shifters
{
    /// <summary>Tunables for a TeamBot run.</summary>
    public sealed class BotOptions
    {
        public float DefaultTimeout = 20f;
        /// <summary>No progress for this long while walking => hop once, then fail as STUCK.</summary>
        public float StuckSeconds = 1.6f;
        /// <summary>Whole-solution timeout (simulated seconds).</summary>
        public float RunTimeout = 900f;
        /// <summary>Allow DebugTeleport (development only - never in a final solution run).</summary>
        public bool AllowDebugAssists;
        /// <summary>Interact must go through InteractionSensor.TryInteract (no direct IInteractable.Interact fallback).</summary>
        public bool StrictInteract;
        /// <summary>Interact switches to the character first (only the active character's sensor scans).</summary>
        public bool AutoSwitchForInteract = true;
        public bool ContinueAfterFailure;
        public bool DiagnosticShots = true;
        public bool Verbose = true;
        /// <summary>Gravity multiplier for thrown bodies while falling. 0 = auto (characters Tuning.FallGravityMultiplier
        /// like the motor applies, props 1).</summary>
        public float ThrowFallGravityMultiplier = 0f;
        public float ThrowSpeedScale = 1f;
        /// <summary>Preferred throw elevation: picks the throw spot and the arc (player throws are 38..63 deg). Steep lobs
        /// land slower: thrown bodies tumble/slide 2-5 m (even off a 6 m ledge) after a 38-44 deg touchdown.</summary>
        public float ThrowElevation = 60f;
        /// <summary>Fail (instead of WARN) when a throw needs an elevation a player can't aim
        /// (Tuning.ThrowBaseAngle .. +25 deg, i.e. 38..63).</summary>
        public bool StrictPlayerThrows;
        /// <summary>ThrowTo aims the touchdown this much SHORT of the landing point (along the throw) to absorb the
        /// post-landing slide. Characters only; props use a third of it.</summary>
        public float ThrowSlideAllowance = 1.0f;
        public float StackJumpDistance = 1.6f;
        public float InteractDistance = 1.1f;
        /// <summary>Campaign wave: skip every story script / broadcast / PiP hold (sets StoryPlayer.AutoSkip and
        /// CampaignDirector.BotMode while the bot runs; null-safe while those modules are absent). Default TRUE.
        /// The command line <c>-botStory 1</c> forces story ON for every run.</summary>
        public bool SkipStory = true;
        /// <summary>Story-ON run (CAMPAIGN §22 item 3): the inverse of SkipStory.</summary>
        public bool StoryOn { get => !SkipStory; set => SkipStory = !value; }
        /// <summary>Back up every shifters_* PlayerPrefs key at the outermost run and restore + assert it afterwards.</summary>
        public bool GuardPrefs = true;
        /// <summary>Re-take ExternalControl of every team member the bot has not released when something else handed
        /// it back (a blocking story script that started before Attach restores ExternalControl=false at its end).
        /// Checked every frame by BotDriver, never while a blocking story script plays. Default TRUE.</summary>
        public bool RetakeControl = true;
    }

    /// <summary>One logged action with its timing.</summary>
    public sealed class BotStep
    {
        public string Action, Who, Detail, Note;
        public string Outcome = "RUNNING";
        public float Start, Seconds;
        public bool Ended => Outcome != "RUNNING";
        public string Title => Action + (string.IsNullOrEmpty(Who) ? "" : " " + Who) + (string.IsNullOrEmpty(Detail) ? "" : " " + Detail);
    }

    /// <summary>Result of one TeamBot.Run (one seed).</summary>
    public sealed class BotRunResult
    {
        public string Label;
        public int Seed;
        /// <summary>Team key: the seed as text, or "MIN" / "MAX" for the extreme teams.</summary>
        public string Team = "";
        public bool Passed;
        public bool UsedDebugAssists;
        public float Seconds;
        public string FailReason = "";
        public readonly List<BotStep> Steps = new List<BotStep>();
    }

    public sealed partial class TeamBot : IDisposable
    {
        /// <summary>The attached bot (null when none).</summary>
        public static TeamBot Current { get; private set; }
        public static BotRunResult LastResult { get; private set; }
        /// <summary>Every finished run this play session (RunSeeds summary).</summary>
        public static readonly List<BotRunResult> Results = new List<BotRunResult>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; LastResult = null; Results.Clear(); }

        public DevPlayContext Ctx { get; }
        public BotOptions Options { get; }
        public BotDriver Driver { get; }
        public string Label { get; set; } = "bot";
        public bool Failed { get; private set; }
        public string FailReason { get; private set; } = "";
        public bool UsedDebugAssists { get; private set; }
        public readonly List<BotStep> Steps = new List<BotStep>();
        /// <summary>Set when GameEvents.LevelCompleted fired while attached.</summary>
        public bool LevelCompleted { get; private set; }
        public LevelResult CompletedResult { get; private set; }
        /// <summary>Bot clock (simulated seconds under DevPlay).</summary>
        public float Now => Driver != null ? Driver.Now : Time.unscaledTime;
        public float AttachedAt { get; }

        int _failCount;
        bool _disposed;
        readonly HashSet<ShifterCharacter> _owned = new HashSet<ShifterCharacter>(); // taken by TakeControl, not released
        readonly List<ShifterCharacter> _lost = new List<ShifterCharacter>(3);

        TeamBot(DevPlayContext ctx, BotOptions options)
        {
            Ctx = ctx;
            Options = options ?? new BotOptions();
            Driver = BotDriver.Ensure();
            AttachedAt = Driver.Now;
        }

        // ------------------------------------------------------------------ attach / dispose
        /// <summary>Take ExternalControl of every character (restored on Dispose). Call after the team spawned
        /// (see WaitForTeam). Prefer TeamBot.Run which does all of this.</summary>
        public static TeamBot Attach(DevPlayContext ctx, BotOptions options = null)
        {
            Current?.Dispose();
            var bot = new TeamBot(ctx ?? DevPlayHarness.Context ?? LooseContext(), options);
            Current = bot;
            GameEvents.LevelCompleted += bot.OnLevelCompleted;
            GameEvents.TeamSpawned += bot.OnTeamSpawned;
            bot.TakeControlOfTeam();
            bot.Log("attached: " + bot.TeamSummary());
            return bot;
        }

        static DevPlayContext LooseContext() => new DevPlayContext { Result = new DevPlayResult(), Config = new DevPlayConfig() };

        void OnLevelCompleted(LevelResult r) { LevelCompleted = true; CompletedResult = r; Log("EVENT LevelCompleted (survivors " + r.Survivors + ")"); }
        void OnTeamSpawned() { TakeControlOfTeam(); Log("EVENT TeamSpawned -> re-attached: " + TeamSummary()); }

        void TakeControlOfTeam()
        {
            foreach (var c in Team) TakeControl(c);
        }

        /// <summary>Bot drives this character (AI and player brain go idle).</summary>
        public void TakeControl(ShifterCharacter c)
        {
            if (c == null) return;
            _owned.Add(c);
            c.ExternalControl = true;
            Driver.Stop(c);
        }

        /// <summary>Give the character back to its AI/brain (used by FollowLeader).</summary>
        public void ReleaseControl(ShifterCharacter c)
        {
            if (c == null) return;
            _owned.Remove(c);
            Driver.Release(c);
            c.ExternalControl = false;
        }

        /// <summary>Re-assert ExternalControl=true on every character the bot took (Attach / TeamSpawned / TakeControl)
        /// and has not released (ReleaseControl) when something else turned it off - e.g. StoryPlayer.RestoreBlocking at the end of a blocking
        /// script that started before Attach. Keeps the current bot command (no Stop). Does nothing while a blocking
        /// story script plays or when Options.RetakeControl is false. BotDriver calls it every frame; levels may call it
        /// too. Returns how many characters were re-taken.</summary>
        public int RetakeControl()
        {
            if (_disposed || !Options.RetakeControl) return 0;
            _lost.Clear();
            foreach (var c in _owned) if (c != null && !c.ExternalControl) _lost.Add(c);
            if (_lost.Count == 0) return 0;
            var sp = BotReflect.FindFirst("StoryPlayer");
            if (sp != null && BotReflect.TryGet(sp, "IsBlocking", out bool blocking) && blocking) { _lost.Clear(); return 0; }
            var names = new StringBuilder();
            foreach (var c in _lost) { c.ExternalControl = true; names.Append(' ').Append(c.DisplayName); }
            int n = _lost.Count;
            _lost.Clear();
            Log("re-took ExternalControl (handed back by story/other):" + names);
            return n;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.TeamSpawned -= OnTeamSpawned;
            bool exitOwnsTeam = LevelCompleted || ExitBusy(FindExit()); // LevelExit keeps them partying in the cab
            if (!exitOwnsTeam) foreach (var c in Team) if (c != null) c.ExternalControl = false;
            if (Driver != null) Driver.ReleaseAll();
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------------ team access
        /// <summary>Team in slot order (CharacterManager.Team, or the ShifterCharacter registry as fallback).</summary>
        public List<ShifterCharacter> Team
        {
            get
            {
                var list = new List<ShifterCharacter>(3);
                var cm = CharacterManager.Instance;
                if (cm != null && cm.Team.Count > 0) { foreach (var c in cm.Team) if (c != null) list.Add(c); }
                else
                {
                    foreach (var c in ShifterCharacter.All) if (c != null) list.Add(c);
                    list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
                }
                return list;
            }
        }

        public ShifterCharacter Get(Archetype a) { foreach (var c in Team) if (c.Archetype == a) return c; return null; }
        public ShifterCharacter Brute => Get(Archetype.Brute);
        public ShifterCharacter Sprinter => Get(Archetype.Sprinter);
        public ShifterCharacter Brainiac => Get(Archetype.Brainiac);
        public ShifterCharacter Active => CharacterManager.Instance != null ? CharacterManager.Instance.Active : null;

        /// <summary>Whoever currently holds the gun (WeaponHolder.HasWeapon, else Profile.HasWeapon).</summary>
        public ShifterCharacter GunOwner
        {
            get
            {
                foreach (var c in Team) if (c.Weapon != null && c.Weapon.HasWeapon) return c;
                foreach (var c in Team) if (c.Profile != null && c.Profile.HasWeapon) return c;
                return null;
            }
        }

        /// <summary>Alive teammates except the given ones.</summary>
        public List<ShifterCharacter> Others(params ShifterCharacter[] except)
        {
            var list = new List<ShifterCharacter>();
            foreach (var c in Team)
            {
                if (!c.IsAlive) continue;
                bool skip = false;
                foreach (var e in except) if (e == c) skip = true;
                if (!skip) list.Add(c);
            }
            return list;
        }

        public string TeamSummary()
        {
            var sb = new StringBuilder();
            foreach (var c in Team)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(c.Slot).Append(':').Append(c.DisplayName).Append(' ').Append(c.Archetype);
                if (c.Stats != null) sb.Append(" STR").Append(c.Stats.Strength).Append(" SPD").Append(c.Stats.Speed).Append(" IQ").Append(c.Stats.IQ);
                sb.Append(' ').Append(c.Mass.ToString("0")).Append("kg");
                if (c.Weapon != null && c.Weapon.HasWeapon) sb.Append(" GUN:").Append(c.Weapon.Data.Type);
            }
            return sb.Length > 0 ? sb.ToString() : "(no characters)";
        }

        // ------------------------------------------------------------------ logging & steps
        public void Log(string msg)
        {
            string line = "[BOT] " + Now.ToString("0.00").PadLeft(7) + " " + msg;
            if (Ctx != null) Ctx.Log(line); else Debug.Log("[DEVPLAY] " + line);
        }

        public void Warn(string msg)
        {
            string line = "[BOT] " + Now.ToString("0.00").PadLeft(7) + " WARN " + msg;
            Debug.LogWarning("[DEVPLAY] " + line);
            Ctx?.Result?.messages.Add(line);
        }

        BotStep Begin(string action, ShifterCharacter c, string detail)
        {
            if (Failed && !Options.ContinueAfterFailure) return null;
            var s = new BotStep { Action = action, Who = c != null ? c.DisplayName : "", Detail = detail, Start = Now };
            Steps.Add(s);
            if (Options.Verbose) Log(">> " + s.Title);
            return s;
        }

        void End(BotStep s, bool ok = true, string note = null)
        {
            if (s == null || s.Ended) return;
            if (note != null) s.Note = note;
            s.Seconds = Now - s.Start;
            s.Outcome = ok ? "OK" : "FAIL";
            if (Options.Verbose || !ok) Log((ok ? "OK   " : "FAIL ") + s.Title + "  " + s.Seconds.ToString("0.00") + "s" + (string.IsNullOrEmpty(s.Note) ? "" : "  " + s.Note));
        }

        void FailStep(BotStep s, string why, ShifterCharacter focus = null)
        {
            if (s != null && !s.Ended) { s.Note = why; End(s, false); }
            Fail((s != null ? s.Title + ": " : "") + why, focus);
        }

        /// <summary>Record a skipped action (capability missing) without failing.</summary>
        public void Skip(string action, string reason)
        {
            var s = new BotStep { Action = action, Detail = "", Start = Now, Outcome = "SKIP", Note = reason };
            Steps.Add(s);
            Log("SKIP " + action + ": " + reason);
        }

        /// <summary>Fail the run: failed check + state dump + diagnostic screenshots. Later actions are skipped.</summary>
        public void Fail(string what, ShifterCharacter focus = null)
        {
            if (Failed && !Options.ContinueAfterFailure) { Log("(after failure) " + what); return; }
            Failed = true;
            _failCount++;
            if (string.IsNullOrEmpty(FailReason)) FailReason = what;
            Ctx?.Check(false, "[BOT] " + Label + ": " + what);
            Diagnose("FAIL" + _failCount.ToString("00"), focus, what);
        }

        bool Ready(BotStep s, ShifterCharacter c)
        {
            if (s == null) return false;
            if (c == null) { FailStep(s, "character is null (archetype missing or team not spawned)"); return false; }
            if (!c.IsAlive) { FailStep(s, c.DisplayName + " is " + c.State + (string.IsNullOrEmpty(c.LastCause) ? "" : " (" + c.LastCause + ")"), c); return false; }
            return true;
        }

        // ------------------------------------------------------------------ run wrappers
        /// <summary>Wait until CharacterManager has spawned 3 characters (or ShifterCharacter.All has 3).</summary>
        public static IEnumerator WaitForTeam(DevPlayContext ctx, float timeout = 15f)
        {
            var driver = BotDriver.Ensure();
            float t0 = driver.Now;
            while (driver.Now - t0 < timeout)
            {
                var cm = CharacterManager.Instance;
                int n = cm != null ? cm.Team.Count : ShifterCharacter.All.Count;
                if (n >= 3) break;
                yield return null;
            }
            // let physics settle (grounding) a moment
            for (int i = 0; i < 20; i++) yield return null;
        }

        /// <summary>Standard entry point for solutions: waits for the team, applies -botSeed if given, attaches,
        /// runs the solution exception-safely, prints a summary + timings and detaches.</summary>
        public static IEnumerator Run(DevPlayContext ctx, Func<TeamBot, IEnumerator> solution, BotOptions options = null, string label = null)
        {
            ctx = ctx ?? DevPlayHarness.Context ?? LooseContext();
            var driver = BotDriver.Ensure();
            driver.HoldHarness(ctx);
            options = options ?? new BotOptions();
            bool prefsPushed = options.GuardPrefs; if (prefsPushed) BotPrefsGuard.Push(ctx);
            BotStory.Apply(options);
            int seedArg = BotArgs.TeamSeed("-botSeed", 0);
            if (seedArg != 0 && GameSession.Instance != null && !IsCurrentTeam(seedArg))
                yield return ReloadWithSeed(ctx, seedArg);
            yield return WaitForTeam(ctx);
            BotStory.Apply(options);

            var bot = Attach(ctx, options);
            bot.Label = label ?? (solution.Method.DeclaringType != null ? solution.Method.DeclaringType.Name : "solution");
            var result = new BotRunResult { Label = bot.Label, Seed = GameSession.Instance != null ? GameSession.Instance.Seed : 0, Team = CurrentTeamKey };
            bot.Log("RUN " + bot.Label + " seed=" + result.Seed + " team=" + result.Team + " story=" + (options.SkipStory ? "SKIP" : "ON"));
            float t0 = bot.Now;
            if (bot.Team.Count < 3) bot.Fail("team not spawned (" + bot.Team.Count + " characters) - does the scene have a LevelManager?");
            else
            {
                var routine = new BotRoutine(solution(bot), () => bot.Failed && !bot.Options.ContinueAfterFailure);
                while (true)
                {
                    bool more = routine.Step();
                    if (routine.Error != null)
                    {
                        Debug.LogException(routine.Error);
                        bot.Fail("EXCEPTION in solution: " + routine.Error.GetType().Name + ": " + routine.Error.Message);
                        break;
                    }
                    if (!more) break;
                    if (bot.Failed && !bot.Options.ContinueAfterFailure) { routine.Abort(); break; }
                    if (bot.Now - t0 > bot.Options.RunTimeout) { routine.Abort(); bot.Fail("run timeout (" + bot.Options.RunTimeout + " s)"); break; }
                    yield return null;
                }
            }
            result.Passed = !bot.Failed;
            result.UsedDebugAssists = bot.UsedDebugAssists;
            result.FailReason = bot.FailReason;
            result.Seconds = bot.Now - t0;
            result.Steps.AddRange(bot.Steps);
            bot.PrintSummary(result);
            bot.Log("TIME " + result.Label + " team=" + result.Team + " " + result.Seconds.ToString("0.0") + " s " + (result.Passed ? "PASS" : "FAIL")
                    + (result.UsedDebugAssists ? " (DEBUG ASSISTS)" : ""));
            if (result.Passed)
            {
                int skipped = 0;
                foreach (var st in result.Steps) if (st.Outcome == "SKIP") skipped++;
                ctx.Check(true, "[BOT] " + result.Label + " seed=" + result.Seed + " PASSED in " + result.Seconds.ToString("0.0") + " s sim"
                                + (skipped > 0 ? " (" + skipped + " step(s) SKIPPED)" : "") + (result.UsedDebugAssists ? " !!! USED DEBUG ASSISTS" : ""));
            }
            bot.Dispose();
            LastResult = result;
            Results.Add(result);
            BotStory.Restore();
            if (prefsPushed) BotPrefsGuard.Pop(ctx);
            driver.ReleaseHarness(ctx);
        }

        /// <summary>Summary + per-action timing table in the log.</summary>
        public void PrintSummary(BotRunResult r)
        {
            var sb = new StringBuilder();
            sb.Append("SUMMARY ").Append(r.Label).Append(" seed=").Append(r.Seed).Append(r.Passed ? " PASS" : " FAIL")
              .Append(" in ").Append(r.Seconds.ToString("0.0")).Append(" s sim");
            if (r.UsedDebugAssists) sb.Append("  !!! USED DEBUG ASSISTS - not a valid solution run");
            if (!r.Passed) sb.Append("  reason: ").Append(r.FailReason);
            Log(sb.ToString());
            // aggregate timings per action type
            var agg = new Dictionary<string, Vector3>(); // x = count, y = total s, z = max s
            int ok = 0, fail = 0, skip = 0;
            foreach (var s in r.Steps)
            {
                if (s.Outcome == "OK") ok++; else if (s.Outcome == "SKIP") skip++; else fail++;
                if (s.Outcome == "SKIP") continue;
                agg.TryGetValue(s.Action, out var v);
                v.x += 1; v.y += s.Seconds; v.z = Mathf.Max(v.z, s.Seconds);
                agg[s.Action] = v;
            }
            var tb = new StringBuilder("TIMINGS steps ok=" + ok + " fail=" + fail + " skip=" + skip + " :");
            foreach (var kv in agg)
                tb.Append(' ').Append(kv.Key).Append(" x").Append((int)kv.Value.x).Append(" avg ").Append((kv.Value.y / kv.Value.x).ToString("0.00"))
                  .Append("s max ").Append(kv.Value.z.ToString("0.00")).Append("s;");
            Log(tb.ToString());
        }

        // ------------------------------------------------------------------ diagnostics
        /// <summary>Screenshot (main camera) + optional focus shot + state dump in the log.</summary>
        public void Snapshot(string label, ShifterCharacter focus = null)
        {
            if (Failed && !Options.ContinueAfterFailure) return;
            Ctx?.Shot("bot_" + label);
            if (focus != null) BotDiagnostics.FocusShot(Ctx, "bot_" + label + "_focus", focus.Center, focus.transform.forward);
            Log("SNAPSHOT " + label + "\n" + Dump());
        }

        /// <summary>Text dump: characters, puzzle signals, recent steps.</summary>
        public string Dump() => BotDiagnostics.Dump(this);

        void Diagnose(string tag, ShifterCharacter focus, string what)
        {
            string dump = Dump();
            Debug.LogWarning("[DEVPLAY] [BOT] " + tag + " " + what + "\n" + dump);
            if (!Options.DiagnosticShots || Ctx == null) return;
            try
            {
                string safe = Label.Replace(' ', '_');
                Ctx.Shot("bot_" + safe + "_" + tag + "_main");
                var f = focus != null ? focus : Active;
                if (f != null) BotDiagnostics.FocusShot(Ctx, "bot_" + safe + "_" + tag + "_focus", f.Center, f.transform.forward);
                BotDiagnostics.OverviewShot(Ctx, "bot_" + safe + "_" + tag + "_overview", Team);
                if (!string.IsNullOrEmpty(Ctx.Config?.shotsDir))
                    File.WriteAllText(Path.Combine(Ctx.Config.shotsDir, "bot_" + safe + "_" + tag + ".txt"), what + "\n\n" + dump);
            }
            catch (Exception e) { Debug.LogWarning("[BOT] diagnostics failed: " + e.Message); }
        }
    }

    /// <summary>Command-line arguments for bot runs (-botSeed 7, -botSeeds 1,2,3).</summary>
    public static class BotArgs
    {
        public static string Str(string name, string def = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return def;
        }

        public static int Int(string name, int def = 0) => int.TryParse(Str(name, ""), out int v) ? v : def;

        public static bool Has(string name)
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a == name) return true;
            return false;
        }

        /// <summary>One team token: a seed number, or MIN / MAX (extreme teams, returned as TeamBot.SeedMin / SeedMax).
        /// 0 = not a team.</summary>
        public static int ParseTeam(string token)
        {
            if (string.IsNullOrEmpty(token)) return 0;
            token = token.Trim();
            if (string.Equals(token, "MIN", StringComparison.OrdinalIgnoreCase)) return TeamBot.SeedMin;
            if (string.Equals(token, "MAX", StringComparison.OrdinalIgnoreCase)) return TeamBot.SeedMax;
            return int.TryParse(token, out int v) ? v : 0;
        }

        /// <summary>A single-team argument (-botSeed 7 / -botSeed MIN). def when absent or unparsable.</summary>
        public static int TeamSeed(string name, int def = 0)
        {
            int v = ParseTeam(Str(name, ""));
            return v != 0 ? v : def;
        }

        /// <summary>Team list: -botTeams 1,2,3,MIN,MAX or -botSeeds a,b,c (MIN/MAX accepted too) or -botSeed s, else the
        /// defaults. MIN / MAX come back as TeamBot.SeedMin / SeedMax (negative sentinels; TeamBot.ForceTeam builds the
        /// extreme team for them), so every solution that runs BotArgs.Seeds() through RunSeeds gets them for free.</summary>
        public static int[] Seeds(params int[] defaults)
        {
            foreach (var key in new[] { "-botTeams", "-botSeeds" })
            {
                var s = Str(key, "");
                if (string.IsNullOrEmpty(s)) continue;
                var list = new List<int>();
                foreach (var p in s.Split(',')) { int v = ParseTeam(p); if (v != 0) list.Add(v); }
                if (list.Count > 0) return list.ToArray();
            }
            int one = TeamSeed("-botSeed", 0);
            return one != 0 ? new[] { one } : defaults;
        }
    }
}
